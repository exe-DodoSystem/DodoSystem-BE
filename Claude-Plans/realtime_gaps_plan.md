# Realtime Gaps Plan — DodoSystem Backend

> Phân tích các điểm còn thiếu Real-Time (SignalR) sau khi đối chiếu toàn bộ documentation hiện tại.
> Tạo: 2026-06-18 — **Triển khai hoàn thành: 2026-06-18**

---

## 1. Tổng quan — Đã có vs Còn thiếu

### Events đã triển khai (Phase 1–3)

| Event | Trigger | Gửi tới |
|-------|---------|---------|
| `punch.received` | Nhân viên submit punch | `user:{userId}` |
| `attendance.updated` | Background Job xử lý xong DailyTimesheet | `user:{userId}` |
| `dashboard.refresh` | Cùng lúc với `attendance.updated` | `tenant:{tenantId}:dashboard` |
| `appeal.processed` | HR approve/reject appeal | `user:{userId}` (nhân viên submit) |
| `payroll.published` | Admin chốt 1 hoặc nhiều phiếu lương | `user:{userId}` (nhân viên có phiếu) |

### Vấn đề còn lại

Có **3 vấn đề cấu trúc** và **8 events còn thiếu** được phát hiện sau khi đối chiếu với toàn bộ luồng HR, Attendance, Payroll, và Dashboard.

---

## 2. Vấn đề cấu trúc

### Problem 1 — Group `tenant:{tenantId}:managers` chưa được dùng

Group `managers` được định nghĩa và join trong `NotificationHub.OnConnectedAsync` nhưng **không có event nào** gửi đến group này. Manager cần biết khi có hoạt động liên quan đến nhân viên họ quản lý.

### Problem 2 — `dashboard.refresh` chỉ trigger từ Attendance

Hiện tại `dashboard.refresh` chỉ được gửi khi `AttendanceResolutionService` hoàn thành batch. Nhưng `AdminDashboardDto` tổng hợp từ nhiều nguồn: Payroll (counts), Appeals (pending count), Employees. Những thay đổi bên ngoài luồng Attendance không trigger refresh:

- HR publish/paid payroll → `PayrollSummary` trên dashboard thay đổi nhưng không có signal
- HR approve/reject appeal → `pendingAppealsCount` thay đổi ngay nhưng dashboard không biết
- Nhân viên mới onboarding xong → `totalEmployees` tăng nhưng dashboard không biết

### Problem 3 — Nhân viên không nhận thông báo khi HR tác động trực tiếp lên hồ sơ của họ

Nhiều hành động HR thay đổi dữ liệu liên quan đến nhân viên (lịch ca, thưởng/phạt, đánh dấu thanh toán) nhưng nhân viên không nhận được notification. Nhân viên phải tự reload để biết.

---

## 3. Events còn thiếu — Chi tiết

---

### GAP-01: `appeal.submitted` — HR không biết có đơn mới

**Trigger:** Sau `POST /api/v1/attendance/appeals` thành công (nhân viên gửi đơn giải trình).

**Gửi tới:** `tenant:{tenantId}:admins` (TenantAdmin + HRManager)

**Data:**
```json
{
  "appealId": "uuid",
  "employeeId": "uuid",
  "employeeName": "Nguyễn Văn A",
  "workDate": "2026-06-15",
  "appealType": "In",
  "reason": "Quên bấm check-in",
  "submittedAt": "2026-06-16T08:00:00Z"
}
```

**File cần sửa:** `AttendanceService.cs` — sau `_timesheetAppealRepository.AddAsync(appeal)`, gọi notify.

**Interface mới trong `IRealtimeNotificationService`:**
```csharp
Task NotifyAppealSubmittedAsync(Guid tenantId, object data);
```

**Impact UX:** HR không phải F5 liên tục tab `/appeals/pending`. Badge số đơn chờ trên sidebar tự tăng.

---

### GAP-02: `payroll.paid` — Nhân viên biết lương đã được chuyển

**Trigger:** Sau `PUT /api/payrolls/{id}/mark-paid` thành công.

**Gửi tới:** `user:{userId}` (nhân viên có phiếu)

**Data:**
```json
{
  "payrollId": "uuid",
  "month": 6,
  "year": 2026,
  "netSalary": 12500000.00,
  "paidAt": "2026-06-25T10:00:00Z"
}
```

**File cần sửa:** `PayrollService.cs` — trong `MarkAsPaidAsync`, sau `UpdateAsync(payroll)`, thêm notify tương tự pattern của `PublishPayrollAsync`.

