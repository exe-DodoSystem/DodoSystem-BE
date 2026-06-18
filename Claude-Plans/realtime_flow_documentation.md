# Realtime Flow Documentation — DodoSystem Backend

> Tài liệu phân tích chi tiết luồng Real-Time (SignalR) trong hệ thống DodoSystem.
> Cập nhật: 2026-06-18 (phản ánh toàn bộ implementation đã hoàn thành Phase 1–3 + GAP-01 đến GAP-08)

---

## 1. Tổng quan kiến trúc

### Vấn đề giải quyết

Hệ thống ban đầu hoạt động hoàn toàn theo mô hình **request-response thuần túy** — client phải chủ động gọi API để biết có thay đổi. Điều này tạo ra 4 điểm nghẽn UX:

| Vấn đề | Hậu quả |
|--------|---------|
| Check-in → Background Job → DailyTimesheet không có phản hồi | Mobile app phải poll `GET /my-today` liên tục |
| HR approve appeal → nhân viên không biết kết quả | Nhân viên phải tự poll `GET /appeals` |
| Dashboard Admin là snapshot tĩnh | Admin phải tự reload trang để thấy số mới |
| Admin chốt phiếu lương → nhân viên không nhận thông báo | Nhân viên phải tự kiểm tra `GET /payrolls/my` |

### Giải pháp — ASP.NET Core SignalR

Dùng **WebSocket qua SignalR** (built-in ASP.NET Core, không thêm NuGet bên ngoài). SignalR tự động fallback: `WebSocket → Server-Sent Events → Long Polling`.

### Kiến trúc tổng quát

```
[Mobile App / Web Frontend]
        ↕ WebSocket (SignalR) — ws://host/hubs/notifications?access_token=JWT
[NotificationHub — WebAPI]
        ↑ IHubContext<NotificationHub> (inject vào service)
[IRealtimeNotificationService] (Application layer interface)
        ↑ inject
[AttendanceResolutionService]  ← emit attendance.updated, dashboard.refresh
[AttendanceService]            ← emit punch.received, appeal.submitted, appeal.processed,
                                          attendance.manual_adjusted, dashboard.refresh
[PayrollService]               ← emit payroll.published, payroll.paid, payroll.generated,
                                          bonus_deduction.entry_added, dashboard.refresh
[ShiftManagementService]       ← emit shift.assigned
[InviteService]                ← emit employee.onboarded, dashboard.refresh
```

### Nguyên tắc bất biến

- Notify luôn gọi **sau khi transaction commit** — không bao giờ gọi bên trong `ExecuteAsync`
- Notify là **best-effort** — dùng fire-and-forget `_ = Task.ContinueWith(log)`, không `await`
- Notify **không được throw** ra ngoài — wrap trong `try-catch`, log Warning, không ảnh hưởng flow chính
- Check `UserId != null` trước khi notify — nhân viên chưa có tài khoản thì skip
- Không query DB hay tính toán nặng trong notify — chỉ map DTO đơn giản từ dữ liệu đã có

---

## 2. Các File

### File mới tạo

| File | Layer | Mô tả |
|------|-------|-------|
| `IRealtimeNotificationService.cs` | Application/Interfaces/IServices | Interface cho notification service |
| `NotificationHub.cs` | WebAPI/Hubs | SignalR Hub, quản lý kết nối và group |
| `UserIdProvider.cs` | WebAPI/Hubs | Map JWT claim → userId của connection |
| `SignalRNotificationService.cs` | WebAPI/Services | Implementation của interface dùng `IHubContext` |

### File đã sửa

| File | Thay đổi |
|------|---------|
| `DependencyInjection.cs` | Thêm `AddSignalR()`, `AddSingleton<IUserIdProvider>`, `AddScoped<IRealtimeNotificationService>`, JWT `OnMessageReceived` event |
| `WebApplicationExtensions.cs` | Thêm `MapHub<NotificationHub>("/hubs/notifications")` |
| `AttendanceResolutionService.cs` | Inject `IRealtimeNotificationService`, emit `attendance.updated` + `dashboard.refresh` |
| `AttendanceService.cs` | Inject `IRealtimeNotificationService`, emit `punch.received`, `appeal.submitted`, `appeal.processed`, `attendance.manual_adjusted`, `dashboard.refresh` |
| `PayrollService.cs` | Inject `IRealtimeNotificationService`, emit `payroll.published`, `payroll.paid`, `payroll.generated`, `bonus_deduction.entry_added`, `dashboard.refresh` |
| `ShiftManagementService.cs` | Inject `IRealtimeNotificationService`, emit `shift.assigned` |
| `InviteService.cs` | Inject `IRealtimeNotificationService`, emit `employee.onboarded` + `dashboard.refresh` |

---

## 3. Interface & Implementation

### 3.1 IRealtimeNotificationService

**File**: [IRealtimeNotificationService.cs](../SMEFLOWSystem.Application/Interfaces/IServices/IRealtimeNotificationService.cs)

