# Dashboard Flow Documentation — DodoSystem Backend

> Tài liệu phân tích chi tiết luồng Dashboard trong hệ thống DodoSystem.
> Cập nhật: 2026-06-18 (thêm các trigger dashboard.refresh từ Payroll/HR/Invite)

---

## 1. Tổng quan kiến trúc

Module Dashboard cung cấp **3 endpoint riêng biệt** dành cho 3 nhóm người dùng khác nhau:

| Endpoint | Dành cho | Phạm vi dữ liệu |
|----------|----------|----------------|
| `GET /admin` | TenantAdmin, HRManager | Toàn bộ công ty |
| `GET /manager` | Manager | Phòng ban do Manager quản lý |
| `GET /employee` | Mọi user đã đăng nhập | Cá nhân |

### Nguyên tắc thiết kế

- **Không có entity mới** — Dashboard tổng hợp từ các entity đã có: `Employee`, `DailyTimesheet`, `TimesheetAppeal`, `Payroll`, `ShiftPattern`
- **Không có migration** — Chỉ thêm 2 method mới vào repository đã có
- **Tính toán in-memory** — Load data song song bằng `Task.WhenAll`, aggregate trong C# (không dùng SQL phức tạp)
- **WorkDate theo múi giờ Vietnam** — Cắt ngày lúc 04:00 sáng VN (ca đêm khuya thuộc ngày hôm trước)
- **Scope filter cho Manager** — `IHrAuthorizationService.GetAccessibleDepartmentIdsAsync()` trả về danh sách phòng ban mà Manager được quyền xem
- **`dashboard.refresh` tự động từ nhiều nguồn** — Frontend không cần polling; dashboard tự cập nhật khi nhận SignalR event `dashboard.refresh` (xem bảng trigger bên dưới)

### Các trigger của `dashboard.refresh`

| Hành động | Service | Ghi chú |
|-----------|---------|---------|
| Background Job xử lý timesheet xong | `AttendanceResolutionService` | Mỗi batch xử lý |
| HR duyệt/từ chối appeal | `AttendanceService.ProcessAppealAsync` | Sau transaction |
| Admin chốt 1 phiếu lương | `PayrollService.PublishPayrollAsync` | Sau `UpdateAsync` |
| Admin chốt toàn bộ Draft | `PayrollService.PublishAllDraftAsync` | Sau `UpdateRangeAsync` |
| Admin mark paid phiếu lương | `PayrollService.MarkPaidAsync` | Sau `UpdateAsync` |
| Nhân viên hoàn tất onboarding | `InviteService.CompleteOnboardingInternalAsync` | Sau `UpdateInviteAsync` |

> **Frontend pattern:** Lắng nghe `dashboard.refresh` → gọi lại `GET /api/v1/dashboard/admin` (hoặc manager/employee). Event không chứa data — chỉ là tín hiệu refresh.

---

## 2. Các Entity & Repository được dùng lại

Module Dashboard **không tạo entity mới**, chỉ đọc từ:

| Entity / Repository | Dùng để lấy |
|--------------------|-------------|
| `Employee` / `IEmployeeRepository` | Số lượng nhân viên, phân theo phòng ban |
| `DailyTimesheet` / `IDailyTimesheetRepository` | Thống kê chấm công hôm nay và theo tháng |
| `TimesheetAppeal` / `ITimesheetAppealRepository` | Số lượng appeal đang chờ duyệt |
| `Payroll` / `IPayrollRepository` | Thống kê lương theo tháng |
| `ShiftPattern` / `IShiftPatternRepository` | Ca làm việc hiện tại của nhân viên (Employee Dashboard) |

### Các method mới được thêm vào repository đã có

**`IDailyTimesheetRepository` — thêm `GetByTenantDateAsync`:**
```csharp
Task<List<DailyTimesheet>> GetByTenantDateAsync(Guid tenantId, DateOnly workDate);
```
Dùng để lấy chấm công **hôm nay** cho toàn tenant (Admin/Manager Dashboard).
Implementation dùng `.AsNoTracking().Include(d => d.Employee).AsSplitQuery()`.

**`ITimesheetAppealRepository` — thêm `GetPendingCountAsync`:**
```csharp
Task<int> GetPendingCountAsync(Guid tenantId);
```
Tối ưu đếm appeal mà không cần load toàn bộ object.

---

## 3. Controllers & API Endpoints

