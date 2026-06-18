# HR Flow Documentation — DodoSystem Backend

> Tài liệu phân tích chi tiết luồng Nhân Sự (HR) trong hệ thống DodoSystem.
> Cập nhật: 2026-06-18 (thêm realtime notifications GAP-04, GAP-08)

---

## 1. Tổng quan kiến trúc

Hệ thống HR bao gồm **7 module chính**, vận hành theo mô hình phân lớp Clean Architecture:

| Module | Mô tả |
|--------|-------|
| **Employee** | Quản lý hồ sơ nhân viên (CRUD, tìm kiếm, phân trang) |
| **Department** | Quản lý phòng ban |
| **Position** | Quản lý chức vụ trong từng phòng ban |
| **Invite / Onboarding** | Mời người dùng mới qua email, hoàn tất đăng ký |
| **Manager Authorization** | Gán Manager vào phòng ban để quản lý |
| **Shift & Pattern** | Định nghĩa ca làm việc và lịch ca (cycle-based) |
| **Payroll** | Tính lương, duyệt, thanh toán theo tháng |

### Nguyên tắc bất biến

- Mọi entity đều có `TenantId` → Multi-tenant isolation qua EF Core Global Query Filter
- **Phân quyền 4 cấp**: `TenantAdmin` > `HRManager` > `Manager` > `Employee`
- **Manager scope**: Manager chỉ thấy nhân viên trong phòng ban mình được giao (bảng `ManagerDepartment`)
- `DepartmentId` và `PositionId` phải đi cùng nhau — không thể có một mà thiếu cái kia
- Nhân viên bị "xóa" là **soft delete** → chuyển status về `Resigned`, set `IsDeleted = true`
- Payroll đã `Published` hoặc `Paid` → **không thể tính lại** (idempotent guard)
- Shift và ShiftPattern đã được sử dụng → **không thể sửa/xóa** (ghi lịch sử bất biến)

---

## 2. Các Entity chính

| Entity | File | Mô tả |
|--------|------|-------|
| `Employee` | `Core/Entities/Employee.cs` | Hồ sơ nhân viên. Có thể không liên kết với `User` (UserId nullable) |
| `Department` | `Core/Entities/Department.cs` | Phòng ban của tenant |
| `Position` | `Core/Entities/Position.cs` | Chức vụ, phải thuộc một Department |
| `Invite` | `Core/Entities/Invite.cs` | Lời mời onboarding qua email. Có token hết hạn sau 7 ngày |
| `ManagerDepartment` | `Core/Entities/ManagerDepartment.cs` | Bảng gán Manager ↔ Department (many-to-many) |
| `Shift` | `Core/Entities/Shift.cs` | Ca làm việc, chứa các `ShiftSegment` |
| `ShiftSegment` | `Core/Entities/ShiftSegment.cs` | Khoảng thời gian trong ca. Hỗ trợ `StartDayOffset`/`EndDayOffset` cho ca đêm |
| `ShiftPattern` | `Core/Entities/ShiftPattern.cs` | Lịch ca luân phiên theo chu kỳ (`CycleLengthDays`) |
| `ShiftPatternDay` | `Core/Entities/ShiftPatternDay.cs` | Ngày thứ N trong chu kỳ → ca nào (`ScheduledShiftId` nullable = ngày nghỉ) |
| `EmployeeShiftPattern` | `Core/Entities/EmployeeShiftPattern.cs` | Gán lịch ca cho nhân viên (effective date range) |
| `Payroll` | `Core/Entities/Payroll.cs` | Phiếu lương tháng của nhân viên |

### Employee — Các field quan trọng

| Field | Kiểu | Mô tả |
|-------|------|-------|
| `UserId` | `Guid?` | Nullable — nhân viên có thể tồn tại trước khi có tài khoản |
| `DepartmentId` | `Guid?` | Phòng ban. Phải đi cùng `PositionId` |
| `PositionId` | `Guid?` | Chức vụ. Phải thuộc đúng Department |
| `BaseSalary` | `decimal` | Lương cơ bản dùng để tính Payroll |
| `Status` | `string` | `Working` / `Resigned` / `OnLeave` / `Probation` |
| `HireDate` | `DateOnly` | Ngày vào làm |
| `ResignationDate` | `DateOnly?` | Bắt buộc khi Status = `Resigned` |
| `IsDeleted` | `bool` | Soft delete — không bao giờ xóa thật |

### Payroll — Các field quan trọng

| Field | Kiểu | Mô tả |
|-------|------|-------|
| `BaseSalarySnapshot` | `decimal` | Snapshot lương cơ bản tại thời điểm tính |
| `StandardWorkingDays` | `int` | Số ngày làm việc chuẩn trong tháng (trừ thứ 7, CN, ngày lễ) |
| `ActualWorkingDays` | `int` | Số ngày thực tế có đi làm |
| `BasePay` | `decimal` | = `BaseSalary / StandardDays × ActualDays` |
| `OTPay` | `decimal` | = `OTHours × HourlyRate × 1.5` |
| `PenaltyFee` | `decimal` | = `(LateMinutes + EarlyLeaveMinutes) × MinuteRate` |
| `CustomBonus` | `decimal?` | HR nhập tay |
| `CustomDeduction` | `decimal` | HR nhập tay |
| `NetSalary` | `decimal` | = `BasePay + OTPay - PenaltyFee + CustomBonus - CustomDeduction` |
| `Status` | `PayrollStatus` | `Draft=0` / `Published=1` / `Paid=2` |

---

## 3. Controllers & API Endpoints

### 3.1 HrEmployeesController
- **File**: `WebAPI/Controllers/Hr/HrEmployeesController.cs`
- **Base route**: `api/hr/employees`
- **Auth**: JWT Bearer — `[Authorize]` cho toàn bộ controller

