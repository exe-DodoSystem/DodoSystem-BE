# Payroll Flow Documentation — DodoSystem Backend

> Tài liệu phân tích chi tiết luồng Tính Lương (Payroll) trong hệ thống DodoSystem.
> Cập nhật: 2026-06-18 (phản ánh các bổ sung từ payroll_enhancement_plan.md — TASK 3)

---

## 1. Tổng quan kiến trúc

Hệ thống Payroll hoạt động theo mô hình **on-demand + lifecycle**:

1. **Pre-generate Phase** — Admin định nghĩa các khoản thưởng/phạt theo cấu trúc cho từng nhân viên qua `EmployeeBonusDeductionEntry` (tùy chọn, có thể làm trước hoặc sau bước 2)
2. **Generate Phase** — Admin gọi API → hệ thống đọc `DailyTimesheet` + `EmployeeBonusDeductionEntry` của tháng, tính toán và tạo bản nháp `Payroll` (Draft)
3. **Review Phase** — Admin/HRManager xem, chỉnh sửa `CustomBonus` / `CustomDeduction` thủ công nếu cần (bổ sung vào `StructuredBonus`/`StructuredDeduction` đã tự động)
4. **Publish Phase** — Admin chốt phiếu lương (`Published`) — nhân viên có thể xem
5. **Paid Phase** — Admin đánh dấu đã thanh toán (`Paid`) — hoàn tất vòng đời

### Nguyên tắc bất biến

- Phiếu lương `Published` và `Paid` **không thể bị tính toán lại** — chỉ `Draft` mới được recalculate
- Gọi `generate` lại tháng đã có phiếu `Published/Paid` → **bỏ qua** nhân viên đó (idempotent)
- `BaseSalarySnapshot` lưu lại lương cơ bản **tại thời điểm tính** — nếu lương thay đổi sau, phiếu cũ không bị ảnh hưởng
- `CustomBonus` / `CustomDeduction` chỉ chỉnh được khi **Draft** — chốt rồi không sửa
- `StructuredBonus` / `StructuredDeduction` được tính lại mỗi lần `generate`/`calculate` từ entries hiện tại
- `EmployeeBonusDeductionEntry` chỉ thêm/xóa được khi phiếu lương tháng đó còn **Draft** (hoặc chưa có phiếu)
- Dữ liệu đầu vào của Payroll là `DailyTimesheet` — Payroll không tự tính chấm công, hoàn toàn phụ thuộc vào kết quả từ luồng Attendance

---

## 2. Các Entity chính

| Entity | File | Mô tả |
|--------|------|-------|
| `Payroll` | `Core/Entities/Payroll.cs` | Phiếu lương theo tháng của từng nhân viên. 1 nhân viên × 1 tháng = tối đa 1 record |
| `EmployeeBonusDeductionEntry` | `Core/Entities/EmployeeBonusDeductionEntry.cs` | Các khoản thưởng/phạt có cấu trúc per nhân viên per tháng. Nhiều entries per record |
| `DailyTimesheet` | `Core/Entities/DailyTimesheet.cs` | Nguồn dữ liệu đầu vào: kết quả chấm công từng ngày của nhân viên |
| `Employee` | `Core/Entities/Employee.cs` | Thông tin nhân viên, lấy `BaseSalary` |
| `PublicHoliday` | `Core/Entities/PublicHoliday.cs` | Ngày nghỉ lễ — dùng để trừ khỏi `standardDays` |

### Payroll — Các field quan trọng

| Field | Kiểu | Mô tả |
|-------|------|-------|
| `StandardWorkingDays` | `int` | Ngày làm việc chuẩn trong tháng (trừ T7, CN và ngày lễ) |
| `ActualWorkingDays` | `int` | Số ngày nhân viên thực sự đi làm (tính theo status timesheet) |
| `TotalLateMinutes` | `int` | Tổng phút đi trễ (đã loại ngày `MissingOut`) |
| `TotalEarlyLeaveMinutes` | `int` | Tổng phút về sớm (đã loại ngày `MissingOut`) |
| `AbsentDays` | `int` | Số ngày vắng mặt (status `Absent`) |
| `TotalOTHours` | `decimal` | Tổng giờ tăng ca |
| `BaseSalarySnapshot` | `decimal` | Lương cơ bản tại thời điểm tính — snapshot để audit |
| `BasePay` | `decimal` | Lương theo công: `BaseSalary / standardDays × actualDays` |
| `OTPay` | `decimal` | Lương OT: `otHours × hourlyRate × 1.5` |
| `PenaltyFee` | `decimal` | Phạt đi trễ/về sớm theo phút (tự động từ timesheet) |
| `StructuredBonus` | `decimal` | **[Mới]** Tổng tiền thưởng từ `EmployeeBonusDeductionEntry` (tự động khi generate) |
| `StructuredDeduction` | `decimal` | **[Mới]** Tổng tiền phạt/khấu trừ từ `EmployeeBonusDeductionEntry` (tự động khi generate) |
| `CustomBonus` | `decimal?` | Thưởng thủ công ad-hoc (Admin nhập trực tiếp vào phiếu) |
| `CustomDeduction` | `decimal` | Khấu trừ thủ công ad-hoc (Admin nhập — thuế, BHXH nếu có) |
| `NetSalary` | `decimal` | Lương thực nhận — xem công thức đầy đủ ở Section 7 |
| `Status` | `PayrollStatus` | `Draft` → `Published` → `Paid` |
| `Notes` | `string?` | Ghi chú lý do thưởng/phạt thủ công |

### EmployeeBonusDeductionEntry — Các field

