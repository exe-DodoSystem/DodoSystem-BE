# Attendance Flow Documentation — DodoSystem Backend

> Tài liệu phân tích chi tiết luồng Chấm Công (Attendance) trong hệ thống DodoSystem.
> Cập nhật: 2026-06-18 (phản ánh toàn bộ thay đổi từ Phase 1–7 + realtime notifications GAP-01, GAP-03, GAP-06)

---

## 1. Tổng quan kiến trúc

Hệ thống Attendance hoạt động theo mô hình **2 giai đoạn**:

1. **Submit Phase** — Nhân viên bấm check-in/out → ghi vào `RawPunchLog` (append-only, chưa xử lý)
2. **Resolution Phase** — Background Job định kỳ đọc `RawPunchLog`, tính toán và tạo `DailyTimesheet`

Đây là thiết kế quan trọng: **API không tính ngay kết quả**, kết quả được tính bởi background job.

### Nguyên tắc bất biến

- `RawPunchLog` là **append-only** — không bao giờ UPDATE hay DELETE, chỉ thêm mới và đánh dấu `IsProcessed`
- Khi job xử lý thất bại, log **không bị mark processed** — sẽ được retry tối đa 3 lần trước khi dead-letter
- Khi job xử lý lại một ngày, nó luôn lấy **toàn bộ log của ngày đó** (cả đã và chưa processed) để đảm bảo tính toán đầy đủ
- Mọi tính toán WorkDate đều dùng múi giờ **Vietnam (SE Asia Standard Time / Asia/Ho_Chi_Minh)**
- Mọi timestamp lưu DB đều là **UTC**

---

## 2. Các Entity chính

| Entity | File | Mô tả |
|--------|------|-------|
| `RawPunchLog` | `Core/Entities/RawPunchLog.cs` | Log thô, append-only. Mỗi lần bấm check-in/out tạo 1 record. Có thêm `RetryCount` theo dõi số lần xử lý thất bại |
| `DailyTimesheet` | `Core/Entities/DailyTimesheet.cs` | Bảng tổng hợp chấm công theo ngày của từng nhân viên. Được tạo/cập nhật bởi background job |
| `DailyTimesheetSegment` | `Core/Entities/DailyTimesheetSegment.cs` | Chi tiết theo từng ca (segment) trong ngày: giờ vào, ra, trễ, về sớm |
| `TimesheetAppeal` | `Core/Entities/TimesheetAppeal.cs` | Yêu cầu chỉnh sửa công của nhân viên khi quên check-in/out |
| `TenantAttendanceSetting` | `Core/Entities/TenantAttendanceSetting.cs` | Cấu hình chấm công của từng tenant (GPS, ngưỡng trễ, OT...) |
| `PublicHoliday` | `Core/Entities/PublicHoliday.cs` | Ngày nghỉ lễ của tenant. Hỗ trợ `IsRecurringYearly` để tự lặp hàng năm |
| `Shift` | `Core/Entities/Shift.cs` | Định nghĩa ca làm việc |
| `ShiftSegment` | `Core/Entities/ShiftSegment.cs` | Khoảng thời gian trong ca (sáng, chiều, tối). Có `StartDayOffset`/`EndDayOffset` để hỗ trợ ca đêm xuyên ngày |
| `ShiftPattern` | `Core/Entities/ShiftPattern.cs` | Lịch ca luân phiên (cycle-based) |
| `ShiftPatternDay` | `Core/Entities/ShiftPatternDay.cs` | Ngày thứ mấy trong chu kỳ → ca nào |
| `EmployeeShiftPattern` | `Core/Entities/EmployeeShiftPattern.cs` | Gán lịch ca cho nhân viên |

### RawPunchLog — Các field quan trọng

| Field | Kiểu | Mô tả |
|-------|------|-------|
| `IsProcessed` | `bool` | `false` = chờ job xử lý. `true` = đã được tính vào DailyTimesheet |
| `RetryCount` | `int` | Số lần xử lý thất bại. Khi ≥ 3 → bị đánh dấu dead-letter (mark processed, log Critical) |
| `PunchType` | `string` | `"In"` / `"Out"` / `"Auto"` (hệ thống tự phán quyết) |
| `DeviceId` | `string?` | `"HR_Manual"` khi được tạo bởi HR thay vì thiết bị thực |
| `Timestamp` | `DateTime` | Thời điểm UTC |

### PublicHoliday — Các field

| Field | Kiểu | Mô tả |
|-------|------|-------|
| `TenantId` | `Guid` | Ngày lễ riêng của từng tenant |
| `Date` | `DateOnly` | Ngày lễ |
| `Name` | `string` | Tên ngày lễ (VD: "Ngày Thống Nhất 30/4") |
| `IsRecurringYearly` | `bool` | `true` = tự lặp lại mỗi năm (chỉ so sánh Month + Day) |

---

## 3. Controllers & API Endpoints

### 3.1 AttendanceController
- **File**: `WebAPI/Controllers/AttendanceController.cs`
- **Base route**: `api/v1/attendance`
- **Auth**: Bắt buộc JWT Bearer Token cho toàn bộ controller

#### Employee Endpoints (mọi user đã đăng nhập)

| Method | Endpoint | Mô tả |
|--------|----------|-------|
| `POST` | `/submit-punch` | Nhân viên bấm check-in hoặc check-out |
| `GET` | `/my-today` | Xem trạng thái chấm công hôm nay |
| `GET` | `/my-history?month=&year=` | Xem lịch sử chấm công theo tháng |
| `POST` | `/appeals` | Gửi yêu cầu giải trình (quên check-in/out) |
| `GET` | `/appeals` | Xem danh sách appeal của mình |

#### HR/Admin Endpoints (`[Authorize(Roles = "Admin,HR")]`)

| Method | Endpoint | Mô tả |
|--------|----------|-------|
| `POST` | `/manual-punch` | HR nhập tay check-in/out cho nhân viên |
| `POST` | `/recalculate/{employeeId}?from=&to=` | Tái tính lại công trong khoảng ngày |
| `GET` | `/appeals/pending` | Xem tất cả appeal đang chờ duyệt |
| `PUT` | `/appeals/{appealId}/process` | Duyệt hoặc từ chối appeal |
| `GET` | `/hr-monthly-report?month=&year=` | Báo cáo chấm công tháng toàn bộ nhân viên |
| `POST` | `/holidays` | Thêm ngày lễ mới |
| `GET` | `/holidays` | Xem danh sách ngày lễ |
| `DELETE` | `/holidays/{id}` | Xóa ngày lễ |

### 3.2 AttendanceSettingController
- **File**: `WebAPI/Controllers/AttendanceSettingController.cs`
- **Base route**: `api/v1/attendance/setting`

