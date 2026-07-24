# Kế hoạch nâng cấp Backend cho SystemAdmin — Code-only

**Ngày cập nhật:** 2026-07-23  
**Phạm vi:** Backend `DodoSystem-BE`  
**Trạng thái:** Kế hoạch triển khai  
**Nguyên tắc bắt buộc:** Chỉ sửa code, không thay đổi database.

---

## 1. Ràng buộc triển khai

Kế hoạch này tuân thủ các giới hạn sau:

- Không thêm, xóa hoặc đổi tên bảng.
- Không thêm, xóa hoặc đổi kiểu cột.
- Không tạo EF Core migration.
- Không sửa migration hoặc model snapshot hiện tại.
- Không chạy SQL backfill.
- Không tạo index mới.
- Không thay đổi dữ liệu seed đang có.
- Không yêu cầu reset hoặc recreate database.
- Không sửa dữ liệu production bằng tay.
- Chỉ đọc và cập nhật các trường hiện đã tồn tại.
- Ưu tiên API read-only trước các API thay đổi trạng thái.
- Giữ tương thích với endpoint FE đang sử dụng.

Các thư mục không được thay đổi trong kế hoạch này:

```text
SMEFLOWSystem.Infrastructure/Migrations/
SMEFLOWSystem.Infrastructure/Data/Configurations/
```

Các entity hiện tại cũng không cần thêm field:

```text
SMEFLOWSystem.Core/Entities/Tenant.cs
SMEFLOWSystem.Core/Entities/ModuleSubscription.cs
SMEFLOWSystem.Core/Entities/BillingOrder.cs
SMEFLOWSystem.Core/Entities/PaymentTransaction.cs
```

Nếu một chức năng cần schema mới để làm chính xác, tài liệu sẽ ghi rõ giới hạn và không triển khai phần đó.

---

## 2. Mục tiêu sau khi hoàn thành

Không thay đổi database nhưng vẫn cung cấp được cho FE:

1. Dashboard tổng quan toàn hệ thống.
2. Thống kê tenant theo trạng thái.
3. Thống kê module đang được sử dụng.
4. Thống kê module bị hủy theo dữ liệu hiện có.
5. Danh sách tenant có tìm kiếm, lọc và sắp xếp.
6. Chi tiết tenant có owner, số user và module.
7. Danh sách module và thao tác quản lý module.
8. Danh sách hóa đơn toàn hệ thống.
9. Danh sách giao dịch thanh toán toàn hệ thống.
10. Danh sách user của một tenant.
11. Log có cấu trúc cho các thao tác SystemAdmin.

---

## 3. Hiện trạng Backend

## 3.1. Dashboard

Controller:

```text
SMEFLOWSystem.WebAPI/Controllers/System/SystemDashboardController.cs
```

API hiện có:

```http
GET /api/system/dashboard/module-usage?month={month}&year={year}
GET /api/system/dashboard/module-cancellations?month={month}&year={year}
```

Vấn đề:

- Chỉ có hai thống kê.
- Chưa có KPI tổng tenant.
- Chưa có tenant active, suspended, trial hoặc pending payment.
- Chưa có tenant mới trong tháng.
- Chưa có tenant sắp hết hạn.
- Query tải toàn bộ subscription lên memory rồi mới group.
- API chưa validate `month` và `year`.
- Cách tính cancellation hiện tại không khớp luồng hủy thực tế.

## 3.2. Tenant

Controller:

```text
SMEFLOWSystem.WebAPI/Controllers/System/SystemTenantsController.cs
```

API hiện có:

```http
GET /api/system/tenants?pageNumber={pageNumber}&pageSize={pageSize}
GET /api/system/tenants/{tenantId}
```

Vấn đề:

- Không search.
- Không filter status.
- Không filter module.
- Không filter sắp hết hạn.
- Không sort theo yêu cầu FE.
- Danh sách chỉ trả `OwnerUserId`, không có tên/email owner.
- Không có số user.
- Không có số module active.
- Tenant nội bộ `SYSTEM` có thể xuất hiện như khách hàng.
- Chi tiết tenant có thể trả `ModuleName` rỗng.

## 3.3. Module

Controller:

```text
SMEFLOWSystem.WebAPI/Controllers/ModulesController.cs
```

API hiện có:

```http
GET  /api/modules/active
GET  /api/modules/all
POST /api/modules
PUT  /api/modules/{id}/deactivate
```

Vấn đề:

- Chưa cập nhật tên, mô tả hoặc giá.
- Chưa kích hoạt lại module.
- `GET /api/modules/all` chưa giới hạn cho SystemAdmin.

## 3.4. Role

Controller:

```text
SMEFLOWSystem.WebAPI/Controllers/RoleController.cs
```

Vấn đề:

- Update role kiểm tra trùng tên nhưng không loại trừ chính role đang sửa.
- Client có thể truyền `IsSystemRole`.
- Các system role cần được bảo vệ vì authorization policy phụ thuộc tên role.
- Custom role hiện không tự tạo ra permission mới.

## 3.5. Billing

Hiện SystemAdmin chỉ có API xem module line khi đã biết `billingOrderId`:

```http
GET /api/billingordermodules/by-billing-order-id-ignore-tenant/{billingOrderId}
```

Vấn đề:

- Không có danh sách hóa đơn toàn hệ thống.
- Không có chi tiết hóa đơn đầy đủ.
- Không có danh sách payment transaction.
- API line hiện tại khó dùng vì FE không có nguồn lấy `billingOrderId`.

---

## 4. Những việc có thể và không thể làm khi không sửa database

## 4.1. Có thể làm hoàn toàn bằng code

- Loại tenant `SYSTEM` khỏi danh sách và dashboard.
- Search/filter/sort tenant.
- Projection owner và user count.
- Sửa tên module bị rỗng.
- Thêm dashboard overview.
- Aggregate trong PostgreSQL thay vì application memory.
- Validate request.
- Sửa role duplicate check.
- Bảo vệ system role.
- Cập nhật và kích hoạt lại module.
- Liệt kê subscription hiện có.
- Liệt kê billing order hiện có.
- Liệt kê payment transaction hiện có.
- Log thao tác bằng `ILogger`.
- Thêm unit test và integration test.

## 4.2. Không thể làm đầy đủ nếu không sửa database

### Audit log lưu vĩnh viễn trong database

Không có bảng audit chung cho SystemAdmin. Thay thế code-only:

- Dùng structured logging qua `ILogger`.
- Log `ActorUserId`, action, resource, before/after và reason.
- Không coi đây là audit ledger bất biến.

### Permission matrix cho custom role

Hiện không có bảng permission/role-permission. Vì vậy:

- Không xây permission matrix.
- Không hứa rằng role mới sẽ có quyền API.
- Chỉ bảo vệ các role hệ thống hiện tại.

### Lý do suspend/cancel lưu lâu dài

Không có cột lưu reason. Code-only chỉ có thể:

- Nhận reason trong request.
- Ghi reason vào application log.
- Không thể truy vấn lại reason từ database.

### Lịch sử cancellation tuyệt đối chính xác

Không có `CancelledAt`. Tuy nhiên luồng hủy hiện tại đã:

```text
IsDeleted = true
Status = Suspended
UpdatedAt = thời điểm repository update
```

Do đó có thể dùng bộ điều kiện trên làm cancellation proxy. Cần ghi rõ đây là số liệu suy ra từ dữ liệu hiện có.

---

## 4.3. API reset bootstrap SystemAdmin

### Mục đích

Hiện `POST /api/system/bootstrap` chỉ chạy khi chưa có user mang role `SystemAdmin`.

Trong quá trình development hoặc staging, nếu đã bootstrap sai tài khoản, cần một API để:

1. Xóa liên kết role `SystemAdmin` của user bootstrap hiện tại.
2. Xóa refresh token của user đó.
3. Xóa user bootstrap.
4. Xóa tenant nội bộ `SYSTEM`.
5. Giữ nguyên bản ghi role `SystemAdmin`.
6. Cho phép gọi lại `POST /api/system/bootstrap`.

API này chỉ phục vụ reset môi trường thử nghiệm. Không phải chức năng quản trị tenant thông thường.

### Route đề xuất

```http
DELETE /api/system/bootstrap
```

Hoặc route mô tả rõ hơn:

```http
DELETE /api/system/bootstrap/reset
```

Khuyến nghị dùng:

```http
DELETE /api/system/bootstrap/reset
```

để tránh nhầm với endpoint tạo bootstrap.

### Tuyệt đối không `AllowAnonymous`

Endpoint reset phải:

```csharp
[Authorize(Policy = PolicyNames.SystemAdmin)]
```

Người gọi phải đăng nhập bằng chính tài khoản SystemAdmin hiện tại.

### Chỉ mở ở Development/Staging

Thêm config code-only:

```json
{
  "SystemBootstrap": {
    "AllowReset": false
  }
}
```

Production luôn để:

```text
SystemBootstrap__AllowReset=false
```

Điều kiện cho phép:

```csharp
var environmentAllowed =
    environment.IsDevelopment() || environment.IsStaging();

var configAllowed =
    configuration.GetValue<bool>("SystemBootstrap:AllowReset");

if (!environmentAllowed || !configAllowed)
{
    return NotFound();
}
```

Trả `404` thay vì tiết lộ endpoint destructive đang tồn tại trên production.

Không cho phép bật reset chỉ bằng request/header từ client.

### Request xác nhận

```csharp
public sealed class SystemBootstrapResetRequestDto
{
    public string Confirmation { get; init; } = string.Empty;
    public string CurrentPassword { get; init; } = string.Empty;
}
```

JSON:

```json
{
  "confirmation": "RESET_SYSTEM_BOOTSTRAP",
  "currentPassword": "mat-khau-hien-tai"
}
```

Validation:

- `Confirmation` phải khớp chính xác `RESET_SYSTEM_BOOTSTRAP`.
- `CurrentPassword` bắt buộc.
- User ID lấy từ `ClaimTypes.NameIdentifier`, không lấy từ body.
- Verify password với user SystemAdmin đang đăng nhập.
- Không nhận `tenantId`, `userId` hoặc email do client truyền vào.

Điều này tránh client chỉ định nhầm một tenant khách hàng để xóa.

### Nhận diện chính xác dữ liệu được phép xóa

Service phải tự tìm target từ server:

1. Lấy `actorUserId` từ JWT.
2. Query user bằng `IgnoreQueryFilters()`.
3. Kiểm tra user chưa bị xóa và đang active.
4. Kiểm tra user có role name chính xác `SystemAdmin`.
5. Lấy `user.TenantId`.
6. Query tenant bằng `IgnoreQueryFilters()`.
7. Kiểm tra:

```text
Tenant.Name == SystemTenantConstants.Name
Tenant.OwnerUserId == actorUserId
User.TenantId == Tenant.Id
```

Chỉ khi tất cả điều kiện đúng mới được tiếp tục.

Không dùng điều kiện duy nhất `Tenant.Name == "SYSTEM"` để thực hiện xóa.

### Preflight an toàn

Tenant bootstrap bình thường chỉ nên có:

- Một tenant `SYSTEM`.
- Một owner user.
- Một `UserRole` SystemAdmin.
- Có thể có refresh token do đã login.

Trước khi xóa, service kiểm tra tenant không có dữ liệu nghiệp vụ:

```text
Employees
Departments
Positions
Customers
ModuleSubscriptions
BillingOrders
PaymentTransactions
Orders
Payrolls
Invites
Notifications
TenantAttendanceSetting
Shifts/shift assignments
Attendance/timesheet data
Leave data
```

Repository phải kiểm tra tất cả bảng tenant-scoped đang tồn tại trong `SMEFLOWSystemContext`, ngoại trừ ba nhóm được phép xóa là:

```text
RefreshTokens của bootstrap user
UserRoles của bootstrap user
Bootstrap User
```

Nếu có bất kỳ dữ liệu nghiệp vụ nào, trả:

```http
409 Conflict
```

Ví dụ:

```json
{
  "title": "System bootstrap reset refused",
  "status": 409,
  "detail": "Tenant SYSTEM chứa dữ liệu ngoài phạm vi bootstrap và không thể reset tự động."
}
```

Không cố xóa dây chuyền toàn bộ các bảng của tenant.

Mục tiêu là reset đúng tenant bootstrap tối thiểu, không tạo một API “drop tenant” tổng quát.

### Thứ tự xóa

Các foreign key hiện tại không cấu hình cascade đầy đủ. Không được gọi xóa tenant trực tiếp.

Toàn bộ thao tác phải nằm trong một database transaction hiện có:

```csharp
await _transaction.ExecuteAsync(async () =>
{
    // 1. Re-query và kiểm tra lại target bên trong transaction.
    // 2. Xóa refresh token của bootstrap user.
    // 3. Xóa UserRole liên kết SystemAdmin.
    // 4. Set Tenant.OwnerUserId = null để bỏ FK NoAction tới User.
    // 5. Xóa bootstrap User.
    // 6. Xóa tenant SYSTEM.
});
```

Thứ tự chi tiết:

1. Xóa tất cả `RefreshTokens` có `TenantId` và `UserId` đúng target.
2. Xóa `UserRoles` có `TenantId`, `UserId` và `RoleId` SystemAdmin đúng target.
3. Đặt `Tenant.OwnerUserId = null`, save trong transaction.
4. Xóa user target.
5. Xóa tenant target.
6. Không xóa bản ghi trong bảng `Roles`.

Nếu bất kỳ bước nào lỗi:

- Rollback toàn bộ.
- Không để lại tenant mất owner hoặc user mất role một phần.

### Repository/service đề xuất

Application:

```text
SMEFLOWSystem.Application/DTOs/SystemDtos/SystemBootstrapResetRequestDto.cs
SMEFLOWSystem.Application/Interfaces/IServices/System/ISystemBootstrapResetService.cs
SMEFLOWSystem.Application/Services/System/SystemBootstrapResetService.cs
SMEFLOWSystem.Application/Interfaces/IRepositories/ISystemBootstrapResetRepository.cs
```

Infrastructure:

```text
SMEFLOWSystem.Infrastructure/Repositories/SystemBootstrapResetRepository.cs
```

WebAPI:

```text
SMEFLOWSystem.WebAPI/Controllers/System/SystemBootstrapController.cs
```

Repository chỉ expose use case hẹp:

```csharp
Task<SystemBootstrapResetTarget?> FindResetTargetAsync(
    Guid actorUserId,
    CancellationToken cancellationToken);

Task<SystemBootstrapDependencyCounts> GetDependencyCountsAsync(
    Guid tenantId,
    CancellationToken cancellationToken);

Task DeleteBootstrapIdentityAsync(
    Guid tenantId,
    Guid userId,
    int systemAdminRoleId,
    CancellationToken cancellationToken);
```

Không tạo:

```csharp
DeleteTenantByIdAsync(Guid tenantId)
```

vì phương thức tổng quát như vậy dễ bị tái sử dụng để xóa tenant khách hàng.

### Response thành công

```http
200 OK
```

```json
{
  "message": "System bootstrap account was reset successfully.",
  "bootstrapAvailable": true
}
```

Sau response:

- FE phải xóa access token và refresh token local.
- Chuyển người dùng về màn hình bootstrap/login.
- Có thể gọi lại `POST /api/system/bootstrap`.

Không trả lại password, hash hoặc dữ liệu user đã xóa.

### Access token cũ sau khi reset

JWT hiện có thời hạn 24 giờ và được xác thực bằng chữ ký. Xóa user trong database không tự vô hiệu access token đã phát hành.

Rủi ro:

- Token SystemAdmin cũ vẫn mang claim role cho đến khi hết hạn.
- Xóa refresh token chỉ ngăn phát hành token mới, không hủy access token hiện tại.

Giải pháp code-only đề xuất:

1. Tạo authorization requirement dành cho SystemAdmin:

```text
ActiveSystemAdminRequirement
ActiveSystemAdminHandler
```

2. Handler lấy `NameIdentifier` từ token.
3. Query user bằng `IgnoreQueryFilters()`.
4. Kiểm tra:

```text
User tồn tại
User.IsDeleted == false
User.IsActive == true
User còn liên kết role SystemAdmin
Tenant là SYSTEM
```

5. Policy `PolicyNames.SystemAdmin` yêu cầu đồng thời:

```text
RoleConstants.SystemAdmin
ActiveSystemAdminRequirement
```

Sau reset, token cũ không qua được handler vì user/role link đã bị xóa.

Đây là thay đổi code, không cần blacklist table hoặc migration.

### Rate limit

Nếu project đã có rate limiter, áp dụng policy rất thấp, ví dụ:

```text
3 request / 10 phút / user
```

Nếu chưa có rate limiter, không cần thêm package chỉ cho use case này; password verification, confirmation phrase, authorization và environment flag là lớp bảo vệ chính.

### Log bắt buộc

Trước khi reset:

```text
BOOTSTRAP_RESET_REQUESTED
ActorUserId
SystemTenantId
Environment
IP
```

Sau khi thành công:

```text
BOOTSTRAP_RESET_SUCCEEDED
DeletedUserId
DeletedTenantId
Environment
```

Khi bị từ chối:

```text
BOOTSTRAP_RESET_REFUSED
ReasonCode
ActorUserId
Environment
```

Không log:

- `CurrentPassword`
- Password hash
- JWT
- Refresh token

### Test bắt buộc

Unit test:

- Sai confirmation trả `400`.
- Sai current password trả `400` hoặc `401` theo convention đã chọn.
- Actor không phải SystemAdmin trả `403`.
- Actor không phải owner của tenant SYSTEM bị từ chối.
- Tenant name không phải SYSTEM bị từ chối.
- Dependency count khác 0 trả `409`.
- Role `SystemAdmin` không bị xóa.

Integration test:

1. Bootstrap lần đầu thành công.
2. Login SystemAdmin và nhận token.
3. Bật `AllowReset` trong test environment.
4. Gọi reset với xác nhận đúng.
5. User bootstrap không còn.
6. Tenant SYSTEM không còn.
7. Refresh token không còn.
8. UserRole SystemAdmin của user không còn.
9. Bản ghi role SystemAdmin vẫn còn.
10. Token cũ gọi API SystemAdmin nhận `403`.
11. Gọi bootstrap lần hai thành công.
12. Reset lỗi giữa chừng rollback toàn bộ.
13. Reset tenant có dependency trả `409` và không xóa gì.
14. Production environment luôn trả `404` dù config bị bật nhầm.

### Quy tắc deploy

- Default config luôn là `AllowReset = false`.
- Chỉ set `true` trên local hoặc staging khi thực sự cần.
- Sau khi reset và bootstrap lại xong, set về `false`.
- Không đưa nút reset vào menu SystemAdmin production.
- Nếu FE có nút ở development, phải có modal nhập lại password và confirmation phrase.
- Không gọi API reset tự động trong startup, seed hoặc CI/CD deploy.

### Tiêu chí hoàn thành

- Không có migration.
- Không sửa entity.
- Không xóa role SystemAdmin.
- Không thể chọn tenant/user tùy ý từ request.
- Không thể chạy production.
- Có preflight dependency.
- Có transaction và rollback.
- Bootstrap lại được sau reset.
- Token cũ mất quyền truy cập API SystemAdmin.

---

## 5. Nguyên tắc kiến trúc

Tất cả endpoint `/api/system/*` phải:

- Có `[Authorize(Policy = PolicyNames.SystemAdmin)]`.
- Dùng `IgnoreQueryFilters()` có chủ đích.
- Không phụ thuộc tenant ID trong JWT để giới hạn dữ liệu.
- Không nhận `X-Tenant-Id` làm phạm vi quản trị.
- Dùng `AsNoTracking()` cho read query.
- Có phân trang cho danh sách.
- Có giới hạn `PageSize`.
- Có `CancellationToken`.
- Projection trực tiếp sang DTO.
- Không trả password hash, refresh token hoặc payment raw data.
- Trả lỗi thống nhất bằng `ProblemDetails`.
- Không tải toàn bộ bảng lên memory để filter/group.

Khuyến nghị tách read repository dành riêng cho SystemAdmin:

```text
ISystemDashboardReadRepository
ISystemTenantReadRepository
ISystemBillingReadRepository
```

Lợi ích:

- Không làm repository nghiệp vụ hiện tại quá lớn.
- Dễ kiểm soát chỗ sử dụng `IgnoreQueryFilters()`.
- Dễ projection thẳng sang DTO.
- Không ảnh hưởng luồng TenantAdmin đang chạy.

---

## 6. Phase P0 — Chỉ đọc, ít rủi ro, phục vụ FE ngay

## 6.1. Bước P0.1 — Tạo hằng số nhận diện tenant nội bộ

Không thêm `Tenant.Kind` vào database.

Tạo file:

```text
SMEFLOWSystem.SharedKernel/Common/SystemTenantConstants.cs
```

Nội dung:

```csharp
namespace SMEFLOWSystem.SharedKernel.Common;

public static class SystemTenantConstants
{
    public const string Name = "SYSTEM";
}
```

Sửa `SystemBootstrapService` dùng hằng số này thay vì hard-code:

```csharp
Name = SystemTenantConstants.Name
```

Các query danh sách khách hàng và dashboard thêm:

```csharp
.Where(t => t.Name != SystemTenantConstants.Name)
```

### Vì sao chấp nhận cách này?

- Không đổi database.
- `SystemBootstrapService` đang tạo đúng một tenant tên `SYSTEM`.
- Hiện chưa có API SystemAdmin đổi tên tenant nội bộ.
- Đây là thay đổi nhỏ, dễ rollback.

### Hạn chế

Cách nhận diện bằng name không phải thiết kế dài hạn. Trong phạm vi “không sửa database”, đây là phương án ít rủi ro nhất.

### Tiêu chí hoàn thành

- Tenant `SYSTEM` không xuất hiện trong API khách hàng.
- Tenant `SYSTEM` không được tính trong KPI.
- Không xóa hoặc cập nhật bản ghi tenant `SYSTEM`.

---

## 6.2. Bước P0.2 — Sửa `ModuleName` trong tenant detail

File:

```text
SMEFLOWSystem.Infrastructure/Repositories/ModuleSubscriptionRepository.cs
```

Sửa `GetByTenantIgnoreTenantAsync()`:

```csharp
public Task<List<ModuleSubscription>> GetByTenantIgnoreTenantAsync(Guid tenantId)
    => _context.ModuleSubscriptions
        .IgnoreQueryFilters()
        .AsNoTracking()
        .Include(x => x.Module)
        .Where(x => x.TenantId == tenantId && !x.IsDeleted)
        .OrderBy(x => x.Module!.Name)
        .ToListAsync();
```

Không thay entity hoặc database.

### Tiêu chí hoàn thành

- `GET /api/system/tenants/{tenantId}` trả đúng `ModuleName`.
- Không có N+1 query.
- Có integration test với tenant có từ hai module.

---

## 6.3. Bước P0.3 — Validate query chung

Không sửa `PagingRequestDto` nếu lo ảnh hưởng các endpoint khác.

Tạo query DTO riêng cho SystemAdmin:

```csharp
public sealed class SystemPagingQueryDto
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}
```

Validation:

- `PageNumber >= 1`.
- `PageSize >= 1`.
- `PageSize <= 100`.
- `month` từ 1 đến 12.
- `year` từ 2020 đến năm hiện tại + 1.
- Khoảng trend tối đa 24 tháng.

Có thể dùng FluentValidation theo convention hiện tại.

Không để input sai tạo `ArgumentOutOfRangeException` và trả `500`.

---

## 6.4. Bước P0.4 — Thêm dashboard overview

### Endpoint

```http
GET /api/system/dashboard/overview?month=7&year=2026
```

### DTO

Tạo hoặc bổ sung:

```text
SMEFLOWSystem.Application/DTOs/SystemDtos/SystemDashboardDto.cs
```

```csharp
public sealed class SystemDashboardOverviewDto
{
    public int Month { get; init; }
    public int Year { get; init; }
    public int TotalTenants { get; init; }
    public int ActiveTenants { get; init; }
    public int TrialTenants { get; init; }
    public int PendingPaymentTenants { get; init; }
    public int SuspendedTenants { get; init; }
    public int NewTenantsInPeriod { get; init; }
    public int ExpiringIn7Days { get; init; }
    public int ExpiringIn30Days { get; init; }
    public int ActiveSubscriptions { get; init; }
    public int CancelledSubscriptionsInPeriod { get; init; }
    public int PendingBillingOrders { get; init; }
    public int FailedPaymentsInPeriod { get; init; }
}
```