| Field | Kiểu | Mô tả |
|-------|------|-------|
| `TenantId` | `Guid` | Multi-tenant isolation |
| `EmployeeId` | `Guid` | Nhân viên được áp dụng |
| `Month` | `int` | Tháng áp dụng |
| `Year` | `int` | Năm áp dụng |
| `Type` | `BonusDeductionType` | `Bonus` (0) hoặc `Deduction` (1) |
| `Category` | `BonusDeductionCategory` | Phân loại: Performance, Holiday, Attendance, Disciplinary, AbsencePenalty, TaxDeduction, Insurance, Other |
| `Amount` | `decimal` | Số tiền (> 0) |
| `Reason` | `string?` | Lý do / mô tả |
| `CreatedByUserId` | `Guid?` | Người tạo entry — audit trail |
| `CreatedAt` | `DateTime` | Thời điểm tạo |

### PublicHoliday — Các field

| Field | Kiểu | Mô tả |
|-------|------|-------|
| `TenantId` | `Guid` | Ngày lễ riêng của từng tenant |
| `Date` | `DateOnly` | Ngày lễ |
| `Name` | `string` | Tên ngày lễ (VD: "Ngày Thống Nhất 30/4") |
| `IsRecurringYearly` | `bool` | `true` = tự lặp mỗi năm (chỉ so sánh Month + Day) |

---

## 3. Controllers & API Endpoints

### PayrollController
- **File**: `WebAPI/Controllers/PayrollController.cs`
- **Base route**: `api/payrolls`
- **Auth**: Bắt buộc JWT Bearer Token cho toàn bộ controller

#### Admin/HR Endpoints — Quản lý phiếu lương

| Method | Endpoint | Role | Mô tả |
|--------|----------|------|-------|
| `POST` | `/generate` | TenantAdmin | Tạo bản nháp lương cho **toàn bộ** nhân viên trong tháng |
| `POST` | `/calculate/{employeeId}` | TenantAdmin, HRManager | Tính lại lương cho **1 nhân viên** cụ thể |
| `GET` | `/paged` | TenantAdmin, HRManager, Manager | Xem danh sách phiếu lương (có phân trang, filter, sort) |
| `PUT` | `/{payrollId}/publish` | TenantAdmin, HRManager | Chốt 1 phiếu lương (Draft → Published) |
| `PUT` | `/publish-all` | TenantAdmin | Chốt **tất cả** phiếu Draft trong tháng |
| `PUT` | `/{payrollId}/mark-paid` | TenantAdmin | Đánh dấu đã thanh toán (Published → Paid) |
| `PUT` | `/{payrollId}/manual-fields` | TenantAdmin, HRManager, Manager | Chỉnh `CustomBonus` / `CustomDeduction` ad-hoc (chỉ khi Draft) |
| `PUT` | `/employee-bonus-penalty` | TenantAdmin, HRManager | Gán `CustomBonus`/`CustomDeduction` cho 1 NV theo tháng/năm (không cần biết payrollId) |
| `PUT` | `/bulk-bonus-penalty` | TenantAdmin, HRManager | Gán `CustomBonus`/`CustomDeduction` hàng loạt cho nhiều NV |

#### Admin/HR Endpoints — Quản lý entries thưởng/phạt có cấu trúc

| Method | Endpoint | Role | Mô tả |
|--------|----------|------|-------|
| `POST` | `/entries` | TenantAdmin, HRManager | Tạo 1 entry bonus/deduction cho nhân viên |
| `POST` | `/entries/bulk` | TenantAdmin, HRManager | Tạo hàng loạt cùng 1 loại cho nhiều nhân viên |
| `GET` | `/entries` | TenantAdmin, HRManager, Manager | Xem danh sách entries (filter theo employeeId, month, year, type) |
| `DELETE` | `/entries/{id}` | TenantAdmin, HRManager | Xóa entry (chỉ khi phiếu lương tháng đó còn Draft) |

#### Employee Endpoints

| Method | Endpoint | Role | Mô tả |
|--------|----------|------|-------|
| `GET` | `/my` | Mọi user đã đăng nhập | Xem phiếu lương của chính mình (chỉ trả `Published` và `Paid`) |

---

## 4. DTOs

### Request DTOs (Input)

| DTO | Dùng cho |
|-----|---------|
| `PayrollQueryDto` | Query params của `GET /paged` — filter theo department, employee, month, year, status, phân trang, sort |
| `UpdatePayrollDto` | Body của `PUT /{id}/manual-fields` — `CustomBonus`, `CustomDeduction`, `Reason` |
| `EmployeeBonusPenaltyDto` | Body của `PUT /employee-bonus-penalty` — `EmployeeId`, `Month`, `Year`, `CustomBonus`, `CustomDeduction`, `Reason` |
| `BulkBonusPenaltyDto` | Body của `PUT /bulk-bonus-penalty` — danh sách `EmployeeBonusPenaltyDto` |
| `CreateBonusDeductionEntryDto` | Body của `POST /entries` — `EmployeeId`, `Month`, `Year`, `Type`, `Category`, `Amount`, `Reason` |
| `CreateBulkBonusDeductionDto` | Body của `POST /entries/bulk` — `EmployeeIds[]`, `Month`, `Year`, `Type`, `Category`, `Amount`, `Reason` |
| `BonusDeductionEntryQueryDto` | Query params của `GET /entries` — filter theo employee, department, month, year, type, category |

### Response DTOs (Output)

| DTO | Dùng cho |
|-----|---------|
| `PayrollDto` | Response của mọi endpoint trả phiếu lương — gồm đầy đủ thông tin nhân viên + các số liệu lương (bao gồm `StructuredBonus`, `StructuredDeduction`, `BonusEntries`, `DeductionEntries`) |
| `PagedResultDto<PayrollDto>` | Response của `GET /paged` — có `Items`, `TotalCount`, `PageNumber`, `PageSize` |
| `BonusDeductionEntryDto` | Response của các entry endpoints — `Id`, `EmployeeName`, `Type`, `Category`, `Amount`, `Reason`, `CreatedAt`, `CreatedByName` |
| `PagedResultDto<BonusDeductionEntryDto>` | Response của `GET /entries` |