| Method | Endpoint | Mô tả |
|--------|----------|-------|
| `GET` | `/` | Lấy cấu hình chấm công của tenant |
| `POST` | `/` | Tạo hoặc cập nhật cấu hình (Admin/HR) |

---

## 4. DTOs

### Request DTOs (Input)

| DTO | Dùng cho |
|-----|---------|
| `SubmitPunchRequestDto` | Body của `POST /submit-punch` |
| `ManualPunchRequestDto` | Body của `POST /manual-punch` |
| `SubmitAppealRequestDto` | Body của `POST /appeals` |
| `ApproveAppealRequestDto` | Body của `PUT /appeals/{id}/process` |
| `CreatePublicHolidayDto` | Body của `POST /holidays` |
| `UpdateAttendanceSettingRequestDto` | Body của `POST /setting` |

### Response DTOs (Output)

| DTO | Dùng cho |
|-----|---------|
| `RawPunchLogDto` | Response sau khi submit punch |
| `TodayAttendanceDto` | Trạng thái chấm công hôm nay |
| `MyAttendanceHistoryItemDto` | 1 ngày trong lịch sử |
| `MyAttendanceSegmentDto` | Chi tiết từng ca trong ngày |
| `TimesheetAppealDto` | Record appeal |
| `PublicHolidayDto` | Record ngày lễ |
| `AttendanceSettingDto` | Cấu hình tenant |
| `HRMonthlyReportItemDto` | Tóm tắt 1 nhân viên trong báo cáo tháng |

---

## 5. Services

### 5.1 AttendanceService
- **Interface**: `Application/Interfaces/IServices/IAttendanceService.cs`
- **Implementation**: `Application/Services/AttendanceService.cs`

Xử lý tất cả request từ Controller: validate input, kiểm tra nghiệp vụ, persist vào DB.

### 5.2 AttendanceResolutionService
- **Interface**: `Application/Interfaces/IServices/IAttendanceResolutionService.cs`
- **Implementation**: `Application/Services/AttendanceResolutionService.cs`

Service lõi tính toán `DailyTimesheet` từ `RawPunchLog`. Được gọi bởi Background Job (Hangfire). Hỗ trợ xử lý multi-tenant: lặp qua tất cả tenant nếu không có tenant context, hoặc chỉ xử lý tenant hiện tại.

---

## 6. Luồng Chi Tiết Từng Use Case

---

### LUỒNG A: Nhân viên Check-in / Check-out

```
[MOBILE APP] → POST /api/v1/attendance/submit-punch
{
  latitude: 10.7769,
  longitude: 106.7009,
  selfieUrl: "https://...",
  deviceId: "iPhone-XYZ",
  punchType: "Auto",         // "In" | "Out" | "Auto"
  isMockLocation: false      // FakeGPS flag từ app
}
```

**Bước 1 — Authenticate & load context:**
- Extract `userId` từ JWT claim (`ClaimTypes.NameIdentifier`)
- Load `Employee` từ DB theo `userId`
- Load `TenantAttendanceSetting` của tenant

**Bước 2 — Kiểm tra GPS:**
```
if isMockLocation == true
    → Từ chối: "FakeGPS: Phát hiện sử dụng phần mềm giả mạo vị trí"

if setting.Latitude != null && setting.Longitude != null:
    if request.Latitude == null || request.Longitude == null
        → Từ chối: "BatBuocGPS: Vui lòng bật định vị GPS để chấm công"

    distance = GeoHelper.DistanceInMeters(userLat, userLon, setting.Lat, setting.Lon)
    if distance > setting.CheckInRadiusMeters
        → Từ chối: "NgoaiVung: Bạn đang ở ngoài vùng chấm công ({distance}m > {radius}m)"
```

**Bước 3 — Tạo RawPunchLog:**
```
RawPunchLog {
  Id: UUID mới,
  TenantId, EmployeeId,
  Timestamp: DateTime.UtcNow,
  DeviceId, Latitude, Longitude, SelfieUrl,
  PunchType: request.PunchType ?? "Auto",
  IsProcessed: false,   ← Chưa được xử lý
  RetryCount: 0
}
```

**Bước 4 — Trả về:**
```json
{
  "data": {
    "id": "uuid",
    "employeeId": "uuid",
    "timestamp": "2026-05-15T01:30:00Z",
    "deviceId": "iPhone-XYZ",
    "isProcessed": false,
    "punchType": "Auto"
  },
  "message": "Punch submitted successfully"
}
```

> **Lưu ý:** `DailyTimesheet` chưa được tạo ở bước này. Background Job sẽ tính toán sau.

---

### LUỒNG B: Background Job xử lý (AttendanceResolutionRecurringJob)

File: `Application/BackgroundJobs/AttendanceResolutionRecurringJob.cs`

Job chạy định kỳ (Hangfire), gọi `ProcessUnresolvedPunchesAsync()`.

#### B.1 — Xác định tenant cần xử lý

```
if _currentTenantService.TenantId == null:
    // Background job: không có HTTP context → xử lý tất cả tenant
    tenants = _tenantRepository.GetAllIgnoreTenantAsync()  // IgnoreQueryFilters
    foreach tenant:
        _currentTenantService.SetTenantId(tenant.Id)
        ProcessTenantAsync(tenant.Id)
    _currentTenantService.SetTenantId(null)   // Reset sau khi xong
else:
    ProcessTenantAsync(_currentTenantService.TenantId)
```

> **Lưu ý kiến trúc:** `SMEFLOWSystemContext` dùng `_currentTenantService.TenantId` (dynamic) trong query filter — không cache vào readonly field. Nên khi `SetTenantId` được gọi, tất cả query tiếp theo tự động lọc đúng tenant.

#### B.2 — Lấy batch logs chưa xử lý

```sql
SELECT TOP 500 * FROM RawPunchLogs
WHERE IsProcessed = false
ORDER BY Timestamp, Id
```

Dừng khi batch rỗng. Giới hạn số batch per run theo `MaxBatchesPerRun`.

#### B.3 — Deduplication (lọc log trùng)

Mục tiêu: tránh bấm nhầm nhiều lần trong thời gian ngắn.

**Quy tắc dedup:**
- Group log theo `EmployeeId`
- Sort theo `Timestamp` tăng dần
- Chỉ dedup giữa 2 log **cùng PunchType** — log In và Out kề nhau **không** bị dedup dù rất gần nhau
- Nếu 2 log cùng PunchType cách nhau < `DedupWindowMinutes` → bỏ log sau