| Method | Endpoint | Role | Mô tả |
|--------|----------|------|-------|
| `GET` | `/` | Mọi role HR | Danh sách nhân viên (phân trang, filter, sort) |
| `GET` | `/{id}` | Mọi role HR | Chi tiết 1 nhân viên |
| `POST` | `/` | TenantAdmin, HRManager | Tạo nhân viên mới |
| `PUT` | `/{id}` | TenantAdmin, HRManager | Cập nhật thông tin |
| `DELETE` | `/{id}` | TenantAdmin, HRManager | Soft-delete (→ Resigned) |
| `GET` | `/department/{departmentId}` | TenantAdmin, HRManager, Manager | Lấy toàn bộ NV trong 1 phòng ban (không phân trang) |

### 3.2 HrDepartmentsController
- **File**: `WebAPI/Controllers/Hr/HrDepartmentsController.cs`
- **Base route**: `api/hr/departments`

| Method | Endpoint | Role | Mô tả |
|--------|----------|------|-------|
| `GET` | `/` | Mọi role HR | Danh sách phòng ban được phép truy cập |
| `POST` | `/` | TenantAdmin, HRManager | Tạo phòng ban mới |
| `PUT` | `/{id}` | TenantAdmin, HRManager | Cập nhật |
| `DELETE` | `/{id}` | TenantAdmin, HRManager | Xóa (lỗi nếu còn nhân viên) |

### 3.3 HrPositionsController
- **File**: `WebAPI/Controllers/Hr/HrPositionsController.cs`
- **Base route**: `api/hr/positions`

| Method | Endpoint | Role | Mô tả |
|--------|----------|------|-------|
| `GET` | `/?departmentId=` | Mọi role HR | Danh sách chức vụ theo phòng ban |
| `POST` | `/` | TenantAdmin, HRManager | Tạo chức vụ (phải thuộc 1 phòng ban) |
| `PUT` | `/{id}` | TenantAdmin, HRManager | Cập nhật |
| `DELETE` | `/{id}` | TenantAdmin, HRManager | Xóa (lỗi nếu đang có nhân viên dùng) |

### 3.4 HrInvitesController
- **File**: `WebAPI/Controllers/Hr/HrInvitesController.cs`
- **Base route**: `api/hr/invites`

| Method | Endpoint | Auth | Mô tả |
|--------|----------|------|-------|
| `POST` | `/send` | `[Authorize]` | Gửi email mời nhân viên mới |
| `GET` | `/validate?token=` | `[AllowAnonymous]` | Kiểm tra token có hợp lệ không |
| `POST` | `/complete` | `[AllowAnonymous]` | Hoàn tất đăng ký từ lời mời |

### 3.5 ManagerDepartmentsController
- **File**: `WebAPI/Controllers/Hr/ManagerDepartmentsController.cs`
- **Base route**: `api/hr/managers`

| Method | Endpoint | Role | Mô tả |
|--------|----------|------|-------|
| `GET` | `/{userId}/departments` | TenantAdmin, Manager, HRManager | Danh sách phòng ban Manager đang quản lý |
| `POST` | `/{userId}/departments` | TenantAdmin | Gán Manager vào 1 hoặc nhiều phòng ban |
| `DELETE` | `/{userId}/departments/{departmentId}` | TenantAdmin | Gỡ quyền Manager khỏi 1 phòng ban |
| `PUT` | `/{userId}/departments` | TenantAdmin | Thay thế toàn bộ danh sách phòng ban |

### 3.6 HrShiftsController
- **File**: `WebAPI/Controllers/Hr/HrShiftsController.cs`
- **Base route**: `api/hr/shifts`

| Method | Endpoint | Role | Mô tả |
|--------|----------|------|-------|
| `GET` | `/` | TenantAdmin, HRManager | Danh sách ca làm việc (phân trang) |
| `GET` | `/{id}` | TenantAdmin, HRManager | Chi tiết ca + segments |
| `POST` | `/` | TenantAdmin, HRManager | Tạo ca mới |
| `PUT` | `/{id}` | TenantAdmin, HRManager | Cập nhật (lỗi nếu đã được dùng) |
| `DELETE` | `/{id}` | TenantAdmin, HRManager | Soft delete (lỗi nếu đã được dùng) |

### 3.7 HrShiftPatternsController
- **File**: `WebAPI/Controllers/Hr/HrShiftPatternsController.cs`
- **Base route**: `api/hr/shift-patterns`

| Method | Endpoint | Role | Mô tả |
|--------|----------|------|-------|
| `GET` | `/` | TenantAdmin, HRManager | Danh sách lịch ca (phân trang) |
| `GET` | `/{id}` | TenantAdmin, HRManager | Chi tiết lịch ca + days |
| `POST` | `/` | TenantAdmin, HRManager | Tạo lịch ca mới |
| `PUT` | `/{id}` | TenantAdmin, HRManager | Cập nhật (lỗi nếu đã được assign) |
| `DELETE` | `/{id}` | TenantAdmin, HRManager | Soft delete (lỗi nếu đã được assign) |

### 3.8 HrShiftAssignmentsController
- **File**: `WebAPI/Controllers/Hr/HrShiftAssignmentsController.cs`
- **Base route**: `api/hr/shift-assignments`

| Method | Endpoint | Role | Mô tả |
|--------|----------|------|-------|
| `POST` | `/bulk` | TenantAdmin, HRManager, Manager | Gán lịch ca hàng loạt cho N nhân viên |
| `GET` | `/` | TenantAdmin, HRManager, Manager | Danh sách gán lịch ca (phân trang) |
| `GET` | `/{id}` | TenantAdmin, HRManager, Manager | Chi tiết 1 bản gán |
| `GET` | `/my-current` | Mọi user | Lịch ca hiện tại đang gán của bản thân |

