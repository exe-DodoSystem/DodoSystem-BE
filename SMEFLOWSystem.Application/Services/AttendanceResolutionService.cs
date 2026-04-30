using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ShareKernel.Common.Enum;
using SMEFLOWSystem.Application.Interfaces.IRepositories;
using SMEFLOWSystem.Application.Interfaces.IServices;
using SMEFLOWSystem.Application.Options;
using SMEFLOWSystem.Core.Entities;
using SMEFLOWSystem.SharedKernel.Interfaces;
using System.Text.Json;

namespace SMEFLOWSystem.Application.Services;

public class AttendanceResolutionService : IAttendanceResolutionService
{
    private readonly ILogger<AttendanceResolutionService> _logger;
    private readonly AttendanceResolutionOptions _options;
    private readonly IRawPunchLogRepository _rawPunchLogRepository;
    private readonly IShiftPatternRepository _shiftPatternRepository;
    private readonly IDailyTimesheetRepository _dailyTimesheetRepository;
    private readonly IAttendanceSettingRepository _attendanceSettingRepository;
    private readonly IOvertimeRequestRepository _overtimeRequestRepository;
    private readonly ILeaveRequestRepository _leaveRequestRepository;
    private readonly ITransaction _transaction;
    private readonly ICurrentTenantService _currentTenantService;
    private readonly ITenantRepository _tenantRepository;

    private static readonly TimeZoneInfo VietnamTimeZone = GetVietnamTimeZone();

    public AttendanceResolutionService(
        ILogger<AttendanceResolutionService> logger,
        IOptions<AttendanceResolutionOptions> options,
        IRawPunchLogRepository rawPunchLogRepository,
        IShiftPatternRepository shiftPatternRepository,
        IDailyTimesheetRepository dailyTimesheetRepository,
        IAttendanceSettingRepository attendanceSettingRepository,
        IOvertimeRequestRepository overtimeRequestRepository,
        ILeaveRequestRepository leaveRequestRepository,
        ITransaction transaction,
        ICurrentTenantService currentTenantService,
        ITenantRepository tenantRepository)
    {
        _logger = logger;
        _options = options.Value;
        _rawPunchLogRepository = rawPunchLogRepository;
        _shiftPatternRepository = shiftPatternRepository;
        _dailyTimesheetRepository = dailyTimesheetRepository;
        _attendanceSettingRepository = attendanceSettingRepository;
        _overtimeRequestRepository = overtimeRequestRepository;
        _leaveRequestRepository = leaveRequestRepository;
        _transaction = transaction;
        _currentTenantService = currentTenantService;
        _tenantRepository = tenantRepository;
    }