```
Ví dụ (DedupWindowMinutes = 5):
  08:00 In  → giữ (lastKept = 08:00 In)
  08:02 Out → giữ (PunchType khác → không dedup)
  08:03 Out → BỎ (cùng PunchType Out, diff = 1 phút < 5)
  08:10 In  → giữ (PunchType khác → không dedup)
```

#### B.4 — Nhóm log theo Employee + WorkDate

```
WorkDate = theo múi giờ Vietnam:
  localTime = ConvertTimeFromUtc(log.Timestamp, VietnamTimeZone)
  if localTime.TimeOfDay < DayStartCutOffTime (default 04:00):
    WorkDate = localTime.Date - 1 ngày   ← ca đêm khuya thuộc về ngày hôm trước
  else:
    WorkDate = localTime.Date
```

#### B.5 — Bulk Load dữ liệu tham chiếu (tránh N+1)

Trước khi vào vòng lặp, load sẵn tất cả vào memory cho toàn bộ batch:

```
employeeIds = distinct EmployeeIds trong batch
minDate / maxDate = min/max WorkDate trong batch

BulkLoad:
  EmployeeShiftPatterns ← GetActivePatternsForEmployeesAsync(employeeIds, minDate, maxDate)
  ShiftPatterns         ← GetPatternsWithDaysAsync(patternIds)
  Shifts                ← GetShiftsWithSegmentsAsync(shiftIds)
  LeaveSegments         ← GetApprovedSegmentsForEmployeesAsync(employeeIds, minDate, maxDate)
  ApprovedOTs           ← GetApprovedRequestsForEmployeesAsync(employeeIds, minDate, maxDate)
  ExistingTimesheets    ← GetWithSegmentsForEmployeesAsync(employeeIds, minDate, maxDate)
  PublicHolidays        ← GetAllAsync(tenantId)
```

Trong vòng lặp, mọi lookup đều dùng in-memory lists → không query DB thêm (trừ `GetByEmployeeAndDateRangeAsync` cho allLogsForDay).

#### B.6 — Xử lý từng nhóm Employee-Date

Mỗi nhóm chạy trong:
1. **SemaphoreSlim lock** theo key `{EmployeeId}:{WorkDate}` — ngăn race condition nếu nhiều batch xử lý cùng lúc
2. **Transaction** — đảm bảo atomicity: tính toán + mark processed hoặc không làm gì cả

```
foreach group (EmployeeId, WorkDate):
    lock = SemaphoreSlim["{EmployeeId}:{WorkDate}"]
    await lock.WaitAsync()
    try:
        await transaction.ExecuteAsync(async () =>
        {
            // 1. Lấy TOÀN BỘ log của ngày đó (cả đã và chưa processed)
            utcDayStart = ConvertTimeToUtc(WorkDate + cutOffTime, VietnamTimeZone)
            utcDayEnd   = utcDayStart.AddDays(1)
            allLogsForDay = GetByEmployeeAndDateRangeAsync(EmployeeId, utcDayStart, utcDayEnd)

            // 2. Tính toán và upsert DailyTimesheet
            UpsertDailyTimesheetAsync(EmployeeId, WorkDate, allLogsForDay, ...)

            // 3. Chỉ mark processed cho log MỚI trong batch này (không phải allLogsForDay)
            newLogIds = group.Select(x => x.Log.Id)
            MarkProcessedAsync(newLogIds)
        })
    catch Exception:
        // Xem B.7 — Retry mechanism
    finally:
        lock.Release()
```

#### B.7 — Retry mechanism khi xử lý thất bại

```
const MaxRetryCount = 3

catch (Exception ex):
    LogError(ex, "Thất bại khi xử lý EmployeeId {id} ngày {date}")

    failedLogIds = group.Select(x => x.Log.Id)

    // Log đã thử quá MaxRetryCount lần → dead-letter (không retry nữa)
    maxRetriedLogs = failedLogIds.Where(log.RetryCount >= MaxRetryCount)
    if maxRetriedLogs.Any():
        LogCritical("Log đã thất bại {Max} lần, cần kiểm tra thủ công")
        MarkProcessedAsync(maxRetriedLogs)   ← mark để không làm nghẽn job

    // Log còn lại → tăng RetryCount, để job sau pick up lại
    retryableLogIds = failedLogIds.Except(maxRetriedLogs)
    if retryableLogIds.Any():
        IncrementRetryCountAsync(retryableLogIds)
    // KHÔNG gọi MarkProcessedAsync → job lần sau sẽ retry
```

**Vòng đời retry của 1 log:**
```
Lần 1: Fail → RetryCount 0→1, IsProcessed=false → retry
Lần 2: Fail → RetryCount 1→2, IsProcessed=false → retry
Lần 3: Fail → RetryCount 2→3, IsProcessed=false → retry
Lần 4: Fail → RetryCount=3 >= 3 → MarkProcessed (dead-letter) + LogCritical
```

#### B.8 — UpsertDailyTimesheetAsync — Logic chi tiết

**Bước 8.0 — Kiểm tra ngày lễ không có log (early-return):**

```
isPublicHoliday = PublicHolidays.Any(
    h.Date == workDate ||
    (h.IsRecurringYearly && h.Month == workDate.Month && h.Day == workDate.Day))

if isPublicHoliday && orderedLogs.Count == 0:
    // Ngày lễ, nhân viên không đi làm → ghi Holiday và thoát luôn
    // Dù nhân viên có ca được gán hay không, status vẫn là Holiday
    Create/Update DailyTimesheet { Status = "Holiday", tất cả metric = 0, Segments = [] }
    return   ← Bỏ qua toàn bộ tính toán phía dưới
```

> Nếu nhân viên **đi làm vào ngày lễ** (`orderedLogs.Count > 0`) → tiếp tục xử lý bình thường, OT được tính nếu có OvertimeRequest được duyệt.

**Bước 8.1 — Xác định ca làm việc:**

```
ESP = EmployeeShiftPattern hiệu lực tại WorkDate
      (EffectiveStartDate ≤ WorkDate ≤ EffectiveEndDate)

if ESP != null:
    pattern = ShiftPattern của ESP
    dayOffset = WorkDate.DayNumber - ESP.EffectiveStartDate.DayNumber
    dayIndex  = dayOffset % pattern.CycleLengthDays
    shift     = Shift của ShiftPatternDay[dayIndex]
    shiftSegments = shift.Segments (sorted by StartDayOffset, StartTime)
else:
    shift = null, shiftSegments = null
```

**Bước 8.2 — Load thông tin nghỉ phép và OT:**

```
approvedLeaveSegments = LeaveSegments
    .Where(s.LeaveDate == workDate && s.LeaveRequest.EmployeeId == employeeId)

approvedLeaveSegmentIds = Set<Guid>(approvedLeaveSegments.Select(s.TargetShiftSegmentId))

approvedOT = ApprovedOTs
    .FirstOrDefault(x.EmployeeId == employeeId && x.OvertimeDate == workDate)
```