### DashboardController
- **File**: `WebAPI/Controllers/DashboardController.cs`
- **Base route**: `api/v1/dashboard`
- **Auth**: Bắt buộc JWT Bearer Token cho toàn bộ controller (`[Authorize]`)
- **Dependencies**: `IDashboardService`, `ICurrentTenantService`, `ICurrentUserService`

| Method | Endpoint | Roles được phép | Query params |
|--------|----------|----------------|-------------|
| `GET` | `/admin` | `TenantAdmin`, `HRManager` | `?month=&year=` |
| `GET` | `/manager` | `Manager` | `?month=&year=` |
| `GET` | `/employee` | Mọi role đã đăng nhập | `?month=&year=` |

**Cách lấy context:**
- `tenantId` ← `ICurrentTenantService.TenantId` (throw `UnauthorizedAccessException` nếu null)
- `userId` ← `ICurrentUserService.UserId` (throw `UnauthorizedAccessException` nếu null)
- `month`, `year` ← Query params, mặc định là tháng/năm UTC hiện tại nếu không truyền

---

## 4. DTOs

### Sub-DTOs (dùng chung nhiều Response)

| DTO | Mô tả |
|-----|-------|
| `AlertItemDto` | 1 cảnh báo: `Type`, `Severity`, `Message`, `Count` |
| `DepartmentEmployeeCountDto` | Số NV theo phòng ban: `DepartmentId`, `DepartmentName`, `Count` |
| `TodayAttendanceSummaryDto` | Tổng hợp chấm công hôm nay: `CheckedIn`, `Absent`, `Late`, `MissingOut`, `OnLeave`, `TotalExpected` |
| `MonthlyAttendanceStatsDto` | Thống kê tháng: `TotalWorkDays`, `TotalAbsentDays`, `TotalOTHours`, `TotalLateMinutes`, `TotalEmployeeRecords` |
| `PayrollSummaryDto` | Tổng hợp lương tháng: `DraftCount`, `PublishedCount`, `PaidCount`, `TotalNetSalary`, `TotalPaidSalary` |
| `CurrentShiftDto` | Ca làm việc hiện tại: `ShiftPatternId`, `ShiftName`, `StartTime`, `EndTime` |
| `MyMonthSummaryDto` | Tóm tắt tháng cá nhân: `WorkDays`, `AbsentDays`, `LateDays`, `TotalOTHours`, `TotalLateMinutes` |

### Response DTOs

**`AdminDashboardDto`:**
```json
{
  "totalEmployees": 45,
  "employeesByDepartment": [
    { "departmentId": "uuid", "departmentName": "Kỹ thuật", "count": 12 }
  ],
  "todayAttendance": {
    "workDate": "2026-06-03",
    "checkedIn": 38, "absent": 3, "late": 5, "missingOut": 1, "onLeave": 2, "totalExpected": 44
  },
  "monthlyStats": {
    "month": 6, "year": 2026,
    "totalWorkDays": 920, "totalAbsentDays": 15, "totalOTHours": 42.5,
    "totalLateMinutes": 380, "totalEmployeeRecords": 990
  },
  "payrollSummary": {
    "month": 6, "year": 2026,
    "draftCount": 5, "publishedCount": 30, "paidCount": 10,
    "totalNetSalary": 450000000, "totalPaidSalary": 120000000
  },
  "pendingAppealsCount": 3,
  "alerts": [
    { "type": "PendingAppeals", "severity": "Medium", "message": "Có 3 đơn giải trình đang chờ xử lý.", "count": 3 }
  ]
}
```

**`ManagerDashboardDto`:**
```json
{
  "deptEmployeeCount": 12,
  "employeesByDepartment": [...],
  "deptTodayAttendance": { ... },
  "deptMonthlyStats": { ... },
  "draftPayrollCount": 2,
  "deptPendingAppealsCount": 1,
  "alerts": [...]
}
```

**`EmployeeDashboardDto`:**
```json
{
  "myTodayStatus": { "hasCheckedIn": true, "checkInTime": "...", "status": "Late", "lateMinutes": 15 },
  "myMonthSummary": {
    "month": 6, "year": 2026,
    "workDays": 18, "absentDays": 0, "lateDays": 3, "totalOTHours": 4.0, "totalLateMinutes": 45
  },
  "myCurrentShift": {
    "shiftPatternId": "uuid", "shiftName": "Ca sáng",
    "startTime": "08:00:00", "endTime": "17:00:00"
  },
  "myLatestPayroll": { "netSalary": 15000000, "status": "Published", ... },
  "myPendingAppealsCount": 0
}
```