---

## 5. Services

### 5.1 PayrollService
- **Interface**: `Application/Interfaces/IServices/IPayrollService.cs`
- **Implementation**: `Application/Services/PayrollService.cs`

Xử lý toàn bộ nghiệp vụ Payroll: tính lương, chuyển trạng thái, query, chỉnh sửa thủ công, CRUD entries.

**Dependencies được inject:**

| Dependency | Mục đích |
|-----------|---------|
| `IPayrollRepository` | CRUD phiếu lương |
| `IEmployeeRepository` | Lấy `BaseSalary` và thông tin nhân viên |
| `IDailyTimesheetRepository` | Đọc kết quả chấm công (bulk load theo tenant/tháng) |
| `IPublicHolidayRepository` | Lấy ngày lễ để trừ khỏi `standardDays` |
| `IManualMonthlyTimesheetRepository` | Fallback khi không có DailyTimesheet |
| `IBonusDeductionEntryRepository` | **[Mới]** CRUD entries thưởng/phạt có cấu trúc |
| `IMapper` | Map `Payroll` → `PayrollDto`, `Entry` → `BonusDeductionEntryDto` |
| `ILogger<PayrollService>` | Logging |
| `IRealtimeNotificationService` | Thông báo realtime |
| `IHrAuthorizationService` | Kiểm tra quyền HR |
| `ICurrentUserService` | Lấy userId hiện tại |

---

## 6. Luồng Chi Tiết Từng Use Case

---

### LUỒNG A: Tạo bảng lương tháng (Generate Monthly Payroll)

```
[ADMIN] → POST /api/payrolls/generate?month=5&year=2026
```

**Bước 1 — Lấy danh sách nhân viên đang làm việc:**
```
employees = GetAllActiveEmployeeByTenantId(tenantId)
if employees rỗng → return false
```

**Bước 2 — Load dữ liệu dùng chung (1 lần cho toàn bộ nhân viên):**
```
// Load phiếu lương đã có của tháng này
existingPayrolls = GetByTenantMonthAsync(tenantId, month, year)

// Load ngày lễ — tính 1 lần, dùng cho tất cả nhân viên
holidays = GetAllAsync(tenantId)
holidayDatesInMonth = holidays
    .Select(h => IsRecurringYearly ? DateOnly(year, h.Month, h.Day) : h.Date)
    .Where(d.Month == month && d.Year == year)
    .ToHashSet()

// Bulk load timesheet — 1 DB query thay vì N queries
allTimesheets = GetByTenantMonthAsync(tenantId, month, year)
timesheetByEmployee = allTimesheets.GroupBy(EmployeeId).ToDictionary(...)

// [Mới] Bulk load bonus/deduction entries — 1 DB query
allEntries = GetByTenantMonthYearAsync(tenantId, month, year)
entriesByEmployee = allEntries.GroupBy(EmployeeId).ToDictionary(...)

// Tính standardDays 1 lần — dùng chung cho tất cả nhân viên
standardDays = Enumerable.Range(1, daysInMonth)
    .Select(day => DateOnly(year, month, day))
    .Count(date => != Saturday && != Sunday && !holidayDatesInMonth.Contains(date))
```

**Bước 3 — Lặp từng nhân viên:**
```
foreach emp in employees:
    timesheets = timesheetByEmployee[emp.Id] ?? empty list
    existingPayroll = existingPayrolls.FirstOrDefault(EmployeeId == emp.Id)

    // Idempotent: bỏ qua nếu đã Published hoặc Paid
    if existingPayroll != null && (Published || Paid):
        continue

    // Tính chỉ số từ timesheet
    actualDays = timesheets.Count(t =>
        t.ActualWorkHours > 0 ||
        Status in [Normal, Late, EarlyLeave, MissingOut, OnLeave])

    // Loại ngày MissingOut khỏi penalty (earlyLeaveMinutes ngày đó ≈ cả ca)
    lateMinutes       = timesheets.Where(Status != MissingOut).Sum(TotalLateMinutes)
    earlyLeaveMinutes = timesheets.Where(Status != MissingOut).Sum(TotalEarlyLeaveMinutes)
    absentDays        = timesheets.Count(Status == Absent)
    otHours           = timesheets.Sum(OTHours)

    // [Mới] Tính structured bonus/deduction từ entries
    entries           = entriesByEmployee[emp.Id] ?? empty list
    structuredBonus   = entries.Where(Type == Bonus).Sum(Amount)
    structuredDeduction = entries.Where(Type == Deduction).Sum(Amount)

    // Tính tiền
    if standardDays > 0:
        dailyRate   = BaseSalary / standardDays
        hourlyRate  = dailyRate / 8
        minuteRate  = hourlyRate / 60

        basePay    = dailyRate × actualDays
        otPay      = otHours × hourlyRate × 1.5
        penaltyFee = (lateMinutes + earlyLeaveMinutes) × minuteRate

    // [Mới] Công thức đầy đủ
    netSalary = basePay + otPay - penaltyFee
              + structuredBonus + (CustomBonus ?? 0)
              - structuredDeduction - CustomDeduction

    // Upsert
    if existingPayroll != null (Draft):
        → Cập nhật đè, giữ nguyên CustomBonus/CustomDeduction
        → Cập nhật lại StructuredBonus/StructuredDeduction từ entries mới nhất
    else:
        → Tạo mới với Status = Draft, CustomBonus = 0, CustomDeduction = 0
        → StructuredBonus/StructuredDeduction từ entries
```

**Bước 4 — Lưu DB:**
```
AddRangeAsync(newPayrolls)
UpdateRangeAsync(updatePayrolls)

return newPayrolls.Any() || updatePayrolls.Any()
// false → controller trả "Tất cả nhân viên đã có phiếu lương"
// true  → controller trả "Tạo phiếu lương Draft thành công"
```