**Bước 8.3a — TH1: Không có ca làm việc (shiftSegments == null hoặc rỗng):**

```
if shiftSegments == null || shiftSegments.Count == 0:
    pairs = BuildPairs(orderedLogs)    ← Ghép In-Out theo thứ tự thời gian
    foreach pair:
        actualWorkedMinutes = (pair.OutLog.Timestamp - pair.InLog.Timestamp).Minutes
        totalActualWorkedMinutes += actualWorkedMinutes
        totalLateOutOTMinutes += actualWorkedMinutes  ← Tính là OT nguyên ngày
        segment = new DailyTimesheetSegment { Status = "NoShift" }
```

**Bước 8.3b — TH2: Có ca làm việc — Proximity Matching:**

```
unmappedLogs = orderedLogs.ToList()   ← Pool log chưa được ghép

foreach targetSegment in shiftSegments:
    expectedIn  = WorkDate + targetSegment.StartTime + StartDayOffset (ngày)
    expectedOut = WorkDate + targetSegment.EndTime   + EndDayOffset   (ngày)

    // Tìm InLog gần expectedIn nhất (trong ProximityWindowMinutes = 240')
    bestInLog = unmappedLogs
        .MinBy(diff = |ConvertToLocal(log.Timestamp) - expectedIn|)
        .Where(diff <= proximityWindowMinutes)
    if bestInLog found: unmappedLogs.Remove(bestInLog)

    // Tìm OutLog gần expectedOut nhất, phải sau InLog
    bestOutLog = unmappedLogs
        .Where(log.Timestamp >= bestInLog.Timestamp)   // Ra phải sau Vào
        .MinBy(diff = |ConvertToLocal(log.Timestamp) - expectedOut|)
        .Where(diff <= proximityWindowMinutes)
    if bestOutLog found: unmappedLogs.Remove(bestOutLog)

    // Xử lý trường hợp thiếu log:
    if bestInLog == null && bestOutLog == null:
        status = approvedLeaveSegmentIds.Contains(segment.Id) ? "OnLeave" : "Absent"
        segments.Add({ Status = status, LateMinutes = 0, EarlyLeaveMinutes = 0 })
        continue

    // Tính Đi Trễ / Về Sớm:
    actualIn  = ConvertToLocal(bestInLog.Timestamp)
    actualOut = ConvertToLocal(bestOutLog.Timestamp)

    if bestInLog == null:          // Thiếu In
        lateMins = max(0, actualOut - expectedIn)   ← Phạt từ đầu ca
        hasMissingOut = true
    else:
        rawLate = actualIn - expectedIn
        lateMins = max(0, rawLate - graceMinutes)   ← Trừ grace period
        earlyInOT = max(0, expectedIn - actualIn)   ← Đến sớm trước ca

    if bestOutLog == null:         // Thiếu Out
        earlyMins = max(0, expectedOut - actualIn)  ← Phạt từ lúc quẹt vào
        hasMissingOut = true
    else:
        rawEarly = expectedOut - actualOut
        earlyMins = max(0, rawEarly)
        lateOutOT = max(0, actualOut - expectedOut) ← Về trễ sau ca

    if approvedLeaveSegmentIds.Contains(segment.Id):
        lateMins = 0, earlyMins = 0    ← Không tính phạt khi nghỉ phép

    totalLate  += lateMins
    totalEarly += earlyMins
    totalActualWorkedMinutes += (actualOut - actualIn).Minutes

    segmentStatus = approvedLeaveSegmentIds.Contains(segment.Id) ? "OnLeave"
                  : (bestInLog == null || bestOutLog == null)     ? "MissingOut"
                  : "Normal"
    segments.Add({ ..., Status = segmentStatus })
```

**Bước 8.4 — Áp dụng ngưỡng trễ/về sớm:**

```
if totalLate  <= LateThresholdMinutes  → totalLate  = 0   ← Trong ngưỡng tha thứ
if totalEarly <= EarlyLeaveThresholdMinutes → totalEarly = 0
```

**Bước 8.5 — Tính OT hợp lệ:**

```
if approvedOT != null:
    approvedMinutes = (approvedOT.ApprovedHours ?? approvedOT.RequestedHours) × 60
    validLateOut = min(totalLateOutOTMinutes, approvedMinutes)
    validEarlyIn = min(totalEarlyInOTMinutes, approvedMinutes - validLateOut)
    totalOtMinutes = validLateOut + validEarlyIn
else:
    totalOtMinutes = 0
    // Flag "OTWithoutRequest" nếu có làm OT thực tế

otHours = CalculateValidOTHours(totalOtMinutes)   ← Round theo OTBlockMinutes
```

**Bước 8.6 — Xác định DayStatus (theo thứ tự ưu tiên):**

```
if segments.Count == 0:
    if isPublicHoliday    → "Holiday"   ← Ngày lễ, không có ca, không có log
    elif shiftSegments empty → "NoShift"
    else                  → "Absent"    ← Có ca nhưng không có log nào hết
elif all segments == "OnLeave"  → "OnLeave"
elif all segments == "NoShift"  → "NoShift"
elif any segment  == "Absent"   → "Absent"
elif any segment  == "MissingOut" → "MissingOut"
elif totalLate > 0 && totalEarly > 0 → "Late"    ← Vừa trễ vừa về sớm → ưu tiên Late
elif totalLate > 0              → "Late"
elif totalEarly > 0             → "EarlyLeave"
else                            → "Normal"
```

**Bước 8.7 — Xác định AnomalyFlags:**

```
anomalyFlags = []
if hasMissingOut:                         → "MissingOut"
if outBeforeInCount > 0:                  → "OutBeforeIn"
if unmatchedPairCount > 0:                → "UnmappedPairs"
if (totalOTActual > 0) && approvedOT == null: → "OTWithoutRequest"
if orphanedOutLog != null:                → "OrphanedOut"

SystemAnomalyFlag = join(",", anomalyFlags)
```

**Bước 8.8 — Tạo hoặc cập nhật DailyTimesheet:**

```
existing = ExistingTimesheets.FirstOrDefault(EmployeeId == x && WorkDate == x)

if existing == null:
    AddAsync(new DailyTimesheet { ..., Segments = segments })
    bulkData.ExistingTimesheets.Add(newTimesheet)   ← Cache cho batch hiện tại
else:
    if existing.IsManuallyAdjusted:
        Log("Bỏ qua vì HR đã chỉnh sửa tay")
        return   ← Không ghi đè dữ liệu HR đã chỉnh

    existing.Status = dayStatus
    existing.Segments.Clear()   ← EF Core cascade delete (DailyTimesheetId non-nullable)
    foreach segment: existing.Segments.Add(segment)
    UpdateAsync(existing)
```