    public Task ProcessUnresolvedPunchesAsync()
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Attendance resolution job is disabled by configuration.");
            return Task.CompletedTask;
        }

        return ProcessInternalAsync();
    }

    private async Task ProcessInternalAsync()
    {
        var tenantId = _currentTenantService.TenantId;

        if (tenantId == null)
        {
            var tenants = await _tenantRepository.GetAllIgnoreTenantAsync();
            if (tenants.Count == 0)
            {
                _logger.LogWarning("Attendance resolution skipped because no tenants were found.");
                return;
            }

            try
            {
                foreach (var tenant in tenants)
                {
                    _currentTenantService.SetTenantId(tenant.Id);
                    await ProcessTenantAsync(tenant.Id);
                }
            }
            finally
            {
                _currentTenantService.SetTenantId(null);
            }
            return;
        }

        await ProcessTenantAsync(tenantId.Value);
    }

    private async Task ProcessTenantAsync(Guid tenantId)
    {
        _logger.LogInformation("Attendance resolution processing tenant {TenantId}.", tenantId);

        var batchSize = _options.BatchSize;
        var dedupWindowMinutes = _options.DedupWindowMinutes;
        var attendanceSetting = await _attendanceSettingRepository.GetByTenantIdAsync(tenantId);

        var maxBatches = _options.MaxBatchesPerRun;
        var executedBatches = 0;

        while (true)
        {
            if (maxBatches > 0 && executedBatches >= maxBatches)
            {
                _logger.LogInformation("Attendance resolution reached max batches per run: {MaxBatches}.", maxBatches);
                return;
            }

            var rawLogs = await _rawPunchLogRepository.GetUnprocessedBatchAsync(batchSize);
            if (rawLogs.Count == 0)
            {
                _logger.LogInformation("Attendance resolution found no unprocessed punch logs.");
                return;
            }

            var dedupedLogs = DeduplicateLogs(rawLogs, dedupWindowMinutes);
            var cutOffTime = attendanceSetting?.DayStartCutOffTime ?? new TimeSpan(12, 1, 0);
            var groupedByEmployeeDate = dedupedLogs
                .Select(log =>
                {
                    var localTime = TimeZoneInfo.ConvertTimeFromUtc(log.Timestamp, VietnamTimeZone);
                    var workDate = localTime.TimeOfDay < cutOffTime
                        ? DateOnly.FromDateTime(localTime.AddDays(-1))
                        : DateOnly.FromDateTime(localTime);
                    return new { Log = log, EmployeeId = log.EmployeeId, WorkDate = workDate, LocalTime = localTime };
                })
                .GroupBy(x => new { x.EmployeeId, x.WorkDate })
                .ToList();

            foreach (var group in groupedByEmployeeDate)
            {
                try
                {
                    await _transaction.ExecuteAsync(async () =>
                    {
                        var orderedLogs = group.OrderBy(x => x.LocalTime).ThenBy(x => x.Log.Id).ToList();
                        var pairResult = BuildPairs(orderedLogs.Select(x => x.Log).ToList(), group.Key.WorkDate);

                        await UpsertDailyTimesheetAsync(
                            group.Key.EmployeeId,
                            group.Key.WorkDate,
                            pairResult.Pairs,
                            pairResult.OutBeforeInCount,
                            attendanceSetting);

                        // Chỉ mark processed cho những log của nhóm đã xử lý thành công
                        var processedLogIds = group.Select(x => x.Log.Id).ToList();
                        await _rawPunchLogRepository.MarkProcessedAsync(processedLogIds);
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Thất bại khi xử lý chấm công cho EmployeeId: {EmployeeId}, Ngày: {WorkDate}. Sẽ thử lại ở lô tiếp theo.", group.Key.EmployeeId, group.Key.WorkDate);
                }
            }

            executedBatches++;
        }
    }

    private static List<RawPunchLog> DeduplicateLogs(List<RawPunchLog> rawLogs, int dedupWindowMinutes)
    {
        if (dedupWindowMinutes <= 0)
            return rawLogs;

        var result = new List<RawPunchLog>();

        foreach (var employeeGroup in rawLogs.GroupBy(x => x.EmployeeId))
        {
            var ordered = employeeGroup.OrderBy(x => x.Timestamp).ThenBy(x => x.Id).ToList();
            RawPunchLog? lastKept = null;

            foreach (var log in ordered)
            {
                if (lastKept == null)
                {
                    result.Add(log);
                    lastKept = log;
                    continue;
                }

                var diffMinutes = Math.Abs((log.Timestamp - lastKept.Timestamp).TotalMinutes);
                if (diffMinutes >= dedupWindowMinutes)
                {
                    result.Add(log);
                    lastKept = log;
                }
            }
        }

        return result;
    }

    private sealed class PunchPair
    {
        public RawPunchLog InLog { get; init; } = default!;
        public RawPunchLog? OutLog { get; init; }
        public bool MissingOut { get; init; }
        public DateOnly WorkDate { get; init; }
    }

    private sealed record PairResult(List<PunchPair> Pairs, int OutBeforeInCount);

    private static PairResult BuildPairs(List<RawPunchLog> orderedLogs, DateOnly workDate)
    {
        var pairs = new List<PunchPair>();
        RawPunchLog? open = null;
        var outBeforeInCount = 0;

        foreach (var log in orderedLogs)
        {
            if (open == null)
            {
                var firstKind = ResolvePunchType(log.PunchType, isOpenEmpty: true);
                if (firstKind == StatusEnum.PunchOut)
                {
                    outBeforeInCount++;
                    continue;
                }

                open = log;
                continue;
            }

            var punchKind = ResolvePunchType(log.PunchType, isOpenEmpty: false);
            if (punchKind == StatusEnum.PunchIn)
            {
                pairs.Add(new PunchPair
                {
                    InLog = open,
                    OutLog = null,
                    MissingOut = true,
                    WorkDate = workDate
                });
                open = log;
                continue;
            }

            pairs.Add(new PunchPair
            {
                InLog = open,
                OutLog = log,
                MissingOut = false,
                WorkDate = workDate
            });
            open = null;
        }

        if (open != null)
        {
            pairs.Add(new PunchPair
            {
                InLog = open,
                OutLog = null,
                MissingOut = true,
                WorkDate = workDate
            });
        }

        return new PairResult(pairs, outBeforeInCount);
    }

    private static string ResolvePunchType(string? punchType, bool isOpenEmpty)
    {
        if (string.Equals(punchType, "In", StringComparison.OrdinalIgnoreCase))
            return StatusEnum.PunchIn;

        if (string.Equals(punchType, "Out", StringComparison.OrdinalIgnoreCase))
            return StatusEnum.PunchOut;

        return isOpenEmpty ? StatusEnum.PunchIn : StatusEnum.PunchOut;
    }

    private async Task UpsertDailyTimesheetAsync(
        Guid employeeId,
        DateOnly workDate,
        List<PunchPair> pairs,
        int outBeforeInCount,
        TenantAttendanceSetting? attendanceSetting)
    {
        var shiftInfo = await _shiftPatternRepository.GetActivePatternDetailsAsync(employeeId, workDate);
        var shift = await ResolveShiftAsync(shiftInfo, workDate);
        var shiftSegments = shift?.Segments
            .OrderBy(s => s.StartDayOffset)
            .ThenBy(s => s.StartTime)
            .ToList();

        // Lấy danh sách nghỉ phép đã duyệt (Approved) của nhân viên trong ngày
        var approvedLeaveSegments = await _leaveRequestRepository
            .GetApprovedSegmentsByEmployeeDateAsync(employeeId, workDate);
        var approvedLeaveSegmentIds = new HashSet<Guid>(
            approvedLeaveSegments.Select(s => s.TargetShiftSegmentId));

        // Lấy đơn xin OT đã duyệt (Approved) để tính OT hợp lệ
        var approvedOT = await _overtimeRequestRepository
            .GetApprovedByEmployeeDateAsync(employeeId, workDate);

        var segments = new List<DailyTimesheetSegment>();
        var totalLate = 0;
        var totalEarly = 0;
        var totalStayLateMinutes = 0; // Số phút ở lại trễ (chưa phải OT)
        var totalActualWorkedMinutes = 0;
        var unmatchedPairCount = 0;

        // TH1: Ngày nghỉ (Shift == null) nhưng có đi làm (OT nguyên ngày)
        if (shiftSegments == null || shiftSegments.Count == 0)
        {
            // Nếu không có ca làm việc, tính tổng thời gian làm việc thực tế từ các cặp PunchPair
            foreach (var pair in pairs)
            {
                var actualInUtc = pair.InLog.Timestamp;
                var actualOutUtc = pair.OutLog?.Timestamp ?? actualInUtc;
                var actualInLocal = TimeZoneInfo.ConvertTimeFromUtc(actualInUtc, VietnamTimeZone);
                var actualOutLocal = TimeZoneInfo.ConvertTimeFromUtc(actualOutUtc, VietnamTimeZone);
                var workedMins = (int)Math.Round((actualOutLocal - actualInLocal).TotalMinutes);

                totalActualWorkedMinutes += workedMins;
                totalStayLateMinutes += workedMins;

                segments.Add(new DailyTimesheetSegment
                {
                    Id = Guid.NewGuid(),
                    DailyTimesheetId = Guid.Empty,
                    TargetShiftSegmentId = null,
                    ActualCheckIn = actualInUtc,
                    ActualCheckOut = pair.OutLog?.Timestamp,
                    CheckInLatitude = pair.InLog.Latitude,
                    CheckInLongitude = pair.InLog.Longitude,
                    CheckInSelfieUrl = pair.InLog.SelfieUrl ?? string.Empty,
                    CheckOutLatitude = pair.OutLog?.Latitude,
                    CheckOutLongitude = pair.OutLog?.Longitude,
                    CheckOutSelfieUrl = pair.OutLog?.SelfieUrl ?? string.Empty,
                    LateMinutes = 0,
                    EarlyLeaveMinutes = 0,
                    Status = StatusEnum.AttendanceNoShift
                });
            }
        }
        // TH2: Ngày đi làm bình thường (Có ShiftSegments) 
        else
        {
            // Bắt đầu Mapping Proximity
            var unmappedPairs = pairs.ToList(); // Danh sách các thẻ chưa được gán
            foreach (var targetSegment in shiftSegments)
            {
                var expectedIn = CombineDateTime(workDate, targetSegment.StartTime, targetSegment.StartDayOffset);
                var expectedOut = CombineDateTime(workDate, targetSegment.EndTime, targetSegment.EndDayOffset);
                // 1. Tìm PunchPair phù hợp nhất (Gần với ExpectedIn nhất)
                PunchPair? bestMatch = null;
                if (unmappedPairs.Count > 0)
                {
                    var proximityWindowMinutes = _options.ProximityWindowMinutes <= 0
                        ? int.MaxValue
                        : _options.ProximityWindowMinutes;

                    var bestCandidate = unmappedPairs
                        .Select(p => new
                        {
                            Pair = p,
                            DiffMinutes = Math.Abs((TimeZoneInfo.ConvertTimeFromUtc(p.InLog.Timestamp, VietnamTimeZone) - expectedIn).TotalMinutes)
                        })
                        .OrderBy(x => x.DiffMinutes)
                        .First();

                    if (bestCandidate.DiffMinutes <= proximityWindowMinutes)
                    {
                        bestMatch = bestCandidate.Pair;
                        unmappedPairs.Remove(bestMatch); // Bỏ ra khỏi danh sách
                    }
                }
                // 2. Nếu không có Pair nào map được -> VẮNG MẶT
                if (bestMatch == null)
                {
                    if (approvedLeaveSegmentIds.Contains(targetSegment.Id))
                    {
                        // Có xin phép -> OnLeave
                        segments.Add(new DailyTimesheetSegment
                        {
                            Id = Guid.NewGuid(),
                            DailyTimesheetId = Guid.Empty,
                            TargetShiftSegmentId = targetSegment.Id,
                            ActualCheckIn = null,
                            ActualCheckOut = null,
                            LateMinutes = 0,
                            EarlyLeaveMinutes = 0,
                            Status = StatusEnum.AttendanceOnLeave
                        });
                    }
                    else
                    {
                        // Trốn làm -> Phạt vắng mặt nguyên ca
                        var penaltyMins = (int)(expectedOut - expectedIn).TotalMinutes;
                        totalEarly += penaltyMins; // Phạt vào EarlyLeave hoặc Late
                        segments.Add(new DailyTimesheetSegment
                        {
                            Id = Guid.NewGuid(),
                            DailyTimesheetId = Guid.Empty,
                            TargetShiftSegmentId = targetSegment.Id,
                            ActualCheckIn = null,
                            ActualCheckOut = null,
                            LateMinutes = 0,
                            EarlyLeaveMinutes = penaltyMins,
                            Status = StatusEnum.AttendanceAbsent
                        });
                    }
                    continue;
                }
                // 3. Có thẻ (Có bestMatch) -> Tính Đi Trễ, Về Sớm
                var actualInUtc = bestMatch.InLog.Timestamp;
                var actualOutUtc = bestMatch.OutLog?.Timestamp;
                var actualIn = TimeZoneInfo.ConvertTimeFromUtc(actualInUtc, VietnamTimeZone);
                var actualOut = actualOutUtc.HasValue
                    ? TimeZoneInfo.ConvertTimeFromUtc(actualOutUtc.Value, VietnamTimeZone)
                    : (DateTime?)null;
                // --- TÍNH TOÁN ACTUAL WORKED ---
                if (actualOut.HasValue)
                {
                    totalActualWorkedMinutes += (int)Math.Round((actualOut.Value - actualIn).TotalMinutes);
                }
                // --- TÍNH ĐI TRỄ / VỀ SỚM (Áp dụng Grace Period) ---
                var lateMins = 0;
                var earlyMins = 0;
                var grace = shift?.GracePeriodMinutes ?? 0;
                // Tính trễ: Chỉ phạt phần VƯỢT QUÁ grace, không phạt toàn bộ rawLate
                // VD: Ca 8h, grace 5p, vào 8h07 → rawLate=7, lateMins=7-5=2 (không phải 7)
                var rawLate = (int)Math.Round((actualIn - expectedIn).TotalMinutes);
                if (rawLate > grace) lateMins = rawLate - grace;
                                                         // Tính về sớm / Missing Out
                if (!actualOut.HasValue)
                {
                    // Missing Out phạt nặng (Từ lúc quẹt thẻ in -> Hết ca)
                    earlyMins = Math.Max(0, (int)Math.Round((expectedOut - actualIn).TotalMinutes));
                }
                else
                {
                    var rawEarly = (int)Math.Round((expectedOut - actualOut.Value).TotalMinutes);
                    if (rawEarly > 0) earlyMins = rawEarly;
                }
                // --- TÍNH OT (Đến sớm + Về trễ) ---
                // Đến sớm (Early In OT): Math.Max(0,...) đảm bảo không bao giờ âm
                var earlyInMins = Math.Max(0, (int)Math.Round((expectedIn - actualIn).TotalMinutes));
                var earlyInMinThreshold = attendanceSetting?.MinimumOTMinutes ?? 0;
                if (earlyInMins >= earlyInMinThreshold)
                    totalStayLateMinutes += earlyInMins;
                // Về trễ (Late Out OT)
                if (actualOut.HasValue)
                {
                    var stayLateMins = (int)Math.Round((actualOut.Value - expectedOut).TotalMinutes);
                    if (stayLateMins > 0) totalStayLateMinutes += stayLateMins;
                }
                // Miễn phạt nếu có phép
                if (approvedLeaveSegmentIds.Contains(targetSegment.Id))
                {
                    lateMins = 0; earlyMins = 0;
                }
                totalLate += lateMins;
                totalEarly += earlyMins;
                // ... Thêm vào segments ...

                segments.Add(new DailyTimesheetSegment
                {
                    Id = Guid.NewGuid(),
                    DailyTimesheetId = Guid.Empty,
                    TargetShiftSegmentId = targetSegment.Id,
                    ActualCheckIn = actualInUtc,
                    ActualCheckOut = actualOutUtc,
                    CheckInLatitude = bestMatch.InLog.Latitude,
                    CheckInLongitude = bestMatch.InLog.Longitude,
                    CheckInSelfieUrl = bestMatch.InLog.SelfieUrl ?? string.Empty,
                    CheckOutLatitude = bestMatch.OutLog?.Latitude,
                    CheckOutLongitude = bestMatch.OutLog?.Longitude,
                    CheckOutSelfieUrl = bestMatch.OutLog?.SelfieUrl ?? string.Empty,
                    LateMinutes = lateMins,
                    EarlyLeaveMinutes = earlyMins,
                    Status = approvedLeaveSegmentIds.Contains(targetSegment.Id) ? StatusEnum.AttendanceOnLeave : (bestMatch.MissingOut ? StatusEnum.AttendanceMissingOut : StatusEnum.AttendanceNormal)
                });
            }

            if (unmappedPairs.Count > 0)
            {
                unmatchedPairCount += unmappedPairs.Count;
            }
        }

        var dailyLateThreshold = attendanceSetting?.LateThresholdMinutes ?? 0;
        if (totalLate <= dailyLateThreshold)
            totalLate = 0;

        var dailyEarlyThreshold = attendanceSetting?.EarlyLeaveThresholdMinutes ?? 0;
        if (totalEarly <= dailyEarlyThreshold)
            totalEarly = 0;

        

        // OT hợp lệ chỉ được tính khi có OvertimeRequest đã duyệt
        var totalOtMinutes = 0;
        if (approvedOT != null)
        {
            // Lấy số giờ được duyệt (ApprovedHours), hoặc fallback sang RequestedHours
            var approvedMinutes = (int)((approvedOT.ApprovedHours ?? approvedOT.RequestedHours) * 60);
            // OT hợp lệ = Min(Số phút ở lại trễ thực tế, Số phút được duyệt)
            totalOtMinutes = Math.Min(totalStayLateMinutes, approvedMinutes);
        }

        // Fix: Dùng CombineDateTime để tính đúng cho ca đêm (StartDayOffset/EndDayOffset)
        // VD: Ca đêm 22:00 → 06:00 hôm sau: EndTime - StartTime = -16h (sai). Phải dùng DateTime thực
        var standardHours = shiftSegments == null
            ? 0m
            : Math.Round((decimal)shiftSegments.Sum(s =>
            {
                var segStart = CombineDateTime(workDate, s.StartTime, s.StartDayOffset);
                var segEnd   = CombineDateTime(workDate, s.EndTime,   s.EndDayOffset);
                return (segEnd - segStart).TotalHours;
            }), 2);

        var otHours = attendanceSetting?.CalculateValidOTHours(totalOtMinutes) ?? Math.Round(totalOtMinutes / 60m, 2);
        var actualWorkHours = Math.Round(totalActualWorkedMinutes / 60m, 2);

        var absentSegments = segments.Where(s => s.Status == StatusEnum.AttendanceAbsent).ToList();
        var dayStatus = StatusEnum.AttendanceNormal;
        if (absentSegments.Count > 0) dayStatus = StatusEnum.AttendanceAbsent;
        else if (segments.Any(s => s.Status == StatusEnum.AttendanceMissingOut)) dayStatus = StatusEnum.AttendanceMissingOut;
        else if (totalLate > 0) dayStatus = StatusEnum.AttendanceLate;
        else if (totalEarly > 0) dayStatus = StatusEnum.AttendanceEarlyLeave;

        var resolutionLog = new
        {
            PairCount = pairs.Count,
            MissingOutCount = pairs.Count(p => p.MissingOut),
            OutBeforeInCount = outBeforeInCount,
            UnmappedPairCount = unmatchedPairCount,
            ProximityWindowMinutes = _options.ProximityWindowMinutes,
            TotalLateMinutes = totalLate,
            TotalEarlyLeaveMinutes = totalEarly,
            AbsentSegmentCount = absentSegments.Count,                                  // Số ca vắng mặt không phép
            AbsentPenaltyMinutes = absentSegments.Sum(s => s.EarlyLeaveMinutes),        // Tổng phút bị phạt do vắng mặt
            StayLateMinutes = totalStayLateMinutes,
            ApprovedOTMinutes = totalOtMinutes,
            HasApprovedOTRequest = approvedOT != null,
            LeaveSegmentCount = approvedLeaveSegments.Count,
            TotalOTHours = otHours,
            TotalActualWorkedMinutes = totalActualWorkedMinutes
        };

        // Xác định anomaly flag
        var anomalyFlags = new List<string>();
        if (pairs.Any(p => p.MissingOut)) anomalyFlags.Add("MissingOut");
        if (outBeforeInCount > 0) anomalyFlags.Add("OutBeforeIn");
        if (unmatchedPairCount > 0) anomalyFlags.Add("UnmappedPairs");
        if (totalStayLateMinutes > 0 && approvedOT == null) anomalyFlags.Add("OTWithoutRequest");
        var anomalyFlag = anomalyFlags.Count > 0 ? string.Join(",", anomalyFlags) : string.Empty;

        var existing = await _dailyTimesheetRepository.GetByEmployeeDateAsync(employeeId, workDate);
        if (existing == null)
        {
            var timesheet = new DailyTimesheet
            {
                Id = Guid.NewGuid(),
                EmployeeId = employeeId,
                TenantId = tenantId,
                WorkDate = workDate,
                ExpectedShiftId = shift?.Id,
                ExpectedShiftSource = shift == null ? "None" : "ShiftPattern",
                StandardWorkingHours = standardHours,
                TotalActualWorkedMinutes = totalActualWorkedMinutes,
                ActualWorkHours = actualWorkHours,
                OTHours = otHours,
                TotalLateMinutes = totalLate,
                LateMinutes = totalLate,
                TotalEarlyLeaveMinutes = totalEarly,
                EarlyLeaveMinutes = totalEarly,
                Status = dayStatus,
                SystemAnomalyFlag = anomalyFlag,
                ResolutionLogJson = JsonSerializer.Serialize(resolutionLog),
                IsManuallyAdjusted = false,
                Segments = segments
            };

            await _dailyTimesheetRepository.AddAsync(timesheet);
            return;
        }

        existing.ExpectedShiftId = shift?.Id;
        existing.ExpectedShiftSource = shift == null ? "None" : "ShiftPattern";
        existing.StandardWorkingHours = standardHours;
        existing.TotalActualWorkedMinutes = totalActualWorkedMinutes;
        existing.ActualWorkHours = actualWorkHours;
        existing.OTHours = otHours;
        existing.TotalLateMinutes = totalLate;
        existing.LateMinutes = totalLate;
        existing.TotalEarlyLeaveMinutes = totalEarly;
        existing.EarlyLeaveMinutes = totalEarly;
        existing.Status = dayStatus;
        existing.SystemAnomalyFlag = anomalyFlag;
        existing.ResolutionLogJson = JsonSerializer.Serialize(resolutionLog);

        existing.Segments.Clear();
        foreach (var segment in segments)
        {
            segment.DailyTimesheetId = existing.Id;
            existing.Segments.Add(segment);
        }

        await _dailyTimesheetRepository.UpdateAsync(existing);
    }

    private async Task<Shift?> ResolveShiftAsync((EmployeeShiftPattern? Pattern, ShiftPattern? Definition) details, DateOnly workDate)
    {
        if (details.Pattern == null || details.Definition == null)
            return null;

        var cycleLength = details.Definition.CycleLengthDays;
        if (cycleLength <= 0)
            return null;

        var dayOffset = workDate.DayNumber - details.Pattern.EffectiveStartDate.DayNumber;
        var dayIndex = dayOffset % cycleLength;
        if (dayIndex < 0) dayIndex += cycleLength;

        var patternDay = await _shiftPatternRepository.GetShiftPatternWithDaysAsync(
            details.Definition.Id, dayIndex);   // Fix: thêm ShiftPatternId để tránh lấy nhầm lịch khác
        if (patternDay?.ScheduledShiftId == null)
            return null;

        return await _shiftPatternRepository.GetShiftWithSegmentsAsync(patternDay.ScheduledShiftId.Value);
    }

    private static DateTime CombineDateTime(DateOnly date, TimeSpan time, int dayOffset)
    {
        return date.ToDateTime(TimeOnly.MinValue).AddDays(dayOffset).Add(time);
    }

    // Lấy múi giờ Việt Nam, hỗ trợ cả Windows và Linux.
    private static TimeZoneInfo GetVietnamTimeZone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time"); }
        catch { /* Windows không có */ }
        try { return TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh"); }
        catch { /* Linux không có */ }
        return TimeZoneInfo.CreateCustomTimeZone("VN", TimeSpan.FromHours(7), "Vietnam", "Vietnam Standard Time");
    }
}