```csharp
public interface IRealtimeNotificationService
{
    // Phase 1–3 (đã có)
    Task NotifyAttendanceUpdatedAsync(Guid userId, Guid tenantId, object data);
    Task NotifyAppealProcessedAsync(Guid userId, object data);
    Task NotifyPayrollPublishedAsync(Guid userId, object data);
    Task NotifyPunchReceivedAsync(Guid userId, object data);

    // GAP-06 — expose riêng để dùng ở nhiều service
    Task NotifyDashboardRefreshAsync(Guid tenantId);

    // GAP-01: HR nhận khi nhân viên gửi appeal
    Task NotifyAppealSubmittedAsync(Guid tenantId, object data);

    // GAP-02: Nhân viên nhận khi lương đã được chuyển
    Task NotifyPayrollPaidAsync(Guid userId, object data);

    // GAP-03: Nhân viên nhận khi được gán lịch ca mới
    Task NotifyShiftAssignedAsync(Guid userId, object data);

    // GAP-04: Nhân viên nhận khi có khoản thưởng/phạt mới
    Task NotifyBonusDeductionEntryAddedAsync(Guid userId, object data);

    // GAP-07: Nhân viên nhận khi HR điều chỉnh chấm công thủ công
    Task NotifyAttendanceManualAdjustedAsync(Guid userId, object data);

    // GAP-05: Admin nhận khi generate/calculate xong
    Task NotifyPayrollGeneratedAsync(Guid userId, object data);

    // GAP-08: HR nhận khi nhân viên mới hoàn tất onboarding
    Task NotifyEmployeeOnboardedAsync(Guid tenantId, object data);
}
```

> **Lý do dùng `object data`:** Application layer không được phụ thuộc vào SignalR. `IHubContext` chỉ tồn tại ở WebAPI layer. Application layer chỉ gọi interface, không biết gì về transport.

### 3.2 NotificationHub

**File**: [NotificationHub.cs](../SMEFLOWSystem.WebAPI/Hubs/NotificationHub.cs)

Hub yêu cầu JWT (`[Authorize]`). Khi client connect, `OnConnectedAsync` tự động join client vào các group phù hợp dựa trên claims trong JWT:

```
Claims JWT cần có:
  ClaimTypes.NameIdentifier  → userId  (string của Guid)
  "tenantId"                 → tenantId (string của Guid)
  ClaimTypes.Role            → role name(s)
```

### 3.3 UserIdProvider

**File**: [UserIdProvider.cs](../SMEFLOWSystem.WebAPI/Hubs/UserIdProvider.cs)

Map `ClaimTypes.NameIdentifier` từ JWT → `Context.UserIdentifier` của SignalR.

### 3.4 SignalRNotificationService

**File**: [SignalRNotificationService.cs](../SMEFLOWSystem.WebAPI/Services/SignalRNotificationService.cs)

Implementation dùng `IHubContext<NotificationHub>` để gửi event đến group. Mỗi method đều có `try-catch` riêng, không throw ra ngoài.

---

## 4. Groups (Nhóm kết nối)

Khi client connect, `OnConnectedAsync` thêm connection vào các group:

| Group Key | Thành viên | Điều kiện join |
|-----------|-----------|---------------|
| `user:{userId}` | Đúng 1 user (tất cả tab/device của user đó) | Luôn luôn (nếu có userId trong JWT) |
| `tenant:{tenantId}:dashboard` | Tất cả user đang connect trong tenant | Luôn luôn (nếu có tenantId trong JWT) |
| `tenant:{tenantId}:admins` | TenantAdmin + HRManager của tenant | Role là `"TenantAdmin"` hoặc `"HRManager"` |
| `tenant:{tenantId}:managers` | Manager của tenant | Role là `"Manager"` |

> **Lưu ý:** Một connection chỉ join `admins` **hoặc** `managers` (break sau khi join lần đầu). Nhưng mọi connection đều join `dashboard` và `user:{userId}`.

---

## 5. Events

### Bảng tổng hợp

| Event Name | Trigger | Gửi tới | Data Shape |
|------------|---------|---------|-----------|
| `punch.received` | Sau `POST /submit-punch` thành công | `user:{userId}` | `{ received, punchType, timestamp, message }` |
| `attendance.updated` | Sau Background Job xử lý xong 1 DailyTimesheet | `user:{userId}` | `{ workDate, status, totalLateMinutes, hasCheckedIn, hasCheckedOut }` |
| `dashboard.refresh` | Nhiều trigger — xem chi tiết bên dưới | `tenant:{tenantId}:dashboard` | `{ tenantId }` |
| `appeal.processed` | Sau HR approve hoặc reject appeal | `user:{userId}` (nhân viên submit appeal) | `{ appealId, workDate, status, rejectReason, processedAt }` |
| `payroll.published` | Sau admin publish 1 hoặc nhiều phiếu lương | `user:{userId}` (nhân viên có phiếu) | `{ payrollId, month, year, netSalary }` |
| `appeal.submitted` | Sau nhân viên gửi đơn giải trình | `tenant:{tenantId}:admins` | `{ appealId, employeeId, employeeName, workDate, appealType, reason, submittedAt }` |
| `payroll.paid` | Sau admin đánh dấu đã thanh toán 1 phiếu | `user:{userId}` (nhân viên có phiếu) | `{ payrollId, month, year, netSalary, paidAt }` |
| `shift.assigned` | Sau HR/Manager gán lịch ca mới | `user:{userId}` (từng nhân viên được gán) | `{ shiftPatternId, shiftPatternName, effectiveStartDate, assignedBy }` |
| `bonus_deduction.entry_added` | Sau HR tạo entry thưởng/phạt | `user:{userId}` (nhân viên có entry) | `{ type, category, amount, reason, month, year, createdAt }` |
| `attendance.manual_adjusted` | Sau HR nhập tay punch cho nhân viên | `user:{userId}` (nhân viên bị điều chỉnh) | `{ workDate, punchType, timestamp, adjustedBy, note }` |
| `payroll.generated` | Sau generate/calculate xong | `user:{userId}` (admin/HR gọi API) | `{ month, year, generatedCount, skippedCount, type }` |
| `employee.onboarded` | Sau nhân viên hoàn tất onboarding | `tenant:{tenantId}:admins` | `{ employeeId, employeeName, email, departmentName, positionName, onboardedAt }` |

### Các trigger của `dashboard.refresh`