> **Lưu ý reuse:** `MyTodayStatus` dùng `TodayAttendanceDto` từ `AttendanceDtos`. `MyLatestPayroll` dùng `PayrollDto` từ `PayrollDtos` và chỉ trả về nếu status là `Published` hoặc `Paid`.

---

## 5. Service

### IDashboardService / DashboardService
- **Interface**: `Application/Interfaces/IServices/IDashboardService.cs`
- **Implementation**: `Application/Services/DashboardService.cs`
- **DI Registration**: `Application/Extensions/DependencyInjection.cs` → `AddScoped<IDashboardService, DashboardService>()`

**Dependencies được inject:**

| Dependency | Dùng để |
|-----------|---------|
| `IEmployeeRepository` | Lấy danh sách nhân viên active |
| `IDailyTimesheetRepository` | Lấy timesheet hôm nay + theo tháng |
| `ITimesheetAppealRepository` | Đếm appeal đang chờ duyệt |
| `IPayrollRepository` | Lấy payroll theo tháng |
| `IAttendanceService` | Reuse `GetMyTodayStatusAsync` cho Employee Dashboard |
| `IShiftPatternRepository` | Tra cứu ca làm việc hiện tại |
| `IHrAuthorizationService` | Lấy danh sách departmentId Manager được quản lý |
| `IMapper` | Map Payroll entity → PayrollDto |
| `ILogger<DashboardService>` | Logging |

---

## 6. Helper: Tính WorkDate theo múi giờ Vietnam

```
private static DateOnly GetVietnamWorkDate():
    VietnamTimeZone = "SE Asia Standard Time" (Windows) hoặc "Asia/Ho_Chi_Minh" (Linux)
    localNow = ConvertTimeFromUtc(DateTime.UtcNow, VietnamTimeZone)
    cutoff = 04:00 AM

    if localNow.TimeOfDay < cutoff:
        workDate = ngày hôm qua   ← Ca đêm khuya (00:00–03:59) thuộc ngày trước
    else:
        workDate = hôm nay
```

> **Lý do:** Nhất quán với logic của `AttendanceResolutionService` — log bấm lúc 02:00 sáng thuộc về WorkDate của ngày hôm trước.

---

## 7. Luồng Chi Tiết Từng Endpoint

---

### LUỒNG A: Admin Dashboard

```
GET /api/v1/dashboard/admin?month=6&year=2026
Authorization: Bearer <TenantAdmin hoặc HRManager token>
```

**Bước 1 — Xác định context:**
```
tenantId = ICurrentTenantService.TenantId
workDate = GetVietnamWorkDate()
month, year = query params hoặc tháng/năm hiện tại
```

**Bước 2 — Load song song 5 tasks:**
```
Task.WhenAll(
  employees       = _employeeRepo.GetAllActiveEmployeeByTenantId(tenantId),
  todayTimesheets = _timesheetRepo.GetByTenantDateAsync(tenantId, workDate),
  monthTimesheets = _timesheetRepo.GetByTenantMonthAsync(tenantId, month, year),
  pendingAppeals  = _appealRepo.GetPendingAsync(tenantId),
  payrolls        = _payrollRepo.GetByTenantMonthAsync(tenantId, month, year)
)
```

> `GetAllActiveEmployeeByTenantId` đã có `.Include(e => e.Department)` nên truy cập `employee.Department.Name` trực tiếp — không phát sinh query thêm.

**Bước 3 — Aggregate in-memory:**