### 3.9 PayrollController
- **File**: `WebAPI/Controllers/PayrollController.cs`
- **Base route**: `api/payrolls`

| Method | Endpoint | Role | Mô tả |
|--------|----------|------|-------|
| `POST` | `/generate?month=&year=` | TenantAdmin | Sinh phiếu lương tháng cho toàn bộ NV |
| `POST` | `/calculate/{employeeId}?month=&year=` | TenantAdmin, HRManager | Tính lại cho 1 NV |
| `GET` | `/paged` | TenantAdmin, HRManager, Manager | Danh sách phiếu lương (phân trang, filter) |
| `GET` | `/my` | Mọi user | Phiếu lương của bản thân (chỉ Published/Paid) |
| `PUT` | `/{payrollId}/publish` | TenantAdmin, HRManager | Chốt phiếu lương (Draft → Published) |
| `PUT` | `/{payrollId}/mark-paid` | TenantAdmin | Đánh dấu đã thanh toán (Published → Paid) |
| `PUT` | `/publish-all?month=&year=` | TenantAdmin | Chốt tất cả Draft trong tháng |
| `PUT` | `/{payrollId}/manual-fields` | TenantAdmin, HRManager, Manager | Điều chỉnh CustomBonus/CustomDeduction |

---

## 4. DTOs

### Request DTOs (Input)

| DTO | Dùng cho |
|-----|---------|
| `EmployeeCreateDto` | Tạo nhân viên mới |
| `EmployeeUpdateDto` | Cập nhật thông tin nhân viên |
| `EmployeeQueryDto` | Filter/sort/page danh sách nhân viên |
| `DepartmentCreateDto` | Tạo phòng ban |
| `DepartmentUpdateDto` | Cập nhật phòng ban |
| `PositionCreateDto` | Tạo chức vụ (có `DepartmentId`) |
| `PositionUpdateDto` | Cập nhật chức vụ |
| `HrInviteSendRequestDto` | Gửi lời mời (Email, RoleId, DepartmentId?, PositionId?, Message) |
| `HrInviteCompleteRequestDto` | Hoàn tất onboarding (Token, FullName, Password, Phone?) |
| `AssignManagerDepartmentDto` | Gán/Thay thế danh sách phòng ban cho Manager |
| `ShiftCreateDto` | Tạo/cập nhật ca làm việc (gồm Segments[]) |
| `ShiftPatternCreateDto` | Tạo/cập nhật lịch ca (gồm Days[]) |
| `ShiftAssignmentBulkCreateDto` | Gán lịch ca cho nhiều NV cùng lúc |
| `ShiftQueryDto` | Filter/sort ca làm việc |
| `ShiftPatternQueryDto` | Filter/sort lịch ca |
| `ShiftAssignmentQueryDto` | Filter/sort gán lịch ca |
| `UpdatePayrollDto` | Điều chỉnh CustomBonus, CustomDeduction, Reason |
| `PayrollQueryDto` | Filter/sort phiếu lương |

### Response DTOs (Output)

| DTO | Dùng cho |
|-----|---------|
| `EmployeeDto` | Chi tiết nhân viên (có DepartmentName, PositionName) |
| `DepartmentDto` | Chi tiết phòng ban |
| `PositionDto` | Chi tiết chức vụ |
| `ManagerDepartmentDto` | Bản ghi gán Manager ↔ Department |
| `ShiftDto` | Ca làm việc + Segments |
| `ShiftPatternDto` | Lịch ca + Days + Shifts |
| `EmployeeShiftPatternDto` | Bản gán lịch ca (có EmployeeName, ShiftPatternName) |
| `MyCurrentShiftAssignmentDto` | Lịch ca hiện tại đầy đủ (gồm ShiftPattern chi tiết) |
| `PayrollDto` | Phiếu lương đầy đủ |

---

## 5. Services

### 5.1 HrEmployeeService
- **Interface**: `Application/Interfaces/IServices/IHrEmployeeService.cs`
- **Implementation**: `Application/Services/HrEmployeeService.cs`
- Inject: `IHrAuthorizationService` để kiểm tra scope phòng ban của Manager

### 5.2 HrDepartmentService
- **Implementation**: `Application/Services/HrDepartmentService.cs`

### 5.3 HrPositionService
- **Implementation**: `Application/Services/HrPositionService.cs`

### 5.4 InviteService
- **Implementation**: `Application/Services/InviteService.cs`
- Sử dụng Outbox pattern + RabbitMQ để gửi email không đồng bộ

### 5.5 ManagerDepartmentService
- **Implementation**: `Application/Services/ManagerDepartmentService.cs`

### 5.6 ShiftManagementService
- **Implementation**: `Application/Services/ShiftManagementService.cs`
- Xử lý cả 3 controller: Shifts, ShiftPatterns, ShiftAssignments

### 5.7 HrAuthorizationService
- **Implementation**: `Application/Services/HrAuthorizationService.cs`
- Helper service cho phân quyền Manager: `GetAccessibleDepartmentIdsAsync()`, `EnsureEmployeeAccessAsync()`, `EnsureDepartmentAccessAsync()`

### 5.8 PayrollService
- **Implementation**: `Application/Services/PayrollService.cs`
- Tính lương từ `DailyTimesheet` + `PublicHoliday`

---

## 6. Luồng Chi Tiết Từng Use Case

---

### LUỒNG A: Thiết lập cấu trúc tổ chức (TenantAdmin)

```
[ADMIN] → Thiết lập Department → Position → Manager scope

[1] POST /api/hr/departments
    { name: "Kỹ thuật", description: "..." }
    ← DepartmentDto created

[2] POST /api/hr/positions
    { name: "Backend Developer", departmentId: "uuid-kt" }
    ← PositionDto created (validate: dept phải tồn tại)

[3] POST /api/hr/managers/{managerUserId}/departments
    { departmentIds: ["uuid-kt", "uuid-qa"] }
    ← Manager được gán quyền quản lý 2 phòng ban

[4] GET /api/hr/managers/{managerUserId}/departments
    ← Xác nhận danh sách phòng ban đã gán
```