Tất cả số liệu trên đều lấy được từ các cột hiện có.

### Định nghĩa từng field

| Field | Cách tính với schema hiện tại |
|---|---|
| `TotalTenants` | Tenant chưa xóa và name khác `SYSTEM` |
| `ActiveTenants` | Tenant status `Active` |
| `TrialTenants` | Tenant status `Trial` |
| `PendingPaymentTenants` | Tenant status `PendingPayment` |
| `SuspendedTenants` | Tenant status `Suspended` |
| `NewTenantsInPeriod` | `CreatedAt` nằm trong tháng |
| `ExpiringIn7Days` | `SubscriptionEndDate` từ hôm nay đến 7 ngày tới |
| `ExpiringIn30Days` | `SubscriptionEndDate` từ hôm nay đến 30 ngày tới |
| `ActiveSubscriptions` | `!IsDeleted`, Active/Trial và `EndDate > now` |
| `CancelledSubscriptionsInPeriod` | `IsDeleted`, Suspended và `UpdatedAt` trong tháng |
| `PendingBillingOrders` | Billing order `PaymentStatus = Pending` |
| `FailedPaymentsInPeriod` | Payment transaction `Status = Failed` trong tháng |

### Không được tính sai

- Không cộng `ActiveCompaniesCount` của từng module để ra tổng active tenant.
- Một tenant có nhiều module chỉ được tính một lần trong tenant KPI.
- Tenant `SYSTEM` không được tính.
- Billing order pending không phải doanh thu.

### Read repository

Tạo:

```text
SMEFLOWSystem.Application/Interfaces/IRepositories/ISystemDashboardReadRepository.cs
SMEFLOWSystem.Infrastructure/Repositories/SystemDashboardReadRepository.cs
```

Interface gợi ý:

```csharp
Task<SystemDashboardOverviewDto> GetOverviewAsync(
    DateTime periodStartUtc,
    DateTime periodEndUtc,
    DateOnly today,
    CancellationToken cancellationToken);

Task<List<ModuleUsageStatDto>> GetModuleUsageAsync(
    DateTime periodStartUtc,
    DateTime periodEndUtc,
    CancellationToken cancellationToken);

Task<List<ModuleCancellationStatDto>> GetModuleCancellationsAsync(
    DateTime periodStartUtc,
    DateTime periodEndUtc,
    CancellationToken cancellationToken);
```

### Query rule

- Aggregate tại database.
- Không gọi `GetAllIgnoreTenantAsync()` rồi LINQ trong memory.
- Dùng khoảng `[start, exclusiveEnd)`.
- `AsNoTracking()`.

---

## 6.5. Bước P0.5 — Sửa module usage

Giữ nguyên endpoint:

```http
GET /api/system/dashboard/module-usage?month=7&year=2026
```

Điều kiện:

```text
IsDeleted = false
Status IN (Active, Trial)
StartDate < periodEndExclusive
EndDate >= periodStart
```

Group:

```text
ModuleId
ModuleName
COUNT(DISTINCT TenantId)
```

Nên trả tất cả module, kể cả module có count bằng `0`, để biểu đồ FE không thay đổi category giữa các tháng.

Không thay response contract hiện tại.

---

## 6.6. Bước P0.6 — Sửa module cancellation bằng dữ liệu hiện có

Luồng hủy hiện tại trong `ModuleSubscriptionService`:

```csharp
sub.IsDeleted = true;
sub.Status = StatusEnum.ModuleSuspended;
await _moduleSubscriptionRepo.UpdateIgnoreTenantAsync(sub);
```

Repository đồng thời đặt:

```csharp
UpdatedAt = DateTime.UtcNow;
```

Vấn đề hiện tại:

`GetAllIgnoreTenantAsync()` lọc `!IsDeleted`, nên subscription đã hủy bị loại trước khi dashboard thống kê.

### Sửa code

Không dùng `GetAllIgnoreTenantAsync()` cho báo cáo cancellation.

Tạo query riêng có:

```csharp
_context.ModuleSubscriptions
    .IgnoreQueryFilters()
    .AsNoTracking()
    .Where(x =>
        x.IsDeleted
        && x.Status == StatusEnum.ModuleSuspended
        && x.UpdatedAt >= periodStartUtc
        && x.UpdatedAt < periodEndUtc)
```

Sau đó group theo module và count distinct tenant.

### Ý nghĩa số liệu

Tên hiển thị FE nên là:

```text
Số tenant hủy module
```

Nhưng tài liệu API phải ghi:

> Số liệu được suy ra từ subscription đã soft-delete, có trạng thái Suspended và UpdatedAt nằm trong kỳ.

### Hạn chế

- Không có `CancelledAt`, nên `UpdatedAt` được dùng làm thời điểm hủy.
- Nếu code khác cập nhật một subscription đã soft-delete sau khi hủy, tháng thống kê có thể thay đổi.
- Không được tính `EndDate` trong tháng là cancellation.

### Thống kê hết hạn

Nếu FE cần số hết hạn, tạo endpoint riêng:

```http
GET /api/system/dashboard/module-expirations?month=7&year=2026
```

Điều kiện:

```text
IsDeleted = false
EndDate >= periodStart
EndDate < periodEndExclusive
```

Không trộn expiration vào cancellation.

---

## 6.7. Bước P0.7 — Nâng cấp danh sách tenant

### Query DTO

Tạo:

```text
SMEFLOWSystem.Application/DTOs/SystemDtos/SystemTenantQueryDto.cs
```

```csharp
public sealed class SystemTenantQueryDto
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? Search { get; init; }
    public string? Status { get; init; }
    public int? ModuleId { get; init; }
    public int? ExpiringInDays { get; init; }
    public string SortBy { get; init; } = "createdAt";
    public string SortDirection { get; init; } = "desc";
}
```

Allowlist `SortBy`:

```text
name
status
createdAt
subscriptionEndDate
```

Allowlist `SortDirection`:

```text
asc
desc
```

### Endpoint

Giữ route hiện tại và bổ sung query:

```http
GET /api/system/tenants
    ?pageNumber=1
    &pageSize=20
    &search=dodo
    &status=Active
    &moduleId=1
    &expiringInDays=30
    &sortBy=subscriptionEndDate
    &sortDirection=asc
```

### List DTO

```csharp
public sealed class SystemTenantListItemDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public DateOnly? SubscriptionEndDate { get; init; }
    public int? RemainingDays { get; init; }
    public int ActiveModuleCount { get; init; }
    public int UserCount { get; init; }
    public Guid? OwnerUserId { get; init; }
    public string? OwnerName { get; init; }
    public string? OwnerEmail { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}
```

Tất cả field trên đều có thể projection từ entity hiện tại.

### Read repository

Tạo:

```text
SMEFLOWSystem.Application/Interfaces/IRepositories/ISystemTenantReadRepository.cs
SMEFLOWSystem.Infrastructure/Repositories/SystemTenantReadRepository.cs
```

### Query phải làm

1. `IgnoreQueryFilters()`.
2. `AsNoTracking()`.
3. Loại `IsDeleted`.
4. Loại `Name == SYSTEM`.
5. Search tên không phân biệt hoa thường.
6. Filter status theo allowlist.
7. Filter module bằng subquery `Any`.
8. Filter expiration.
9. Projection owner.
10. Đếm user chưa bị xóa.
11. Đếm active/trial subscription chưa hết hạn.
12. Sort trước khi `Skip/Take`.
13. Không `Include` collection lớn chỉ để count.

### Tương thích FE cũ

Các field cũ vẫn giữ:

- `Id`
- `Name`
- `Status`
- `SubscriptionEndDate`
- `OwnerUserId`
- `CreatedAt`
- `UpdatedAt`

Chỉ bổ sung field, không xóa hoặc đổi tên field cũ.

---

## 6.8. Bước P0.8 — Nâng cấp tenant detail

Giữ endpoint:

```http
GET /api/system/tenants/{tenantId}
```

### Detail DTO