| Action | Service | Method |
|--------|---------|--------|
| Background Job xử lý DailyTimesheet | `AttendanceResolutionService` | `NotifyAttendanceUpdatedAsync` (bên trong đã gọi refresh) |
| HR approve/reject appeal | `AttendanceService` | `ProcessAppealAsync` |
| Admin publish 1 phiếu | `PayrollService` | `PublishPayrollAsync` |
| Admin publish toàn bộ Draft | `PayrollService` | `PublishAllDraftAsync` |
| Admin mark-paid 1 phiếu | `PayrollService` | `MarkPaidAsync` |
| Nhân viên hoàn tất onboarding | `InviteService` | `CompleteOnboardingInternalAsync` |

> **`dashboard.refresh` chỉ gửi tín hiệu, không gửi data.** Frontend nhận xong tự gọi lại `GET /dashboard/admin`. Tránh tính toán heavy trong SignalR.

---

## 6. Luồng Chi Tiết Từng Event

---

### EVENT A: `punch.received`

**Trigger:** Ngay sau khi `RawPunchLog` được lưu DB thành công trong `SubmitPunchAsync`.

**File:** [AttendanceService.cs](../SMEFLOWSystem.Application/Services/AttendanceService.cs)

```
[MOBILE APP] → POST /api/v1/attendance/submit-punch
    ↓
Server validate GPS, tạo RawPunchLog { IsProcessed=false }
    ↓
AddAsync(punch) + SaveChanges
    ↓
[Ngoài transaction, fire-and-forget]
if employee.UserId != null:
    _ = _realtime.NotifyPunchReceivedAsync(userId, {
        received:  true,
        punchType: "Auto",
        timestamp: "2026-05-15T01:30:00Z",
        message:   "Đã ghi nhận chấm công, đang chờ xử lý..."
    }).ContinueWith(log if faulted)
    ↓
Response trả về RawPunchLogDto { isProcessed: false }
```

**Mục đích UX:** Cho mobile app biết server đã nhận ngay tức thì (trong vài ms), không cần poll. `isProcessed=false` trong response là bình thường — DailyTimesheet sẽ được tính sau bởi background job.

---

### EVENT B: `attendance.updated` + `dashboard.refresh`

**Trigger:** Sau Background Job (Hangfire) xử lý xong 1 nhóm (EmployeeId, WorkDate).

**File:** [AttendanceResolutionService.cs](../SMEFLOWSystem.Application/Services/AttendanceResolutionService.cs)

```
[HANGFIRE JOB] — chạy định kỳ (mặc định */15 * * * *)
    ↓
foreach group (EmployeeId, WorkDate) in batch:
    await transaction.ExecuteAsync(async () =>
    {
        UpsertDailyTimesheetAsync(...)   ← tính toán + upsert
        MarkProcessedAsync(newLogIds)    ← đánh dấu đã xử lý
        updatedTimesheet = bulkData.ExistingTimesheets
            .FirstOrDefault(d.EmployeeId == x && d.WorkDate == x)
    })
    ↓ [Transaction đã commit — ngoài transaction block]
    if employeeMap[employeeId].UserId != null:
        todayDto = {
            workDate:          "2026-05-15",
            status:            "Late",
            totalLateMinutes:  15,
            hasCheckedIn:      true,
            hasCheckedOut:     true
        }
        _ = _realtime.NotifyAttendanceUpdatedAsync(userId, tenantId, todayDto)
            .ContinueWith(log if faulted)
```

**Bên trong `NotifyAttendanceUpdatedAsync`:**
```
// Gửi cho nhân viên đó
hub.Clients.Group("user:{userId}").SendAsync("attendance.updated", todayDto)

// Gửi tín hiệu refresh cho toàn tenant
hub.Clients.Group("tenant:{tenantId}:dashboard").SendAsync("dashboard.refresh", { tenantId })
```

**Mục đích UX:** Nhân viên thấy trạng thái chấm công cập nhật ngay mà không cần reload. Admin/Manager nhận signal để refresh dashboard với số liệu mới nhất.

---

### EVENT C: `appeal.processed`

**Trigger:** Sau HR approve hoặc reject appeal (`ProcessAppealAsync`), ngoài transaction block.

**File:** [AttendanceService.cs](../SMEFLOWSystem.Application/Services/AttendanceService.cs)

```
[HR] → PUT /api/v1/attendance/appeals/{id}/process
    ↓
await transaction.ExecuteAsync(async () =>
{
    if isApproved:
        appeal.Status = "Approved"
        AddAsync(RawPunchLog HR_Manual In/Out)
        Reset IsManuallyAdjusted = false
        MarkUnprocessedForRecalculateAsync(...)
    else:
        appeal.Status = "Rejected"
        appeal.RejectReason = request.RejectReason
    UpdateAsync(appeal)
})
    ↓ [Transaction đã commit — ngoài transaction block]
employee = GetByIdAsync(appeal.EmployeeId)
if employee.UserId != null:
    _ = _realtime.NotifyAppealProcessedAsync(userId, {
        appealId:     "uuid",
        workDate:     "2026-05-15",
        status:       "Approved",   // hoặc "Rejected"
        rejectReason: null,         // null nếu Approved, string nếu Rejected
        processedAt:  "2026-06-03T..."
    }).ContinueWith(log if faulted)
```

**Mục đích UX:** Nhân viên nhận thông báo ngay khi HR xử lý đơn — không cần check lại `GET /appeals`.

---

### EVENT D: `payroll.published` (1 phiếu)

**Trigger:** Sau `PublishPayrollAsync` — admin chốt 1 phiếu lương.

**File:** [PayrollService.cs](../SMEFLOWSystem.Application/Services/PayrollService.cs)