---

### LUỒNG B: Mời nhân viên mới qua email (Invite Onboarding)

```
[HR ADMIN]
  [1] POST /api/hr/invites/send
      { email: "nhanvien@gmail.com", roleId: 3, departmentId: "uuid-kt", positionId: "uuid-be", message: "Chào mừng!" }
      ← Validate: email chưa tồn tại, DepartmentId+PositionId đồng thời
      ← Invite record: token = UUID, ExpiryDate = now+7days, IsUsed=false
      ← OutboxMessage → RabbitMQ → Email Service gửi email có link onboarding

[EMAIL]
  Link: https://app.example.com/onboarding/{token}

[NHÂN VIÊN - unauthenticated]
  [2] GET /api/hr/invites/validate?token={token}
      ← Trả về: email, tenantId, roleId, departmentId, positionId, expiryDate, isUsed

  [3] POST /api/hr/invites/complete
      { token: "...", fullName: "Nguyễn Văn A", password: "...", phone: "0901234567" }
      ← Validate: token hợp lệ, module HR đang Active/Trial, email chưa dùng
      ← Tạo User (IsActive=true, IsVerified=true)
      ← Tạo UserRole (gán role từ Invite)
      ← Tạo Employee (Status="Working", BaseSalary=0, HireDate=today)
      ← Nếu role=Manager && có DepartmentId → auto tạo ManagerDepartment
      ← invite.IsUsed = true
      ← [Realtime - GAP-08] NotifyEmployeeOnboardedAsync(tenantId, { employeeName, departmentName, ... })
         → HR Admin thấy ngay qua "tenant:{tenantId}:admins"
      ← [Realtime] NotifyDashboardRefreshAsync(tenantId)
         → Dashboard tự cập nhật totalEmployees
```

**Sơ đồ Invite → Onboarding:**
```
HR gửi invite
     ↓
Invite record (IsUsed=false, token, expiry)
     ↓
OutboxMessage → RabbitMQ → Email (async)
     ↓
Nhân viên nhận link → /validate (kiểm tra)
     ↓
/complete → User + UserRole + Employee tạo cùng lúc
     ↓
invite.IsUsed = true
     ↓
[Fire-and-forget] NotifyEmployeeOnboardedAsync(tenantId, {...})
[Fire-and-forget] NotifyDashboardRefreshAsync(tenantId)
```

---

### LUỒNG C: Tạo nhân viên trực tiếp (không qua invite)

```
POST /api/hr/employees
{
  fullName: "Trần Thị B",
  email: "b@company.com",
  phone: "0912345678",
  hireDate: "2026-06-01",
  baseSalary: 15000000,
  status: "Working",          // KHÔNG được là "Resigned"
  departmentId: "uuid-kt",    // Phải đi cùng positionId
  positionId: "uuid-be",      // Phải thuộc departmentId
  userId: null                // Nullable — chưa có tài khoản
}
```

**Validate:**
```
1. EnsureHrAccess() → chỉ HR role mới vào được
2. Nếu caller là Manager → EnsureDepartmentAccess(request.DepartmentId)
3. ValidateDepartmentPositionAsync:
   - Nếu có 1 trong 2 (DeptId/PosId) mà không có cả 2 → throw "phải đi cùng nhau"
   - Dept phải tồn tại
   - Position phải tồn tại
   - position.DepartmentId == request.DepartmentId (Position thuộc đúng Dept)
4. Status không được là "Resigned"
```

---

### LUỒNG D: Cập nhật nhân viên / Offboarding

```
PUT /api/hr/employees/{id}
{
  fullName: ...,
  status: "Resigned",
  resignationDate: "2026-06-30",   // BẮT BUỘC khi Resigned
  ...
}
```

**Validate:**
```
- EnsureHrAccess() + EnsureEmployeeAccess(emp) → scope check
- Nếu status="Resigned" && resignationDate == null → throw
- Nếu status != "Resigned" → resignationDate bị xóa (set null)
```

**Soft Delete (DELETE `/{id}`):**
```
emp.Status = "Resigned"
emp.ResignationDate ??= today
emp.IsDeleted = true
emp.UpdatedAt = now
→ KHÔNG xóa record khỏi DB
```

---

### LUỒNG E: Quản lý Ca làm việc (Shift)

#### E.1 — Tạo Shift

```
POST /api/hr/shifts
{
  code: "CA_SANG",
  name: "Ca Sáng",
  gracePeriodMinutes: 10,
  isCrossDay: false,
  segments: [
    { startTime: "08:00", endTime: "12:00", startDayOffset: 0, endDayOffset: 0 },
    { startTime: "13:00", endTime: "17:30", startDayOffset: 0, endDayOffset: 0 }
  ]
}
```

**Validate Segments:**
```
- Mỗi segment: EndTime > StartTime (theo absolute minutes = dayOffset × 24×60 + time)
- Các segment không được chồng lấn nhau (overlap)
- Sort theo StartTime và check khoảng cách
```

#### E.2 — Tạo ShiftPattern (Lịch ca chu kỳ)

```
POST /api/hr/shift-patterns
{
  name: "Lịch 5 ngày làm 2 ngày nghỉ",
  cycleLengthDays: 7,      // Max: 7
  days: [
    { dayIndex: 0, scheduledShiftId: "uuid-ca-sang" },
    { dayIndex: 1, scheduledShiftId: "uuid-ca-sang" },
    { dayIndex: 2, scheduledShiftId: "uuid-ca-sang" },
    { dayIndex: 3, scheduledShiftId: "uuid-ca-sang" },
    { dayIndex: 4, scheduledShiftId: "uuid-ca-sang" },
    { dayIndex: 5, scheduledShiftId: null },    // null = ngày nghỉ
    { dayIndex: 6, scheduledShiftId: null }
  ]
}
```