```csharp
public sealed class SystemTenantDetailDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public DateOnly? SubscriptionEndDate { get; init; }
    public int? RemainingDays { get; init; }
    public SystemTenantOwnerDto? Owner { get; init; }
    public int UserCount { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public IReadOnlyList<SystemTenantModuleDto> Modules { get; init; }
        = Array.Empty<SystemTenantModuleDto>();
}
```

Owner DTO:

```csharp
public sealed class SystemTenantOwnerDto
{
    public Guid Id { get; init; }
    public string FullName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Phone { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public bool IsVerified { get; init; }
}
```

Không trả:

- `PasswordHash`
- Refresh token
- Access token
- Dữ liệu employee không cần thiết

### Quy tắc

- Nếu tenant là `SYSTEM`, trả `404`.
- Nếu tenant bị soft-delete, trả `404`.
- Module chỉ trả subscription chưa bị xóa.
- Sắp xếp module theo name.

---

## 6.9. Bước P0.9 — Sửa RoleService

Files:

```text
SMEFLOWSystem.Application/Services/RoleService.cs
SMEFLOWSystem.Application/Interfaces/IRepositories/IRoleRepository.cs
SMEFLOWSystem.Infrastructure/Repositories/RoleRepository.cs
SMEFLOWSystem.Application/DTOs/RoleDtos/
SMEFLOWSystem.WebAPI/Controllers/RoleController.cs
```

### Sửa duplicate check

Repository thêm:

```csharp
Task<bool> ExistsByNameExceptIdAsync(string name, int excludedRoleId);
```

So sánh:

- Trim.
- Không phân biệt hoa thường.
- Loại trừ role hiện tại.

### Bảo vệ system role

Danh sách:

```text
SystemAdmin
TenantAdmin
HRManager
Manager
Employee
```

Rule:

- Không đổi name system role.
- Không đổi `IsSystemRole` từ true thành false.
- Có thể sửa description.
- Không cho tạo custom role trùng tên system role.

### Không cho client tự đặt system role

Tách request DTO:

```csharp
public sealed class RoleCreateDto
{
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
}

public sealed class RoleUpdateDto
{
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
}
```

Backend tự gán:

```csharp
IsSystemRole = false
```

### Giới hạn phải ghi rõ

Custom role chỉ là dữ liệu role. Nó chưa có permission API vì hệ thống chưa có permission model.

---

## 6.10. Bước P0.10 — Hoàn thiện quản lý module

Files:

```text
SMEFLOWSystem.Application/DTOs/ModuleDtos/ModuleUpdateDto.cs
SMEFLOWSystem.Application/Interfaces/IServices/IModuleService.cs
SMEFLOWSystem.Application/Services/ModuleService.cs
SMEFLOWSystem.Application/Interfaces/IRepositories/IModuleRepository.cs
SMEFLOWSystem.Infrastructure/Repositories/ModuleRepository.cs
SMEFLOWSystem.WebAPI/Controllers/ModulesController.cs
```

### Endpoint mới

```http
PUT /api/modules/{id}
PUT /api/modules/{id}/activate
```

### Update DTO

```csharp
public sealed class ModuleUpdateDto
{
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public decimal MonthlyPrice { get; init; }
}
```

Không cho sửa `Code` và `ShortCode` để tránh làm hỏng code đang được dùng ở `RequireModule`.

### Business rule

- `Name` không rỗng.
- `MonthlyPrice >= 0`.
- Module không tồn tại trả `404`.
- Activate module đang active có thể trả `200` idempotent.
- Deactivate module đã inactive có thể trả `200` idempotent.
- Deactivate catalog module chỉ chặn lựa chọn/mua mới.
- Không tự thay đổi subscription hiện tại.

### Authorization

- `GET /api/modules/active`: giữ public nếu trang đăng ký cần dùng.
- `GET /api/modules/all`: thêm policy `SystemAdmin`.
- `POST`, `PUT`, `activate`, `deactivate`: policy `SystemAdmin`.

---

## 7. Phase P1 — Bổ sung màn hình read-only toàn hệ thống

P1 vẫn không thay đổi database. Chủ yếu bổ sung query trên các bảng đang có.

## 7.1. Bước P1.1 — Danh sách user của tenant

Endpoint:

```http
GET /api/system/tenants/{tenantId}/users
    ?pageNumber=1
    &pageSize=20
    &search=
    &role=
    &isActive=
```

DTO:

```csharp
public sealed class SystemTenantUserDto
{
    public Guid Id { get; init; }
    public string FullName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Phone { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public bool IsVerified { get; init; }
    public IReadOnlyList<string> Roles { get; init; } = Array.Empty<string>();
    public DateTime CreatedAt { get; init; }
}
```

Quy tắc:

- Chỉ read-only.
- Không trả password hash.
- Không trả refresh token.
- Không trả tenant `SYSTEM`.
- Search name/email.
- Role filter theo role name.

---

## 7.2. Bước P1.2 — Danh sách subscription toàn hệ thống

Endpoint:

```http
GET /api/system/subscriptions
    ?pageNumber=1
    &pageSize=20
    &searchTenant=
    &tenantId=
    &moduleId=
    &status=
    &includeCancelled=false
    &expiringFrom=
    &expiringTo=
```

DTO:

```csharp
public sealed class SystemSubscriptionDto
{
    public Guid Id { get; init; }
    public Guid TenantId { get; init; }
    public string TenantName { get; init; } = string.Empty;
    public int ModuleId { get; init; }
    public string ModuleCode { get; init; } = string.Empty;
    public string ModuleName { get; init; } = string.Empty;
    public DateTime StartDate { get; init; }
    public DateTime EndDate { get; init; }
    public string Status { get; init; } = string.Empty;
    public bool IsDeleted { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public int RemainingDays { get; init; }
}
```

### Ý nghĩa `includeCancelled`

Với schema hiện tại:

- `IsDeleted = true` và `Status = Suspended` được xem là đã hủy.
- Khi `includeCancelled = false`, loại các bản ghi soft-delete.
- Khi `includeCancelled = true`, dùng `IgnoreQueryFilters()` và không lọc `IsDeleted`.

---

## 7.3. Bước P1.3 — Danh sách billing order toàn hệ thống

Tạo:

```text
SMEFLOWSystem.WebAPI/Controllers/System/SystemBillingOrdersController.cs
SMEFLOWSystem.Application/Interfaces/IServices/System/ISystemBillingService.cs
SMEFLOWSystem.Application/Services/System/SystemBillingService.cs
SMEFLOWSystem.Application/Interfaces/IRepositories/ISystemBillingReadRepository.cs
SMEFLOWSystem.Infrastructure/Repositories/SystemBillingReadRepository.cs
```

### Endpoint danh sách

```http
GET /api/system/billing-orders
    ?pageNumber=1
    &pageSize=20
    &search=
    &tenantId=
    &paymentStatus=
    &status=
    &from=
    &to=
    &sortBy=billingDate
    &sortDirection=desc
```

Search theo:

- `BillingOrderNumber`
- `Tenant.Name`

DTO:

```csharp
public sealed class SystemBillingOrderListItemDto
{
    public Guid Id { get; init; }
    public string BillingOrderNumber { get; init; } = string.Empty;
    public Guid TenantId { get; init; }
    public string TenantName { get; init; } = string.Empty;
    public DateTime BillingDate { get; init; }
    public decimal TotalAmount { get; init; }
    public decimal DiscountAmount { get; init; }
    public decimal FinalAmount { get; init; }
    public string PaymentStatus { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public int ModuleCount { get; init; }
    public DateTime? CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}
```

### Endpoint detail

```http
GET /api/system/billing-orders/{billingOrderId}
```

Response gồm:

- Header billing order.
- Tenant ID/name.
- Billing order module lines.
- Module ID/code/name.
- Quantity.
- Unit price.
- Line total.
- Payment transaction liên quan.

Không trả `PaymentTransaction.RawData`.

### Tiền

- Giữ kiểu `decimal`.
- API trả số.
- FE format VND.
- Không dùng `double`.

---

## 7.4. Bước P1.4 — Danh sách payment transaction

Endpoint:

```http
GET /api/system/payment-transactions
    ?pageNumber=1
    &pageSize=20
    &tenantId=
    &billingOrderId=
    &gateway=
    &status=
    &from=
    &to=
```

DTO:

```csharp
public sealed class SystemPaymentTransactionDto
{
    public Guid Id { get; init; }
    public Guid TenantId { get; init; }
    public string TenantName { get; init; } = string.Empty;
    public Guid BillingOrderId { get; init; }
    public string BillingOrderNumber { get; init; } = string.Empty;
    public string Gateway { get; init; } = string.Empty;
    public string GatewayTransactionId { get; init; } = string.Empty;
    public string? GatewayResponseCode { get; init; }
    public decimal Amount { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public DateTime? ProcessedAt { get; init; }
}
```

Không trả:

- `RawData`
- Webhook secret
- API key
- Payment configuration

---

## 7.5. Bước P1.5 — Module trend

Endpoint:

```http
GET /api/system/dashboard/module-trends
    ?fromMonth=1
    &fromYear=2026
    &toMonth=7
    &toYear=2026
```

Giới hạn tối đa 24 tháng.

Response:

```json
{
  "from": "2026-01",
  "to": "2026-07",
  "series": [
    {
      "moduleId": 1,
      "moduleName": "HR",
      "points": [
        {
          "month": 1,
          "year": 2026,
          "activeCompanies": 10,
          "cancellations": 1,
          "expirations": 2
        }
      ]
    }
  ]
}
```

Cancellation vẫn dùng proxy:

```text
IsDeleted = true
Status = Suspended
UpdatedAt trong kỳ
```

Expiration dùng:

```text
IsDeleted = false
EndDate trong kỳ
```

---

## 8. Phase P2 — Thao tác thay đổi trạng thái bằng field hiện có

Phase này không sửa database nhưng có thay đổi dữ liệu nghiệp vụ. Chỉ triển khai sau khi P0/P1 ổn định và team xác nhận business rule.

## 8.1. Structured logging thay cho bảng audit

Tạo event ID cố định:

```csharp
public static class SystemAdminLogEvents
{
    public static readonly EventId TenantStatusChanged = new(5101, nameof(TenantStatusChanged));
    public static readonly EventId SubscriptionExtended = new(5102, nameof(SubscriptionExtended));
    public static readonly EventId SubscriptionSuspended = new(5103, nameof(SubscriptionSuspended));
    public static readonly EventId SubscriptionReactivated = new(5104, nameof(SubscriptionReactivated));
    public static readonly EventId ModuleUpdated = new(5105, nameof(ModuleUpdated));
}
```

Log mẫu:

```csharp
_logger.LogWarning(
    SystemAdminLogEvents.TenantStatusChanged,
    "SystemAdmin action {Action}; ActorUserId={ActorUserId}; TenantId={TenantId}; BeforeStatus={BeforeStatus}; AfterStatus={AfterStatus}; Reason={Reason}",
    "TENANT_STATUS_CHANGED",
    actorUserId,
    tenantId,
    beforeStatus,
    afterStatus,
    reason);
```

### Không log

- Password/password hash.
- Access/refresh token.
- API key.
- Raw webhook.
- Full payment payload.

### Hạn chế

Log server có thể bị rotate và không phải audit database. Cần cấu hình retention ở hệ thống logging hiện có nếu muốn lưu lâu.

---

## 8.2. Bước P2.1 — Suspend/reactivate tenant

Endpoint:

```http
PATCH /api/system/tenants/{tenantId}/status
```

Request:

```json
{
  "status": "Suspended",
  "reason": "Vi phạm điều khoản sử dụng"
}
```

Chỉ cập nhật field hiện có:

```text
Tenant.Status
Tenant.UpdatedAt
```

### Transition cho phép

```text
Active    -> Suspended
Trial     -> Suspended
Suspended -> Active
```

Không cho SystemAdmin dùng endpoint này để:

- Đánh dấu payment thành công.
- Chuyển `PendingPayment -> Active`.
- Sửa tenant `SYSTEM`.
- Sửa tenant soft-delete.

`PendingPayment -> Active` phải tiếp tục đi qua payment flow hiện tại.

### Reason

- Bắt buộc khi suspend.
- Chỉ ghi structured log.
- Không lưu database.

### Idempotency

- Nếu status mới bằng status hiện tại, trả `200` với dữ liệu hiện tại.
- Không update `UpdatedAt` nếu không có thay đổi.

---

## 8.3. Bước P2.2 — Gia hạn subscription

Endpoint:

```http
POST /api/system/subscriptions/{subscriptionId}/extend
```

Request:

```json
{
  "newEndDate": "2026-12-31T17:00:00Z",
  "reason": "Điều chỉnh thủ công theo hợp đồng"
}
```

Chỉ cập nhật:

```text
ModuleSubscription.EndDate
ModuleSubscription.UpdatedAt
```

Rule:

- Subscription phải tồn tại.
- `IsDeleted` phải false.
- `newEndDate > EndDate`.
- Không cho rút ngắn hạn qua endpoint extend.
- Không đổi trạng thái billing order.
- Không đánh dấu payment paid.
- Ghi structured log before/after.

Nếu subscription đã expired và status không usable, không tự active trong endpoint extend. Việc active thực hiện bằng command riêng để tránh gộp nhiều thay đổi.

---

## 8.4. Bước P2.3 — Suspend/reactivate subscription

### Suspend

```http
POST /api/system/subscriptions/{subscriptionId}/suspend
```

Chỉ cập nhật:

```text
Status = Suspended
UpdatedAt = now
```

Không đặt `IsDeleted = true`, vì đây là admin suspension chứ không phải tenant chủ động cancel.

### Reactivate

```http
POST /api/system/subscriptions/{subscriptionId}/reactivate
```

Rule:

- `IsDeleted` phải false.
- Module catalog phải active.
- `EndDate > now`.
- Status hiện tại phải `Suspended`.
- Chuyển status sang `Active`.
- Ghi structured log.

### Không reactivate cancellation

Subscription có:

```text
IsDeleted = true
```

được xem là đã cancel. Không khôi phục bằng endpoint reactivate để tránh thay đổi nghĩa của soft-delete.

---

## 8.5. Bước P2.4 — Chuẩn hóa kiểm tra subscription usable

Nếu bổ sung admin suspension, phải đảm bảo `Status = Suspended` thực sự chặn module.

Tạo helper/domain rule code-only:

```csharp
public static class ModuleSubscriptionRules
{
    public static bool IsUsable(ModuleSubscription subscription, DateTime nowUtc)
    {
        return !subscription.IsDeleted
            && (subscription.Status == StatusEnum.ModuleActive
                || subscription.Status == StatusEnum.ModuleTrial)
            && subscription.StartDate <= nowUtc
            && subscription.EndDate > nowUtc;
    }
}
```

Áp dụng thống nhất tại:

- `AuthService`
- `ModuleRequirementFilter` hoặc `ModuleSubscriptionService`
- Dashboard usage
- Invite/module access nếu đang kiểm tra riêng

### Lỗi cần tránh

`ModuleRequirementFilter` không được chỉ kiểm tra subscription có tồn tại. Nó phải kiểm tra:

- Không soft-delete.
- Status Active/Trial.
- Đã đến ngày bắt đầu.
- Chưa hết hạn.

Nếu không sửa phần này, admin suspend subscription nhưng user vẫn có thể gọi API module.

---

## 9. API mục tiêu

| Nhóm | Method | Endpoint | Phase | Thay đổi DB |
|---|---|---|---|---|
| Bootstrap | DELETE | `/api/system/bootstrap/reset` | Tiện ích Dev/Staging | Chỉ xóa bootstrap identity được xác nhận |
| Dashboard | GET | `/api/system/dashboard/overview` | P0 | Không |
| Dashboard | GET | `/api/system/dashboard/module-usage` | P0 sửa | Không |
| Dashboard | GET | `/api/system/dashboard/module-cancellations` | P0 sửa | Không |
| Dashboard | GET | `/api/system/dashboard/module-expirations` | P0 | Không |
| Dashboard | GET | `/api/system/dashboard/module-trends` | P1 | Không |
| Tenant | GET | `/api/system/tenants` | P0 sửa | Không |
| Tenant | GET | `/api/system/tenants/{id}` | P0 sửa | Không |
| Tenant user | GET | `/api/system/tenants/{id}/users` | P1 | Không |
| Tenant status | PATCH | `/api/system/tenants/{id}/status` | P2 | Không thêm schema |
| Module | GET | `/api/modules/all` | P0 sửa auth | Không |
| Module | POST | `/api/modules` | Đã có | Không |
| Module | PUT | `/api/modules/{id}` | P0 | Không |
| Module | PUT | `/api/modules/{id}/activate` | P0 | Không |
| Module | PUT | `/api/modules/{id}/deactivate` | Đã có | Không |
| Subscription | GET | `/api/system/subscriptions` | P1 | Không |
| Subscription | POST | `/api/system/subscriptions/{id}/extend` | P2 | Không thêm schema |
| Subscription | POST | `/api/system/subscriptions/{id}/suspend` | P2 | Không thêm schema |
| Subscription | POST | `/api/system/subscriptions/{id}/reactivate` | P2 | Không thêm schema |
| Billing | GET | `/api/system/billing-orders` | P1 | Không |
| Billing | GET | `/api/system/billing-orders/{id}` | P1 | Không |
| Payment | GET | `/api/system/payment-transactions` | P1 | Không |