**Interface mới trong `IRealtimeNotificationService`:**
```csharp
Task NotifyPayrollPaidAsync(Guid userId, object data);
```

**Impact UX:** Nhân viên nhận push notification/toast "Lương tháng 6 đã được chuyển khoản". Họ không cần check app để biết tiền đã về.

---

### GAP-03: `shift.assigned` — Nhân viên biết lịch ca mới

**Trigger:** Sau `POST /api/hr/shift-assignments/bulk` thành công — khi HR/Manager gán lịch ca mới.

**Gửi tới:** `user:{userId}` cho từng nhân viên trong `employeeIds` (chỉ gửi cho nhân viên có UserId).

**Data:**
```json
{
  "shiftPatternId": "uuid",
  "shiftPatternName": "Ca Sáng 5+2",
  "effectiveStartDate": "2026-07-01",
  "assignedBy": "HR Manager"
}
```

**File cần sửa:** `ShiftManagementService.cs` — sau `BulkInsertAssignmentsAsync`, bulk load employees (pattern giống `PublishAllDraftAsync`) để tránh N+1.

**Interface mới trong `IRealtimeNotificationService`:**
```csharp
Task NotifyShiftAssignedAsync(Guid userId, object data);
```

**Impact UX:** Nhân viên không bị bất ngờ khi lịch ca thay đổi. Mobile app cập nhật calendar tự động.

---

### GAP-04: `bonus_deduction.entry_added` — Nhân viên biết có khoản thưởng/phạt mới

**Trigger:** Sau `POST /api/payrolls/entries` hoặc `POST /api/payrolls/entries/bulk` thành công.

**Gửi tới:** `user:{userId}` cho từng nhân viên có entry được tạo.

**Data:**
```json
{
  "type": "Bonus",
  "category": "Performance",
  "amount": 1000000,
  "reason": "Thưởng KPI tháng 6",
  "month": 6,
  "year": 2026,
  "createdAt": "2026-06-15T09:00:00Z"
}
```

**File cần sửa:** `PayrollService.cs` — trong `CreateBonusDeductionEntryAsync` và `CreateBulkBonusDeductionEntriesAsync`.

**Interface mới trong `IRealtimeNotificationService`:**
```csharp
Task NotifyBonusDeductionEntryAddedAsync(Guid userId, object data);
```

**Impact UX:** Nhân viên thấy thông báo "Bạn vừa được thưởng 1.000.000đ KPI tháng 6" ngay lập tức. Transparent, giảm thắc mắc lương.

---

### GAP-05: `payroll.generated` — Admin biết generate/calculate xong

**Trigger:** Sau `POST /api/payrolls/generate` hoặc `POST /api/payrolls/calculate/{id}` thành công.

**Gửi tới:** `user:{userId}` của **admin người gọi API** (không gửi cho tenant rộng).

**Data:**
```json
{
  "month": 6,
  "year": 2026,
  "generatedCount": 45,
  "skippedCount": 3,
  "type": "bulk"
}
```

**File đã sửa:** `PayrollService.cs` — `GenerateMonthlyPayrollAsync` và `CalculatePayrollForEmployeeAsync`.

**Interface mới trong `IRealtimeNotificationService`:**
```csharp
Task NotifyPayrollGeneratedAsync(Guid userId, object data);
```

**Lưu ý triển khai — `suppressGenerateNotify` flag:**
`CalculatePayrollForEmployeeAsync` có thêm tham số `bool suppressGenerateNotify = false`. Các chỗ gọi nội bộ từ `CreateEntryAsync`, `DeleteEntryAsync`, và `CreateBulkEntriesAsync` đều truyền `suppressGenerateNotify: true` để tránh emit `payroll.generated` noise khi chỉ recalculate sau thêm/xóa bonus entry. Chỉ gọi trực tiếp từ API endpoint mới emit notification.

**Impact UX:** Generate cho 100+ nhân viên có thể mất vài giây. Admin không cần reload trang để biết đã xong hay chưa. Toast "Đã tạo 45 phiếu lương Draft" xuất hiện tự động.

---

### GAP-06: `dashboard.refresh` mở rộng — Trigger đầy đủ

**Vấn đề:** `dashboard.refresh` hiện chỉ được gửi từ `AttendanceResolutionService`. Cần bổ sung thêm các trigger:

| Action | File | Vì sao cần refresh |
|--------|------|--------------------|
| `PublishPayrollAsync` | `PayrollService.cs` | `PayrollSummary.PublishedCount` tăng |
| `PublishAllDraftAsync` | `PayrollService.cs` | Tương tự |
| `MarkAsPaidAsync` | `PayrollService.cs` | `PayrollSummary.PaidCount` tăng |
| `ProcessAppealAsync` (approve/reject) | `AttendanceService.cs` | `pendingAppealsCount` giảm |
| `InviteService.CompleteAsync` | `InviteService.cs` | `totalEmployees` tăng |