**Validate:**
```
- cycleLengthDays > 0 và ≤ 7
- dayIndex phải trong [0, cycleLengthDays-1]
- Không có dayIndex trùng nhau
- scheduledShiftId phải tồn tại (nếu không null)
```

#### E.3 — Gán lịch ca cho nhân viên (Bulk)

```
POST /api/hr/shift-assignments/bulk
{
  shiftPatternId: "uuid-pattern",
  employeeIds: ["uuid-emp1", "uuid-emp2", "uuid-emp3"],
  effectiveStartDate: "2026-06-01"
}
```

**Logic gán:**
```
1. Validate: shiftPattern tồn tại, tất cả employeeIds hợp lệ
2. Nếu Manager: EnsureEmployeeAccess(emp) cho từng nhân viên
3. BulkEndPreviousAssignmentsAsync → đặt EffectiveEndDate = effectiveStartDate - 1 ngày cho
   tất cả assignment cũ của các nhân viên này
4. BulkInsertAssignmentsAsync → tạo EmployeeShiftPattern mới:
   { EffectiveStartDate = request.EffectiveStartDate, EffectiveEndDate = null }
5. [Realtime - GAP-04] Bulk load employees (1 query GetByIdsAsync), emit per employee:
   foreach emp with UserId != null:
       _ = _realtime.NotifyShiftAssignedAsync(emp.UserId.Value, {
           shiftPatternName:    shiftPattern.Name,
           effectiveStartDate:  request.EffectiveStartDate
       }).ContinueWith(log if faulted)
   → Gửi tới "user:{userId}" từng nhân viên, mobile app cập nhật calendar
```

#### E.4 — Nhân viên xem lịch ca hiện tại

```
GET /api/hr/shift-assignments/my-current
← Tìm Employee từ UserId (JWT claim)
← Lấy EmployeeShiftPattern active hôm nay
← Load đầy đủ ShiftPattern với Days → Shifts → Segments
← MyCurrentShiftAssignmentDto
```

---

### LUỒNG F: Tính lương hàng tháng (Payroll)

#### F.1 — Generate toàn bộ phiếu lương

```
POST /api/payrolls/generate?month=5&year=2026
```

**Logic GenerateMonthlyPayrollAsync:**

```
Bước 1 — Load dữ liệu (tránh N+1):
  employees = GetAllActiveEmployeeByTenantId(tenantId)
  existingPayrolls = GetByTenantMonthAsync(tenantId, month, year)
  holidays = GetAllAsync(tenantId)       // Ngày lễ của tenant
  allTimesheets = GetByTenantMonthAsync(tenantId, month, year)   // Bulk load
  timesheetByEmployee = Dictionary<EmployeeId, List<Timesheet>>

Bước 2 — Tính StandardWorkingDays (1 lần, dùng chung):
  standardDays = count(weekday in month)
                - count(publicHolidays in month)
                // Thứ 7 và CN không tính
                // Holidays hỗ trợ IsRecurringYearly

Bước 3 — Với mỗi nhân viên:
  if existingPayroll.Status == Published/Paid → BỎ QUA (idempotent)

  actualDays = timesheets có ActualWorkHours > 0
               hoặc Status ∈ { Normal, Late, EarlyLeave, MissingOut, OnLeave }
  
  lateMinutes = sum(TotalLateMinutes) WHERE Status != MissingOut
  earlyLeaveMinutes = sum(TotalEarlyLeaveMinutes) WHERE Status != MissingOut
  // ← MissingOut loại khỏi penalty: AttendanceResolution tính earlyLeave quá nặng cho ngày MissingOut
  
  absentDays = count(Status == Absent)
  otHours = sum(OTHours)

Bước 4 — Công thức lương:
  basePay     = (BaseSalary / standardDays) × actualDays
  hourlyRate  = (BaseSalary / standardDays) / 8
  otPay       = otHours × hourlyRate × 1.5
  minuteRate  = hourlyRate / 60
  penaltyFee  = (lateMinutes + earlyLeaveMinutes) × minuteRate
  netSalary   = basePay + otPay - penaltyFee + (customBonus ?? 0) - customDeduction

Bước 5 — Upsert:
  Nếu existingPayroll (Draft) → UPDATE (giữ nguyên CustomBonus/Deduction đã nhập tay)
  Nếu không có → INSERT mới với Status=Draft, CustomBonus=0, CustomDeduction=0
```

**Ví dụ tính lương:**
```
BaseSalary = 15,000,000 VND
StandardDays = 22 ngày
ActualDays = 20 ngày
LateMinutes = 45 phút
OTHours = 4 giờ

HourlyRate = (15,000,000 / 22) / 8 = 85,227 VND/giờ
MinuteRate = 85,227 / 60 = 1,421 VND/phút

BasePay     = (15,000,000 / 22) × 20 = 13,636,364 VND
OTPay       = 4 × 85,227 × 1.5       = 511,364 VND
PenaltyFee  = 45 × 1,421              = 63,947 VND
NetSalary   = 13,636,364 + 511,364 - 63,947 = 14,083,781 VND
```

#### F.2 — Điều chỉnh thủ công (Custom Bonus/Deduction)

```
PUT /api/payrolls/{payrollId}/manual-fields
{
  customBonus: 500000,
  customDeduction: 100000,
  reason: "Thưởng KPI tháng 5, khấu trừ trang thiết bị"
}
```