**Realtime notification (GAP-05):**
```
if _currentUser.UserId.HasValue && result == true:
    _ = _realtime.NotifyPayrollGeneratedAsync(_currentUser.UserId.Value, {
        month:          month,
        year:           year,
        generatedCount: newPayrolls.Count + updatePayrolls.Count,
        skippedCount:   skippedCount,
        type:           "bulk"
    }).ContinueWith(log if faulted)
→ Gửi tới "user:{userId}" (admin đang thao tác)
```

---

### LUỒNG B: Tính lại lương 1 nhân viên (Calculate For Employee)

```
[ADMIN / HR] → POST /api/payrolls/calculate/{employeeId}?month=5&year=2026
```

Logic tương tự Luồng A nhưng chỉ cho 1 nhân viên:

```
emp = GetByIdAsync(employeeId)
if emp.TenantId != tenantId → throw "Không tìm thấy nhân viên"

existingPayroll = GetByEmployeeMonthAsync(employeeId, tenantId, month, year).FirstOrDefault()
if existingPayroll != null && Status != Draft:
    → throw "Phiếu lương đã chốt, không thể tính toán lại"

// Load holidays cho tháng này
// Tính standardDays (trừ T7, CN, ngày lễ)
// Tính actualDays, lateMinutes, earlyLeaveMinutes, absentDays, otHours

// [Mới] Load entries cho nhân viên này
entries = GetByEmployeeMonthYearAsync(tenantId, employeeId, month, year)
structuredBonus     = entries.Where(Type == Bonus).Sum(Amount)
structuredDeduction = entries.Where(Type == Deduction).Sum(Amount)

// Tính basePay, otPay, penaltyFee
// netSalary = basePay + otPay - penaltyFee
//           + structuredBonus + (CustomBonus ?? 0)
//           - structuredDeduction - CustomDeduction

if existingPayroll != null (Draft):
    → Update + trả về PayrollDto
else:
    → Create Draft + trả về PayrollDto
```

**Realtime notification (GAP-05) — chỉ khi `suppressGenerateNotify = false`:**
```
if _currentUser.UserId.HasValue && !suppressGenerateNotify:
    _ = _realtime.NotifyPayrollGeneratedAsync(_currentUser.UserId.Value, {
        month:          month,
        year:           year,
        generatedCount: 1,
        type:           "single"
    }).ContinueWith(log if faulted)

// suppressGenerateNotify = true khi gọi nội bộ từ:
//   CreateEntryAsync, DeleteEntryAsync, CreateBulkEntriesAsync
// → tránh emit payroll.generated khi mục đích là bonus_deduction.entry_added
```

---

### LUỒNG B2: Quản lý Entries Thưởng/Phạt (Bonus/Deduction Entries)

```
[ADMIN / HR] → POST /api/payrolls/entries
{
  "employeeId": "...",
  "month": 6, "year": 2026,
  "type": 0,           // 0 = Bonus, 1 = Deduction
  "category": 0,       // 0 = Performance
  "amount": 1000000,
  "reason": "Thưởng KPI tháng 6"
}
```

```
// Validate
if Amount <= 0 → throw
if Month < 1 || > 12 → throw
if payroll != null && payroll.Status != Draft → throw "Phiếu lương đã chốt"

// Lưu entry
CreateEntry(...)

// Nếu đã có phiếu Draft → tính lại ngay (suppress payroll.generated)
if draftPayroll exists:
    CalculatePayrollForEmployeeAsync(employeeId, month, year, suppressGenerateNotify: true)

// Realtime notification (GAP-07)
if emp.UserId != null:
    _ = _realtime.NotifyBonusDeductionEntryAddedAsync(emp.UserId.Value, {
        type:      entry.Type,      // "Bonus" | "Deduction"
        category:  entry.Category,
        amount:    entry.Amount,
        reason:    entry.Reason,
        month:     month,
        year:      year,
        createdAt: DateTime.UtcNow
    }).ContinueWith(log if faulted)

return BonusDeductionEntryDto
```

**Xóa entry:**
```
[ADMIN / HR] → DELETE /api/payrolls/entries/{id}

// Guard: không xóa nếu phiếu đã chốt
if payroll != null && payroll.Status != Draft → throw

DeleteEntry(id)
// Suppress payroll.generated — chỉ là recalculate nội bộ
if draftPayroll exists → CalculatePayrollForEmployeeAsync(..., suppressGenerateNotify: true)
// Không emit notification khi xóa entry
```

**Bulk create:**
```
[ADMIN / HR] → POST /api/payrolls/entries/bulk
{
  "employeeIds": ["id1", "id2", ...],
  "month": 6, "year": 2026,
  "type": 1,        // Deduction
  "category": 5,    // TaxDeduction
  "amount": 300000,
  "reason": "Thuế TNCN tháng 6"
}
// Tạo 1 entry per employee
// Nếu có Draft → CalculatePayrollForEmployeeAsync(suppressGenerateNotify: true) per employee
// Sau đó emit NotifyBonusDeductionEntryAddedAsync per employee (nếu có UserId)
```

---

### LUỒNG C: Chỉnh sửa thủ công (Update Manual Fields)

```
[ADMIN / HR / Manager] → PUT /api/payrolls/{payrollId}/manual-fields
{
  "customBonus": 500000,
  "customDeduction": 200000,
  "reason": "Thưởng hoàn thành KPI tháng 5"
}
```

**Điều kiện:** Phiếu phải đang ở trạng thái `Draft`.