```
TotalEmployees = employees.Count

EmployeesByDepartment:
  employees
    .Where(e.DepartmentId != null && e.Department != null)
    .GroupBy(e.DepartmentId)
    .Select(g => { DepartmentId, DepartmentName = g.First().Department.Name, Count })
    .OrderBy(DepartmentName)

TodayAttendance:
  CheckedIn  = count(Status ∈ {Normal, Late, EarlyLeave, Present})
  Absent     = count(Status == Absent)
  Late       = count(Status == Late)
  MissingOut = count(Status == MissingOut)
  OnLeave    = count(Status == OnLeave)
  TotalExpected = todayTimesheets.Count

MonthlyStats:
  TotalWorkDays      = count(Status ∈ {Normal, Late, EarlyLeave, Present})
  TotalAbsentDays    = count(Status == Absent)
  TotalOTHours       = SUM(OTHours)
  TotalLateMinutes   = SUM(TotalLateMinutes)
  TotalEmployeeRecords = monthTimesheets.Count

PayrollSummary:
  DraftCount     = count(Status == Draft)
  PublishedCount = count(Status == Published)
  PaidCount      = count(Status == Paid)
  TotalNetSalary = SUM(NetSalary) — tất cả status
  TotalPaidSalary = SUM(NetSalary) chỉ Paid

Alerts = BuildAlerts(pendingAppealsCount, draftCount, frequentAbsentCount, missingOutCount)
  (xem Phần 8 — Quy tắc Alerts)
```

---

### LUỒNG B: Manager Dashboard

```
GET /api/v1/dashboard/manager?month=6&year=2026
Authorization: Bearer <Manager token>
```

**Bước 1 — Xác định phòng ban được quản lý:**
```
tenantId = ICurrentTenantService.TenantId
userId   = ICurrentUserService.UserId
workDate = GetVietnamWorkDate()

departmentIds = await _hrAuth.GetAccessibleDepartmentIdsAsync()

if departmentIds != null && departmentIds.Count == 0:
    return ManagerDashboardDto mặc định (rỗng)   ← Manager chưa được giao phòng ban
```

**Bước 2 — Filter nhân viên theo phòng ban:**
```
allEmployees = await _employeeRepo.GetAllActiveEmployeeByTenantId(tenantId)

employees = departmentIds == null
    ? allEmployees                           ← null = quyền xem tất cả
    : allEmployees.Where(e.DepartmentId ∈ departmentIds)

empIds = employees.Select(e.Id).ToHashSet()

if empIds.Count == 0:
    return ManagerDashboardDto mặc định (rỗng)
```

**Bước 3 — Load song song 4 tasks (scope toàn tenant):**
```
Task.WhenAll(
  todayTimesheets = _timesheetRepo.GetByTenantDateAsync(tenantId, workDate),
  monthTimesheets = _timesheetRepo.GetByTenantMonthAsync(tenantId, month, year),
  pendingAppeals  = _appealRepo.GetPendingAsync(tenantId),
  payrolls        = _payrollRepo.GetByTenantMonthAsync(tenantId, month, year)
)
```

**Bước 4 — Filter theo empIds (in-memory):**
```
todayTimesheets = todayTimesheets.Where(t.EmployeeId ∈ empIds)
monthTimesheets = monthTimesheets.Where(t.EmployeeId ∈ empIds)
pendingAppeals  = pendingAppeals.Where(a.EmployeeId ∈ empIds)
payrolls        = payrolls.Where(p.EmployeeId ∈ empIds)
```

> **Lý do load toàn tenant rồi filter:** Tránh thêm method phức tạp vào repository. Phòng ban thường chiếm một phần nhỏ tenant, overhead không đáng kể.

**Bước 5 — Aggregate** (tương tự Admin nhưng scope hẹp theo phòng ban):
```
DeptEmployeeCount = employees.Count
EmployeesByDepartment = ... (tương tự Admin)
DeptTodayAttendance = ... (tương tự Admin)
DeptMonthlyStats = ... (tương tự Admin)
DraftPayrollCount = count(payrolls.Status == Draft)
DeptPendingAppealsCount = pendingAppeals.Count
Alerts = BuildAlerts(...)
```

---

### LUỒNG C: Employee Dashboard

```
GET /api/v1/dashboard/employee?month=6&year=2026
Authorization: Bearer <bất kỳ token đã đăng nhập>
```

**Bước 1 — Xác định Employee:**
```
userId   = ICurrentUserService.UserId
workDate = GetVietnamWorkDate()

employee = await _employeeRepo.GetByUserIdAsync(userId)
if employee == null:
    throw KeyNotFoundException("Không tìm thấy hồ sơ nhân sự cho tài khoản này.")
```

**Bước 2 — Load song song 5 tasks:**
```
Task.WhenAll(
  todayStatus  = _attendanceService.GetMyTodayStatusAsync(userId),
  monthTs      = _timesheetRepo.GetByEmployeeMonthAsync(employee.Id, month, year),
  shiftData    = _shiftPatternRepo.GetActivePatternDetailsAsync(employee.Id, workDate),
  payrolls     = _payrollRepo.GetByEmployeeMonthAsync(employee.Id, employee.TenantId, month, year),
  appeals      = _appealRepo.GetByEmployeeAsync(employee.Id)
)
```