---

## 10. Chuẩn hóa lỗi API

Không cần sửa database.

Nên thống nhất dùng `ProblemDetails`.

### HTTP status

| Trường hợp | Status |
|---|---|
| Query/body không hợp lệ | 400 |
| Chưa đăng nhập | 401 |
| Không có role SystemAdmin | 403 |
| Không tìm thấy resource | 404 |
| Trùng code/name | 409 |
| State transition không hợp lệ | 409 |
| Lỗi ngoài dự kiến | 500 |

### Ví dụ

```json
{
  "title": "Invalid tenant status transition",
  "status": 409,
  "detail": "Không thể chuyển tenant từ PendingPayment sang Active bằng thao tác quản trị.",
  "traceId": "..."
}
```

Không trả stack trace hoặc exception message nội bộ trên production.

---

## 11. Test plan

## 11.1. Unit test P0

### SystemDashboardService

- Month mặc định.
- Year mặc định.
- Month `0` trả validation error.
- Month `13` trả validation error.
- Tenant `SYSTEM` không được tính.
- Một tenant mua nhiều module không bị cộng lặp trong tenant KPI.
- Usage chỉ tính Active/Trial chưa hết hạn.
- Cancellation chỉ tính soft-delete + Suspended + UpdatedAt trong kỳ.
- Expiration không bị tính là cancellation.

### SystemTenantService

- Không trả tenant `SYSTEM`.
- Map owner name/email đúng.
- Map module name đúng.
- User count đúng.
- Active module count đúng.
- Search/filter/sort đúng.
- Page size trên 100 bị từ chối.

### RoleService

- Giữ nguyên name và sửa description thành công.
- Đổi sang tên role khác đã tồn tại trả conflict.
- Duplicate check không phân biệt hoa thường.
- Không đổi tên system role.
- Create custom role luôn có `IsSystemRole = false`.

### ModuleService

- Update name/description/price thành công.
- Không cho giá âm.
- Không cho name rỗng.
- Không sửa code/shortcode.
- Activate/deactivate idempotent.
- Deactivate module không sửa subscription.

---

## 11.2. Integration test P0/P1

Nên dùng PostgreSQL test container hoặc database test tương đương, không chỉ EF InMemory.

Case:

1. SystemAdmin gọi được endpoint `/api/system/*`.
2. TenantAdmin nhận `403`.
3. User không token nhận `401`.
4. API cross-tenant trả nhiều tenant, không bị giới hạn bởi tenant trong JWT.
5. Tenant `SYSTEM` không xuất hiện.
6. Tenant detail trả module name.
7. Tenant list kết hợp search/filter/sort/paging đúng.
8. Dashboard query count đúng.
9. Cancellation query đọc được soft-deleted subscription.
10. Billing list trả đúng cross-tenant.
11. Billing detail trả module name.
12. Payment list không trả `RawData`.
13. User list không trả password hash.

---

## 11.3. Integration test P2

1. Không thể đổi status tenant `SYSTEM`.
2. Không thể `PendingPayment -> Active` bằng admin endpoint.
3. Suspend tenant ghi structured log.
4. Extend chỉ cho end date lớn hơn hiện tại.
5. Suspend subscription không set `IsDeleted`.
6. Reactivate subscription hết hạn bị từ chối.
7. Reactivate subscription đã cancel bị từ chối.
8. ModuleRequirementFilter từ chối subscription Suspended.
9. ModuleRequirementFilter từ chối subscription hết hạn.
10. Request lặp lại không tạo thay đổi không cần thiết.

---

## 11.4. Security test

- Không dùng `X-Tenant-Id` để vượt quyền.
- Sort field ngoài allowlist bị từ chối.
- Status ngoài allowlist bị từ chối.
- Page size quá lớn bị từ chối.
- DTO không lộ password hash.
- DTO không lộ refresh token.
- DTO không lộ payment raw data.
- Structured log không chứa secret/token/password.
- Role khác không gọi được SystemAdmin API.

---

## 12. Thứ tự triển khai an toàn

## Sprint 1 — P0 nền tảng, read-only và bootstrap utility

- [ ] Thêm API reset bootstrap chỉ cho Development/Staging.
- [ ] Thêm preflight, transaction và kiểm tra lại active SystemAdmin.
- [ ] Bảo đảm reset không xóa role `SystemAdmin`.
- [ ] Bảo đảm token SystemAdmin cũ không còn quyền sau reset.
- [ ] Tạo `SystemTenantConstants`.
- [ ] Loại tenant `SYSTEM` khỏi tenant list và dashboard.
- [ ] Sửa `Include(Module)` trong tenant detail.
- [ ] Thêm validation month/year/page size.
- [ ] Tạo dashboard overview.
- [ ] Đưa dashboard aggregation xuống database query.
- [ ] Sửa cancellation query đọc soft-deleted subscription.
- [ ] Tách expiration khỏi cancellation.
- [ ] Thêm tenant search/filter/sort.
- [ ] Bổ sung owner, user count và active module count.
- [ ] Sửa role duplicate check.
- [ ] Bảo vệ system role.
- [ ] Thêm update/activate module.
- [ ] Thêm authorization cho `GET /api/modules/all`.
- [ ] Viết unit/integration test.

## Sprint 2 — P1 read-only mở rộng

- [ ] Thêm tenant user list.
- [ ] Thêm global subscription list.
- [ ] Thêm global billing order list.
- [ ] Thêm billing order detail.
- [ ] Thêm payment transaction list.
- [ ] Thêm module trend.
- [ ] Test cross-tenant authorization.
- [ ] Test DTO không lộ dữ liệu nhạy cảm.

## Sprint 3 — P2 mutation có kiểm soát

- [ ] Thêm structured logging event.
- [ ] Thêm tenant status command.
- [ ] Thêm subscription extend command.
- [ ] Thêm subscription suspend command.
- [ ] Thêm subscription reactivate command.
- [ ] Chuẩn hóa `ModuleSubscriptionRules.IsUsable`.
- [ ] Sửa `ModuleRequirementFilter`.
- [ ] Test state transition.
- [ ] Test rollback khi service lỗi.

P2 có thể hoãn hoàn toàn nếu team chỉ muốn SystemAdmin xem dữ liệu.

---

## 13. Kiểm tra bắt buộc trước khi deploy

### Kiểm tra source

Lệnh kiểm tra không có migration mới:

```powershell
git diff --name-only | Select-String "Migrations|ModelSnapshot|Data/Configurations"
```

Kết quả phải rỗng.

Kiểm tra không đổi entity/schema:

```powershell
git diff --name-only | Select-String "Core/Entities"
```

Kết quả phải rỗng cho phạm vi plan này.

### Build và test

```powershell
dotnet restore SMEFLOWSystem.sln
dotnet build SMEFLOWSystem.sln --no-restore
dotnet test SMEFLOWSystem.sln --no-build
```

### Smoke test staging

- Login SystemAdmin.
- Gọi dashboard overview.
- Gọi tenant list.
- Xác nhận không thấy `SYSTEM`.
- Gọi tenant detail.
- Xác nhận module name không rỗng.
- Test filter tenant.
- Test module all với SystemAdmin.
- Test module all với role khác.
- Test billing list.
- Test payment list.

Nếu triển khai P2:

- Test suspend/reactivate trên tenant test.
- Test subscription trên subscription test.
- Không dùng tenant production thật cho smoke test mutation.

---

## 14. Cách deploy để không làm rối server

1. Không sửa trực tiếp source trên server.
2. Làm trên branch riêng.
3. Review diff.
4. Xác nhận không có migration.
5. Build và test CI.
6. Deploy lên staging trước.
7. Chạy smoke test.
8. Build image có commit SHA cố định.
9. Deploy đúng image đã test lên production.
10. Theo dõi log và health.

Do không có thay đổi database, rollback chỉ cần:

1. Deploy lại image/tag trước đó.
2. Không chạy database rollback.
3. Không restore database.

Đối với P2, code có thể đã cập nhật status/end date bằng các field hiện tại. Rollback image không tự hoàn tác dữ liệu nghiệp vụ đã được SystemAdmin thay đổi. Vì vậy:

- P2 phải có feature flag hoặc chỉ mở route sau khi xác nhận.
- Không test mutation trên dữ liệu thật.
- FE cần confirmation dialog cho thao tác nhạy cảm.

---

## 15. Danh sách file dự kiến thay đổi

## P0

```text
SMEFLOWSystem.SharedKernel/Common/SystemTenantConstants.cs

SMEFLOWSystem.Application/DTOs/SystemDtos/SystemBootstrapResetRequestDto.cs
SMEFLOWSystem.Application/DTOs/SystemDtos/SystemDashboardDto.cs
SMEFLOWSystem.Application/DTOs/SystemDtos/SystemTenantDto.cs
SMEFLOWSystem.Application/DTOs/SystemDtos/SystemTenantQueryDto.cs
SMEFLOWSystem.Application/DTOs/ModuleDtos/ModuleUpdateDto.cs
SMEFLOWSystem.Application/DTOs/RoleDtos/RoleCreateDto.cs
SMEFLOWSystem.Application/DTOs/RoleDtos/RoleUpdateDto.cs

SMEFLOWSystem.Application/Interfaces/IRepositories/ISystemDashboardReadRepository.cs
SMEFLOWSystem.Application/Interfaces/IRepositories/ISystemTenantReadRepository.cs
SMEFLOWSystem.Application/Interfaces/IRepositories/ISystemBootstrapResetRepository.cs
SMEFLOWSystem.Application/Interfaces/IServices/System/ISystemBootstrapResetService.cs
SMEFLOWSystem.Application/Interfaces/IServices/System/ISystemDashboardService.cs
SMEFLOWSystem.Application/Interfaces/IServices/System/ISystemTenantService.cs
SMEFLOWSystem.Application/Services/System/SystemDashboardService.cs
SMEFLOWSystem.Application/Services/System/SystemTenantService.cs
SMEFLOWSystem.Application/Services/System/SystemBootstrapService.cs
SMEFLOWSystem.Application/Services/System/SystemBootstrapResetService.cs
SMEFLOWSystem.Application/Services/ModuleService.cs
SMEFLOWSystem.Application/Services/RoleService.cs

SMEFLOWSystem.Infrastructure/Repositories/SystemDashboardReadRepository.cs
SMEFLOWSystem.Infrastructure/Repositories/SystemTenantReadRepository.cs
SMEFLOWSystem.Infrastructure/Repositories/SystemBootstrapResetRepository.cs
SMEFLOWSystem.Infrastructure/Repositories/ModuleSubscriptionRepository.cs
SMEFLOWSystem.Infrastructure/Repositories/ModuleRepository.cs
SMEFLOWSystem.Infrastructure/Repositories/RoleRepository.cs

SMEFLOWSystem.WebAPI/Controllers/System/SystemDashboardController.cs
SMEFLOWSystem.WebAPI/Controllers/System/SystemTenantsController.cs
SMEFLOWSystem.WebAPI/Controllers/System/SystemBootstrapController.cs
SMEFLOWSystem.WebAPI/Authorization/ActiveSystemAdminRequirement.cs
SMEFLOWSystem.WebAPI/Authorization/ActiveSystemAdminHandler.cs
SMEFLOWSystem.WebAPI/Controllers/ModulesController.cs
SMEFLOWSystem.WebAPI/Controllers/RoleController.cs
```

## P1

```text
SMEFLOWSystem.Application/DTOs/SystemDtos/SystemTenantUserDto.cs
SMEFLOWSystem.Application/DTOs/SystemDtos/SystemSubscriptionDto.cs
SMEFLOWSystem.Application/DTOs/SystemDtos/SystemBillingOrderDto.cs
SMEFLOWSystem.Application/DTOs/SystemDtos/SystemPaymentTransactionDto.cs

SMEFLOWSystem.Application/Interfaces/IRepositories/ISystemBillingReadRepository.cs
SMEFLOWSystem.Application/Interfaces/IServices/System/ISystemBillingService.cs
SMEFLOWSystem.Application/Services/System/SystemBillingService.cs

SMEFLOWSystem.Infrastructure/Repositories/SystemBillingReadRepository.cs

SMEFLOWSystem.WebAPI/Controllers/System/SystemSubscriptionsController.cs
SMEFLOWSystem.WebAPI/Controllers/System/SystemBillingOrdersController.cs
SMEFLOWSystem.WebAPI/Controllers/System/SystemPaymentTransactionsController.cs
```

## P2

```text
SMEFLOWSystem.Application/Helpers/ModuleSubscriptionRules.cs
SMEFLOWSystem.Application/Services/System/SystemTenantService.cs
SMEFLOWSystem.Application/Services/System/SystemSubscriptionService.cs
SMEFLOWSystem.WebAPI/Filters/ModuleRequirementFilter.cs
SMEFLOWSystem.WebAPI/Controllers/System/SystemTenantsController.cs
SMEFLOWSystem.WebAPI/Controllers/System/SystemSubscriptionsController.cs
SMEFLOWSystem.WebAPI/Logging/SystemAdminLogEvents.cs
```

Không có file trong:

```text
SMEFLOWSystem.Infrastructure/Migrations/
SMEFLOWSystem.Infrastructure/Data/Configurations/
SMEFLOWSystem.Core/Entities/
```

---

## 16. Definition of Done

Một hạng mục chỉ hoàn thành khi:

1. Không có migration hoặc thay đổi schema.
2. Không có thay đổi file entity.
3. API có policy SystemAdmin đúng.
4. Tenant `SYSTEM` không xuất hiện trong dữ liệu khách hàng.
5. Request được validate.
6. Danh sách có phân trang và page size tối đa.
7. Query filter/group chạy ở database.
8. Không có N+1 query.
9. DTO không lộ dữ liệu nhạy cảm.
10. Endpoint cũ không bị đổi route hoặc xóa field.
11. Có unit test.
12. Có integration test authorization.
13. Swagger mô tả đúng.
14. Build và test pass.
15. Staging smoke test pass.
16. Diff xác nhận không đụng migration/model snapshot.

---

## 17. Kết quả FE có thể làm

### Sau P0

- Dashboard KPI.
- Biểu đồ module usage.
- Biểu đồ cancellation và expiration tách riêng.
- Tenant table có search/filter/sort.
- Tenant detail có owner, user count và module.
- Quản lý module.
- Quản lý danh mục role an toàn hơn.

### Sau P1

- Tenant user tab.
- Subscription toàn hệ thống.
- Hóa đơn toàn hệ thống.
- Chi tiết hóa đơn.
- Payment transaction.
- Module trend.

### Sau P2

- Suspend/reactivate tenant.
- Extend/suspend/reactivate subscription.
- Log thao tác SystemAdmin trên hệ thống logging hiện tại.

---

## 18. Ngoài phạm vi

Các hạng mục sau không thực hiện vì cần thay đổi database hoặc kiến trúc lớn:

- Bảng audit log mới.
- Permission matrix.
- Role-permission mapping.
- Lưu cancellation reason vào database.
- Lưu tenant suspension reason vào database.
- Thêm `Tenant.Kind`.
- Thêm `CancelledAt`.
- Thêm `RowVersion`.
- Thêm database index.
- Materialized view cho dashboard.
- Thay đổi lịch sử dữ liệu cũ.

Nếu sau này cho phép migration, các hạng mục này phải được lập thành kế hoạch riêng, không gộp vào plan code-only này.