```
payroll = GetByIdAsync(payrollId)
if payroll.Status != Draft → throw "Chỉ được cập nhật khi Draft"

payroll.CustomBonus     = dto.CustomBonus
payroll.CustomDeduction = dto.CustomDeduction ?? 0
if reason != null → payroll.Notes = reason

// [Cập nhật] Tính lại NetSalary — bao gồm cả StructuredBonus/StructuredDeduction
netSalary = BasePay + OTPay - PenaltyFee
          + StructuredBonus + (CustomBonus ?? 0)
          - StructuredDeduction - CustomDeduction

UpdateAsync(payroll)
return PayrollDto
```

> **Lưu ý:** `manual-fields` chỉ thay đổi `CustomBonus`/`CustomDeduction`. `StructuredBonus`/`StructuredDeduction` không bị ảnh hưởng — chúng chỉ thay đổi khi `generate`/`calculate` được gọi lại.

---

### LUỒNG D: Chốt phiếu lương (Publish)

#### D.1 — Chốt từng phiếu

```
[ADMIN / HR] → PUT /api/payrolls/{payrollId}/publish
```

```
payroll = GetByIdAsync(payrollId)
if payroll.Status != Draft → throw "Chỉ phiếu Draft mới được chốt"

payroll.Status = Published
UpdateAsync(payroll)

// Realtime notifications (GAP-05 / dashboard)
employee = GetByIdAsync(payroll.EmployeeId)
if employee.UserId != null:
    _ = _realtime.NotifyPayrollPublishedAsync(employee.UserId.Value, {
        payrollId: payroll.Id,
        month:     payroll.Month,
        year:      payroll.Year,
        netSalary: payroll.NetSalary
    }).ContinueWith(log if faulted)

_ = _realtime.NotifyDashboardRefreshAsync(payroll.TenantId)
    .ContinueWith(log if faulted)

return { published: true }
```

#### D.2 — Chốt toàn bộ Draft trong tháng (batch)

```
[ADMIN] → PUT /api/payrolls/publish-all?month=5&year=2026
```

```
drafts = GetDraftsByTenantMonthAsync(tenantId, month, year)
if drafts rỗng → return { publishedCount: 0 }

foreach payroll in drafts:
    payroll.Status = Published

UpdateRangeAsync(drafts)

// Realtime notifications — bulk load để tránh N+1
employeeIds = drafts.Select(d.EmployeeId).Distinct()
employees = GetByIdsAsync(employeeIds)   ← 1 query
employeeMap = employees.ToDictionary(e.Id)

foreach draft in drafts:
    if employeeMap[draft.EmployeeId].UserId != null:
        _ = _realtime.NotifyPayrollPublishedAsync(userId, {
            payrollId: draft.Id,
            month:     draft.Month,
            year:      draft.Year,
            netSalary: draft.NetSalary
        }).ContinueWith(log if faulted)

_ = _realtime.NotifyDashboardRefreshAsync(tenantId).ContinueWith(log if faulted)

return { message: "Đã chốt N phiếu lương", publishedCount: N }
```

> **Lưu ý:** `publish-all` không có logic guard từng phiếu — `GetDraftsByTenantMonthAsync` đã chỉ lấy Draft. Dùng `GetByIdsAsync` (bulk load 1 query) tránh N+1 khi load employees.

---

### LUỒNG E: Đánh dấu đã thanh toán (Mark Paid)

```
[ADMIN] → PUT /api/payrolls/{payrollId}/mark-paid
```

```
payroll = GetByIdAsync(payrollId)
if payroll == null → return false
if payroll.Status != Published → throw "Chỉ phiếu Published mới được đánh dấu đã thanh toán"

payroll.Status = Paid
UpdateAsync(payroll)

// Realtime notifications (GAP-02)
employee = GetByIdAsync(payroll.EmployeeId)
if employee.UserId != null:
    _ = _realtime.NotifyPayrollPaidAsync(employee.UserId.Value, {
        payrollId: payroll.Id,
        month:     payroll.Month,
        year:      payroll.Year,
        netSalary: payroll.NetSalary,
        paidAt:    DateTime.UtcNow
    }).ContinueWith(log if faulted)

_ = _realtime.NotifyDashboardRefreshAsync(payroll.TenantId)
    .ContinueWith(log if faulted)

return { paid: true }
```

---

### LUỒNG F: Nhân viên xem phiếu lương

```
[EMPLOYEE] → GET /api/payrolls/my?month=5&year=2026   (month/year optional)
```

```
employee = GetByUserIdAsync(userId)
if employee == null || employee.TenantId != tenantId → return []

(items, _) = GetByEmployeeIdPagedAsync(employee.Id, month, year, page=1, size=100)

// Chỉ trả về phiếu đã Public hoặc đã thanh toán — nhân viên không thấy Draft
visibleItems = items.Where(Status == Published || Status == Paid)

return Map<List<PayrollDto>>(visibleItems)
```

> `PayrollDto` trả về bao gồm `BonusEntries` và `DeductionEntries` — nhân viên thấy được breakdown chi tiết thưởng/phạt.

---

### LUỒNG G: Admin xem danh sách bảng lương (Paged)

```
[ADMIN / HR / Manager] → GET /api/payrolls/paged
    ?departmentId=...&employeeId=...&month=5&year=2026
    &status=Draft&pageNumber=1&pageSize=10&sortBy=NetSalary&sortDir=desc
```

```
(items, totalCount) = GetPagedAsync(
    tenantId, departmentId, employeeId, month, year,
    status, pageNumber, pageSize, sortBy, sortDir)

return PagedResultDto {
    Items: Map<List<PayrollDto>>(items),
    TotalCount, PageNumber, PageSize
}
```

> Filter `status` nhận chuỗi (`"Draft"`, `"Published"`, `"Paid"`) — xử lý ở tầng Repository.

---

## 7. Công thức tính lương