> **Về `todayStatus`:** Reuse hoàn toàn `IAttendanceService.GetMyTodayStatusAsync` — không duplicate logic. Kết quả là `TodayAttendanceDto` (type cụ thể, không phải `object`).

**Bước 3 — Tính MyMonthSummary:**
```
WorkDays   = count(Status ∈ {Normal, Late, EarlyLeave, Present})
AbsentDays = count(Status == Absent)
LateDays   = count(Status == Late)
TotalOTHours     = SUM(OTHours)
TotalLateMinutes = SUM(TotalLateMinutes)
```

**Bước 4 — Tính MyCurrentShift (tra cứu ca từ ShiftPattern):**

```
(esp, definition) = shiftData   ← GetActivePatternDetailsAsync

if esp == null || definition == null || definition.CycleLengthDays == 0:
    myCurrentShift = null   ← Nhân viên chưa được gán ca

else:
    dayOffset = workDate.DayNumber - esp.EffectiveStartDate.DayNumber
    dayIndex  = dayOffset % definition.CycleLengthDays
    if dayIndex < 0: dayIndex += definition.CycleLengthDays   ← Normalize negative modulo

    patternDay = definition.Days.FirstOrDefault(d.DayIndex == dayIndex)
    if patternDay?.ScheduledShiftId == null:
        myCurrentShift = null   ← Ngày nghỉ trong lịch ca

    else:
        shift = await _shiftPatternRepo.GetShiftWithSegmentsAsync(patternDay.ScheduledShiftId)
        sortedSegments = shift.Segments.OrderBy(StartDayOffset, StartTime)

        myCurrentShift = {
            ShiftPatternId = definition.Id,
            ShiftName      = shift.Name,
            StartTime      = TimeOnly.FromTimeSpan(firstSegment.StartTime),
            EndTime        = TimeOnly.FromTimeSpan(lastSegment.EndTime)
        }
```

> **Lưu ý:** `GetShiftWithSegmentsAsync` là 1 query bổ sung không thể chạy song song với 5 tasks ban đầu vì phụ thuộc vào `patternDay.ScheduledShiftId`. Đây là trường hợp ngoại lệ có thể chấp nhận — chỉ xảy ra khi nhân viên có ca.

**Bước 5 — Tính MyLatestPayroll:**
```
payroll = payrolls.FirstOrDefault()

if payroll != null && payroll.Status ∈ {Published, Paid}:
    myLatestPayroll = _mapper.Map<PayrollDto>(payroll)
else:
    myLatestPayroll = null   ← Không trả về Draft payroll cho nhân viên
```

**Bước 6 — Tính MyPendingAppealsCount:**
```
myPendingAppealsCount = appeals.Count(a.Status == StatusEnum.ApprovalPending)
```

---

## 8. Quy tắc Alerts (dùng chung Admin và Manager)

Alerts chỉ được thêm vào danh sách khi `Count > 0` (không trả về alert rỗng):

| Type | Điều kiện | Severity | Message mẫu |
|------|-----------|----------|-------------|
| `PendingAppeals` | `pendingAppealsCount > 0` | `High` nếu > 5, `Medium` nếu ≤ 5 | "Có N đơn giải trình đang chờ xử lý." |
| `UnpublishedPayroll` | `draftPayrollCount > 0` | `Medium` | "Có N phiếu lương chưa được publish." |
| `FrequentAbsent` | NV có ≥ 3 ngày `Absent` trong tháng > 0 | `High` nếu > 2 NV, `Medium` nếu ≤ 2 | "Có N nhân viên vắng mặt từ 3 ngày trở lên trong tháng." |
| `MissingOutUnresolved` | NV có `MissingOut` **và chưa có appeal** > 0 | `High` nếu > 2 NV, `Medium` nếu ≤ 2 | "Có N nhân viên có ngày thiếu chấm ra chưa giải trình." |

**Logic tính `FrequentAbsent`:**
```
frequentAbsentCount = monthTimesheets
    .Where(Status == AttendanceAbsent)
    .GroupBy(EmployeeId)
    .Count(group.Count() >= 3)
```