**Cách triển khai:** Reuse event `dashboard.refresh` đã có — chỉ cần thêm `_realtime.NotifyDashboardRefreshAsync(tenantId)` ở cuối các method trên. Không cần event mới.

**Interface cần thêm overload:**
```csharp
// Đã có:
Task NotifyAttendanceUpdatedAsync(Guid userId, Guid tenantId, object data);
// → bên trong đã gọi NotifyDashboardRefreshAsync

// Cần expose riêng để dùng ở Payroll/HR:
Task NotifyDashboardRefreshAsync(Guid tenantId);
```

**Impact UX:** Admin Dashboard số liệu tự cập nhật khi HR chốt lương, duyệt appeal, hay nhân viên mới join — không cần F5.

---

### GAP-07: `attendance.manual_adjusted` — Nhân viên biết HR đã điều chỉnh chấm công

**Trigger:** Sau `POST /api/v1/attendance/manual-punch` thành công (HR nhập tay cho nhân viên).

**Gửi tới:** `user:{userId}` (nhân viên bị/được điều chỉnh).

**Data:**
```json
{
  "workDate": "2026-06-15",
  "punchType": "In",
  "timestamp": "2026-06-15T01:30:00Z",
  "adjustedBy": "HR Manager",
  "note": "Nhân viên có mặt nhưng quên bấm"
}
```

**File cần sửa:** `AttendanceService.cs` — trong `ManualPunchAsync`.

**Interface mới trong `IRealtimeNotificationService`:**
```csharp
Task NotifyAttendanceManualAdjustedAsync(Guid userId, object data);
```

**Impact UX:** Nhân viên biết HR đã bổ sung check-in/out thay mình — minh bạch, tránh thắc mắc.

---

### GAP-08: `employee.onboarded` — HR biết nhân viên mới đã join

**Trigger:** Sau `POST /api/hr/invites/complete` thành công.

**Gửi tới:** `tenant:{tenantId}:admins`

**Data:**
```json
{
  "employeeId": "uuid",
  "employeeName": "Trần Thị B",
  "email": "b@company.com",
  "departmentName": "Kỹ thuật",
  "positionName": "Backend Developer",
  "onboardedAt": "2026-06-18T09:00:00Z"
}
```

**File cần sửa:** `InviteService.cs` — sau `invite.IsUsed = true`.

**Interface mới trong `IRealtimeNotificationService`:**
```csharp
Task NotifyEmployeeOnboardedAsync(Guid tenantId, object data);
```

**Impact UX:** HR Admin nhận alert "Trần Thị B vừa hoàn tất onboarding" — có thể gán lịch ca và BaseSalary ngay mà không cần poll danh sách nhân viên.

---

## 4. Tổng hợp thay đổi cần làm

### 4.1 Interface `IRealtimeNotificationService` — Thêm methods

```csharp
// Hiện có:
Task NotifyAttendanceUpdatedAsync(Guid userId, Guid tenantId, object data);
Task NotifyAppealProcessedAsync(Guid userId, object data);
Task NotifyPayrollPublishedAsync(Guid userId, object data);
Task NotifyPunchReceivedAsync(Guid userId, object data);

// Cần thêm:
Task NotifyDashboardRefreshAsync(Guid tenantId);                    // GAP-06 — expose riêng
Task NotifyAppealSubmittedAsync(Guid tenantId, object data);       // GAP-01
Task NotifyPayrollPaidAsync(Guid userId, object data);             // GAP-02
Task NotifyShiftAssignedAsync(Guid userId, object data);           // GAP-03
Task NotifyBonusDeductionEntryAddedAsync(Guid userId, object data);// GAP-04
Task NotifyPayrollGeneratedAsync(Guid userId, object data);        // GAP-05
Task NotifyAttendanceManualAdjustedAsync(Guid userId, object data);// GAP-07
Task NotifyEmployeeOnboardedAsync(Guid tenantId, object data);     // GAP-08
```

### 4.2 `SignalRNotificationService` — Implement methods mới

Mỗi method mới implement theo pattern đã có:
- `try-catch` riêng, không throw ra ngoài
- Gửi đến đúng group (user, admins, managers, dashboard)

### 4.3 Events mới và target group