```
[ADMIN] → PUT /api/payrolls/{id}/publish
    ↓
payroll.Status = PayrollStatus.Published
await _payrollRepository.UpdateAsync(payroll)
    ↓ [Sau UpdateAsync]
employee = GetByIdAsync(payroll.EmployeeId)
if employee.UserId != null:
    _ = _realtime.NotifyPayrollPublishedAsync(userId, {
        payrollId:  "uuid",
        month:      5,
        year:       2026,
        netSalary:  12500000.00
    }).ContinueWith(log if faulted)
```

---

### EVENT E: `payroll.published` (hàng loạt)

**Trigger:** Sau `PublishAllDraftAsync` — admin chốt toàn bộ phiếu lương của 1 tháng.

**File:** [PayrollService.cs](../SMEFLOWSystem.Application/Services/PayrollService.cs)

```
[ADMIN] → PUT /api/payrolls/publish-all?month=5&year=2026
    ↓
drafts = GetDraftsByTenantMonthAsync(tenantId, month, year)
foreach draft: draft.Status = Published
await UpdateRangeAsync(drafts)   ← 1 query cho tất cả
    ↓ [Sau UpdateRangeAsync — Bulk load employees để tránh N+1]
employeeIds = drafts.Select(EmployeeId).Distinct()
employees = GetByIdsAsync(employeeIds)   ← 1 query
employeeMap = employees.ToDictionary(e.Id)
    ↓
foreach draft:
    if employeeMap[draft.EmployeeId].UserId != null:
        _ = _realtime.NotifyPayrollPublishedAsync(userId, {...})
            .ContinueWith(log if faulted)
```

> Dùng `GetByIdsAsync` (bulk load 1 query) thay vì N lần `GetByIdAsync` để tránh N+1 queries.

---

### EVENT F: `appeal.submitted`

**Trigger:** Sau `_appealRepository.AddAsync(appeal)` trong `SubmitAppealAsync` — ngoài transaction (không có transaction ở đây).

**File:** [AttendanceService.cs](../SMEFLOWSystem.Application/Services/AttendanceService.cs)

```
[NHÂN VIÊN] → POST /api/v1/attendance/appeals
    ↓
Validate: ngày hợp lệ, không có pending appeal trùng
    ↓
AddAsync(TimesheetAppeal { Status: "PendingApproval" })
    ↓
[Fire-and-forget]
_ = _realtime.NotifyAppealSubmittedAsync(tenantId, {
    appealId:    "uuid",
    employeeId:  "uuid",
    employeeName:"Nguyễn Văn A",
    workDate:    "2026-06-15",
    appealType:  "In",
    reason:      "Quên bấm check-in",
    submittedAt: "2026-06-16T08:00:00Z"
}).ContinueWith(log if faulted)
```

**Mục đích UX:** HR nhận alert ngay lập tức — badge số đơn chờ trên sidebar tự tăng, không cần F5.

---

### EVENT G: `payroll.paid`

**Trigger:** Sau `MarkPaidAsync` — admin đánh dấu đã chuyển khoản.

**File:** [PayrollService.cs](../SMEFLOWSystem.Application/Services/PayrollService.cs)

```
[ADMIN] → PUT /api/payrolls/{id}/mark-paid
    ↓
payroll.Status = Paid
UpdateAsync(payroll)
    ↓
employee = GetByIdAsync(payroll.EmployeeId)
if employee.UserId != null:
    _ = _realtime.NotifyPayrollPaidAsync(userId, {
        payrollId:  "uuid",
        month:      6,
        year:       2026,
        netSalary:  12500000.00,
        paidAt:     "2026-06-25T10:00:00Z"
    }).ContinueWith(log if faulted)

_ = _realtime.NotifyDashboardRefreshAsync(tenantId).ContinueWith(...)
```

**Mục đích UX:** Nhân viên nhận toast "Lương tháng 6 đã được chuyển khoản" ngay, không cần kiểm tra app.

---

### EVENT H: `shift.assigned`

**Trigger:** Sau `BulkInsertAssignmentsAsync` trong `BulkAssignPatternAsync`.

**File:** [ShiftManagementService.cs](../SMEFLOWSystem.Application/Services/ShiftManagementService.cs)

```
[HR/MANAGER] → POST /api/hr/shift-assignments/bulk
    ↓
BulkEndPreviousAssignmentsAsync(...)
BulkInsertAssignmentsAsync(assignments)
    ↓
hrName = (load tên HR từ DB)
foreach emp in employees:
    if emp.UserId != null:
        _ = _realtime.NotifyShiftAssignedAsync(emp.UserId, {
            shiftPatternId:   "uuid",
            shiftPatternName: "Ca Sáng 5+2",
            effectiveStartDate: "2026-07-01",
            assignedBy:       "HR Manager"
        }).ContinueWith(log if faulted)
```

> Employees được load trước bằng `GetByIdsAsync` (1 query) để tránh N+1.

**Mục đích UX:** Nhân viên không bị bất ngờ khi lịch ca thay đổi, mobile app cập nhật calendar tự động.

---

### EVENT I: `bonus_deduction.entry_added`

**Trigger:** Sau `CreateEntryAsync` hoặc `CreateBulkEntriesAsync` trong `PayrollService`.

**File:** [PayrollService.cs](../SMEFLOWSystem.Application/Services/PayrollService.cs)

```
[HR] → POST /api/payrolls/entries  hoặc  POST /api/payrolls/entries/bulk
    ↓
AddAsync(entry) / AddRangeAsync(entries)
[Nếu có Draft → CalculatePayrollForEmployeeAsync(suppressGenerateNotify: true)]
    ↓
if emp.UserId != null:
    _ = _realtime.NotifyBonusDeductionEntryAddedAsync(userId, {
        type:      "Bonus",
        category:  "Performance",
        amount:    1000000,
        reason:    "Thưởng KPI tháng 6",
        month:     6,
        year:      2026,
        createdAt: "2026-06-15T09:00:00Z"
    }).ContinueWith(log if faulted)
```