**Logic tính `MissingOutUnresolved` (có trừ employee đã appeal):**
```
missingOutEmpIds = monthTimesheets
    .Where(Status == AttendanceMissingOut)
    .Select(EmployeeId).ToHashSet()

appealedEmpIds = pendingAppeals
    .Select(EmployeeId).ToHashSet()

missingOutCount = missingOutEmpIds.Except(appealedEmpIds).Count()
```

> **Lý do trừ appealedEmpIds:** Tránh cảnh báo trùng — nếu nhân viên đã submit appeal thì không cần cảnh báo MissingOut nữa, HR chỉ cần xử lý appeal.

---

## 9. Edge Cases

| Trường hợp | Hành vi |
|-----------|---------|
| Manager chưa được giao phòng ban | `GetAccessibleDepartmentIdsAsync()` trả về list rỗng → return `ManagerDashboardDto` mặc định, không lỗi |
| Employee không có hồ sơ nhân sự | Throw `KeyNotFoundException` → HTTP 404/500 tùy GlobalExceptionHandler |
| `GetByTenantDateAsync` trả list rỗng (background job chưa chạy hôm nay) | `TodayAttendance` có tất cả count = 0, `TotalExpected = 0` — không lỗi |
| Employee chưa có timesheet tháng này | `MyMonthSummary` có tất cả count = 0 |
| Admin xem tháng không có payroll | `PayrollSummary` count = 0, salary = 0 |
| Employee chưa được gán ca (`esp == null`) | `MyCurrentShift = null` |
| Payroll tháng này chỉ có Draft | `MyLatestPayroll = null` (không trả Draft cho nhân viên) |
| `workDate` tính lúc 02:00 AM VN | `workDate = ngày hôm qua` (cutoff 04:00) |
| `dayIndex` âm (modulo) | `dayIndex += CycleLengthDays` để normalize về dương |

---

## 10. Query Pattern — Tránh N+1

| Endpoint | Số query thực tế |
|----------|----------------|
| Admin Dashboard | 5 queries song song (Task.WhenAll) |
| Manager Dashboard | 1 (employees) + 4 song song (timesheets, appeals, payrolls) = 5 |
| Employee Dashboard | 1 (employee) + 5 song song + tối đa 1 (GetShiftWithSegments) = 7 |

**`EmployeesByDepartment` không phát sinh query thêm** vì `GetAllActiveEmployeeByTenantId` đã include `.Include(e => e.Department)`.

---

## 11. Security & Authorization

| Loại | Rule |
|------|------|
| Authentication | JWT Bearer Token bắt buộc cho mọi endpoint (`[Authorize]` cấp controller) |
| Admin endpoint | `[Authorize(Roles = "TenantAdmin,HRManager")]` |
| Manager endpoint | `[Authorize(Roles = "Manager")]` |
| Employee endpoint | Chỉ `[Authorize]` — mọi role đã đăng nhập |
| Tenant isolation | `ICurrentTenantService.TenantId` → ném `UnauthorizedAccessException` nếu null |
| User identity | `ICurrentUserService.UserId` → ném `UnauthorizedAccessException` nếu null |
| Manager scope | `IHrAuthorizationService.GetAccessibleDepartmentIdsAsync()` giới hạn dữ liệu theo phòng ban |
| Employee payroll visibility | Chỉ trả `PayrollDto` khi status là `Published` hoặc `Paid` — không để lộ Draft |

---

## 12. Files tham khảo

### Controller
- [DashboardController.cs](../SMEFLOWSystem.WebAPI/Controllers/DashboardController.cs)

### Service
- [IDashboardService.cs](../SMEFLOWSystem.Application/Interfaces/IServices/IDashboardService.cs)
- [DashboardService.cs](../SMEFLOWSystem.Application/Services/DashboardService.cs)