| Event | Target Group | Method |
|-------|-------------|--------|
| `appeal.submitted` | `tenant:{tenantId}:admins` | `NotifyAppealSubmittedAsync` |
| `payroll.paid` | `user:{userId}` | `NotifyPayrollPaidAsync` |
| `shift.assigned` | `user:{userId}` (per nhân viên) | `NotifyShiftAssignedAsync` |
| `bonus_deduction.entry_added` | `user:{userId}` | `NotifyBonusDeductionEntryAddedAsync` |
| `payroll.generated` | `user:{userId}` (admin caller) | `NotifyPayrollGeneratedAsync` |
| `dashboard.refresh` (mở rộng) | `tenant:{tenantId}:dashboard` | `NotifyDashboardRefreshAsync` |
| `attendance.manual_adjusted` | `user:{userId}` | `NotifyAttendanceManualAdjustedAsync` |
| `employee.onboarded` | `tenant:{tenantId}:admins` | `NotifyEmployeeOnboardedAsync` |

### 4.4 Services cần inject `IRealtimeNotificationService`

| Service | Events cần gọi |
|---------|----------------|
| `AttendanceService.cs` | `appeal.submitted`, `attendance.manual_adjusted` |
| `PayrollService.cs` | `payroll.paid`, `payroll.generated`, `bonus_deduction.entry_added`, `dashboard.refresh` |
| `ShiftManagementService.cs` | `shift.assigned` |
| `InviteService.cs` | `employee.onboarded`, `dashboard.refresh` |

> **Lưu ý:** `AttendanceService` đã inject `IRealtimeNotificationService`, chỉ cần thêm method call.
> `PayrollService` đã inject, chỉ cần thêm method call.
> `ShiftManagementService` và `InviteService` cần inject mới.

---

## 5. Thứ tự ưu tiên triển khai

### Phase A — Priority cao (trực tiếp ảnh hưởng workflow hàng ngày)

| # | Gap | Lý do |
|---|-----|-------|
| 1 | GAP-06: `dashboard.refresh` mở rộng | Ít code nhất, impact lớn nhất — chỉ thêm call trong các method đã có |
| 2 | GAP-01: `appeal.submitted` | HR hiện đang phải poll thủ công — UX tệ nhất |
| 3 | GAP-02: `payroll.paid` | Nhân viên cần biết lương đã về — hành động có giá trị cao |

### Phase B — Priority trung bình (UX improvement)

| # | Gap | Lý do |
|---|-----|-------|
| 4 | GAP-03: `shift.assigned` | Nhân viên cần biết lịch ca sớm để chuẩn bị |
| 5 | GAP-04: `bonus_deduction.entry_added` | Minh bạch lương — giảm thắc mắc |
| 6 | GAP-07: `attendance.manual_adjusted` | Audit trail visible cho nhân viên |

### Phase C — Nice to have

| # | Gap | Lý do |
|---|-----|-------|
| 7 | GAP-05: `payroll.generated` | Chỉ cần thiết khi generate chậm (nhiều NV) |
| 8 | GAP-08: `employee.onboarded` | HR có thể tự reload — ít urgent hơn |

---

## 6. Frontend — Events mới cần lắng nghe

```typescript
// GAP-01 — HR nhận khi nhân viên gửi appeal
connection.on("appeal.submitted", (data) => {
    // { appealId, employeeName, workDate, appealType, submittedAt }
    incrementPendingAppealsCount();
    showNotification(`${data.employeeName} vừa gửi đơn giải trình ngày ${data.workDate}`);
});

// GAP-02 — Nhân viên nhận khi lương được đánh dấu đã thanh toán
connection.on("payroll.paid", (data) => {
    // { payrollId, month, year, netSalary, paidAt }
    showNotification(`Lương tháng ${data.month}/${data.year} đã được thanh toán`);
    refreshPayrollList();
});

// GAP-03 — Nhân viên nhận khi lịch ca mới được gán
connection.on("shift.assigned", (data) => {
    // { shiftPatternId, shiftPatternName, effectiveStartDate }
    showNotification(`Lịch ca mới: ${data.shiftPatternName} từ ${data.effectiveStartDate}`);
    refreshShiftInfo();
});

// GAP-04 — Nhân viên nhận khi có khoản thưởng/phạt mới
connection.on("bonus_deduction.entry_added", (data) => {
    // { type, category, amount, reason, month, year }
    const label = data.type === "Bonus" ? "Thưởng" : "Khấu trừ";
    showNotification(`${label}: ${formatMoney(data.amount)} — ${data.reason}`);
});

// GAP-05 — Admin nhận khi generate/calculate xong
connection.on("payroll.generated", (data) => {
    // { month, year, generatedCount, skippedCount, type }
    showNotification(`Đã tạo ${data.generatedCount} phiếu lương tháng ${data.month}/${data.year}`);
    refreshPayrollList();
});

// GAP-06 — Dashboard tự refresh khi có thay đổi từ Payroll/HR (không cần data)
// Đã dùng event "dashboard.refresh" — chỉ cần gọi fetchDashboardData() như cũ

// GAP-07 — Nhân viên nhận khi HR nhập tay chấm công
connection.on("attendance.manual_adjusted", (data) => {
    // { workDate, punchType, timestamp, adjustedBy }
    showNotification(`HR đã điều chỉnh chấm công ngày ${data.workDate}`);
    refreshTodayAttendance();
});

// GAP-08 — HR nhận khi nhân viên mới onboard xong
connection.on("employee.onboarded", (data) => {
    // { employeeId, employeeName, departmentName, positionName }
    showNotification(`${data.employeeName} đã hoàn tất đăng ký (${data.departmentName})`);
    refreshEmployeeList();
});
```