> **`suppressGenerateNotify: true`** khi gọi `CalculatePayrollForEmployeeAsync` để tránh emit `payroll.generated` thừa.

**Mục đích UX:** Nhân viên thấy thông báo "Bạn vừa được thưởng 1.000.000đ KPI tháng 6" ngay lập tức — minh bạch, giảm thắc mắc lương.

---

### EVENT J: `attendance.manual_adjusted`

**Trigger:** Sau `_punchLogRepo.AddAsync(punch)` trong `ManualPunchAsync`.

**File:** [AttendanceService.cs](../SMEFLOWSystem.Application/Services/AttendanceService.cs)

```
[HR] → POST /api/v1/attendance/manual-punch
    ↓
Tạo RawPunchLog { DeviceId = "HR_Manual", IsProcessed = false }
AddAsync(punch)
    ↓
hrName = (load tên HR từ DB nếu có)
if employee.UserId != null:
    localTime = ConvertTimeFromUtc(punch.Timestamp, VietnamTimeZone)
    _ = _realtime.NotifyAttendanceManualAdjustedAsync(userId, {
        workDate:   "2026-06-15",
        punchType:  "In",
        timestamp:  "2026-06-15T01:30:00Z",
        adjustedBy: "HR Manager",
        note:       "Nhân viên có mặt nhưng quên bấm"
    }).ContinueWith(log if faulted)
```

**Mục đích UX:** Nhân viên biết HR đã bổ sung check-in/out thay mình — minh bạch, tránh thắc mắc.

---

### EVENT K: `payroll.generated`

**Trigger:** Sau `AddRangeAsync`/`UpdateRangeAsync` trong `GenerateMonthlyPayrollAsync`, hoặc sau `UpdateAsync`/`AddAsync` trong `CalculatePayrollForEmployeeAsync` (chỉ khi `suppressGenerateNotify = false`, tức là gọi trực tiếp từ API, không gọi nội bộ từ entry methods).

**File:** [PayrollService.cs](../SMEFLOWSystem.Application/Services/PayrollService.cs)

```
[ADMIN] → POST /api/payrolls/generate  hoặc  POST /api/payrolls/calculate/{id}
    ↓
[Tính toán và lưu DB xong]
    ↓
if _currentUser.UserId.HasValue && !suppressGenerateNotify:
    _ = _realtime.NotifyPayrollGeneratedAsync(userId, {
        month:          6,
        year:           2026,
        generatedCount: 45,
        skippedCount:   3,
        type:           "bulk"  // hoặc "single"
    }).ContinueWith(log if faulted)
```

> **`suppressGenerateNotify` flag:** `CreateEntryAsync`, `DeleteEntryAsync`, `CreateBulkEntriesAsync` gọi `CalculatePayrollForEmployeeAsync(suppressGenerateNotify: true)` để tránh noise.

**Mục đích UX:** Admin không cần reload trang sau generate. Toast "Đã tạo 45 phiếu lương Draft" xuất hiện tự động.

---

### EVENT L: `employee.onboarded`

**Trigger:** Sau `_inviteRepository.UpdateInviteAsync(invite)` trong `CompleteOnboardingInternalAsync`.

**File:** [InviteService.cs](../SMEFLOWSystem.Application/Services/InviteService.cs)

```
[NHÂN VIÊN] → POST /api/hr/invites/complete
    ↓
Tạo User + UserRole + Employee
Nếu role Manager → tạo ManagerDepartment
invite.IsUsed = true
UpdateInviteAsync(invite)
    ↓
reloadedEmployee = GetByIdAsync(employee.Id)
_ = _realtime.NotifyEmployeeOnboardedAsync(tenantId, {
    employeeId:    "uuid",
    employeeName:  "Trần Thị B",
    email:         "b@company.com",
    departmentName:"Kỹ thuật",
    positionName:  "Backend Developer",
    onboardedAt:   "2026-06-18T09:00:00Z"
}).ContinueWith(log if faulted)

_ = _realtime.NotifyDashboardRefreshAsync(tenantId).ContinueWith(...)
```

**Mục đích UX:** HR Admin nhận alert "Trần Thị B vừa hoàn tất onboarding" — có thể gán lịch ca và BaseSalary ngay.

---

## 7. Đăng ký DI & Middleware

### 7.1 DependencyInjection.cs (trong `AddWebApi`)

```csharp
// SignalR
services.AddSignalR();
services.AddSingleton<IUserIdProvider, UserIdProvider>();
services.AddScoped<IRealtimeNotificationService, SignalRNotificationService>();
```

**DI Lifetime:** `IRealtimeNotificationService` đăng ký là `Scoped`. `IHubContext<T>` là Singleton — inject Singleton vào Scoped là an toàn.

### 7.2 JWT Bearer nhận token qua query string (cũng trong `AddWebApi`)

SignalR không thể gửi `Authorization: Bearer` header qua WebSocket — phải dùng query string:

```csharp
options.Events = new JwtBearerEvents
{
    OnMessageReceived = context =>
    {
        var accessToken = context.Request.Query["access_token"];
        var path = context.HttpContext.Request.Path;
        if (!string.IsNullOrEmpty(accessToken) &&
            path.StartsWithSegments("/hubs/notifications"))
        {
            context.Token = accessToken;
        }
        return Task.CompletedTask;
    }
};
```

> Chỉ áp dụng cho path `/hubs/notifications` — các API thông thường vẫn dùng header.

### 7.3 WebApplicationExtensions.cs (trong `UseWebApi`)