---

### LUỒNG C: Nhân viên xem trạng thái hôm nay

```
GET /api/v1/attendance/my-today
```

**Bước 1 — Xác định WorkDate hôm nay:**
```
localNow = ConvertTimeFromUtc(DateTime.UtcNow, VietnamTimeZone)
cutOff   = attendanceSetting.DayStartCutOffTime ?? 04:00
workDate = localNow.TimeOfDay < cutOff
    ? localNow.Date - 1 ngày
    : localNow.Date
```

**Bước 2 — Đọc DailyTimesheet:**
```
timesheet = GetByEmployeeDateAsync(employee.Id, workDate)
```

**Bước 3a — Nếu đã có DailyTimesheet (job đã chạy):**
```
firstSegment = Segments.OrderBy(ActualCheckIn).First(s.ActualCheckIn != null)
lastSegment  = Segments.OrderByDescending(ActualCheckOut).First()

result = {
    hasCheckedIn  = firstSegment.ActualCheckIn != null,
    checkInTime   = firstSegment.ActualCheckIn,
    hasCheckedOut = lastSegment.ActualCheckOut != null,
    checkOutTime  = lastSegment.ActualCheckOut,
    lateMinutes   = timesheet.TotalLateMinutes,
    status        = timesheet.Status ?? "Present"
}
```

**Bước 3b — Nếu chưa có DailyTimesheet (job chưa chạy) — fallback:**
```
// Tính UTC range chính xác cho workDate theo múi giờ VN
localDayStart = workDate.ToDateTime(cutOffTime)
fromDateUtc   = ConvertTimeToUtc(localDayStart, VietnamTimeZone)   ← local → UTC
toDateUtc     = fromDateUtc.AddDays(1)

rawLogs = GetByEmployeeAndDateRangeAsync(employee.Id, fromDateUtc, toDateUtc)
if rawLogs.Any():
    result.hasCheckedIn  = true
    result.checkInTime   = rawLogs.First().Timestamp
    result.hasCheckedOut = rawLogs.Count > 1
    result.checkOutTime  = rawLogs.Last().Timestamp (nếu có)
```

---

### LUỒNG D: Nhân viên gửi Appeal (quên check-in/out)

#### D.1 — Nhân viên gửi yêu cầu

```
POST /api/v1/attendance/appeals
{
  workDate: "2026-05-15",
  appealType: "In" | "Out" | "Both",
  requestedCheckIn: "2026-05-15T01:30:00Z",    // UTC
  requestedCheckOut: "2026-05-15T10:00:00Z",   // UTC (nếu appealType = Out hoặc Both)
  reason: "Quên bấm check-in, có mặt từ 8:30",
  attachmentUrl: "https://..."
}
```

**Validate đầu vào:**
```
today = DateOnly.FromDateTime(ConvertTimeFromUtc(DateTime.UtcNow, VietnamTimeZone))

// Không cho appeal ngày hôm nay hoặc tương lai
if request.WorkDate >= today:
    throw "Không thể giải trình cho ngày hôm nay hoặc tương lai"

// Không cho appeal quá 30 ngày về trước
if (today - request.WorkDate) > 30:
    throw "Không thể giải trình cho ngày quá hạn 30 ngày"

// Không cho gửi 2 appeal cùng ngày khi đang chờ duyệt
existingPending = GetPendingByEmployeeDateAsync(employee.Id, request.WorkDate)
if existingPending != null:
    throw "Đã có đơn giải trình đang chờ duyệt cho ngày này"
```

**Tạo appeal:**
```
TimesheetAppeal {
    Status: "PendingApproval",
    EmployeeId, WorkDate, AppealType,
    RequestedCheckIn, RequestedCheckOut,
    Reason, AttachmentUrl
}
```

**Realtime notification (GAP-01):**
```
[Fire-and-forget]
_ = _realtime.NotifyAppealSubmittedAsync(tenantId, {
    appealId:     "uuid",
    employeeId:   employee.Id,
    employeeName: employee.FullName,
    workDate:     request.WorkDate,
    appealType:   request.AppealType,
    reason:       request.Reason,
    submittedAt:  DateTime.UtcNow
}).ContinueWith(log if faulted)
→ Gửi tới group "tenant:{tenantId}:admins" — HR thấy ngay lập tức
```

#### D.2 — HR xem và xử lý

```
GET  /api/v1/attendance/appeals/pending    → List<TimesheetAppealDto> (Status = "PendingApproval")
PUT  /api/v1/attendance/appeals/{id}/process
{
  isApproved: true | false,
  rejectReason: "..." (nếu từ chối)
}
```

**Toàn bộ khối approve/reject chạy trong 1 transaction:**

**Nếu Approved:**
```
appeal.Status = "Approved"
appeal.ApprovedBy = hrEmployee.Id
appeal.ApprovedAt = DateTime.UtcNow

// Tạo HR_Manual punch logs với IsProcessed = false (explicit)
if (appealType == "In" || appealType == "Both") && requestedCheckIn != null:
    AddAsync(new RawPunchLog {
        EmployeeId, Timestamp = requestedCheckIn,
        PunchType = "In",
        DeviceId = "HR_Manual",
        IsProcessed = false   ← Explicit set để job pick up
    })

if (appealType == "Out" || appealType == "Both") && requestedCheckOut != null:
    AddAsync(new RawPunchLog {
        EmployeeId, Timestamp = requestedCheckOut,
        PunchType = "Out",
        DeviceId = "HR_Manual",
        IsProcessed = false
    })

// GAP-02: Nếu ngày đó đã bị chỉnh tay bởi HR → reset để cho phép recalculate
existingTimesheet = GetByEmployeeDateAsync(appeal.EmployeeId, appeal.WorkDate)
if existingTimesheet != null && existingTimesheet.IsManuallyAdjusted:
    existingTimesheet.IsManuallyAdjusted = false
    UpdateAsync(existingTimesheet)

// Reset toàn bộ log trong ngày đó → job sẽ tái tính
cutOffTime = attendanceSetting.DayStartCutOffTime ?? 04:00
utcFrom    = ConvertTimeToUtc(appeal.WorkDate + cutOffTime, VietnamTimeZone)
utcTo      = utcFrom.AddDays(1)
MarkUnprocessedForRecalculateAsync(appeal.EmployeeId, utcFrom, utcTo)

UpdateAsync(appeal)
```

**Nếu Rejected:**
```
appeal.Status = "Rejected"
appeal.RejectReason = request.RejectReason
UpdateAsync(appeal)
// Không tạo RawPunchLog, không reset IsProcessed
```