**Validate:**
```
- Status phải là Draft
- NetSalary được tính lại ngay: BasePay + OTPay - PenaltyFee + CustomBonus - CustomDeduction
- Reason lưu vào Notes field
```

#### F.3 — Vòng đời phiếu lương

```
Sinh phiếu Draft:
  POST /generate  → Status = Draft

Review & chỉnh sửa:
  PUT /manual-fields  ← Chỉ được khi Draft
  POST /calculate/{empId}  ← Tính lại khi có thay đổi timesheet

Chốt phiếu:
  PUT /{id}/publish    → Draft → Published (khóa edit)
  PUT /publish-all     → Chốt toàn bộ Draft trong tháng

Xác nhận thanh toán:
  PUT /{id}/mark-paid  → Published → Paid

Nhân viên xem:
  GET /my  ← Chỉ thấy Published và Paid
```

**Vòng đời trạng thái Payroll:**
```
[Draft] ←─── tính lại ──── [Draft]
   │                           ↑
   │ publish                   │ generate (nếu đã có Draft cũ)
   ↓                           │
[Published] ──────────────────┘
   │
   │ mark-paid
   ↓
[Paid]  ← Trạng thái cuối, không thể thay đổi
```

---

### LUỒNG G: Xem danh sách nhân viên theo phạm vi quyền hạn

```
GET /api/hr/employees?departmentId=&status=Working&pageNumber=1&pageSize=20&sortBy=fullName&sortDir=asc
```

**Phân quyền truy cập:**
```
if role == TenantAdmin || HRManager:
    accessibleIds = null   ← Không giới hạn, dùng query.DepartmentId như thường

if role == Manager:
    accessibleIds = GetManagerDepartmentIds(currentUserId)   // [uuid-kt, uuid-qa]

    if query.DepartmentId có nhưng không nằm trong accessibleIds:
        → throw Forbidden

    if query.DepartmentId == null (Manager không filter cụ thể):
        if accessibleIds.Count == 1:
            → tự động filter theo phòng ban duy nhất đó
        if accessibleIds.Count > 1:
            → loop từng phòng ban, ghép lại, paginate thủ công
```

---

### LUỒNG H: Kịch bản tổng hợp — Onboard nhân viên mới → Gán ca → Tính lương

```
Tháng 6/2026:

Ngày 1:
  [1] POST /departments → Tạo "Kỹ thuật"
  [2] POST /positions   → Tạo "Backend Dev" thuộc "Kỹ thuật"
  [3] POST /hr/shifts   → Tạo ca "Ca Sáng" (8:00-17:30)
  [4] POST /hr/shift-patterns → Tạo "5+2" (5 ngày làm, 2 ngày nghỉ)

Ngày 2:
  [5] POST /invites/send  → Gửi lời mời email cho nhân viên mới
  [6] POST /invites/complete → Nhân viên hoàn tất đăng ký
      ← User + Employee được tạo tự động

Ngày 3:
  [7] PUT /employees/{id} → HR cập nhật BaseSalary cho nhân viên
  [8] POST /shift-assignments/bulk → Gán lịch "5+2" bắt đầu từ 2026-06-03

Cuối tháng 6:
  [9] POST /payrolls/generate?month=6&year=2026 → Tính lương toàn bộ NV
  [10] PUT /payrolls/{id}/manual-fields → HR thêm CustomBonus KPI
  [11] PUT /payrolls/publish-all?month=6&year=2026 → Chốt tất cả
  [12] PUT /payrolls/{id}/mark-paid → Đánh dấu đã chuyển khoản
```

---

## 7. Phân quyền chi tiết

### Ma trận phân quyền

| Hành động | Employee | Manager | HRManager | TenantAdmin |
|-----------|----------|---------|-----------|-------------|
| Xem nhân viên trong phòng ban mình | ❌ | ✅ (scope) | ✅ (all) | ✅ (all) |
| Tạo/sửa/xóa nhân viên | ❌ | ❌ | ✅ | ✅ |
| Quản lý phòng ban/chức vụ | ❌ | ❌ | ✅ | ✅ |
| Gửi invite | ❌ | ❌ | ❌ | ✅ |
| Gán Manager vào phòng ban | ❌ | ❌ | ❌ | ✅ |
| Quản lý ca làm việc | ❌ | ❌ | ✅ | ✅ |
| Gán lịch ca cho NV | ❌ | ✅ (scope) | ✅ | ✅ |
| Xem lịch ca của mình | ✅ | ✅ | ✅ | ✅ |
| Generate/calculate payroll | ❌ | ❌ | ✅ (calculate only) | ✅ |
| Điều chỉnh custom bonus | ❌ | ✅ (scope) | ✅ | ✅ |
| Publish payroll | ❌ | ❌ | ✅ | ✅ |
| Mark paid | ❌ | ❌ | ❌ | ✅ |
| Xem payroll của mình | ✅ (Published+Paid) | ✅ (scope) | ✅ | ✅ |

### HrAuthorizationService — Logic kiểm tra

```
GetAccessibleDepartmentIdsAsync():
  if role == TenantAdmin || HRManager → return null  (không giới hạn)
  if role == Manager → return List<Guid> từ ManagerDepartment
  else → throw Forbidden

EnsureEmployeeAccessAsync(emp):
  if TenantAdmin/HRManager → OK
  if Manager:
    deptIds = GetAccessibleDepartmentIdsAsync()
    if emp.DepartmentId not in deptIds → throw Forbidden

EnsureDepartmentAccessAsync(deptId):
  if TenantAdmin/HRManager → OK
  if Manager:
    deptIds = GetAccessibleDepartmentIdsAsync()
    if deptId not in deptIds → throw Forbidden
```

---

## 8. Các Status Codes

### Employee.Status