```csharp
app.MapHub<NotificationHub>("/hubs/notifications");
```

> Phải đặt **trước** `app.UseMiddleware<ModuleAccessMiddleware>()` và `app.MapControllers()`.

---

## 8. Kết nối từ Frontend

### React / Next.js (TypeScript)

```typescript
import * as signalR from "@microsoft/signalr";

const connection = new signalR.HubConnectionBuilder()
    .withUrl("/hubs/notifications", {
        accessTokenFactory: () => localStorage.getItem("token") ?? ""
    })
    .withAutomaticReconnect()
    .build();

// Lắng nghe từng event
connection.on("punch.received", (data) => {
    // { received: true, punchType, timestamp, message }
    showToast(data.message);
});

connection.on("attendance.updated", (data) => {
    // { workDate, status, totalLateMinutes, hasCheckedIn, hasCheckedOut }
    setTodayAttendance(data);
});

connection.on("dashboard.refresh", () => {
    // Không có data, chỉ là tín hiệu
    fetchDashboardData();   // gọi lại GET /dashboard/admin
});

connection.on("appeal.processed", (data) => {
    // { appealId, workDate, status, rejectReason, processedAt }
    showNotification(`Đơn giải trình ngày ${data.workDate}: ${data.status}`);
});

connection.on("payroll.published", (data) => {
    // { payrollId, month, year, netSalary }
    showNotification(`Phiếu lương tháng ${data.month}/${data.year} đã được chốt`);
});

// === Events mới (GAP-01 đến GAP-08) ===

connection.on("appeal.submitted", (data) => {
    // HR nhận: { appealId, employeeId, employeeName, workDate, appealType, reason, submittedAt }
    // Chỉ admin/HR group nhận
    setBadgeCount(prev => prev + 1);
    showNotification(`${data.employeeName} vừa nộp đơn giải trình ngày ${data.workDate}`);
});

connection.on("payroll.paid", (data) => {
    // { payrollId, month, year, netSalary, paidAt }
    showToast(`Lương tháng ${data.month}/${data.year} đã được chuyển khoản: ${formatCurrency(data.netSalary)}`);
});

connection.on("shift.assigned", (data) => {
    // { shiftPatternId, shiftPatternName, effectiveStartDate, assignedBy }
    showNotification(`Lịch làm việc mới: ${data.shiftPatternName} từ ${data.effectiveStartDate}`);
    refreshCalendar();
});

connection.on("bonus_deduction.entry_added", (data) => {
    // { type, category, amount, reason, month, year, createdAt }
    const sign = data.type === "Bonus" ? "+" : "-";
    showToast(`${sign}${formatCurrency(data.amount)} — ${data.reason}`);
});

connection.on("attendance.manual_adjusted", (data) => {
    // { workDate, punchType, timestamp, adjustedBy, note }
    showNotification(`HR đã bổ sung ${data.punchType} ngày ${data.workDate} — ${data.note}`);
    refreshAttendanceRecord(data.workDate);
});

connection.on("payroll.generated", (data) => {
    // Admin nhận: { month, year, generatedCount, skippedCount, type }
    showToast(`Đã tạo ${data.generatedCount} phiếu lương tháng ${data.month}/${data.year} (bỏ qua: ${data.skippedCount})`);
    refreshPayrollList();
});

connection.on("employee.onboarded", (data) => {
    // HR Admin nhận: { employeeId, employeeName, email, departmentName, positionName, onboardedAt }
    // Chỉ admin/HR group nhận
    showNotification(`${data.employeeName} vừa hoàn tất onboarding — ${data.departmentName}`);
    fetchDashboardData();
});

await connection.start();
```

### Flutter / Dart (Mobile)

```dart
// Package: signalr_netcore
final hubConnection = HubConnectionBuilder()
    .withUrl("/hubs/notifications",
        options: HttpConnectionOptions(
            accessTokenFactory: () async => await getStoredToken()))
    .withAutomaticReconnect()
    .build();

hubConnection.on("attendance.updated", (args) {
    final data = args?[0] as Map<String, dynamic>;
    // Refresh UI
});

hubConnection.on("payroll.published", (args) {
    final data = args?[0] as Map<String, dynamic>;
    showLocalNotification("Phiếu lương tháng ${data['month']} đã sẵn sàng");
});

hubConnection.on("payroll.paid", (args) {
    final data = args?[0] as Map<String, dynamic>;
    showLocalNotification("Lương tháng ${data['month']} đã chuyển khoản");
});

hubConnection.on("shift.assigned", (args) {
    final data = args?[0] as Map<String, dynamic>;
    refreshShiftCalendar();
});

hubConnection.on("bonus_deduction.entry_added", (args) {
    final data = args?[0] as Map<String, dynamic>;
    showLocalNotification(data['reason']);
});

hubConnection.on("attendance.manual_adjusted", (args) {
    final data = args?[0] as Map<String, dynamic>;
    refreshDailyAttendance(data['workDate']);
});

await hubConnection.start();
```

> **Quan trọng:** Khi reconnect, `OnConnectedAsync` được gọi lại → client tự re-join groups. `withAutomaticReconnect()` xử lý việc này tự động.

---

## 9. Thứ tự Events theo Kịch Bản

### Kịch bản 1: Nhân viên check-in thành công

```
08:30 — [MOBILE] POST /submit-punch
    ← Server: { isProcessed: false }
    ← SignalR → CLIENT: "punch.received" { received: true, message: "Đang chờ xử lý..." }

[VÀI PHÚT SAU — HANGFIRE JOB chạy]
    ← SignalR → CLIENT (nhân viên): "attendance.updated" { status: "Late", totalLateMinutes: 15 }
    ← SignalR → ALL CLIENTS trong tenant: "dashboard.refresh" { tenantId }
```