### DTOs
- [AdminDashboardDto.cs](../SMEFLOWSystem.Application/DTOs/DashboardDtos/AdminDashboardDto.cs)
- [ManagerDashboardDto.cs](../SMEFLOWSystem.Application/DTOs/DashboardDtos/ManagerDashboardDto.cs)
- [EmployeeDashboardDto.cs](../SMEFLOWSystem.Application/DTOs/DashboardDtos/EmployeeDashboardDto.cs)
- [AlertItemDto.cs](../SMEFLOWSystem.Application/DTOs/DashboardDtos/AlertItemDto.cs)
- [TodayAttendanceSummaryDto.cs](../SMEFLOWSystem.Application/DTOs/DashboardDtos/TodayAttendanceSummaryDto.cs)
- [MonthlyAttendanceStatsDto.cs](../SMEFLOWSystem.Application/DTOs/DashboardDtos/MonthlyAttendanceStatsDto.cs)
- [PayrollSummaryDto.cs](../SMEFLOWSystem.Application/DTOs/DashboardDtos/PayrollSummaryDto.cs)
- [CurrentShiftDto.cs](../SMEFLOWSystem.Application/DTOs/DashboardDtos/CurrentShiftDto.cs)
- [MyMonthSummaryDto.cs](../SMEFLOWSystem.Application/DTOs/DashboardDtos/MyMonthSummaryDto.cs)
- [DepartmentEmployeeCountDto.cs](../SMEFLOWSystem.Application/DTOs/DashboardDtos/DepartmentEmployeeCountDto.cs)

### Repositories (được mở rộng)
- [IDailyTimesheetRepository.cs](../SMEFLOWSystem.Application/Interfaces/IRepositories/IDailyTimesheetRepository.cs) — thêm `GetByTenantDateAsync`
- [DailyTimesheetRepository.cs](../SMEFLOWSystem.Infrastructure/Repositories/DailyTimesheetRepository.cs) — implement `GetByTenantDateAsync`
- [ITimesheetAppealRepository.cs](../SMEFLOWSystem.Application/Interfaces/IRepositories/ITimesheetAppealRepository.cs) — thêm `GetPendingCountAsync`
- [TimesheetAppealRepository.cs](../SMEFLOWSystem.Infrastructure/Repositories/TimesheetAppealRepository.cs) — implement `GetPendingCountAsync`

### DI Registration
- [DependencyInjection.cs](../SMEFLOWSystem.Application/Extensions/DependencyInjection.cs) — `services.AddScoped<IDashboardService, DashboardService>()`

---

## 13. Điểm quan trọng cần nhớ

1. **Không có entity mới** — Dashboard chỉ aggregate từ các module khác (Attendance, Payroll, Employee)

2. **WorkDate cutoff 04:00 sáng VN** — Nhất quán với `AttendanceResolutionService`. Gọi lúc 02:00 AM → `workDate` = ngày hôm qua

3. **Manager filter in-memory** — Load toàn tenant rồi filter bằng `HashSet<Guid>`, không dùng SQL WHERE với danh sách phòng ban. Phù hợp cho quy mô SME

4. **`FrequentAbsent` = vắng mặt (Absent), không phải đi trễ (Late)** — Tên `FrequentAbsent` đúng với nghĩa vắng mặt ≥ 3 ngày/tháng

5. **`MissingOutUnresolved` trừ appealedEmpIds** — Không cảnh báo nhân viên đã submit appeal. `appealedEmpIds` lấy từ `GetPendingAsync` (chỉ `PendingApproval`), không bao gồm Approved/Rejected

6. **Alert chỉ xuất hiện khi `Count > 0`** — Frontend nhận list rỗng khi không có vấn đề gì, không nhận 4 alert với `count = 0`

7. **Employee Dashboard không trả Draft payroll** — Chỉ `Published` và `Paid` mới được map ra `PayrollDto` cho nhân viên xem

8. **`GetShiftWithSegmentsAsync` không thể song song** — Phụ thuộc vào `patternDay.ScheduledShiftId` từ kết quả của `GetActivePatternDetailsAsync`. Là 1 query thêm, nhưng chỉ xảy ra khi nhân viên có ca

9. **Reuse `IAttendanceService` từ DashboardService** — Không vi phạm circular dependency vì Attendance không phụ thuộc Dashboard

10. **`StatusEnum.ApprovalPending`** — Dùng constant từ `ShareKernel.Common.Enum.StatusEnum`, không dùng magic string `"PendingApproval"`

11. **`dashboard.refresh` đến từ nhiều module** — Không chỉ Attendance mà còn Payroll (publish, mark-paid) và HR (invite complete) cũng gửi `dashboard.refresh`. Frontend chỉ cần lắng nghe 1 event là `dashboard.refresh` rồi gọi lại API tương ứng — không cần subscribe từng module riêng.

12. **`dashboard.refresh` là best-effort** — SignalR không guarantee delivery. Frontend nên có polling fallback (hoặc refresh khi user chuyển tab) để đảm bảo data luôn mới dù SignalR bị mất kết nối.