---

## 7. Nguyên tắc triển khai (nhắc lại từ realtime_flow_documentation.md)

Mọi notify mới phải tuân thủ:

1. **Gọi ngoài transaction** — không bao giờ gọi notify bên trong `ExecuteAsync`
2. **Fire-and-forget** — `_ = _realtime.NotifyXxxAsync(...).ContinueWith(t => { if (t.IsFaulted) _logger.LogWarning(...); })`
3. **Không throw ra ngoài** — wrap trong try-catch trong implementation
4. **Check null trước khi notify** — `if (employee?.UserId != null)` cho user-targeted events
5. **Bulk load để tránh N+1** — khi gửi cho nhiều user (ví dụ shift.assigned bulk), dùng `GetByIdsAsync` trước
6. **Gửi sau khi DB đã commit** — đảm bảo data đã bền vững trước khi UI nhận event
7. **`suppressGenerateNotify` pattern** — khi một method public gọi nội bộ method khác cũng có emit notify, dùng optional flag `bool suppressXxxNotify = false` để tắt notify ở lớp trong. Ví dụ: `CreateEntryAsync` gọi `CalculatePayrollForEmployeeAsync(suppressGenerateNotify: true)` để tránh emit `payroll.generated` thừa khi chỉ recalculate sau thêm bonus entry.

---

## 8. Checklist triển khai

### Phase A
- [x] Expose `NotifyDashboardRefreshAsync(Guid tenantId)` trong `IRealtimeNotificationService`
- [x] Implement trong `SignalRNotificationService` — gửi `dashboard.refresh` tới group
- [x] Thêm call trong `PayrollService.PublishPayrollAsync`
- [x] Thêm call trong `PayrollService.PublishAllDraftAsync`
- [x] Thêm call trong `PayrollService.MarkAsPaidAsync`
- [x] Thêm call trong `AttendanceService.ProcessAppealAsync` (cả approve lẫn reject)
- [x] Thêm `NotifyAppealSubmittedAsync` vào interface + implementation
- [x] Thêm call trong `AttendanceService.SubmitAppealAsync`
- [x] Thêm `NotifyPayrollPaidAsync` vào interface + implementation
- [x] Thêm call trong `PayrollService.MarkAsPaidAsync`

### Phase B
- [x] Thêm `NotifyShiftAssignedAsync` vào interface + implementation
- [x] Inject `IRealtimeNotificationService` vào `ShiftManagementService`
- [x] Thêm call trong `ShiftManagementService.BulkAssignPatternAsync`
- [x] Thêm `NotifyBonusDeductionEntryAddedAsync` vào interface + implementation
- [x] Thêm call trong `PayrollService.CreateEntryAsync`
- [x] Thêm call trong `PayrollService.CreateBulkEntriesAsync`
- [x] Thêm `NotifyAttendanceManualAdjustedAsync` vào interface + implementation
- [x] Thêm call trong `AttendanceService.ManualPunchAsync`

### Phase C
- [x] Thêm `NotifyPayrollGeneratedAsync` vào interface + implementation
- [x] Thêm call trong `PayrollService.GenerateMonthlyPayrollAsync`
- [x] Thêm call trong `PayrollService.CalculatePayrollForEmployeeAsync` (với `suppressGenerateNotify` flag)
- [x] Thêm `NotifyEmployeeOnboardedAsync` vào interface + implementation
- [x] Inject `IRealtimeNotificationService` vào `InviteService`
- [x] Thêm call trong `InviteService.CompleteOnboardingInternalAsync`
- [x] Thêm call `NotifyDashboardRefreshAsync` trong `InviteService.CompleteOnboardingInternalAsync`