### Kịch bản 2: Nhân viên gửi appeal → HR xử lý

```
[NHÂN VIÊN] POST /appeals → TimesheetAppeal { Status: "PendingApproval" }

[HR] PUT /appeals/{id}/process { isApproved: true }
    ← Tạo RawPunchLog HR_Manual + reset IsManuallyAdjusted + MarkUnprocessed
    ← SignalR → NHÂN VIÊN: "appeal.processed" { status: "Approved" }

[HANGFIRE JOB chạy]
    ← DailyTimesheet tái tính với log HR_Manual
    ← SignalR → NHÂN VIÊN: "attendance.updated" { status: "Normal" }
```

### Kịch bản 3: Admin chốt phiếu lương hàng loạt

```
[ADMIN] PUT /payrolls/publish-all?month=5&year=2026
    ← UpdateRangeAsync tất cả draft → Published (1 query)
    ← Bulk load employees (1 query)
    ← SignalR → TỪNG NHÂN VIÊN: "payroll.published" { month: 5, year: 2026, netSalary: ... }
```

### Kịch bản 4: HR bổ sung check-in thủ công cho nhân viên

```
[HR] POST /api/v1/attendance/manual-punch { employeeId, punchType: "In", timestamp }
    ← Tạo RawPunchLog DeviceId="HR_Manual"
    ← SignalR → NHÂN VIÊN: "attendance.manual_adjusted" { workDate, punchType, adjustedBy }

[HANGFIRE JOB chạy lại xử lý]
    ← Timesheet recalculate
    ← SignalR → NHÂN VIÊN: "attendance.updated" { status: "Normal" }
    ← SignalR → DASHBOARD: "dashboard.refresh" { tenantId }
```

### Kịch bản 5: HR nhân viên nộp đơn giải trình

```
[NHÂN VIÊN] POST /api/v1/attendance/appeals { workDate, appealType: "In", reason }
    ← AddAsync(TimesheetAppeal { Status: "PendingApproval" })
    ← SignalR → HR ADMINS (tenant group): "appeal.submitted" { employeeName, workDate, reason }
    # HR thấy badge notification tăng → vào xử lý

[HR] PUT /api/v1/attendance/appeals/{id}/process { isApproved: true }
    ← Tạo RawPunchLog HR_Manual + reset IsManuallyAdjusted
    ← SignalR → NHÂN VIÊN: "appeal.processed" { status: "Approved" }
    ← SignalR → DASHBOARD: "dashboard.refresh"

[HANGFIRE JOB]
    ← SignalR → NHÂN VIÊN: "attendance.updated" { status: "Normal" }
```

### Kịch bản 6: HR thêm thưởng KPI cho nhân viên

```
[HR] POST /api/payrolls/entries { employeeId, type: "Bonus", amount: 1000000, reason: "KPI" }
    ← AddAsync(BonusDeductionEntry)
    ← CalculatePayrollForEmployeeAsync(suppressGenerateNotify: true)  ← tái tính draft, KHÔNG emit payroll.generated
    ← SignalR → NHÂN VIÊN: "bonus_deduction.entry_added" { type: "Bonus", amount: 1000000, reason: "KPI" }
    # Nhân viên thấy ngay, không cần hỏi HR
```

### Kịch bản 7: Admin generate phiếu lương tháng

```
[ADMIN] POST /api/payrolls/generate?month=6&year=2026
    ← GenerateMonthlyPayrollAsync → AddRangeAsync/UpdateRangeAsync (1 query)
    ← SignalR → ADMIN: "payroll.generated" { month: 6, year: 2026, generatedCount: 45, skippedCount: 3 }
    # Admin thấy toast, KHÔNG cần reload trang
```

### Kịch bản 8: HR gán lịch ca mới cho nhóm nhân viên

```
[HR] POST /api/hr/shift-assignments/bulk { employeeIds: [...], shiftPatternId }
    ← BulkEndPreviousAssignmentsAsync + BulkInsertAssignmentsAsync
    ← Bulk load employees (1 query, tránh N+1)
    ← SignalR → TỪNG NHÂN VIÊN: "shift.assigned" { shiftPatternName, effectiveStartDate }
    # Nhân viên nhận alert, mobile app cập nhật calendar
```

### Kịch bản 9: Nhân viên mới hoàn tất onboarding

```
[NHÂN VIÊN MỚI] POST /api/hr/invites/complete { token, fullName, password }
    ← Tạo User + UserRole + Employee
    ← invite.IsUsed = true, UpdateInviteAsync
    ← SignalR → HR ADMINS (tenant group): "employee.onboarded" { employeeName, departmentName }
    ← SignalR → DASHBOARD (tenant): "dashboard.refresh"
    # HR Admin thấy alert, có thể gán lịch ca và BaseSalary ngay
```

---

## 10. Edge Cases & Cách Xử Lý

| Trường hợp | Xử Lý |
|-----------|--------|
| Employee chưa có UserId | `if (employee?.UserId != null)` — bỏ qua nếu null, không lỗi |
| User không online (không có connection) | SignalR tự bỏ qua — không lỗi, không retry |
| Background Job không có HTTP context | `IHubContext<T>` inject trực tiếp vào service, không cần HTTP context |
| Nhiều tab/device cùng 1 user | Group `user:{userId}` gửi đến TẤT CẢ connection của user đó |
| SignalR send thất bại | `try-catch` trong `SignalRNotificationService` — log Warning, không throw |
| Connection mất và reconnect | `withAutomaticReconnect()` xử lý. `OnConnectedAsync` gọi lại → re-join groups |
| Notify trong transaction bị rollback | Không xảy ra — notify luôn gọi **ngoài** `ExecuteAsync` |
| `updatedTimesheet` null sau transaction | `?? 0`, `?? false` — fallback an toàn trong todayDto |