| Status | Ý nghĩa |
|--------|---------|
| `Working` | Đang làm việc bình thường |
| `Probation` | Đang trong thời gian thử việc |
| `OnLeave` | Đang nghỉ dài hạn (thai sản, ốm...) |
| `Resigned` | Đã nghỉ việc (soft-deleted) |

### PayrollStatus (enum)

| Value | Ý nghĩa | Có thể chỉnh sửa? |
|-------|---------|-----------------|
| `Draft = 0` | Nháp — đang xử lý | ✅ |
| `Published = 1` | Đã chốt — đã thông báo cho NV | ❌ |
| `Paid = 2` | Đã thanh toán | ❌ |

### Invite — Trạng thái

| Field | Ý nghĩa |
|-------|---------|
| `IsUsed = false` | Token còn hiệu lực |
| `IsUsed = true` | Đã được sử dụng |
| `ExpiryDate` | Hết hạn sau 7 ngày kể từ khi gửi |

---

## 9. Business Rules quan trọng

### Employee
1. Không thể tạo nhân viên với Status = `Resigned`
2. Khi Status = `Resigned` → `ResignationDate` bắt buộc
3. `DepartmentId` và `PositionId` phải đi cùng nhau hoặc cùng null
4. `Position` phải thuộc đúng `Department` được chỉ định
5. Delete là soft-delete: set `IsDeleted = true`, `Status = Resigned`

### Invite
1. Email phải chưa tồn tại trong hệ thống
2. Token hết hạn sau 7 ngày
3. Mỗi token chỉ dùng được 1 lần (`IsUsed`)
4. Tenant phải đang có module HR active hoặc trial mới hoàn tất onboarding được
5. Nếu mời với role Manager + có DepartmentId → tự động gán ManagerDepartment khi complete

### Shift & ShiftPattern
1. Shift có `HasUsage` (đang được dùng trong ShiftPattern) → không thể sửa/xóa
2. ShiftPattern có `HasUsage` (đang được assign cho NV) → không thể sửa/xóa
3. Khi assign mới → assignment cũ của NV được `EndDate = newStartDate - 1` (kết thúc tự động)
4. `CycleLengthDays` ≤ 7
5. Segment không được overlap nhau
6. `scheduledShiftId = null` trong `ShiftPatternDay` = ngày nghỉ trong chu kỳ

### Payroll
1. Chỉ tính cho nhân viên có Status = `Working` (hoặc tương đương active)
2. Idempotent: `Published`/`Paid` payroll bị bỏ qua khi generate lại
3. `MissingOut` được tính vào `actualDays` nhưng **không** tính vào penalty (để tránh phạt quá nặng)
4. `CustomBonus`/`CustomDeduction` được giữ nguyên khi tính lại
5. Tính lại chỉ được khi Status = `Draft`
6. `BaseSalarySnapshot` = lương tại thời điểm tính (snapshot, không thay đổi sau khi published)

---

## 10. Thứ tự API theo từng kịch bản

### Kịch bản 1: Setup tổ chức lần đầu (TenantAdmin)

```
[1] POST /hr/departments  × N       → Tạo các phòng ban
[2] POST /hr/positions    × N       → Tạo chức vụ cho mỗi phòng ban
[3] POST /hr/shifts       × N       → Định nghĩa các ca làm việc
[4] POST /hr/shift-patterns × N     → Tạo lịch ca (5+2, 6+1...)
[5] POST /hr/invites/send × N       → Mời quản lý (Manager role) vào
[6] POST /hr/managers/{id}/departments → Gán Manager vào phòng ban
```

### Kịch bản 2: Tuyển dụng nhân viên mới

```
[1] POST /hr/invites/send
    { email, roleId: EmployeeRole, departmentId, positionId, message }

[2] (Nhân viên nhận email, vào link)
    GET /hr/invites/validate?token=...      → Kiểm tra token
    POST /hr/invites/complete               → Hoàn tất đăng ký

[3] GET /hr/employees/{id}                  → HR kiểm tra hồ sơ vừa tạo
[4] PUT /hr/employees/{id}                  → HR cập nhật BaseSalary
[5] POST /hr/shift-assignments/bulk
    { shiftPatternId, employeeIds: [id], effectiveStartDate }
```

### Kịch bản 3: Nhân viên nghỉ việc (Offboarding)

```
[1] PUT /hr/employees/{id}
    { ..., status: "Resigned", resignationDate: "2026-06-30" }
    ← Employee còn trong DB (soft-delete)

Hoặc:
[1] DELETE /hr/employees/{id}
    ← Tự động set Status=Resigned, IsDeleted=true, ResignationDate=today
```

### Kịch bản 4: Tính và duyệt lương tháng

```
// Cuối tháng hoặc đầu tháng sau:
[1] POST /payrolls/generate?month=5&year=2026
    ← Draft payrolls được tạo cho toàn bộ NV active

// HR review từng phiếu:
[2] GET /payrolls/paged?month=5&year=2026&status=Draft

// Điều chỉnh nếu cần:
[3] PUT /payrolls/{id}/manual-fields
    { customBonus: 500000, reason: "Thưởng KPI" }

// Tính lại nếu timesheet thay đổi:
[4] POST /payrolls/calculate/{empId}?month=5&year=2026

// Chốt toàn bộ:
[5] PUT /payrolls/publish-all?month=5&year=2026
    ← Toàn bộ Draft → Published (NV có thể xem)

// Sau khi chuyển khoản:
[6] PUT /payrolls/{id}/mark-paid  × N
```

---

## 11. Security & Authorization

