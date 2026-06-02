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

    private const int MaxRetryCount = 3;
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

        var lastProcessedLogs = new Dictionary<Guid, RawPunchLog>();
        var shiftCache = new Dictionary<string, Shift?>();
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

            var dedupedLogs = DeduplicateLogs(rawLogs, dedupWindowMinutes, lastProcessedLogs);
            var cutOffTime = attendanceSetting?.DayStartCutOffTime ?? new TimeSpan(4, 0, 0);
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
                        var localDayStart = group.Key.WorkDate.ToDateTime(TimeOnly.FromTimeSpan(cutOffTime));
                        var utcDayStart = TimeZoneInfo.ConvertTimeToUtc(localDayStart, VietnamTimeZone);
                        var utcDayEnd = utcDayStart.AddDays(1);

                        var allLogsForDay = await _rawPunchLogRepository.GetByEmployeeAndDateRangeAsync(
                            group.Key.EmployeeId, utcDayStart, utcDayEnd);

                        await UpsertDailyTimesheetAsync(
                            group.Key.EmployeeId,
                            group.Key.WorkDate,
                            allLogsForDay,
                            attendanceSetting,
                            shiftCache);

                        // Chỉ mark processed cho những log CHƯA xử lý thuộc nhóm của batch hiện tại
                        var newLogIds = group.Select(x => x.Log.Id).ToList();
                        await _rawPunchLogRepository.MarkProcessedAsync(newLogIds);
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Thất bại khi xử lý chấm công cho EmployeeId: {EmployeeId}, Ngày: {WorkDate}.", group.Key.EmployeeId, group.Key.WorkDate);

                    var failedLogIds = group.Select(x => x.Log.Id).ToList();

                    // Nếu đã vượt quá số lần retry thì mark processed để không làm nghẽn job (dead-letter)
                    var maxRetriedLogs = group
                        .Where(x => x.Log.RetryCount >= MaxRetryCount)
                        .Select(x => x.Log.Id)
                        .ToList();

                    if (maxRetriedLogs.Count > 0)
                    {
                        _logger.LogCritical("Log chấm công EmployeeId: {EmployeeId} ngày {WorkDate} đã thất bại {Max} lần. Cần kiểm tra thủ công.",
                            group.Key.EmployeeId, group.Key.WorkDate, MaxRetryCount);
                        await _rawPunchLogRepository.MarkProcessedAsync(maxRetriedLogs);
                    }

                    // Tăng RetryCount cho các log còn lại để thử lại lần sau
                    var retryableLogIds = failedLogIds.Except(maxRetriedLogs).ToList();
                    if (retryableLogIds.Count > 0)
                    {
                        await _rawPunchLogRepository.IncrementRetryCountAsync(retryableLogIds);
                    }
                }
            }

            executedBatches++;
        }
    }

    private static List<RawPunchLog> DeduplicateLogs(List<RawPunchLog> rawLogs, int dedupWindowMinutes, Dictionary<Guid, RawPunchLog> lastProcessedLogs)
    {
        if (dedupWindowMinutes <= 0)
            return rawLogs;

        var result = new List<RawPunchLog>();

        foreach (var employeeGroup in rawLogs.GroupBy(x => x.EmployeeId))
        {
            var employeeId = employeeGroup.Key;
            var ordered = employeeGroup.OrderBy(x => x.Timestamp).ThenBy(x => x.Id).ToList();

            lastProcessedLogs.TryGetValue(employeeId, out RawPunchLog? lastKept);

            foreach (var log in ordered)
            {
                if (lastKept == null)
                {
                    result.Add(log);
                    lastKept = log;
                    continue;
                }
                else
                {
                    var diffMinutes = Math.Abs((log.Timestamp - lastKept.Timestamp).TotalMinutes);
                    var samePunchType = string.Equals(log.PunchType, lastKept.PunchType, StringComparison.OrdinalIgnoreCase);

                    if (!samePunchType || diffMinutes >= dedupWindowMinutes)
                    {
                        result.Add(log);
                        lastKept = log;
                    }
                }
            }
            if (lastKept != null)
            {
                lastProcessedLogs[employeeId] = lastKept;
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

    private sealed record PairResult(List<PunchPair> Pairs, int OutBeforeInCount, RawPunchLog? OrphanedOutLog);

    private static PairResult BuildPairs(List<RawPunchLog> orderedLogs, DateOnly workDate)
    {
        var pairs = new List<PunchPair>();
        RawPunchLog? open = null;
        var outBeforeInCount = 0;
        RawPunchLog? orphanedOutLog = null;

        foreach (var log in orderedLogs)
        {
            if (open == null)
            {
                var firstKind = ResolvePunchType(log.PunchType, isOpenEmpty: true);
                if (firstKind == StatusEnum.PunchOut)
                {
                    outBeforeInCount++;
                    if (orphanedOutLog == null) orphanedOutLog = log;
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

        return new PairResult(pairs, outBeforeInCount, orphanedOutLog);
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
        List<RawPunchLog> orderedLogs,
        TenantAttendanceSetting? attendanceSetting,
        Dictionary<string, Shift?> shiftCache)
    {
        var tenantId = _currentTenantService.TenantId ?? throw new InvalidOperationException("TenantId is not set in the current context.");
        var shiftInfo = await _shiftPatternRepository.GetActivePatternDetailsAsync(employeeId, workDate);
        var shift = await ResolveShiftAsync(shiftInfo, workDate, shiftCache);
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
        var totalEarlyInOTMinutes = 0;
        var totalLateOutOTMinutes = 0;
        var totalActualWorkedMinutes = 0;
        
        var missingOutCount = 0;
        var outBeforeInCount = 0;
        var unmatchedPairCount = 0;
        var unmappedLogCount = 0;
        RawPunchLog? orphanedOutLog = null;
        var hasMissingOut = false;
        var proximityWindowMinutes = _options.ProximityWindowMinutes <= 0 ? 240 : _options.ProximityWindowMinutes;

        // TH1: Ngày nghỉ (Shift == null) nhưng có đi làm (OT nguyên ngày)
        if (shiftSegments == null || shiftSegments.Count == 0)
        {
            var pairResult = BuildPairs(orderedLogs, workDate);
            missingOutCount = pairResult.Pairs.Count(p => p.MissingOut);
            outBeforeInCount = pairResult.OutBeforeInCount;
            orphanedOutLog = pairResult.OrphanedOutLog;
            hasMissingOut = pairResult.Pairs.Any(p => p.MissingOut);

            // Nếu không có ca làm việc, tính tổng thời gian làm việc thực tế từ các cặp PunchPair
            foreach (var pair in pairResult.Pairs)
            {
                var actualInUtc = pair.InLog.Timestamp;
                var actualOutUtc = pair.OutLog?.Timestamp ?? actualInUtc;
                var actualInLocal = TimeZoneInfo.ConvertTimeFromUtc(actualInUtc, VietnamTimeZone);
                var actualOutLocal = TimeZoneInfo.ConvertTimeFromUtc(actualOutUtc, VietnamTimeZone);
                var workedMins = (int)Math.Round((actualOutLocal - actualInLocal).TotalMinutes);

                totalActualWorkedMinutes += workedMins;
                totalLateOutOTMinutes += workedMins; // OT nguyên ngày tính tạm vào LateOut

                segments.Add(new DailyTimesheetSegment
                {
                    Id = Guid.NewGuid(),
                    DailyTimesheetId = Guid.Empty,
                    TargetShiftSegmentId = null,
                    ActualCheckIn = actualInUtc,
                    ActualCheckOut = actualOutUtc,
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
            var unmappedLogs = orderedLogs.ToList();

            foreach (var targetSegment in shiftSegments)
            {
                var expectedIn = CombineDateTime(workDate, targetSegment.StartTime, targetSegment.StartDayOffset);
                var expectedOut = CombineDateTime(workDate, targetSegment.EndTime, targetSegment.EndDayOffset);

                // 1. Tìm Thẻ VÀO (InLog) gần với ExpectedIn nhất
                RawPunchLog? bestInLog = null;
                if (unmappedLogs.Count > 0)
                {
                    var bestInCandidate = unmappedLogs
                        .Select(log => new { Log = log, Diff = Math.Abs((TimeZoneInfo.ConvertTimeFromUtc(log.Timestamp, VietnamTimeZone) - expectedIn).TotalMinutes) })
                        .OrderBy(x => x.Diff)
                        .First();

                    if (bestInCandidate.Diff <= proximityWindowMinutes)
                    {
                        bestInLog = bestInCandidate.Log;
                        unmappedLogs.Remove(bestInLog);
                    }
                }

                // 2. Tìm Thẻ RA (OutLog) gần với ExpectedOut nhất
                RawPunchLog? bestOutLog = null;
                if (unmappedLogs.Count > 0)
                {
                    var validOutCandidates = unmappedLogs.AsEnumerable();
                    if (bestInLog != null) 
                        validOutCandidates = validOutCandidates.Where(x => x.Timestamp >= bestInLog.Timestamp); // Ra phải sau Vào

                    if (validOutCandidates.Any())
                    {
                        var bestOutCandidate = validOutCandidates
                            .Select(log => new { Log = log, Diff = Math.Abs((TimeZoneInfo.ConvertTimeFromUtc(log.Timestamp, VietnamTimeZone) - expectedOut).TotalMinutes) })
                            .OrderBy(x => x.Diff)
                            .First();

                        if (bestOutCandidate.Diff <= proximityWindowMinutes)
                        {
                            bestOutLog = bestOutCandidate.Log;
                            unmappedLogs.Remove(bestOutLog);
                        }
                    }
                }

                // 3. Xử lý Vắng mặt nếu không có CẢ In và Out
                if (bestInLog == null && bestOutLog == null)
                {
                    segments.Add(new DailyTimesheetSegment
                    {
                        Id = Guid.NewGuid(),
                        DailyTimesheetId = Guid.Empty,
                        TargetShiftSegmentId = targetSegment.Id,
                        LateMinutes = 0,
                        EarlyLeaveMinutes = 0,
                        Status = approvedLeaveSegmentIds.Contains(targetSegment.Id) ? StatusEnum.AttendanceOnLeave : StatusEnum.AttendanceAbsent
                    });
                    continue;
                }

                // 4. Tính toán Trễ / Sớm / Làm việc
                DateTime? actualIn = bestInLog != null ? TimeZoneInfo.ConvertTimeFromUtc(bestInLog.Timestamp, VietnamTimeZone) : null;
                DateTime? actualOut = bestOutLog != null ? TimeZoneInfo.ConvertTimeFromUtc(bestOutLog.Timestamp, VietnamTimeZone) : null;

                if (actualIn.HasValue && actualOut.HasValue)
                    totalActualWorkedMinutes += (int)Math.Round((actualOut.Value - actualIn.Value).TotalMinutes);

                var lateMins = 0;
                var earlyMins = 0;
                var grace = shift?.GracePeriodMinutes ?? 0;

                // Tính Đi Trễ / Quên quẹt Vào
                if (!actualIn.HasValue) 
                {
                    hasMissingOut = true;
                    missingOutCount++;
                    lateMins = actualOut.HasValue 
                        ? Math.Max(0, (int)Math.Round((actualOut.Value - expectedIn).TotalMinutes)) // Phạt từ đầu ca đến lúc quẹt ra
                        : Math.Max(0, (int)Math.Round((expectedOut - expectedIn).TotalMinutes));
                }
                else 
                {
                    var rawLate = (int)Math.Round((actualIn.Value - expectedIn).TotalMinutes);
                    if (rawLate > grace) lateMins = rawLate - grace;
                    
                    // Tính OT Đến Sớm
                    var earlyInMins = Math.Max(0, (int)Math.Round((expectedIn - actualIn.Value).TotalMinutes));
                    if (earlyInMins >= (attendanceSetting?.MinimumOTMinutes ?? 0)) totalEarlyInOTMinutes += earlyInMins;
                }

                // Tính Về Sớm / Quên quẹt Ra
                if (!actualOut.HasValue) 
                {
                    hasMissingOut = true;
                    missingOutCount++;
                    if (actualIn.HasValue) 
                        earlyMins = Math.Max(0, (int)Math.Round((expectedOut - actualIn.Value).TotalMinutes)); // Phạt từ lúc quẹt vào đến hết ca
                }
                else 
                {
                    var rawEarly = (int)Math.Round((expectedOut - actualOut.Value).TotalMinutes);
                    if (rawEarly > 0) earlyMins = rawEarly;
                    
                    // Tính OT Về Trễ
                    var stayLateMins = (int)Math.Round((actualOut.Value - expectedOut).TotalMinutes);
                    if (stayLateMins > 0) totalLateOutOTMinutes += stayLateMins;
                }

                if (approvedLeaveSegmentIds.Contains(targetSegment.Id))
                {
                    lateMins = 0; earlyMins = 0;
                }
                totalLate += lateMins;
                totalEarly += earlyMins;

                // 5. Lưu phân đoạn
                segments.Add(new DailyTimesheetSegment
                {
                    Id = Guid.NewGuid(),
                    DailyTimesheetId = Guid.Empty,
                    TargetShiftSegmentId = targetSegment.Id,
                    ActualCheckIn = bestInLog?.Timestamp,
                    ActualCheckOut = bestOutLog?.Timestamp,
                    CheckInLatitude = bestInLog?.Latitude,
                    CheckInLongitude = bestInLog?.Longitude,
                    CheckInSelfieUrl = bestInLog?.SelfieUrl ?? "",
                    CheckOutLatitude = bestOutLog?.Latitude,
                    CheckOutLongitude = bestOutLog?.Longitude,
                    CheckOutSelfieUrl = bestOutLog?.SelfieUrl ?? "",
                    LateMinutes = lateMins,
                    EarlyLeaveMinutes = earlyMins,
                    Status = approvedLeaveSegmentIds.Contains(targetSegment.Id) ? StatusEnum.AttendanceOnLeave 
                             : (!actualIn.HasValue || !actualOut.HasValue ? StatusEnum.AttendanceMissingOut : StatusEnum.AttendanceNormal)
                });
            }

            unmappedLogCount = unmappedLogs.Count;
            unmatchedPairCount = unmappedLogCount;
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
            // Phân bổ OT: Ưu tiên LateOut trước, dư thì duyệt EarlyIn
            var validLateOut = Math.Min(totalLateOutOTMinutes, approvedMinutes);
            var validEarlyIn = Math.Min(totalEarlyInOTMinutes, approvedMinutes - validLateOut);
            totalOtMinutes = validLateOut + validEarlyIn;
        }

        // Fix: Dùng CombineDateTime để tính đúng cho ca đêm (StartDayOffset/EndDayOffset)
        var standardHours = shiftSegments == null
            ? 0m
            : Math.Round((decimal)shiftSegments.Sum(s =>
            {
                var segStart = CombineDateTime(workDate, s.StartTime, s.StartDayOffset);
                var segEnd = CombineDateTime(workDate, s.EndTime, s.EndDayOffset);
                return (segEnd - segStart).TotalHours;
            }), 2);

        var otHours = attendanceSetting?.CalculateValidOTHours(totalOtMinutes) ?? Math.Round(totalOtMinutes / 60m, 2);
        var actualWorkHours = Math.Round(totalActualWorkedMinutes / 60m, 2);

        var absentSegments = segments.Where(s => s.Status == StatusEnum.AttendanceAbsent).ToList();
        var dayStatus = StatusEnum.AttendanceNormal;
        if (segments.Count == 0)
        {
            if (shiftSegments == null || shiftSegments.Count == 0)
            {
                dayStatus = StatusEnum.AttendanceNoShift;
            }
            else
            {
                dayStatus = StatusEnum.AttendanceAbsent;
            }
        }
        else if (segments.All(s => s.Status == StatusEnum.AttendanceOnLeave))
        {
            dayStatus = StatusEnum.AttendanceOnLeave;
        }
        else if (segments.All(s => s.Status == StatusEnum.AttendanceNoShift))
        {
            dayStatus = StatusEnum.AttendanceNoShift;
        }
        else if (segments.Any(s => s.Status == StatusEnum.AttendanceAbsent))
        {
            dayStatus = StatusEnum.AttendanceAbsent;
        }
        else if (segments.Any(s => s.Status == StatusEnum.AttendanceMissingOut))
        {
            dayStatus = StatusEnum.AttendanceMissingOut;
        }
        else if (totalLate > 0 && totalEarly > 0)
        {
            dayStatus = StatusEnum.AttendanceLate;
        }
        else if (totalLate > 0)
        {
            dayStatus = StatusEnum.AttendanceLate;
        }
        else if (totalEarly > 0)
        {
            dayStatus = StatusEnum.AttendanceEarlyLeave;
        }

        var resolutionLog = new
        {
            TotalRawLogs = orderedLogs.Count,
            MissingOutCount = missingOutCount,
            OutBeforeInCount = outBeforeInCount,
            UnmappedLogCount = unmappedLogCount,
            ProximityWindowMinutes = proximityWindowMinutes,
            TotalLateMinutes = totalLate,
            TotalEarlyLeaveMinutes = totalEarly,
            AbsentSegmentCount = absentSegments.Count,
            AbsentPenaltyMinutes = absentSegments.Sum(s => s.EarlyLeaveMinutes),
            StayLateMinutes = totalLateOutOTMinutes + totalEarlyInOTMinutes,
            EarlyInOTMinutes = totalEarlyInOTMinutes,
            LateOutOTMinutes = totalLateOutOTMinutes,
            ApprovedOTMinutes = totalOtMinutes,
            HasApprovedOTRequest = approvedOT != null,
            LeaveSegmentCount = approvedLeaveSegments.Count,
            TotalOTHours = otHours,
            TotalActualWorkedMinutes = totalActualWorkedMinutes
        };

        // Xác định anomaly flag
        var anomalyFlags = new List<string>();
        if (hasMissingOut) anomalyFlags.Add("MissingOut");
        if (outBeforeInCount > 0) anomalyFlags.Add("OutBeforeIn");
        if (unmatchedPairCount > 0) anomalyFlags.Add("UnmappedPairs");
        if ((totalLateOutOTMinutes > 0 || totalEarlyInOTMinutes > 0) && approvedOT == null) anomalyFlags.Add("OTWithoutRequest");
        if (orphanedOutLog != null) anomalyFlags.Add("OrphanedOut");
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
        else
        {
            if (existing.IsManuallyAdjusted)
            {
                _logger.LogInformation("Bỏ qua Employee {EmployeeId} ngày {Date} do HR đã chỉnh sửa tay.", employeeId, workDate);
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

    }

    private async Task<Shift?> ResolveShiftAsync((EmployeeShiftPattern? Pattern, ShiftPattern? Definition) details, DateOnly workDate, Dictionary<string, Shift?> shiftCache)
    {
        if (details.Pattern == null || details.Definition == null)
            return null;

        var cycleLength = details.Definition.CycleLengthDays;
        if (cycleLength <= 0)
            return null;

        var dayOffset = workDate.DayNumber - details.Pattern.EffectiveStartDate.DayNumber;
        var dayIndex = dayOffset % cycleLength;
        if (dayIndex < 0) dayIndex += cycleLength;

        var cacheKey = $"{details.Definition.Id}_{dayIndex}";
        if (shiftCache.TryGetValue(cacheKey, out var cachedShift))
            return cachedShift;

        var patternDay = await _shiftPatternRepository.GetShiftPatternWithDaysAsync(
            details.Definition.Id, dayIndex);   // Fix: thêm ShiftPatternId để tránh lấy nhầm lịch khác
        if (patternDay?.ScheduledShiftId == null)
            return null;

        var shift = await _shiftPatternRepository.GetShiftWithSegmentsAsync(patternDay.ScheduledShiftId.Value);
        shiftCache[cacheKey] = shift;
        return shift;
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