---

## 11. Anti-Patterns Phải Tránh

```csharp
// SAI — notify trong transaction (nếu rollback, event đã gửi rồi)
await _transaction.ExecuteAsync(async () =>
{
    await _timesheetRepo.UpsertAsync(...);
    await _realtime.NotifyAsync(...);   // ← KHÔNG làm thế này
});

// SAI — await notify (blocking job/request)
await _realtime.NotifyAttendanceUpdatedAsync(...);  // ← KHÔNG dùng await

// ĐÚNG — ngoài transaction, fire-and-forget với error logging
await _transaction.ExecuteAsync(async () =>
{
    await _timesheetRepo.UpsertAsync(...);
});
_ = _realtime.NotifyAsync(...)
    .ContinueWith(t => { if (t.IsFaulted) _logger.LogWarning(...); });
```

---

## 12. Security & Authorization

| Loại | Rule |
|------|------|
| Hub Authentication | `[Authorize]` trên `NotificationHub` — yêu cầu JWT hợp lệ |
| Token transport | Query string `?access_token=JWT` (WebSocket không hỗ trợ Authorization header) |
| Token scope | `OnMessageReceived` chỉ đọc token từ query string khi path là `/hubs/notifications` |
| Group isolation | Group key dùng `tenantId` — user của tenant A không nhận event của tenant B |
| User isolation | Group `user:{userId}` chỉ nhận event gửi đúng userId đó |
| Role-based groups | `admins` và `managers` group chỉ join được khi role tương ứng trong JWT |

---

## 13. Files Tham Khảo

### Interface & Hub
- [IRealtimeNotificationService.cs](../SMEFLOWSystem.Application/Interfaces/IServices/IRealtimeNotificationService.cs)
- [NotificationHub.cs](../SMEFLOWSystem.WebAPI/Hubs/NotificationHub.cs)
- [UserIdProvider.cs](../SMEFLOWSystem.WebAPI/Hubs/UserIdProvider.cs)
- [SignalRNotificationService.cs](../SMEFLOWSystem.WebAPI/Services/SignalRNotificationService.cs)

### Services tích hợp
- [AttendanceService.cs](../SMEFLOWSystem.Application/Services/AttendanceService.cs) — `appeal.submitted`, `appeal.processed`, `attendance.manual_adjusted`
- [AttendanceResolutionService.cs](../SMEFLOWSystem.Application/Services/AttendanceResolutionService.cs) — `attendance.updated`, `punch.received`, `dashboard.refresh`
- [PayrollService.cs](../SMEFLOWSystem.Application/Services/PayrollService.cs) — `payroll.generated`, `payroll.published`, `payroll.paid`, `bonus_deduction.entry_added`, `dashboard.refresh`
- [ShiftManagementService.cs](../SMEFLOWSystem.Application/Services/ShiftManagementService.cs) — `shift.assigned`
- [InviteService.cs](../SMEFLOWSystem.Application/Services/InviteService.cs) — `employee.onboarded`, `dashboard.refresh`

### Configuration
- [DependencyInjection.cs](../SMEFLOWSystem.WebAPI/Extensions/DependencyInjection.cs)
- [WebApplicationExtensions.cs](../SMEFLOWSystem.WebAPI/Validator/WebApplicationExtensions.cs)

---

## 14. Điểm Quan Trọng Cần Nhớ

1. **Notify là best-effort** — SignalR không guarantee delivery. Frontend phải gọi API một lần khi reconnect để lấy trạng thái hiện tại, không chỉ dựa vào event.

2. **Event là tín hiệu, không phải nguồn dữ liệu** — `dashboard.refresh` không chứa data; `attendance.updated` chứa snapshot tại thời điểm xử lý. Frontend nên dùng event để biết khi nào cần gọi lại API, không cache event làm nguồn chính.

3. **Claim name phải khớp JWT** — Hub đọc `"tenantId"` (không gạch dưới). `UserIdProvider` đọc `ClaimTypes.NameIdentifier`. Xác nhận trong `AuthHelper.cs` trước khi sửa.

4. **Background Job không cần HTTP context** — `IHubContext<T>` hoạt động trong background jobs, hosted services, bất kỳ đâu có DI container. Không cần inject `IHttpContextAccessor`.

5. **Fire-and-forget không lose exception** — pattern `_ = Task.ContinueWith(t => { if (t.IsFaulted) log... })` đảm bảo exception được log nhưng không ảnh hưởng caller.

6. **Groups tự xóa khi disconnect** — SignalR tự remove connection khỏi tất cả groups khi client disconnect. `OnDisconnectedAsync` không cần gọi `RemoveFromGroupAsync` thủ công.

7. **Bulk load employees trong job** — `AttendanceResolutionService` dùng `GetByIdsAsync(employeeIds)` (1 query cho toàn bộ batch) thay vì load từng employee, đảm bảo không N+1 ngay cả khi batch có nhiều nhân viên.

8. **CORS phải có `AllowCredentials()`** — SignalR WebSocket yêu cầu `AllowCredentials()` trong CORS policy. Đã cấu hình trong `AddWebApi` với `policy.AllowCredentials()`.

9. **`suppressGenerateNotify` flag cho internal calls** — Khi một public method gọi nội bộ method khác cũng emit notification cùng loại, dùng `bool suppressXxxNotify = false` optional parameter để tắt notification ở inner layer. Ví dụ: `CreateEntryAsync` gọi `CalculatePayrollForEmployeeAsync(suppressGenerateNotify: true)` để tránh emit `payroll.generated` thừa — chỉ `bonus_deduction.entry_added` được phát ra. Rule: noise từ internal recalculation không nên reach frontend.
