# Báo cáo lỗi Backend phát hiện khi tích hợp Flutter app

**Nguồn:** Phát hiện trong quá trình đối chiếu contract và tích hợp app Flutter (`dodo_system_app`) với API Backend thật, ghi nhận tại `docs/TASKS_TRIEN_KHAI_THEO_API_BE.md`. App đã tự xử lý phòng thủ ở phía client cho các gap nghiêm trọng, nhưng root cause vẫn nằm ở Backend và cần được sửa tại nguồn.

**Cách đọc:** Mỗi mục có ID gap, mức độ, vị trí code chính xác (đã đối chiếu lại trực tiếp với source hiện tại), mô tả lỗi, ảnh hưởng thực tế và đề xuất hướng sửa.

---

## Tóm tắt

| ID | Mức độ | Loại | File chính |
|---|---|---|---|
| [BE-MGR-05](#be-mgr-05-manual-timesheet-không-lọc-phạm-vi-manager) | 🔴 Nghiêm trọng | Lộ dữ liệu ngoài phạm vi | `ManualTimesheetService.cs` |
| [BE-LEAVE-01](#be-leave-01-duyệttừ-chối-nghỉ-phép-không-kiểm-tra-phạm-vi-manager) | 🔴 Nghiêm trọng | Lộ dữ liệu + bypass authorization | `LeaveRequestService.cs`, `LeaveRequestRepository.cs` |
| [BE-HR-01](#be-hr-01-includedeleted-xóa-luôn-rào-chắn-tenant) | 🔴 Nghiêm trọng | Rò rỉ dữ liệu chéo Tenant | `ShiftPatternRepository.cs` |
| [BE-MGR-02](#be-mgr-02-nhân-viên-đã-xóa-mềm-vẫn-hiển-thị-như-đang-hoạt-động) | 🔴 Nghiêm trọng | Thiếu soft-delete filter | `EmployeeRepository.cs`, `SMEFLOWSystemContext.cs` |
| [BE-AUTH-01](#be-auth-01-moduleSubscriptionsController-thiếu-authorize) | 🔴 Nghiêm trọng | Thiếu authorization | `ModuleSubscriptionsController.cs` |
| [BE-ATT-01](#be-att-01-không-có-idempotency-cho-submit-punch) | 🟠 Cao | Thiếu ràng buộc server | `AttendanceService.cs` |
| [BE-ATT-02](#be-att-02-endpoint-multipart-nuốt-mọi-lỗi-nghiệp-vụ-thành-404) | 🟠 Cao | Sai mã lỗi / mất thông tin | `AttendanceController.cs` |
| [BE-MGR-06](#be-mgr-06-lỗi-nghiệp-vụ-không-có-kiểu-rơi-vào-500-toàn-hệ-thống) | 🟠 Cao | Thiếu exception handling toàn cục | `PayrollService.cs` + toàn bộ pipeline |

---

## BE-MGR-05: Manual timesheet không lọc phạm vi Manager

**Mức độ:** 🔴 Nghiêm trọng — lộ dữ liệu ngoài phạm vi được giao.

**Vị trí:** `SMEFLOWSystem.Application/Services/ManualTimesheetService.cs:66-72`

```csharp
public async Task<List<ManualMonthlyTimesheetDto>> GetByMonthAsync(Guid tenantId, int month, int year)
{
    _currentUser.EnsureHrAccess();

    var list = await _manualTimesheetRepo.GetByTenantMonthYearAsync(tenantId, month, year);
    return _mapper.Map<List<ManualMonthlyTimesheetDto>>(list);
}
```

**Mô tả:** `EnsureHrAccess()` cho phép cả role `Manager` gọi endpoint này, nhưng hàm chỉ lọc theo `tenantId` — không gọi `IHrAuthorizationService.GetAccessibleDepartmentIdsAsync()` như mọi service HR khác cùng nhóm (`HrEmployeeService`, `ShiftManagementService`, `AttendanceService.GetHRMonthlyReportAsync`).

**Ảnh hưởng:** Một Manager chỉ được giao 1 phòng ban vẫn nhận được bảng công nhập tay của **toàn bộ nhân viên trong Tenant**, kể cả phòng ban không thuộc quyền quản lý.

**Cách tái hiện:** Đăng nhập bằng tài khoản Manager chỉ được gán 1 phòng ban → gọi `GET /api/hr/manual-timesheets?month=X&year=Y` → response chứa nhân viên của phòng ban khác.

**Đề xuất sửa:** Thêm tham số `departmentIds` vào `GetByTenantMonthYearAsync`, gọi `_hrAuth.GetAccessibleDepartmentIdsAsync()` khi caller là Manager (không phải Admin/HRManager) và lọc theo `Employee.DepartmentId`, cùng pattern đã dùng ở `HrEmployeeService.GetPagedAsync`.

**Ghi chú:** App hiện đang tự lọc lại ở client bằng cách join với `GET /api/hr/employees` (đã lọc đúng phạm vi) — đây chỉ là workaround UX, không thay thế được việc chặn ở server.

---

## BE-LEAVE-01: Duyệt/từ chối nghỉ phép không kiểm tra phạm vi Manager

**Mức độ:** 🔴 Nghiêm trọng — lộ dữ liệu **và** cho phép thao tác ghi ngoài phạm vi.

**Vị trí:**
- `SMEFLOWSystem.Infrastructure/Repositories/LeaveRequestRepository.cs:62-81` (`GetPendingAsync`, `GetAllAsync`)
- `SMEFLOWSystem.Application/Services/LeaveRequestService.cs:271-350` (`ApproveLeaveRequestAsync`, `RejectLeaveRequestAsync`, `GetPendingRequestsAsync`, `GetAllRequestsAsync`)

```csharp
// LeaveRequestRepository.cs
public async Task<List<LeaveRequest>> GetPendingAsync()
{
    return await _context.LeaveRequests
        .Include(r => r.Segments).ThenInclude(s => s.TargetShiftSegment)
        .Include(r => r.Employee)
        .Include(r => r.LeaveTypeNavigation)
        .Where(r => r.Status == "Pending")
        .ToListAsync();          // không lọc theo phòng ban
}
```

```csharp
// LeaveRequestService.cs
public async Task<LeaveRequestDto> ApproveLeaveRequestAsync(Guid hrUserId, Guid requestId, ApproveLeaveRequestDto dto)
{
    var tenantId = _currentTenantService.TenantId ?? throw new UnauthorizedAccessException(...);
    var request = await _leaveRequestRepository.GetByIdAsync(requestId) ?? throw new KeyNotFoundException(...);

    request.Approve(hrUserId, dto.ApproverNote);   // không kiểm tra request.Employee có thuộc phạm vi Manager không
    ...
}
```

**Mô tả:** Hai vấn đề cộng dồn:
1. `GetPendingAsync`/`GetAllAsync` không lọc theo `GetAccessibleDepartmentIdsAsync()` — trả toàn bộ đơn nghỉ phép của Tenant.
2. `ApproveLeaveRequestAsync`/`RejectLeaveRequestAsync` chỉ dùng `hrUserId` để **ghi nhận người duyệt**, không dùng để **authorize** — không có bước gọi `_hrAuth.EnsureEmployeeAccessAsync(request.Employee)` trước khi cho approve/reject.

**Ảnh hưởng:** Manager có thể xem **và duyệt/từ chối** đơn nghỉ phép của bất kỳ nhân viên nào trong Tenant, không giới hạn ở phòng ban được giao. Đây là lỗ hổng authorization thực sự (không chỉ là thiếu field), vì cho phép ghi dữ liệu ngoài phạm vi.

**Cách tái hiện:** Đăng nhập Manager phòng ban A → gọi `POST /api/v1/leaves/{id}/approve` với `id` là đơn của nhân viên phòng ban B → request thành công.

**Đề xuất sửa:**
1. Thêm overload `GetPendingAsync(IEnumerable<Guid>? departmentIds)` / `GetAllAsync(IEnumerable<Guid>? departmentIds)`, lọc theo `r.Employee.DepartmentId` khi caller là Manager.
2. Trong `ApproveLeaveRequestAsync`/`RejectLeaveRequestAsync`, thêm bước: nếu người gọi là Manager (không phải Admin/HRManager) thì `await _hrAuth.EnsureEmployeeAccessAsync(request.Employee)` trước khi gọi `request.Approve(...)`/`request.Reject(...)`, ném `403` nếu ngoài phạm vi — đúng pattern đã áp dụng ở `AttendanceService.ManualPunchAsync`/`RecalculateAttendanceAsync`.

---

## BE-HR-01: `includeDeleted` xóa luôn rào chắn Tenant

**Mức độ:** 🔴 Nghiêm trọng — rò rỉ dữ liệu chéo Tenant.

**Vị trí:** `SMEFLOWSystem.Infrastructure/Repositories/ShiftPatternRepository.cs:100-131`

```csharp
public async Task<(List<ShiftPattern> Items, int TotalCount)> GetPagedAsync(
    string? search, bool includeDeleted, int pageNumber, int pageSize)
{
    var query = _context.ShiftPatterns
        .AsNoTracking()
        .Include(sp => sp.Days).ThenInclude(d => d.ScheduledShift).ThenInclude(s => s.Segments)
        .AsQueryable();

    if (includeDeleted)
        query = query.IgnoreQueryFilters();   // <-- BUG: gỡ luôn TenantId filter, không chỉ IsDeleted
    ...
}
```

**Mô tả:** Global query filter của `ShiftPattern` trong `SMEFLOWSystemContext.cs:99` chỉ có `HasQueryFilter(e => e.TenantId == _currentTenantService.TenantId)` — **không có** điều kiện `IsDeleted` trong cùng filter (soft-delete được xử lý riêng, không qua global filter này). Vì vậy gọi `IgnoreQueryFilters()` để "bao gồm cả bản ghi đã xóa" vô tình **gỡ luôn điều kiện TenantId**, khiến `includeDeleted=true` trả về lịch ca của **mọi Tenant khác** trong hệ thống, không chỉ của Tenant hiện tại.

Cùng pattern lỗi này lặp lại y hệt ở `ShiftRepository.cs:19-32` (`Shift.GetPagedAsync` với tham số `includeDeleted`).

**Ảnh hưởng:** Bất kỳ request nào gọi được endpoint với `includeDeleted=true` sẽ thấy dữ liệu lịch ca xuyên Tenant — vi phạm cách ly dữ liệu multi-tenant nghiêm trọng nhất trong danh sách này.

**Cách tái hiện:** Gọi endpoint danh mục lịch ca (`GET /api/hr/shift-patterns` hoặc tương đương) với `includeDeleted=true` → response chứa `ShiftPattern` có `TenantId` khác Tenant hiện tại.

**Đề xuất sửa:** Không dùng `IgnoreQueryFilters()` để lấy bản ghi đã xóa. Thay bằng cách filter tường minh:
```csharp
var query = _context.ShiftPatterns.IgnoreQueryFilters()
    .Where(x => x.TenantId == _currentTenantService.TenantId);   // tự áp lại TenantId
if (!includeDeleted)
    query = query.Where(x => !x.IsDeleted);
```
Hoặc tốt hơn: thêm `IsDeleted` vào chính `HasQueryFilter` của `ShiftPattern`/`Shift` (`e => e.TenantId == tenantId && !e.IsDeleted`) rồi chỉ `IgnoreQueryFilters()` + tự re-apply điều kiện `TenantId` khi cần xem cả bản ghi đã xóa — không bao giờ gỡ điều kiện Tenant. Áp dụng sửa đồng thời cho `ShiftRepository.cs`.

---

## BE-MGR-02: Nhân viên đã xóa mềm vẫn hiển thị như đang hoạt động

**Mức độ:** 🔴 Nghiêm trọng — thiếu soft-delete filter, không thể phân biệt được từ response.

**Vị trí:**
- `SMEFLOWSystem.Infrastructure/Repositories/EmployeeRepository.cs:18-147` (`GetByIdAsync`, `GetPagedAsync`, `GetByDepartmentIdAsync`)
- `SMEFLOWSystem.Infrastructure/Data/SMEFLOWSystemContext.cs:80` — `modelBuilder.Entity<Employee>().HasQueryFilter(e => e.TenantId == _currentTenantService.TenantId);` (chỉ có `TenantId`, không có `IsDeleted`)
- `SoftDeleteResignedAsync` (dòng 64-70) set `employee.IsDeleted = true` nhưng không có nơi nào query lọc lại field này.

**Mô tả:** `Employee` có field `IsDeleted` và được set `true` khi xóa mềm (`SoftDeleteResignedAsync`), nhưng:
1. Global query filter chỉ theo `TenantId`, không loại `IsDeleted`.
2. `GetPagedAsync`, `GetByIdAsync`, `GetByDepartmentIdAsync` đều không tự thêm `Where(e => !e.IsDeleted)`.
3. `EmployeeDto` không expose field `isDeleted` ra response.

**Ảnh hưởng:** Sau khi xóa một nhân viên, nhân viên đó **vẫn xuất hiện y hệt nhân viên đang hoạt động** trong danh sách/chi tiết — không có field nào để client phân biệt hay lọc. Không thể xây tính năng "khôi phục nhân viên đã xóa" vì không biết ai đã bị xóa.

**Đề xuất sửa:**
1. Thêm `IsDeleted` vào `EmployeeDto`.
2. Mặc định lọc `!IsDeleted` trong `GetPagedAsync`/`GetByDepartmentIdAsync` trừ khi caller chủ động truyền cờ `includeDeleted=true` (áp dụng đúng pattern `IgnoreQueryFilters` đã sửa ở BE-HR-01 — nhớ tự re-apply `TenantId` để tránh lặp lại lỗi rò rỉ chéo Tenant).
3. Thêm tham số lọc `status=deleted` hoặc `includeDeleted` vào `EmployeeQueryDto` để FE/App có thể liệt kê nhân viên đã xóa và làm màn "khôi phục".

---

## BE-AUTH-01: `ModuleSubscriptionsController` thiếu `[Authorize]`

**Mức độ:** 🔴 Nghiêm trọng — thiếu authorization hoàn toàn ở tầng server.

**Vị trí:** `SMEFLOWSystem.WebAPI/Controllers/ModuleSubscriptionsController.cs:1-88`

```csharp
[Route("api/[controller]")]
[ApiController]
public class ModuleSubscriptionsController : ControllerBase   // <-- không có [Authorize] ở class
{
    ...
    [HttpDelete("me/cancel/{moduleId:int}")]
    public async Task<IActionResult> CancelMyModuleSubscription([FromRoute] int moduleId)   // <-- không có [Authorize(Policy = ...)]
    {
        try { await _service.CancelMyModuleSubscriptionAsync(moduleId); ... }
        catch (UnauthorizedAccessException) { return Unauthorized(); }
        catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
    }
}
```

**Mô tả:** Toàn bộ controller không có attribute `[Authorize]` ở class level, và endpoint `me/cancel/{moduleId}` (đáng lẽ chỉ `TenantAdmin` được hủy subscription) cũng không có `[Authorize(Policy = PolicyNames.TenantAdmin)]`. Việc "chặn" `UnauthorizedAccessException` trong catch block chỉ có tác dụng nếu `_service` tự ném ra ngoại lệ đó — cần xác minh `ModuleSubscriptionService` có tự kiểm tra role hay không; nếu không, endpoint này **hoàn toàn public với bất kỳ ai gửi được Bearer token hợp lệ** của bất kỳ role nào, kể cả Employee.

**Ảnh hưởng:** Một nhân viên thường (role `Employee`) có thể tự hủy subscription module (`PAYROLL`, `ATTENDANCE`,...) của toàn Tenant nếu service không tự chặn theo role.

**Đề xuất sửa:** Thêm `[Authorize]` ở class level cho toàn bộ controller (mọi endpoint đều cần đăng nhập tối thiểu), và thêm `[Authorize(Policy = PolicyNames.TenantAdmin)]` riêng cho `CancelMyModuleSubscription`. Kiểm tra lại `ModuleSubscriptionService` xem có logic authorize nội bộ nào đang bù đắp cho thiếu sót này không — nếu không, đây là lỗ hổng cần vá ngay.

---

## BE-ATT-01: Không có idempotency cho submit punch

**Mức độ:** 🟠 Cao — thiếu ràng buộc chống trùng lặp ở server.

**Vị trí:** `SMEFLOWSystem.Application/Services/AttendanceService.cs:75-155` (`SubmitPunchAsync`)

```csharp
public async Task<RawPunchLogDto> SubmitPunchAsync(Guid userId, SubmitPunchRequestDto request)
{
    var employee = await _employeeRepository.GetByUserIdAsync(userId);
    ...
    var punch = new RawPunchLog() { EmployeeId = employee.Id, Timestamp = DateTime.UtcNow, ... };
    await _punchLogRepo.AddAsync(punch);   // luôn insert, không có kiểm tra trùng tại đây
    ...
}
```

**Mô tả:** Mỗi lần gọi `SubmitPunchAsync` đều insert thẳng một `RawPunchLog` mới, không có idempotency key, không unique constraint theo `(EmployeeId, khoảng thời gian ngắn)`, không kiểm tra request trùng lặp tại thời điểm nhận. Việc loại trùng chỉ xảy ra **về sau**, khi `AttendanceResolutionService` xử lý batch các `RawPunchLog` để tính công.

**Ảnh hưởng:** Retry mạng, double-tap trên nhiều thiết bị, hoặc client bug đều có thể tạo nhiều `RawPunchLog` cho cùng một lần chấm công thực tế. App hiện có cooldown 2 phút ở client để giảm thao tác lặp, nhưng đây chỉ là UX, **không chống được** multi-device hoặc client bị bypass.

**Đề xuất sửa:** Thêm kiểm tra tại `SubmitPunchAsync`: trước khi insert, query `RawPunchLog` gần nhất của `employee.Id` trong cửa sổ N giây (ví dụ theo `AttendanceResolutionOptions.DedupWindowMinutes` đã có sẵn ở service resolution) và trả về log cũ (hoặc từ chối) thay vì luôn insert mới. Cân nhắc thêm unique constraint dạng `(EmployeeId, DATE_TRUNC('minute', Timestamp))` nếu nghiệp vụ cho phép, hoặc idempotency key do client sinh và gửi kèm request.

---

## BE-ATT-02: Endpoint multipart nuốt mọi lỗi nghiệp vụ thành 404

**Mức độ:** 🟠 Cao — sai mã lỗi, mất thông tin nghiệp vụ cho client.

**Vị trí:** `SMEFLOWSystem.WebAPI/Controllers/AttendanceController.cs:56-77` (`SubmitPunchForm`)

```csharp
[HttpPost("submit-punch-form")]
[Consumes("multipart/form-data")]
public async Task<IActionResult> SubmitPunchForm([FromForm] SubmitPunchRequestDto request, IFormFile? selfie)
{
    ...
    try
    {
        var result = await _service.SubmitPunchAsync(userId, request);
        return Ok(new { Data = result, Message = "Punch submitted successfully" });
    }
    catch (InvalidOperationException)                                    // <-- bắt MỌI InvalidOperationException
    {
        return NotFound(new { Error = "Employee not found for current user." });   // <-- luôn trả message này
    }
}
```

So sánh với endpoint JSON tương đương (`SubmitPunch`, dòng 42-52) xử lý đúng:
```csharp
catch (InvalidOperationException ex) when (ex.Message.StartsWith("Employee not found"))
{
    return NotFound(new { Error = ex.Message });
}
catch (InvalidOperationException ex)
{
    return BadRequest(new { Error = ex.Message });
}
```

**Mô tả:** `SubmitPunchAsync` ném `InvalidOperationException` cho nhiều tình huống khác nhau — `"Employee not found..."`, `"FakeGPS: ..."`, ngoài vùng geofence, thiếu GPS bắt buộc, v.v. Endpoint JSON (`submit-punch`) phân biệt đúng bằng `when (ex.Message.StartsWith(...))`, nhưng endpoint multipart (`submit-punch-form`, dùng khi có selfie) **bắt gộp mọi `InvalidOperationException` và luôn trả cứng `404 "Employee not found for current user."`** — kể cả khi lỗi thật là FakeGPS hoặc ngoài vùng.

**Ảnh hưởng:** Khi chấm công kèm ảnh selfie, client nhận sai status code (`404` thay vì `400`) và sai message hoàn toàn (luôn báo "không tìm thấy nhân viên" dù lỗi thật là GPS giả hoặc ngoài vùng) — không thể hiển thị đúng lý do cho người dùng.

**Đề xuất sửa:** Áp dụng lại đúng pattern đã có ở endpoint JSON:
```csharp
catch (InvalidOperationException ex) when (ex.Message.StartsWith("Employee not found"))
{
    return NotFound(new { Error = ex.Message });
}
catch (InvalidOperationException ex)
{
    return BadRequest(new { Error = ex.Message });
}
```

---

## BE-MGR-06: Lỗi nghiệp vụ không có kiểu rơi vào 500 toàn hệ thống

**Mức độ:** 🟠 Cao — ảnh hưởng diện rộng, xuất hiện lặp lại ở rất nhiều controller (payroll, role, invites...).

**Vị trí ví dụ:** `SMEFLOWSystem.Application/Services/PayrollService.cs:621-646` (`UpdateManualFieldsAsync`)

```csharp
public async Task<PayrollDto> UpdateManualFieldsAsync(Guid payrollId, UpdatePayrollDto dto)
{
    var payroll = await _payrollRepository.GetByIdAsync(payrollId);
    if (payroll == null) throw new Exception("Không tìm thấy phiếu lương.");   // <-- Exception trần

    if (!_currentUser.IsAdmin() && !_currentUser.IsHrManager())
    {
        var employee = await _employeeRepository.GetByIdAsync(payroll.EmployeeId);
        if (employee == null) throw new KeyNotFoundException("Employee not found");
        await _hrAuth.EnsureEmployeeAccessAsync(employee);   // ném UnauthorizedAccessException nếu ngoài phạm vi
    }

    if (payroll.Status != PayrollStatus.Draft)
        throw new Exception("Chỉ được cập nhật thông tin khi phiếu lương đang ở trạng thái Nháp (Draft).");   // <-- Exception trần
    ...
}
```

**Mô tả:** Toàn bộ pipeline WebAPI (đã kiểm tra `Program.cs`/`WebApplicationExtensions`) **không có bất kỳ** `UseExceptionHandler`, exception filter, hay middleware bắt lỗi toàn cục nào (`grep "UseExceptionHandler|ExceptionFilter|ExceptionMiddleware"` trong `SMEFLOWSystem.WebAPI` không có kết quả). Vì vậy mọi `Exception`/`KeyNotFoundException`/`UnauthorizedAccessException` ném ra từ service mà controller không tự `try/catch` đúng loại sẽ rơi thẳng vào response `500` mặc định của ASP.NET Core — không phân biệt được "not found" (nên là 404), "sai trạng thái" (nên là 400/409), hay "ngoài phạm vi" (nên là 403).

Đây không phải lỗi cục bộ của payroll — cùng pattern xuất hiện ở `RoleController.GetById` (id không tồn tại → 500 thay vì 404, ghi nhận tại `docs/TASKS_TRIEN_KHAI_THEO_API_BE.md:1168`), invite/set-role, và nhiều endpoint payroll khác (`mark-paid`, `bulk-bonus-penalty`...).

**Ảnh hưởng:** Client (Flutter app, FE Web) không thể hiển thị thông báo lỗi chính xác cho người dùng cuối; mọi lỗi nghiệp vụ đều hiện chung chung "Lỗi máy chủ, vui lòng thử lại sau" thay vì lý do thật ("phiếu lương không ở trạng thái Nháp", "bạn không có quyền với nhân viên này"...). Về lâu dài cũng gây khó khăn khi giám sát lỗi (mọi thứ đều log là 500).

**Đề xuất sửa (một lần, áp dụng toàn hệ thống):**
1. Thêm global exception handling middleware (`IExceptionHandler` của .NET 8, hoặc middleware tự viết) map:
   - `KeyNotFoundException` → `404`
   - `UnauthorizedAccessException` → `403`
   - `InvalidOperationException`/business-rule exception (nên tạo riêng class `BusinessRuleException`) → `400`/`409`
   - còn lại (`Exception` chung, lỗi hạ tầng thật) → `500`
2. Dần thay các `throw new Exception("...")` bằng exception có kiểu rõ ràng (`KeyNotFoundException`, `UnauthorizedAccessException`, hoặc custom `BusinessRuleException`) ở các service, bắt đầu từ `PayrollService`, `RoleService`, `InviteService` vì đã được ghi nhận cụ thể.
3. Không cần sửa từng controller catch block riêng lẻ nữa sau khi có middleware — có thể dọn bớt các `try/catch` lặp lại hiện tại.

---

## Ghi chú chung

- Tất cả các gap trên đã được app Flutter xử lý phòng thủ ở client (lọc lại dữ liệu, validate trước khi gửi, ẩn UI theo trạng thái...) — nhưng đây **không phải lớp bảo vệ thay thế cho Backend**. Đề nghị ưu tiên sửa theo thứ tự: `BE-HR-01` (rò rỉ chéo Tenant) → `BE-AUTH-01` (thiếu authorize) → `BE-LEAVE-01`/`BE-MGR-05`/`BE-MGR-02` (lộ dữ liệu ngoài phạm vi) → `BE-ATT-01`/`BE-ATT-02` → `BE-MGR-06` (ảnh hưởng UX diện rộng nhưng không phải lỗ hổng bảo mật).
- Sau khi sửa, cần thông báo lại để phía app gỡ bỏ các đoạn xử lý phòng thủ tương ứng (đã chú thích rõ gap ID trong code Flutter) và bổ sung test hồi quy phía Backend cho từng case.