```
// Ngày công chuẩn (trừ T7, CN, ngày lễ)
standardDays = Count(ngày trong tháng where != Saturday && != Sunday && != Holiday)

// Ngày công thực tế (tính từ timesheet)
actualDays = Count(timesheet where:
    ActualWorkHours > 0
    OR Status in [Normal, Late, EarlyLeave, MissingOut, OnLeave])

// Penalty (loại ngày MissingOut để tránh phạt quá nặng)
lateMinutes       = Sum(TotalLateMinutes)      where Status != MissingOut
earlyLeaveMinutes = Sum(TotalEarlyLeaveMinutes) where Status != MissingOut

// Tính tiền cơ bản
dailyRate   = BaseSalary / standardDays
hourlyRate  = dailyRate / 8
minuteRate  = hourlyRate / 60

BasePay    = dailyRate × actualDays
OTPay      = TotalOTHours × hourlyRate × 1.5
PenaltyFee = (lateMinutes + earlyLeaveMinutes) × minuteRate

// [Mới] Structured entries (tự động từ EmployeeBonusDeductionEntry)
StructuredBonus     = SUM(entries where Type == Bonus)
StructuredDeduction = SUM(entries where Type == Deduction)

// NetSalary đầy đủ
NetSalary = BasePay + OTPay - PenaltyFee
          + StructuredBonus + (CustomBonus ?? 0)
          - StructuredDeduction - CustomDeduction
```

### Phân biệt Structured vs Custom

| | `StructuredBonus` / `StructuredDeduction` | `CustomBonus` / `CustomDeduction` |
|--|---|---|
| **Nguồn** | Tự động từ `EmployeeBonusDeductionEntry` | Admin nhập trực tiếp vào phiếu |
| **Audit** | Có — lưu từng dòng, category, người tạo | Không — chỉ lưu tổng + Notes |
| **Cập nhật** | Khi `generate`/`calculate` được gọi lại | Khi gọi `manual-fields` hoặc `employee-bonus-penalty` |
| **Khi generate đè Draft** | Tính lại từ entries mới nhất | Giữ nguyên |

### Ví dụ minh họa

```
Nhân viên: BaseSalary = 10.000.000 VNĐ
Tháng 6/2026: standardDays = 21
actualDays = 20, lateMinutes = 30, otHours = 4

Entries tháng 6:
  - Bonus / Performance / 1.000.000 / "Thưởng KPI"
  - Deduction / Insurance / 800.000  / "BHXH"

dailyRate   = 10.000.000 / 21 ≈ 476.190 VNĐ/ngày
hourlyRate  = 476.190 / 8    ≈  59.524 VNĐ/giờ
minuteRate  = 59.524 / 60   ≈     992 VNĐ/phút

BasePay    = 476.190 × 20  ≈ 9.523.800 VNĐ
OTPay      = 4 × 59.524 × 1.5 ≈ 357.144 VNĐ
PenaltyFee = 30 × 992 ≈ 29.762 VNĐ

StructuredBonus     = 1.000.000 VNĐ
StructuredDeduction =   800.000 VNĐ
CustomBonus         =         0 VNĐ
CustomDeduction     =         0 VNĐ

NetSalary = 9.523.800 + 357.144 - 29.762
          + 1.000.000 + 0 - 800.000 - 0
          ≈ 10.051.182 VNĐ
```

---

## 8. Trạng thái phiếu lương (PayrollStatus)

### Vòng đời trạng thái

```
Draft (0) ──publish──→ Published (1) ──mark-paid──→ Paid (2)
  ↑
  └── generate / calculate (chỉ được tạo/sửa khi Draft)
  └── entries add/delete (chỉ được thêm/xóa khi Draft)
```

### Chi tiết từng trạng thái

| Status | Giá trị | Ý nghĩa | Ai thấy |
|--------|---------|---------|---------|
| `Draft` | 0 | Bản nháp, có thể tính lại hoặc chỉnh sửa | Admin, HRManager, Manager |
| `Published` | 1 | Đã chốt, nhân viên có thể xem | Admin, HR, Manager, Nhân viên |
| `Paid` | 2 | Đã thanh toán, hoàn tất | Admin, HR, Manager, Nhân viên |

### Guard Rules — Hành động nào được phép theo trạng thái

| Hành động | Draft | Published | Paid |
|-----------|-------|-----------|------|
| `generate` (tái tính) | ✅ Cho phép | ❌ Bỏ qua (idempotent) | ❌ Bỏ qua (idempotent) |
| `calculate` (tái tính 1 NV) | ✅ Cho phép | ❌ Throw exception | ❌ Throw exception |
| `manual-fields` (chỉnh tay) | ✅ Cho phép | ❌ Throw exception | ❌ Throw exception |
| `entries` add/delete | ✅ Cho phép | ❌ Throw exception | ❌ Throw exception |
| `publish` | ✅ Cho phép | ❌ Throw exception | ❌ Throw exception |
| `mark-paid` | ❌ Throw exception | ✅ Cho phép | ❌ Throw exception |
| `GET /my` (nhân viên xem) | ❌ Ẩn | ✅ Hiện | ✅ Hiện |

---

## 9. Ngày lễ (Public Holiday) — Ảnh hưởng lên Payroll

Ngày lễ được quản lý trong luồng Attendance (`POST /api/v1/attendance/holidays`) và **ảnh hưởng tự động** lên Payroll khi generate:

```
// Mỗi lần generate, load lại ngày lễ mới nhất
holidayDatesInMonth = holidays
    .Select(h => IsRecurringYearly
        ? DateOnly(year, h.Date.Month, h.Date.Day)   ← Lặp năm: dùng năm hiện tại
        : h.Date)                                     ← Không lặp: dùng date gốc
    .Where(d.Month == month && d.Year == year)
    .ToHashSet()

standardDays = ... .Count(date => ... && !holidayDatesInMonth.Contains(date))
```

**Tác động:** Ngày lễ làm `standardDays` giảm → `dailyRate` tăng → nhân viên được tính lương cao hơn trong tháng có ngày lễ. Đây là hành vi đúng.