**Realtime notifications — Sau transaction (GAP-03):**
```
// Thông báo cho nhân viên
_ = _realtime.NotifyAppealProcessedAsync(employee.UserId.Value, {
    appealId:     appeal.Id,
    workDate:     appeal.WorkDate,
    status:       isApproved ? "Approved" : "Rejected",
    rejectReason: request.RejectReason,
    processedAt:  DateTime.UtcNow
}).ContinueWith(log if faulted)

// Refresh dashboard toàn tenant
_ = _realtime.NotifyDashboardRefreshAsync(tenantId)
    .ContinueWith(log if faulted)

// Cả 2 đều fire-and-forget, NGOÀI transaction block
```

#### D.3 — Background Job tái tính

```
Job chạy lần tiếp → phát hiện log HR_Manual với IsProcessed=false
→ Xử lý như B.6, load toàn bộ log của ngày đó
→ DailyTimesheet được cập nhật với giờ đã sửa

Nhân viên xem /my-history → thấy ngày đó đã cập nhật
```

---

### LUỒNG E: HR nhập tay check-in/out (Manual Punch)

```
POST /api/v1/attendance/manual-punch
{
  employeeId: "uuid",
  timestamp: "2026-05-15T01:30:00Z",   // UTC
  punchType: "In",
  reason: "Nhân viên có mặt nhưng quên bấm"
}
```

- Không validate GPS (HR tin tưởng)
- Không validate timezone (HR tự nhập UTC)
- Tạo `RawPunchLog` với `DeviceId = "HR_Manual"`, `IsProcessed = false`
- Background Job xử lý như bình thường ở lần chạy tiếp theo

**Realtime notification (GAP-06):**
```
if employee.UserId != null:
    _ = _realtime.NotifyAttendanceManualAdjustedAsync(employee.UserId.Value, {
        workDate:   workDate (tính từ timestamp theo VN timezone),
        punchType:  request.PunchType,
        timestamp:  request.Timestamp,
        adjustedBy: "HR Manager",
        note:       request.Reason
    }).ContinueWith(log if faulted)
→ Gửi tới "user:{userId}" — nhân viên biết HR đã bổ sung check-in/out thay mình
```

---

### LUỒNG F: Tái tính lại (Recalculate)

```
POST /api/v1/attendance/recalculate/{employeeId}?from=2026-05-01&to=2026-05-15
```

**Logic:**
```
cutOffTime = attendanceSetting.DayStartCutOffTime ?? 04:00

// Chuyển date range sang UTC chính xác dựa trên cutoff
utcFrom = ConvertTimeToUtc(fromDate + cutOffTime, VietnamTimeZone)
utcTo   = ConvertTimeToUtc(toDate   + cutOffTime, VietnamTimeZone).AddDays(1)

// Reset tất cả log trong dải đó → job sẽ pick up và tính lại
MarkUnprocessedForRecalculateAsync(employeeId, utcFrom, utcTo)
```

Response trả ngay, kết quả có sau khi background job chạy lần tiếp.

**Khi nào dùng:**
- Sau khi thay đổi `ShiftPattern` của nhân viên
- Sau khi thay đổi `TenantAttendanceSetting`
- Sau khi thêm/xóa `PublicHoliday`
- Sau khi phát hiện dữ liệu tính sai

---

### LUỒNG G: Nhân viên xem lịch sử tháng

```
GET /api/v1/attendance/my-history?month=5&year=2026
```

Trả về tất cả `DailyTimesheet` của tháng, mỗi ngày gồm:

```json
[
  {
    "workDate": "2026-05-15",
    "status": "Late",
    "standardWorkingHours": 8.0,
    "actualWorkHours": 8.5,
    "otHours": 0.0,
    "totalLateMinutes": 15,
    "totalEarlyLeaveMinutes": 0,
    "isManuallyAdjusted": false,
    "systemAnomalyFlag": "",
    "segments": [
      {
        "actualCheckIn": "2026-05-15T01:45:00Z",
        "actualCheckOut": "2026-05-15T10:30:00Z",
        "lateMinutes": 15,
        "earlyLeaveMinutes": 0,
        "status": "Late"
      }
    ]
  }
]
```

---

### LUỒNG H: HR xem báo cáo tháng

```
GET /api/v1/attendance/hr-monthly-report?month=5&year=2026
```

- Aggregate tất cả `DailyTimesheet` của tenant trong tháng
- Group theo Employee
- `MissingPunches` chỉ đếm ngày có `Status = "MissingOut"` — **không tính "NoShift"**

```json
[
  {
    "employeeId": "uuid",
    "employeeName": "Nguyễn Văn A",
    "month": 5, "year": 2026,
    "totalWorkDays": 22,
    "totalActualHours": 176.5,
    "totalOTHours": 4.0,
    "totalLateMinutes": 30,
    "totalEarlyLeaveMinutes": 0,
    "missingPunches": 1
  }
]
```

> `totalWorkDays` = số ngày có `Status` khác `Absent` và `OnLeave`.

---

### LUỒNG I: Quản lý ngày nghỉ lễ (Public Holiday)

#### I.1 — Thêm ngày lễ

```
POST /api/v1/attendance/holidays
{
  date: "2026-04-30",
  name: "Ngày Thống Nhất 30/4",
  isRecurringYearly: true    // true = tự lặp lại mỗi năm (chỉ match Month + Day)
}
```

#### I.2 — Xem danh sách

```
GET /api/v1/attendance/holidays
→ List<PublicHolidayDto> của tenant hiện tại
```

#### I.3 — Xóa ngày lễ

```
DELETE /api/v1/attendance/holidays/{id}
→ Chỉ xóa được ngày lễ thuộc tenant hiện tại
```

#### I.4 — Tác động vào hệ thống

- Sau khi thêm/xóa ngày lễ: chạy recalculate cho khoảng ngày liên quan để cập nhật `DailyTimesheet`
- Ngày lễ **không có log** → `Status = "Holiday"` (kể cả nhân viên có ca được gán ngày đó)
- Ngày lễ **có log** (nhân viên đi làm) → tính bình thường + OT nếu có OvertimeRequest

---

## 7. Thứ tự API theo từng kịch bản

### Kịch bản 1: Ngày làm việc bình thường