| Loại | Rule |
|------|------|
| Authentication | JWT Bearer Token bắt buộc cho tất cả endpoint (trừ invite validate/complete) |
| Multi-tenant | EF Core Global Query Filter theo `TenantId` (dynamic, không cache) |
| Manager scope | Chỉ thấy nhân viên trong phòng ban được giao (bảng `ManagerDepartment`) |
| Employee scope | Chỉ xem payroll và lịch ca của bản thân |
| Invite token | UUID, 7 ngày TTL, single-use (`IsUsed` flag) |
| Module check | Invite complete yêu cầu module HR active/trial |
| Payroll lock | Published/Paid payroll không thể bị sửa hay tính lại |
| Shift lock | Shift/ShiftPattern đang dùng không thể bị sửa/xóa |

---

## 12. Files tham khảo

### Controllers
- [HrEmployeesController.cs](../SMEFLOWSystem.WebAPI/Controllers/Hr/HrEmployeesController.cs)
- [HrDepartmentsController.cs](../SMEFLOWSystem.WebAPI/Controllers/Hr/HrDepartmentsController.cs)
- [HrPositionsController.cs](../SMEFLOWSystem.WebAPI/Controllers/Hr/HrPositionsController.cs)
- [HrInvitesController.cs](../SMEFLOWSystem.WebAPI/Controllers/Hr/HrInvitesController.cs)
- [ManagerDepartmentsController.cs](../SMEFLOWSystem.WebAPI/Controllers/Hr/ManagerDepartmentsController.cs)
- [HrShiftsController.cs](../SMEFLOWSystem.WebAPI/Controllers/Hr/HrShiftsController.cs)
- [HrShiftPatternsController.cs](../SMEFLOWSystem.WebAPI/Controllers/Hr/HrShiftPatternsController.cs)
- [HrShiftAssignmentsController.cs](../SMEFLOWSystem.WebAPI/Controllers/Hr/HrShiftAssignmentsController.cs)
- [PayrollController.cs](../SMEFLOWSystem.WebAPI/Controllers/PayrollController.cs)

### Services
- [HrEmployeeService.cs](../SMEFLOWSystem.Application/Services/HrEmployeeService.cs)
- [HrDepartmentService.cs](../SMEFLOWSystem.Application/Services/HrDepartmentService.cs)
- [HrPositionService.cs](../SMEFLOWSystem.Application/Services/HrPositionService.cs)
- [InviteService.cs](../SMEFLOWSystem.Application/Services/InviteService.cs)
- [ManagerDepartmentService.cs](../SMEFLOWSystem.Application/Services/ManagerDepartmentService.cs)
- [ShiftManagementService.cs](../SMEFLOWSystem.Application/Services/ShiftManagementService.cs)
- [HrAuthorizationService.cs](../SMEFLOWSystem.Application/Services/HrAuthorizationService.cs)
- [PayrollService.cs](../SMEFLOWSystem.Application/Services/PayrollService.cs)

### Entities
- [Employee.cs](../SMEFLOWSystem.Core/Entities/Employee.cs)
- [Department.cs](../SMEFLOWSystem.Core/Entities/Department.cs)
- [Position.cs](../SMEFLOWSystem.Core/Entities/Position.cs)
- [Invite.cs](../SMEFLOWSystem.Core/Entities/Invite.cs)
- [ManagerDepartment.cs](../SMEFLOWSystem.Core/Entities/ManagerDepartment.cs)
- [Shift.cs](../SMEFLOWSystem.Core/Entities/Shift.cs)
- [ShiftSegment.cs](../SMEFLOWSystem.Core/Entities/ShiftSegment.cs)
- [ShiftPattern.cs](../SMEFLOWSystem.Core/Entities/ShiftPattern.cs)
- [ShiftPatternDay.cs](../SMEFLOWSystem.Core/Entities/ShiftPatternDay.cs)
- [EmployeeShiftPattern.cs](../SMEFLOWSystem.Core/Entities/EmployeeShiftPattern.cs)
- [Payroll.cs](../SMEFLOWSystem.Core/Entities/Payroll.cs)

---

## 13. Điểm quan trọng cần nhớ

1. **DepartmentId + PositionId đi cặp** — Không thể có một mà thiếu cái kia. Và Position phải thuộc đúng Department.

2. **Invite onboarding là async** — Email được gửi qua Outbox → RabbitMQ, không gửi trực tiếp trong request. Token có TTL 7 ngày.

3. **Soft delete cho Employee** — Không bao giờ xóa record thật. Chỉ set `IsDeleted=true`, `Status=Resigned`.

4. **Shift lock bất biến** — Một khi Shift/ShiftPattern đã được dùng thực tế (trong pattern hoặc assignment), không thể sửa hay xóa. Phải tạo bản mới.

5. **Assign ca mới → tự động kết thúc ca cũ** — `BulkEndPreviousAssignments` đặt `EffectiveEndDate = newStartDate - 1`. Nhân viên luôn chỉ có 1 assignment active tại 1 thời điểm.

6. **MissingOut không bị phạt trong Payroll** — Ngày `MissingOut` tính vào `actualDays` (được trả lương) nhưng không tính `LateMinutes`/`EarlyLeaveMinutes` (không bị trừ). Lý do: `AttendanceResolution` ghi `EarlyLeaveMinutes` quá lớn cho ngày đó vì thiếu check-out.

7. **BaseSalarySnapshot** — Khi tính lương, snapshot lương cơ bản lại. Sau khi HR tăng lương cho nhân viên, các phiếu lương đã tính trước đó không thay đổi.

8. **Idempotent Payroll Generate** — Gọi `/generate` nhiều lần vẫn an toàn: skip `Published`/`Paid`, update `Draft` cũ (giữ CustomBonus).

9. **Manager multi-department** — Một Manager có thể quản lý nhiều phòng ban. `HrEmployeeService` xử lý trường hợp nhiều phòng ban bằng cách loop và ghép kết quả thủ công (MVP pattern).

10. **Module HR check khi onboarding** — Tenant phải có subscription module HR đang `Active` hoặc `Trial` mới cho phép nhân viên hoàn tất đăng ký từ invite.