**Lưu ý:** Sau khi thêm/xóa ngày lễ, phải gọi lại `generate` để phiếu Draft được tính lại với `standardDays` mới. Phiếu `Published/Paid` không bị ảnh hưởng.

---

## 10. Thứ tự API theo từng kịch bản

### Kịch bản 1: Chu kỳ lương chuẩn hàng tháng (có thưởng/phạt có cấu trúc)

```
[ADMIN — trong tháng, trước khi đóng lương]

  [0] POST /api/payrolls/entries   (lặp cho từng khoản)
      { "employeeId": "...", "month": 6, "year": 2026,
        "type": 0, "category": 0, "amount": 1000000, "reason": "Thưởng KPI" }
      ← Định nghĩa trước các khoản thưởng/phạt

  [0b] POST /api/payrolls/entries/bulk
      { "employeeIds": [...], "type": 1, "category": 5,
        "amount": 800000, "reason": "BHXH tháng 6" }
      ← Gán hàng loạt khoản khấu trừ

[ADMIN — đầu tháng sau]

  [1] POST /api/payrolls/generate?month=6&year=2026
      ← Tạo Draft — tự động tổng hợp entries vào StructuredBonus/StructuredDeduction

  [2] GET /api/payrolls/paged?month=6&year=2026&status=Draft
      ← Admin xem danh sách, review số liệu

  [3] PUT /api/payrolls/{payrollId}/manual-fields   (lặp cho NV cần điều chỉnh đặc biệt)
      { "customBonus": 200000, "reason": "Thưởng thêm đặc biệt" }
      ← Chỉnh thủ công ad-hoc nếu cần

  [4] PUT /api/payrolls/publish-all?month=6&year=2026
      ← Chốt toàn bộ Draft → Published

[NHÂN VIÊN]
  [5] GET /api/payrolls/my?month=6&year=2026
      ← Thấy phiếu lương Status=Published với breakdown BonusEntries/DeductionEntries

[ADMIN — sau khi chuyển khoản]
  [6] PUT /api/payrolls/{payrollId}/mark-paid   (lặp cho từng phiếu)
      ← Published → Paid
```

### Kịch bản 2: Tính lại 1 nhân viên cụ thể

```
[HR phát hiện nhân viên A bị tính sai do Attendance có lỗi]

  [1] (HR sửa dữ liệu Attendance bằng recalculate)
      POST /api/v1/attendance/recalculate/{employeeId}?from=2026-06-01&to=2026-06-30

  [2] POST /api/payrolls/calculate/{employeeId}?month=6&year=2026
      ← Tính lại, cập nhật bản Draft (giữ CustomBonus/Deduction, tính lại StructuredBonus/Deduction từ entries)

  [3] GET /api/payrolls/paged?employeeId={id}&month=6&year=2026
      ← Verify kết quả

  [4] PUT /api/payrolls/{payrollId}/publish   (nếu muốn chốt riêng lẻ)
```

### Kịch bản 3: Tháng có ngày lễ

```
[ADMIN — khi bắt đầu hệ thống]
  [1] POST /api/v1/attendance/holidays
      { "date": "2026-05-01", "name": "Quốc tế Lao Động", "isRecurringYearly": true }
      { "date": "2026-04-30", "name": "Ngày Thống Nhất", "isRecurringYearly": true }

[ADMIN — cuối tháng 5]
  [2] POST /api/payrolls/generate?month=5&year=2026
      ← standardDays = 19 (tháng 5 có 1 ngày lễ: 1/5, T7/CN bị trừ)
      ← dailyRate cao hơn tháng thường → nhân viên không bị thiệt

  [3] Tiếp tục chu kỳ bình thường (Kịch bản 1 bước 2–6)
```

### Kịch bản 4: Phát hiện lỗi sau khi đã Publish

```
[Không thể sửa phiếu đã Published trực tiếp]

Cách xử lý:
  → Nếu cần tính lại hoàn toàn: không có cơ chế "unpublish" trong MVP
  → Workaround: điều chỉnh qua CustomBonus/CustomDeduction trong kỳ sau
  → Hoặc contact Dev để reset status thủ công trong DB (ngoài scope API)
```

---

## 11. Security & Authorization

| Loại | Rule |
|------|------|
| Authentication | JWT Bearer Token bắt buộc cho mọi endpoint |
| `generate` | Chỉ `TenantAdmin` |
| `calculate` | `TenantAdmin`, `HRManager` |
| `publish` / `publish-all` | `TenantAdmin`, `HRManager` |
| `mark-paid` | Chỉ `TenantAdmin` |
| `manual-fields` | `TenantAdmin`, `HRManager`, `Manager` |
| `employee-bonus-penalty` / `bulk-bonus-penalty` | `TenantAdmin`, `HRManager` |
| `entries` POST/DELETE | `TenantAdmin`, `HRManager` |
| `entries` GET | `TenantAdmin`, `HRManager`, `Manager` |
| `paged` | `TenantAdmin`, `HRManager`, `Manager` |
| `my` | Mọi user đã đăng nhập |
| Multi-tenant isolation | `TenantId` được extract từ `ICurrentTenantService` — nhân viên chỉ thấy phiếu lương của tenant mình |
| Employee isolation | `GET /my` tự động lọc theo `userId` → `employeeId` — không thể xem lương người khác |

---

## 12. Performance Notes

| Điểm | Cách xử lý |
|------|-----------|
| N+1 query khi generate | `GetByTenantMonthAsync` bulk load **1 query** cho toàn bộ timesheet, sau đó tra cứu qua Dictionary |
| Entries load | `GetByTenantMonthYearAsync` bulk load **1 query** cho toàn bộ entries, groupBy EmployeeId — không N+1 |
| Holiday load | Load **1 lần** trước vòng lặp, dùng `HashSet<DateOnly>` để lookup O(1) |
| standardDays | Tính **1 lần** trước vòng lặp, không lặp lại cho từng nhân viên |