```
08:30 - Nhân viên đến văn phòng:
  [1] POST /submit-punch  (check-in)
      ← RawPunchLog created: IsProcessed=false, PunchType="Auto"

[BACKGROUND JOB chạy sau vài phút]
  ← DailyTimesheet created: Status="Late" (nếu vào sau giờ chuẩn)

08:31 - Nhân viên xem trạng thái:
  [2] GET /my-today
      ← Nếu job đã chạy: hasCheckedIn=true, status="Late"
      ← Nếu job chưa chạy: fallback đọc RawPunchLog, hasCheckedIn=true

17:30 - Nhân viên ra về:
  [3] POST /submit-punch  (check-out)
      ← RawPunchLog created: IsProcessed=false

[BACKGROUND JOB chạy lại]
  ← DailyTimesheet updated với đủ In + Out
  ← Tính ActualWorkHours, OTHours

Cuối ngày:
  [4] GET /my-today
      ← hasCheckedIn=true, hasCheckedOut=true, status đầy đủ
```

### Kịch bản 2: Nhân viên quên check-in

```
  [1] GET /my-today
      ← Status="MissingOut" hoặc "Absent"

  [2] POST /appeals
      { workDate, appealType:"In", requestedCheckIn, reason }
      ← Validate: không phải ngày tương lai, không quá 30 ngày, không có appeal pending
      ← TimesheetAppeal created: Status="PendingApproval"

  [3] GET /appeals
      ← Nhân viên xem appeal của mình, Status="PendingApproval"

HR thấy appeal:
  [4] GET /appeals/pending    (HR)
  [5] PUT /appeals/{id}/process { isApproved:true }  (HR)
      ← Transaction: tạo RawPunchLog HR_Manual + reset IsManuallyAdjusted + MarkUnprocessed

[BACKGROUND JOB chạy]
  ← Load toàn bộ log ngày đó (cả cũ + mới HR_Manual)
  ← DailyTimesheet cập nhật chính xác

  [6] GET /my-history?month=5&year=2026
      ← Ngày đó đã có check-in, Status="Normal" hoặc "Late"
```

### Kịch bản 3: Setup ban đầu (Admin)

```
  [1] POST /setting
      { latitude, longitude, checkInRadiusMeters, dayStartCutOffTime, lateThresholdMinutes... }
      ← TenantAttendanceSetting được tạo/cập nhật

  [2] POST /holidays  (lặp lại cho từng ngày lễ)
      { date: "2026-04-30", name: "Ngày Thống Nhất", isRecurringYearly: true }
      { date: "2026-05-01", name: "Ngày Quốc Tế Lao Động", isRecurringYearly: true }
      { date: "2026-09-02", name: "Ngày Quốc Khánh", isRecurringYearly: true }

  [3] GET /setting && GET /holidays
      ← Kiểm tra lại cấu hình
```

### Kịch bản 4: Nhân viên đi làm ngày lễ (OT nguyên ngày)

```
30/04 - Nhân viên đi làm (ngày Thống Nhất):
  [1] POST /submit-punch  (check-in 08:00)
  [2] POST /submit-punch  (check-out 17:00)

HR tạo OvertimeRequest được duyệt trước đó:
  [3] (đã có OvertimeRequest Status="Approved" cho ngày 30/4)

[BACKGROUND JOB chạy]
  ← isPublicHoliday = true, nhưng orderedLogs.Count > 0 → KHÔNG early-return
  ← Xử lý bình thường (TH1 hoặc TH2 tùy có ca hay không)
  ← OT được tính dựa trên OvertimeRequest đã duyệt
  ← DailyTimesheet: Status="Normal" (hoặc "NoShift" nếu không có ca)
```

---

## 8. Các Status Codes

### DailyTimesheet.Status — Thứ tự ưu tiên

| Status | Ý nghĩa | Điều kiện |
|--------|---------|-----------|
| `Holiday` | Ngày nghỉ lễ | `isPublicHoliday && orderedLogs.Count == 0` (early-return) hoặc `segments.Count == 0 && isPublicHoliday` |
| `OnLeave` | Nghỉ phép cả ngày | Tất cả segments đều `OnLeave` |
| `NoShift` | Không có ca làm việc | `shiftSegments == null/empty && logs == 0`, hoặc tất cả segments đều `NoShift` |
| `Absent` | Vắng mặt | Có ca nhưng không có log nào, hoặc `segments.Any(Absent)` |
| `MissingOut` | Thiếu check-out | `segments.Any(MissingOut)` |
| `Late` | Đi trễ (kể cả vừa trễ vừa về sớm) | `totalLate > LateThreshold` |
| `EarlyLeave` | Về sớm | `totalEarly > EarlyLeaveThreshold` |
| `Normal` | Bình thường | Không thỏa mãn điều kiện nào trên |

### DailyTimesheetSegment.Status

| Status | Ý nghĩa |
|--------|---------|
| `Normal` | Có đủ In + Out, đúng giờ |
| `Late` | Segment bình thường nhưng đi trễ (dùng ở mức Timesheet) |
| `OnLeave` | Segment này có LeaveRequest được duyệt |
| `Absent` | Không có log nào cho segment này |
| `MissingOut` | Có In nhưng thiếu Out (hoặc ngược lại) |
| `NoShift` | Segment từ ngày không có ca (TH1) |

### SystemAnomalyFlag (comma-separated)

| Flag | Ý nghĩa |
|------|---------|
| `MissingOut` | Thiếu log check-out |
| `OutBeforeIn` | Check-out xuất hiện trước check-in đầu ngày |
| `UnmappedPairs` | Log không khớp được với ShiftSegment nào |
| `OTWithoutRequest` | Làm OT thực tế nhưng không có OvertimeRequest |
| `OrphanedOut` | Check-out không có check-in đi kèm |

### TimesheetAppeal.Status

| Status | Ý nghĩa |
|--------|---------|
| `PendingApproval` | Đang chờ HR duyệt |
| `Approved` | Đã được duyệt, đã tạo RawPunchLog HR_Manual |
| `Rejected` | Bị từ chối |

### PunchType

| Type | Ý nghĩa |
|------|---------|
| `In` | Check-in (tường minh) |
| `Out` | Check-out (tường minh) |
| `Auto` | Hệ thống tự phán quyết In hay Out dựa vào sequence |

---

## 9. Cấu hình TenantAttendanceSetting

| Field | Mặc định | Mô tả |
|-------|---------|-------|
| `Latitude`, `Longitude` | — | Tọa độ địa điểm làm việc (null = không bắt buộc GPS) |
| `CheckInRadiusMeters` | 100 m | Bán kính cho phép check-in |
| `DayStartCutOffTime` | 04:00 | Giờ cắt ngày: log trước 04:00 → thuộc WorkDate hôm trước |
| `LateThresholdMinutes` | 10 phút | Trễ tối đa được bỏ qua (grace period ở mức ngày) |
| `EarlyLeaveThresholdMinutes` | 10 phút | Về sớm tối đa được bỏ qua |
| `MinimumOTMinutes` | 30 phút | OT tối thiểu để được tính |
| `OTBlockMinutes` | 30 phút | OT được round xuống theo block này |

