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

            var groupedByEmployeeDate = dedupedLogs
                .GroupBy(x => new
                {
                    x.EmployeeId,
                    WorkDate = DateOnly.FromDateTime(
                        TimeZoneInfo.ConvertTimeFromUtc(x.Timestamp, VietnamTimeZone))
                })
                .ToList();

            await _transaction.ExecuteAsync(async () =>
            {
                foreach (var group in groupedByEmployeeDate)
                {
                    var orderedLogs = group.OrderBy(x => x.Timestamp).ThenBy(x => x.Id).ToList();
                    var pairResult = BuildPairs(orderedLogs, group.Key.WorkDate);

                    await UpsertDailyTimesheetAsync(
                        group.Key.EmployeeId,
                        group.Key.WorkDate,
                        pairResult.Pairs,
                        pairResult.OutBeforeInCount,
                        attendanceSetting);
                }

                await _rawPunchLogRepository.MarkProcessedAsync(rawLogs.Select(x => x.Id));
            });

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

        for (var i = 0; i < pairs.Count; i++)
        {
            var pair = pairs[i];
            var targetSegment = shiftSegments != null && i < shiftSegments.Count ? shiftSegments[i] : null;

            var expectedIn = targetSegment != null
                ? CombineDateTime(pair.WorkDate, targetSegment.StartTime, targetSegment.StartDayOffset)
                : (DateTime?)null;
            var expectedOut = targetSegment != null
                ? CombineDateTime(pair.WorkDate, targetSegment.EndTime, targetSegment.EndDayOffset)
                : (DateTime?)null;

            var actualIn = pair.InLog.Timestamp;
            var actualOut = pair.OutLog?.Timestamp;

            var lateMinutes = 0;
            var earlyLeaveMinutes = 0;
            var segmentStatus = pair.MissingOut ? "MissingOut" : "Normal";

            // Nếu ShiftSegment này nằm trong danh sách đã được nghỉ phép → miễn trừ
            if (targetSegment != null && approvedLeaveSegmentIds.Contains(targetSegment.Id))
            {
                segmentStatus = "OnLeave";
                // Không tính Late/EarlyLeave cho ca đã có phép
            }
            else
            {
                if (expectedIn.HasValue)
                {
                    var grace = shift?.GracePeriodMinutes ?? 0;
                    var lateThreshold = attendanceSetting?.LateThresholdMinutes ?? 0;
                    var lateAllowance = Math.Max(grace, lateThreshold);
                    var rawLate = (int)Math.Round((actualIn - expectedIn.Value).TotalMinutes);
                    lateMinutes = Math.Max(0, rawLate - lateAllowance);
                }

                if (expectedOut.HasValue && actualOut.HasValue)
                {
                    var earlyThreshold = attendanceSetting?.EarlyLeaveThresholdMinutes ?? 0;
                    var rawEarly = (int)Math.Round((expectedOut.Value - actualOut.Value).TotalMinutes);
                    earlyLeaveMinutes = Math.Max(0, rawEarly - earlyThreshold);
                }
            }

            totalLate += lateMinutes;
            totalEarly += earlyLeaveMinutes;

            // Tính số phút "Ở lại trễ" (Stay Late) — chưa phải OT
            if (expectedOut.HasValue && actualOut.HasValue)
            {
                var stayLate = (int)Math.Round((actualOut.Value - expectedOut.Value).TotalMinutes);
                if (stayLate > 0)
                    totalStayLateMinutes += stayLate;
            }

            segments.Add(new DailyTimesheetSegment
            {
                Id = Guid.NewGuid(),
                DailyTimesheetId = Guid.Empty,
                TargetShiftSegmentId = targetSegment?.Id,
                ActualCheckIn = actualIn,
                ActualCheckOut = actualOut,
                CheckInLatitude = pair.InLog.Latitude,
                CheckInLongitude = pair.InLog.Longitude,
                CheckInSelfieUrl = pair.InLog.SelfieUrl ?? string.Empty,
                CheckOutLatitude = pair.OutLog?.Latitude,
                CheckOutLongitude = pair.OutLog?.Longitude,
                CheckOutSelfieUrl = pair.OutLog?.SelfieUrl ?? string.Empty,
                LateMinutes = lateMinutes,
                EarlyLeaveMinutes = earlyLeaveMinutes,
                Status = segmentStatus
            });
        }

        // OT hợp lệ chỉ được tính khi có OvertimeRequest đã duyệt
        var totalOtMinutes = 0;
        if (approvedOT != null)
        {
            // Lấy số giờ được duyệt (ApprovedHours), hoặc fallback sang RequestedHours
            var approvedMinutes = (int)((approvedOT.ApprovedHours ?? approvedOT.RequestedHours) * 60);
            // OT hợp lệ = Min(Số phút ở lại trễ thực tế, Số phút được duyệt)
            totalOtMinutes = Math.Min(totalStayLateMinutes, approvedMinutes);
        }

        var standardHours = shiftSegments == null
            ? 0m
            : Math.Round((decimal)shiftSegments.Sum(s => (s.EndTime - s.StartTime).TotalHours), 2);

        var resolutionLog = new
        {
            PairCount = pairs.Count,
            MissingOutCount = pairs.Count(p => p.MissingOut),
            OutBeforeInCount = outBeforeInCount,
            TotalLateMinutes = totalLate,
            TotalEarlyLeaveMinutes = totalEarly,
            StayLateMinutes = totalStayLateMinutes,
            ApprovedOTMinutes = totalOtMinutes,
            HasApprovedOTRequest = approvedOT != null,
            LeaveSegmentCount = approvedLeaveSegments.Count,
            TotalOTHours = attendanceSetting?.CalculateValidOTHours(totalOtMinutes) ?? Math.Round(totalOtMinutes / 60m, 2)
        };

        // Xác định anomaly flag
        var anomalyFlags = new List<string>();
        if (pairs.Any(p => p.MissingOut)) anomalyFlags.Add("MissingOut");
        if (outBeforeInCount > 0) anomalyFlags.Add("OutBeforeIn");
        var anomalyFlag = anomalyFlags.Count > 0 ? string.Join(",", anomalyFlags) : string.Empty;

        var existing = await _dailyTimesheetRepository.GetByEmployeeDateAsync(employeeId, workDate);
        if (existing == null)
        {
            var timesheet = new DailyTimesheet
            {
                Id = Guid.NewGuid(),
                EmployeeId = employeeId,
                WorkDate = workDate,
                ExpectedShiftId = shift?.Id,
                ExpectedShiftSource = shift == null ? "None" : "ShiftPattern",
                StandardWorkingHours = standardHours,
                TotalLateMinutes = totalLate,
                TotalEarlyLeaveMinutes = totalEarly,
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
        existing.TotalLateMinutes = totalLate;
        existing.TotalEarlyLeaveMinutes = totalEarly;
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

        var patternDay = details.Definition.Days.FirstOrDefault(d => d.DayIndex == dayIndex);
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