---

## 13. Files tham khảo

### Controllers
- [PayrollController.cs](../SMEFLOWSystem.WebAPI/Controllers/PayrollController.cs)

### Services
- [PayrollService.cs](../SMEFLOWSystem.Application/Services/PayrollService.cs)
- [IPayrollService.cs](../SMEFLOWSystem.Application/Interfaces/IServices/IPayrollService.cs)

### Repositories
- [IPayrollRepository.cs](../SMEFLOWSystem.Application/Interfaces/IRepositories/IPayrollRepository.cs)
- [IBonusDeductionEntryRepository.cs](../SMEFLOWSystem.Application/Interfaces/IRepositories/IBonusDeductionEntryRepository.cs)
- [IPublicHolidayRepository.cs](../SMEFLOWSystem.Application/Interfaces/IRepositories/IPublicHolidayRepository.cs)
- [IDailyTimesheetRepository.cs](../SMEFLOWSystem.Application/Interfaces/IRepositories/IDailyTimesheetRepository.cs)

### Entities
- [Payroll.cs](../SMEFLOWSystem.Core/Entities/Payroll.cs)
- [EmployeeBonusDeductionEntry.cs](../SMEFLOWSystem.Core/Entities/EmployeeBonusDeductionEntry.cs)
- [PublicHoliday.cs](../SMEFLOWSystem.Core/Entities/PublicHoliday.cs)
- [DailyTimesheet.cs](../SMEFLOWSystem.Core/Entities/DailyTimesheet.cs)

### Enums
- [PayrollStatus.cs](../SMEFLOWSystem.SharedKernel/Common/Enum/PayrollStatus.cs)
- [BonusDeductionType.cs](../SMEFLOWSystem.SharedKernel/Common/Enum/BonusDeductionType.cs)
- [BonusDeductionCategory.cs](../SMEFLOWSystem.SharedKernel/Common/Enum/BonusDeductionCategory.cs)
- [StatusEnum.cs](../SMEFLOWSystem.SharedKernel/Common/Enum/StatusEnum.cs)

### DTOs
- [PayrollDto.cs](../SMEFLOWSystem.Application/DTOs/PayrollDtos/PayrollDto.cs)
- [BonusDeductionEntryDto.cs](../SMEFLOWSystem.Application/DTOs/PayrollDtos/BonusDeductionEntryDto.cs)
- [CreateBonusDeductionEntryDto.cs](../SMEFLOWSystem.Application/DTOs/PayrollDtos/CreateBonusDeductionEntryDto.cs)
- [CreateBulkBonusDeductionDto.cs](../SMEFLOWSystem.Application/DTOs/PayrollDtos/CreateBulkBonusDeductionDto.cs)
- [BonusDeductionEntryQueryDto.cs](../SMEFLOWSystem.Application/DTOs/PayrollDtos/BonusDeductionEntryQueryDto.cs)
- [UpdatePayrollDto.cs](../SMEFLOWSystem.Application/DTOs/PayrollDtos/UpdatePayrollDto.cs)
- [PayrollQueryDto.cs](../SMEFLOWSystem.Application/DTOs/PayrollDtos/PayrollQueryDto.cs)

---

## 14. Điểm quan trọng cần nhớ

1. **Payroll phụ thuộc hoàn toàn vào DailyTimesheet** — nếu Attendance sai, Payroll sai. Khi phát hiện chấm công sai, phải recalculate Attendance trước, sau đó mới generate/calculate lại Payroll

2. **Idempotent generate** — gọi `generate` bao nhiêu lần cũng không gây hại: phiếu `Published/Paid` được bỏ qua, phiếu `Draft` được cập nhật với số liệu mới nhất (bao gồm cả `StructuredBonus`/`StructuredDeduction` từ entries)

3. **BaseSalarySnapshot** — lưu lương cơ bản tại thời điểm tính. Nếu nhân viên được tăng lương sau khi phiếu đã tạo, phiếu cũ không đổi — generate lại sẽ dùng lương mới

4. **MissingOut không bị phạt penalty** — ngày quên check-out được tính vào `actualDays` (nhân viên vẫn được tính lương ngày đó) nhưng không bị trừ phút về sớm. HR cần xử lý thủ công qua `CustomDeduction` hoặc thêm 1 entry `AbsencePenalty` nếu muốn phạt

5. **OnLeave tính vào actualDays** — ngày nghỉ phép có lương được tính là ngày đi làm. Logic: `standardDays` không thay đổi, `actualDays` bao gồm `OnLeave` → nhân viên không bị trừ lương ngày nghỉ phép

6. **Ngày lễ trừ khỏi standardDays, không cộng vào actualDays** — ngày lễ làm `standardDays` nhỏ hơn (dailyRate cao hơn). Nhân viên không đi làm ngày lễ → `actualDays` không thêm ngày lễ → công thức tự cân bằng

7. **NetSalary có thể âm về lý thuyết** — nếu tổng khấu trừ vượt quá tổng thu nhập. Hệ thống không có floor = 0, Admin cần kiểm tra thủ công

8. **Không có thuế TNCN / BHXH / BHYT tự động** — dùng `EmployeeBonusDeductionEntry` với `Category = TaxDeduction` hoặc `Insurance` để gán thủ công. MVP chưa tính tự động

9. **Entries bị khóa sau khi publish** — sau khi `publish`, không thể thêm/xóa entry cho tháng đó. Mọi điều chỉnh phải thực hiện trong kỳ lương tiếp theo

10. **StructuredBonus/StructuredDeduction refresh khi generate/calculate** — nếu HR thêm entry sau khi đã generate, phải gọi lại `calculate/{employeeId}` để `StructuredBonus` được cập nhật vào phiếu. Gọi `manual-fields` không tự refresh phần structured này