**Ví dụ tính OT:**
```
OT thực tế: 80 phút
OTBlockMinutes: 30
→ floor(80 / 30) = 2 blocks
→ 2 × 30 = 60 phút = 1.0 giờ OT được tính
```

---

## 10. Security & Authorization

| Loại | Rule |
|------|------|
| Authentication | JWT Bearer Token bắt buộc cho mọi endpoint |
| Employee endpoints | `[Authorize]` — mọi role đã đăng nhập |
| HR/Admin endpoints | `[Authorize(Roles = "Admin,HR")]` |
| User identity | Extract từ `ClaimTypes.NameIdentifier` trong JWT |
| FakeGPS | Mobile app gửi flag `isMockLocation`, server từ chối nếu `true` |
| GPS validation | `GeoHelper.DistanceInMeters()` dùng công thức Haversine |
| Multi-tenant isolation | EF Core Global Query Filter: `e.TenantId == _currentTenantService.TenantId` (dynamic, không cache) |
| Appeal future date | Server từ chối appeal ngày hôm nay hoặc tương lai |
| Appeal deadline | Server từ chối appeal quá 30 ngày trong quá khứ |
| Duplicate appeal | Mỗi (EmployeeId, WorkDate) chỉ có 1 appeal PendingApproval |

---

## 11. Files tham khảo

### Controllers
- [AttendanceController.cs](../SMEFLOWSystem.WebAPI/Controllers/AttendanceController.cs)
- [AttendanceSettingController.cs](../SMEFLOWSystem.WebAPI/Controllers/AttendanceSettingController.cs)

### Services
- [AttendanceService.cs](../SMEFLOWSystem.Application/Services/AttendanceService.cs)
- [AttendanceResolutionService.cs](../SMEFLOWSystem.Application/Services/AttendanceResolutionService.cs)

### Background Job
- [AttendanceResolutionRecurringJob.cs](../SMEFLOWSystem.Application/BackgroundJobs/AttendanceResolutionRecurringJob.cs)

### Repositories
- [RawPunchLogRepository.cs](../SMEFLOWSystem.Infrastructure/Repositories/RawPunchLogRepository.cs)
- [DailyTimesheetRepository.cs](../SMEFLOWSystem.Infrastructure/Repositories/DailyTimesheetRepository.cs)
- [TimesheetAppealRepository.cs](../SMEFLOWSystem.Infrastructure/Repositories/TimesheetAppealRepository.cs)
- [PublicHolidayRepository.cs](../SMEFLOWSystem.Infrastructure/Repositories/PublicHolidayRepository.cs)

### Entities
- [RawPunchLog.cs](../SMEFLOWSystem.Core/Entities/RawPunchLog.cs)
- [DailyTimesheet.cs](../SMEFLOWSystem.Core/Entities/DailyTimesheet.cs)
- [DailyTimesheetSegment.cs](../SMEFLOWSystem.Core/Entities/DailyTimesheetSegment.cs)
- [TimesheetAppeal.cs](../SMEFLOWSystem.Core/Entities/TimesheetAppeal.cs)
- [PublicHoliday.cs](../SMEFLOWSystem.Core/Entities/PublicHoliday.cs)
- [TenantAttendanceSetting.cs](../SMEFLOWSystem.Core/Entities/TenantAttendanceSetting.cs)

### Configuration
- [SMEFLOWSystemContext.cs](../SMEFLOWSystem.Infrastructure/Data/SMEFLOWSystemContext.cs)
- [PublicHolidayConfiguration.cs](../SMEFLOWSystem.Infrastructure/Data/Configurations/PublicHolidayConfiguration.cs)

### Helpers
- [GeoHelper.cs](../SMEFLOWSystem.Application/Helpers/GeoHelper.cs)

---

## 12. Điểm quan trọng cần nhớ

1. **RawPunchLog là append-only** — không bao giờ UPDATE hay DELETE, chỉ thêm mới và đánh dấu `IsProcessed`

2. **Background Job luôn dùng toàn bộ log trong ngày** — khi xử lý 1 nhóm (EmployeeId, WorkDate), job luôn query lại tất cả log của ngày đó (cả đã và chưa processed). Đảm bảo check-in buổi sáng không bị mất khi check-out buổi chiều được xử lý ở batch sau

3. **Retry có giới hạn** — log thất bại sẽ được retry tối đa 3 lần (qua `RetryCount`). Sau 3 lần → dead-letter (mark processed, log Critical, cần xử lý thủ công)

4. **Timezone Vietnam** — Mọi tính toán WorkDate đều dùng `SE Asia Standard Time (Asia/Ho_Chi_Minh)`. Mọi convert phải dùng `ConvertTimeToUtc` (local → UTC) hoặc `ConvertTimeFromUtc` (UTC → local) đúng chiều

5. **Cutoff 04:00 sáng** — Log từ 00:00–03:59 VN thuộc về WorkDate của ngày hôm trước. Ví dụ: check-out 03:00 ngày 16 → WorkDate 15

6. **HR_Manual flag** — Khi HR nhập tay hoặc approve appeal → `DeviceId = "HR_Manual"` để audit trail. Các log này được xử lý giống log thường

7. **Dedup theo PunchType** — Dedup chỉ xảy ra giữa 2 log **cùng PunchType**. Log In và Out kề nhau dù chỉ cách vài giây vẫn được giữ lại cả hai

8. **Public Holiday + shift** — Nếu nhân viên có ca vào ngày lễ nhưng không đi làm → vẫn là `Holiday` (không phải `Absent`). Chỉ khi nhân viên thực sự đi làm (có log) thì mới xử lý bình thường

9. **IsManuallyAdjusted** — Khi HR chỉnh tay DailyTimesheet → `IsManuallyAdjusted = true` → job bỏ qua không ghi đè. Khi HR approve appeal → tự động reset về `false` để cho phép recalculate

10. **Transaction per group** — Mỗi nhóm (EmployeeId, WorkDate) chạy trong 1 transaction riêng với SemaphoreSlim lock. Đảm bảo atomicity và tránh race condition

11. **Bulk Load** — Trước khi vào vòng lặp xử lý, job load sẵn Shifts, LeaveRequests, OTs, Timesheets, PublicHolidays vào memory. Tránh N+1 query cho 99% các lookup

12. **EF Core multi-tenant** — `SMEFLOWSystemContext` dùng `_currentTenantService.TenantId` (dynamic property) trong query filter, không cache vào readonly field. Background job gọi `SetTenantId` trước khi xử lý từng tenant → filter hoạt động đúng
