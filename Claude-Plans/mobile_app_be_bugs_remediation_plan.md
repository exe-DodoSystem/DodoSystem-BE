# Kế hoạch xác minh và xử lý các lỗi Backend khi tích hợp Mobile App

**Ngày đối chiếu source:** 2026-07-24  
**Nguồn đầu vào:** `Claude-Plans/mobile_app_be_bugs_report.md`  
**Phạm vi:** Backend `DodoSystem-BE` hiện tại  
**Baseline kiểm thử:** `dotnet test SMEFLOWSystem.sln --no-restore` — 92/92 test pass, 0 fail

---

## 1. Kết luận xác minh

### 1.1. Bảng kết luận nhanh

| ID | Kết luận | Mức ưu tiên thực tế | Ghi chú sau khi đối chiếu source |
|---|---|---:|---|
| BE-AUTH-01 | **Đúng, nghiêm trọng hơn report mô tả** | P0 / vá ngay | `ModuleSubscriptionsController` không có `[Authorize]`; service chỉ cần có `TenantId`. Vì `CurrentTenantService` còn nhận `X-Tenant-Id` khi chưa đăng nhập, request anonymous có thể đọc/hủy subscription nếu biết Tenant ID. |
| BE-HR-01 | **Đúng, nhưng report sai một phần nguyên nhân** | P0 / vá ngay | `ShiftRepository` và `ShiftPatternRepository` gọi `IgnoreQueryFilters()` khi `includeDeleted=true`, làm mất cả tenant filter lẫn soft-delete filter. Source hiện tại đã có `ApplySoftDeleteQueryFilters`, trái với mô tả “chỉ có TenantId”. Lỗ hổng xuyên Tenant vẫn tồn tại. |
| BE-LEAVE-01 | **Đúng** | P0 | List và approve/reject không áp dụng phạm vi phòng ban của Manager. |
| BE-MGR-05 | **Đúng** | P0 | GET manual timesheets cho phép Manager nhưng chỉ lọc theo Tenant, không lọc phòng ban. Upsert/Delete đã giới hạn Admin/HRManager nên không nằm trong lỗi này. |
| BE-ATT-02 | **Đúng** | P1 | Endpoint multipart bắt mọi `InvalidOperationException` và trả `404 Employee not found`, kể cả Fake GPS/geofence/GPS bắt buộc. |
| BE-ATT-01 | **Đúng** | P1 | Mỗi request luôn insert `RawPunchLog`; dedup trong resolution job không phải idempotency ở thời điểm nhận request. |
| BE-MGR-06 | **Đúng ở cấp hệ thống; một số ví dụ trong report đã cũ** | P1 | Không có global exception handler. `PayrollService` vẫn ném `Exception` chung. Tuy nhiên `RoleController.GetById` hiện đã trả 404 đúng và `UpdateRole` đã catch exception có kiểu. |
| BE-MGR-02 | **Không còn đúng với source hiện tại** | Không sửa như bug | `ApplySoftDeleteQueryFilters` đang ghép `!IsDeleted` vào filter của `Employee`; query mặc định không trả nhân viên đã xóa. Endpoint restore cũng đã tồn tại. Chưa có API liệt kê deleted employee là một product gap riêng, không phải lỗi “deleted vẫn hiện như active”. |

### 1.2. Bằng chứng chính trong source

- `SMEFLOWSystem.WebAPI/Controllers/ModuleSubscriptionsController.cs`
  - Không có `[Authorize]` ở class.
  - `DELETE me/cancel/{moduleId}` không có policy `TenantAdmin`.
- `SMEFLOWSystem.Infrastructure/Tenancy/CurrentTenantService.cs`
  - Nếu không có claim, service vẫn đọc `X-Tenant-Id` từ header.
- `SMEFLOWSystem.Application/Services/ModuleSubscriptionService.cs`
  - `CancelMyModuleSubscriptionAsync` không kiểm tra role; chỉ lấy Tenant ID rồi soft-delete subscription.
- `SMEFLOWSystem.Infrastructure/Data/SMEFLOWSystemContext.cs`
  - Có tenant query filter.
  - `ApplySoftDeleteQueryFilters` ghép thêm điều kiện `!IsDeleted`.
- `SMEFLOWSystem.Infrastructure/Repositories/ShiftRepository.cs`
  - `includeDeleted=true` gọi `IgnoreQueryFilters()`.
- `SMEFLOWSystem.Infrastructure/Repositories/ShiftPatternRepository.cs`
  - Lặp lại cùng lỗi `IgnoreQueryFilters()`.
- `SMEFLOWSystem.Application/Services/LeaveRequestService.cs`
  - Không inject/use `IHrAuthorizationService`.
  - Approve/Reject thay đổi trạng thái trực tiếp sau `GetByIdAsync`.
  - GetPending/GetAll trả toàn Tenant.
- `SMEFLOWSystem.Application/Services/ManualTimesheetService.cs`
  - GET chỉ gọi `EnsureHrAccess()` rồi lấy toàn Tenant.
- `SMEFLOWSystem.WebAPI/Controllers/AttendanceController.cs`
  - JSON endpoint phân biệt employee-not-found và business error.
  - Multipart endpoint đổi mọi `InvalidOperationException` thành 404.
- `SMEFLOWSystem.Application/Services/AttendanceService.cs`
  - `SubmitPunchAsync` luôn tạo và insert `RawPunchLog`.
- `SMEFLOWSystem.Infrastructure/Data/Configurations/RawPunchLogConfiguration.cs`
  - Có index thường `(EmployeeId, Timestamp)`, chưa có unique idempotency constraint.
- `SMEFLOWSystem.WebAPI/Validator/WebApplicationExtensions.cs`
  - Chưa gọi `UseExceptionHandler`.
- `SMEFLOWSystem.Application/Services/PayrollService.cs`
  - Nhiều business condition vẫn `throw new Exception(...)`.

---

## 2. Nguyên tắc triển khai

1. Mỗi phase là một PR/commit độc lập, có test hồi quy và có thể rollback riêng.
2. Ưu tiên chặn truy cập trái phép trước khi cải thiện UX/error contract.
3. Authorization phải được kiểm tra tại server; client filtering/cooldown chỉ là UX.
4. Mọi query dùng `IgnoreQueryFilters()` phải tự áp lại tenant predicate ngay trong cùng query.
5. Quy ước scope phòng ban:
   - `null`: TenantAdmin/HRManager, được xem toàn bộ dữ liệu trong Tenant.
   - danh sách rỗng: Manager chưa được giao phòng ban, kết quả rỗng.
   - danh sách có phần tử: chỉ dữ liệu thuộc các phòng ban đó.
6. Không dùng query “check rồi insert” làm cơ chế idempotency duy nhất vì có race condition.
7. Không map mọi `InvalidOperationException` thành lỗi nghiệp vụ toàn cục; phải chuyển dần sang exception có kiểu.
8. Không gỡ workaround ở Flutter cho đến khi phase tương ứng đã deploy production và qua smoke test.

---

## 3. Thứ tự phase đề xuất

| Thứ tự | Phase | Gap xử lý | Có migration DB | Rủi ro thay đổi |
|---:|---|---|---:|---|
| 0 | Characterization và khóa contract | Tất cả | Không | Thấp |
| 1 | Khóa Module Subscriptions | BE-AUTH-01 | Không | Thấp |
| 2 | Khôi phục tenant isolation khi include deleted | BE-HR-01 | Không | Trung bình |
| 3 | Chặn Manager vượt scope ở Leave Request | BE-LEAVE-01 | Không | Trung bình |
| 4 | Lọc Manual Timesheet theo scope Manager | BE-MGR-05 | Không | Trung bình |
| 5 | Đồng nhất lỗi submit punch JSON/multipart | BE-ATT-02 | Không | Thấp |
| 6 | Idempotency cho submit punch | BE-ATT-01 | **Có** | Cao |
| 7 | Global exception contract và typed exceptions | BE-MGR-06 | Không | Cao |
| 8 | Regression, rollout và gỡ workaround Mobile | Tất cả | Không | Trung bình |

`BE-MGR-02` chỉ được thêm test xác nhận ở Phase 0; không triển khai thay đổi API trong chuỗi sửa bug này.

---

## 4. Phase 0 — Characterization test và khóa contract hiện tại

### Mục tiêu

- Tạo test tái hiện từng lỗi trước khi sửa.
- Khóa hành vi đúng hiện có của soft-delete Employee.
- Ghi rõ response contract mà Flutter đang dùng để tránh sửa một lỗi nhưng làm vỡ client.

### Công việc

1. Thêm nhóm test theo chức năng:
   - `ModuleSubscriptionsAuthorizationTests`
   - `ShiftTenantIsolationTests`
   - `LeaveRequestManagerScopeTests`
   - `ManualTimesheetManagerScopeTests`
   - `EmployeeSoftDeleteQueryFilterTests`
   - `AttendanceSubmitPunchContractTests`
   - `ApiExceptionContractTests`
2. Tạo dữ liệu tối thiểu:
   - Tenant A, Tenant B.
   - Manager A được gán Department A1 nhưng không được gán A2.
   - Employee A1, Employee A2, Employee B1.
   - Bản ghi active và soft-deleted cho Employee/Shift/ShiftPattern.
3. Với `Employee`, khóa các behavior đang đúng:
   - `GetByIdAsync` không tìm thấy employee đã soft-delete.
   - `GetPagedAsync` không trả employee đã soft-delete.
   - `GetByDepartmentIdAsync` không trả employee đã soft-delete.
   - `GetByIdIncludeDeletedAsync(id, tenantId)` chỉ tìm trong đúng Tenant.
   - Restore theo ID hoạt động và sau restore employee xuất hiện lại trong query mặc định.
4. Ghi snapshot contract lỗi hiện tại của hai submit-punch endpoint để test sau Phase 5.
5. Không sửa production code trong phase này, trừ test seam nhỏ nếu thật sự cần.

### Lưu ý test database

- EF InMemory đủ cho test service/scope cơ bản.
- Các test liên quan unique index, concurrency và PostgreSQL error code ở Phase 6 phải chạy với PostgreSQL thật hoặc Testcontainers; không được dựa riêng vào EF InMemory.

### Tiêu chí hoàn thành

- Test tái hiện lỗi phải fail vì đúng nguyên nhân, không fail do setup.
- Test `BE-MGR-02` phải pass, chứng minh đây không còn là bug hiện tại.
- Baseline cũ 92 test vẫn pass.

### Trạng thái triển khai Phase 0 — hoàn thành 2026-07-24

Đã thêm:

- `SMEFLOWSystem.Tests/KnownBugAttributes.cs`
- `SMEFLOWSystem.Tests/PhaseZeroTestContext.cs`
- `SMEFLOWSystem.Tests/PhaseZeroAuthorizationContractTests.cs`
- `SMEFLOWSystem.Tests/PhaseZeroTenantAndSoftDeleteTests.cs`
- `SMEFLOWSystem.Tests/PhaseZeroAttendanceContractTests.cs`

Các test chưa thể pass trước khi triển khai phase sửa tương ứng dùng
`KnownBugFact`. Cơ chế này giữ suite mặc định xanh nhưng vẫn cho phép chạy
assertion bảo mật thật sự theo yêu cầu:

```powershell
$env:RUN_KNOWN_BUG_TESTS='1'
dotnet test SMEFLOWSystem.Tests/SMEFLOWSystem.Tests.csproj `
  --no-restore `
  --filter "Phase=0"
```

Kết quả characterization khi bật cờ:

- 18 test được chạy.
- 6 test pass.
- 12 test fail đúng các gap đã biết.
- Không có lỗi compile hoặc lỗi test setup.

12 failure được phân bổ:

| Gap | Số failure | Nội dung bị khóa bởi test |
|---|---:|---|
| BE-AUTH-01 | 2 | Class chưa yêu cầu authentication; cancel chưa yêu cầu TenantAdmin |
| BE-HR-01 | 2 | Shift và ShiftPattern include-deleted trả 4 bản ghi thay vì 2 bản ghi Tenant hiện tại |
| BE-LEAVE-01 | 2 | Service chưa phụ thuộc HR authorization; repository list chưa nhận department scope |
| BE-MGR-05 | 2 | Service chưa phụ thuộc HR authorization; month query chưa nhận department scope |
| BE-ATT-01 | 2 | DTO chưa có `ClientRequestId`; model chưa có unique idempotency index |
| BE-ATT-02 | 1 | Multipart trả 404 thay vì 400 cho Fake GPS |
| BE-MGR-06 | 1 | WebAPI chưa có implementation `IExceptionHandler` |

Chạy suite mặc định:

```powershell
dotnet test SMEFLOWSystem.sln --no-restore
```

Kết quả:

- 98 test pass.
- 12 known-gap test skipped.
- 0 test fail.
- 92 baseline test cũ tiếp tục pass; 6 test Phase 0 cho behavior hiện đang đúng cũng pass.

Các behavior xanh của Phase 0:

- Employee query mặc định loại soft-deleted employee.
- Lookup include-deleted vẫn giới hạn đúng Tenant.
- Employee xuất hiện lại sau restore.
- JSON submit-punch trả 400 cho business validation.
- JSON và multipart đều trả 404 khi employee không tồn tại.

Khi sửa một gap ở phase sau, đổi `KnownBugFact` tương ứng về `Fact` để biến
assertion đó thành regression gate bắt buộc.

---

## 5. Phase 1 — Khóa `ModuleSubscriptionsController` (BE-AUTH-01)

### Mục tiêu

Chặn hoàn toàn anonymous/Employee/Manager/HRManager tự hủy module của Tenant.

### Thay đổi dự kiến

#### WebAPI

File: `SMEFLOWSystem.WebAPI/Controllers/ModuleSubscriptionsController.cs`

1. Thêm `using Microsoft.AspNetCore.Authorization`.
2. Thêm `[Authorize]` ở class level.
3. Thêm `[Authorize(Policy = PolicyNames.TenantAdmin)]` cho:
   - `DELETE /api/ModuleSubscriptions/me/cancel/{moduleId}`.
4. Giữ các endpoint đọc ở mức authenticated user, trừ khi product owner muốn chỉ TenantAdmin được xem subscription.
5. Chuẩn hóa unauthorized:
   - anonymous: 401 do authentication middleware.
   - authenticated nhưng sai role ở cancel: 403 do authorization middleware.

#### Application — defense in depth

File: `SMEFLOWSystem.Application/Services/ModuleSubscriptionService.cs`

1. Inject `ICurrentUserService`.
2. Gọi `_currentUser.EnsureAdmin()` ở đầu `CancelMyModuleSubscriptionAsync`.
3. Không tin `X-Tenant-Id` là bằng chứng authorization.
4. Thay `throw new Exception("Không tìm thấy...")` bằng typed exception phù hợp; có thể hoàn tất mapping response ở Phase 7.

### Test bắt buộc

| Tình huống | Kỳ vọng |
|---|---|
| Anonymous, không header | 401 |
| Anonymous + `X-Tenant-Id` hợp lệ | 401, subscription không đổi |
| Employee cùng Tenant | 403, subscription không đổi |
| Manager/HRManager cùng Tenant | 403, subscription không đổi |
| TenantAdmin đúng Tenant | 200, chỉ subscription đúng Tenant bị hủy |
| TenantAdmin Tenant A cố tác động Tenant B bằng header | Chỉ claim Tenant A có hiệu lực; Tenant B không đổi |

### Tiêu chí hoàn thành

- Không có đường gọi HTTP hoặc service công khai nào cho phép non-TenantAdmin hủy module.
- Không còn trả 401 cho user đã đăng nhập nhưng thiếu role; trường hợp này phải là 403.
- Không thay đổi contract thành công của endpoint.

### Trạng thái triển khai Phase 1 — hoàn thành 2026-07-24

Đã thay đổi:

- `ModuleSubscriptionsController` có `[Authorize]` ở class level.
- `CancelMyModuleSubscription` có policy `TenantAdmin`.
- `ModuleSubscriptionService` inject `ICurrentUserService` và gọi
  `EnsureAdmin()` trước khi resolve Tenant hoặc truy cập repository.
- Missing subscription dùng `KeyNotFoundException` thay cho `Exception` chung.
- Hai characterization test `BE-AUTH-01` đã được đổi từ `KnownBugFact`
  thành regression test `Fact`.
- Thêm `ModuleSubscriptionAuthorizationTests` kiểm tra defense-in-depth.

Ma trận test service đã khóa:

| Caller | Kết quả |
|---|---|
| Anonymous/no role | `UnauthorizedAccessException`, repository không được gọi |
| Employee | Bị từ chối trước repository |
| Manager | Bị từ chối trước repository |
| HRManager | Bị từ chối trước repository |
| SystemAdmin | Bị từ chối trước repository |
| TenantAdmin | Chỉ subscription của Tenant hiện tại bị mutate |
| TenantAdmin + module không tồn tại | Typed `KeyNotFoundException`, không update |

Kết quả kiểm thử:

- Targeted Phase 1: 9/9 pass.
- Full suite mặc định: 107 pass, 10 known-gap skipped, 0 fail.
- Characterization opt-in Phase 0 sau khi sửa:
  - 8 pass;
  - 10 fail còn lại;
  - hai failure `BE-AUTH-01` đã chuyển sang pass.

---

## 6. Phase 2 — Vá tenant isolation cho `includeDeleted` (BE-HR-01)

### Mục tiêu

Cho phép xem bản ghi đã xóa trong **Tenant hiện tại**, tuyệt đối không gỡ tenant boundary.

### Thay đổi dự kiến

Files:

- `SMEFLOWSystem.Infrastructure/Repositories/ShiftRepository.cs`
- `SMEFLOWSystem.Infrastructure/Repositories/ShiftPatternRepository.cs`
- constructors/DI tests liên quan

Các bước:

1. Inject `ICurrentTenantService` vào hai repository.
2. Resolve `tenantId`; nếu thiếu thì throw unauthorized trước khi tạo query include-deleted.
3. Với `includeDeleted=false`:
   - Dùng query mặc định để EF áp tenant + soft-delete filter.
4. Với `includeDeleted=true`:
   - Có thể dùng `IgnoreQueryFilters()`, nhưng bắt buộc nối ngay:

   ```csharp
   query = query
       .IgnoreQueryFilters()
       .Where(x => x.TenantId == tenantId);
   ```

5. Không dựa vào filter từ service/controller.
6. Rà soát navigation `Segments`/`Days`:
   - xác nhận dữ liệu con luôn cùng Tenant với root;
   - nếu cần, dùng filtered include theo `TenantId` để tránh lộ dữ liệu khi DB bị sai invariant.
7. Tìm toàn repo các vị trí `IgnoreQueryFilters()` và phân loại:
   - system-admin cross-tenant có chủ đích;
   - tenant-scoped và đã tự re-apply TenantId;
   - tenant-scoped chưa an toàn, tạo follow-up ticket nếu ngoài phạm vi phase.

### Không nên làm

- Không xóa `ApplySoftDeleteQueryFilters`.
- Không thêm `!IsDeleted` thủ công vào mọi query mặc định; filter toàn cục đã thực hiện việc đó.
- Không dùng `IgnoreQueryFilters()` rồi lọc Tenant ở memory sau `ToListAsync`.

### Test bắt buộc

Cho cả Shift và ShiftPattern:

| `includeDeleted` | Dữ liệu Tenant A active | Tenant A deleted | Tenant B active/deleted |
|---|---:|---:|---:|
| `false` | Có | Không | Không |
| `true` | Có | Có | Không |

Thêm test:

- `TotalCount` chỉ đếm Tenant hiện tại.
- Search + paging không làm mất tenant predicate.
- Tenant ID null không trả dữ liệu.

### Tiêu chí hoàn thành

- SQL/query expression chứa predicate TenantId ngay cả khi include deleted.
- Không có response nào chứa Shift/ShiftPattern của Tenant khác.
- Không cần migration DB.

### Trạng thái triển khai Phase 2 — hoàn thành 2026-07-27

Đã thay đổi:

- `ShiftRepository` và `ShiftPatternRepository` inject
  `ICurrentTenantService`.
- Hai `GetPagedAsync` resolve Tenant ID trước khi query; thiếu Tenant bị từ
  chối bằng `UnauthorizedAccessException`.
- Với `includeDeleted=false`, query mặc định tiếp tục dùng global tenant +
  soft-delete filter.
- Với `includeDeleted=true`, query dùng `IgnoreQueryFilters()` nhưng áp lại
  ngay `Where(x => x.TenantId == tenantId)` trước search, count và paging.
- Các navigation `Segments`, `Days`, `ScheduledShift` và nested `Segments`
  cũng được lọc theo Tenant để không lộ dữ liệu khi quan hệ bị sai invariant.
- Hai characterization test `BE-HR-01` đã đổi từ `KnownBugFact` thành
  regression test `Fact`.
- Thêm `ShiftTenantIsolationTests` cho default query, include-deleted,
  search, paging và missing tenant.

Kết quả kiểm thử:

- Targeted Phase 2 và navigation hardening: 11/11 pass.
- Full suite mặc định: 116 pass, 8 known-gap skipped, 0 fail.
- Characterization opt-in Phase 0 sau khi sửa:
  - 10 pass;
  - 8 fail còn lại;
  - hai failure `BE-HR-01` đã chuyển sang pass.

Rà soát `IgnoreQueryFilters()` trong Infrastructure:

- Các system-admin read/reset repository chủ động cross-tenant theo chức
  năng hệ thống.
- Các lookup pre-auth như invite token/refresh token/payment gateway dùng
  token hoặc gateway transaction ID thay cho current-tenant filter.
- Các tenant-scoped lookup hiện có như Employee include-deleted và Customer
  internal đã tự áp `TenantId`.
- Lỗi `includeDeleted` không re-apply TenantId được xác nhận và sửa tại hai
  repository Shift trong phạm vi phase này.

---

## 7. Phase 3 — Scope Manager cho Leave Request (BE-LEAVE-01)

### Mục tiêu

Manager chỉ xem và xử lý đơn của nhân viên thuộc phòng ban được giao.

### Thay đổi dự kiến

#### Repository contract

Files:

- `SMEFLOWSystem.Application/Interfaces/IRepositories/ILeaveRequestRepository.cs`
- `SMEFLOWSystem.Infrastructure/Repositories/LeaveRequestRepository.cs`

1. Đổi signatures:

   ```csharp
   Task<List<LeaveRequest>> GetPendingAsync(IReadOnlyCollection<Guid>? departmentIds);
   Task<List<LeaveRequest>> GetAllAsync(IReadOnlyCollection<Guid>? departmentIds);
   ```

2. Nếu `departmentIds == null`, giữ toàn bộ dữ liệu trong Tenant do global filter.
3. Nếu danh sách rỗng, trả rỗng mà không query toàn bảng.
4. Nếu có ID, lọc ở database:

   ```csharp
   query = query.Where(r =>
       r.Employee != null &&
       r.Employee.DepartmentId.HasValue &&
       departmentIds.Contains(r.Employee.DepartmentId.Value));
   ```

5. Không tải toàn Tenant rồi lọc trong service.

#### Service authorization

File: `SMEFLOWSystem.Application/Services/LeaveRequestService.cs`

1. Inject `IHrAuthorizationService`.
2. `GetPendingRequestsAsync` và `GetAllRequestsAsync`:
   - gọi `GetAccessibleDepartmentIdsAsync`;
   - truyền kết quả xuống repository.
3. `ApproveLeaveRequestAsync` và `RejectLeaveRequestAsync`:
   - load request kèm Employee;
   - nếu Employee không tồn tại/không load được, fail trước khi mutate;
   - gọi `EnsureEmployeeAccessAsync(request.Employee)` trước `Approve`/`Reject`;
   - chỉ sau khi authorization pass mới cập nhật request, balance và trigger recalculation.
4. Không dùng `hrUserId` làm bằng chứng quyền; ID đó chỉ dùng cho audit.

#### Controller/error mapping tạm thời

File: `SMEFLOWSystem.WebAPI/Controllers/LeaveRequestController.cs`

- Trước khi Phase 7 có global handler, bổ sung mapping `UnauthorizedAccessException -> 403` cho approve/reject, hoặc dùng helper chung.
- Sau Phase 7 có thể bỏ catch lặp lại.

### Test bắt buộc

| Caller | Target | List | Approve/Reject |
|---|---|---|---|
| TenantAdmin | mọi department cùng Tenant | thấy | được phép |
| HRManager | mọi department cùng Tenant | thấy | được phép |
| Manager A1 | Employee A1 | thấy | được phép |
| Manager A1 | Employee A2 | không thấy | 403, không mutate |
| Manager không có department | bất kỳ | danh sách rỗng | 403 |
| Caller Tenant A | request Tenant B | không thấy | 404 hoặc 403 theo contract, không mutate |

Kiểm tra thêm khi authorization fail:

- `LeaveRequest.Status` không đổi.
- Leave balance không đổi.
- Không gọi recalculation.
- Không ghi approver/rejecter audit.

### Tiêu chí hoàn thành

- Read scope và write scope dùng cùng một nguồn quyền `IHrAuthorizationService`.
- Không có IDOR bằng cách gọi trực tiếp request ID ngoài department.

### Trạng thái triển khai Phase 3 — hoàn thành 2026-07-27

Đã thay đổi:

- `ILeaveRequestRepository.GetPendingAsync` và `GetAllAsync` nhận
  `IReadOnlyCollection<Guid>? departmentIds`.
- `LeaveRequestRepository` áp dụng department predicate ngay trong database
  query; `null` giữ quyền toàn Tenant, còn danh sách rỗng trả rỗng mà không
  query toàn bảng.
- `LeaveRequestService` inject `IHrAuthorizationService`, chuyển read scope
  xuống repository và gọi `EnsureEmployeeAccessAsync` trước khi mutate ở cả
  approve/reject.
- Nếu authorization thất bại, request vẫn `Pending`, không ghi audit và không
  gọi repository update/transaction.
- `LeaveRequestController` map lỗi ngoài phạm vi ở approve/reject thành HTTP
  403 trong khi chờ global exception handler của Phase 7.
- Hai characterization test `BE-LEAVE-01` đã đổi từ `KnownBugFact` thành
  regression test `Fact`.
- Thêm `LeaveRequestManagerScopeTests` bao phủ tenant/department/status query,
  role matrix TenantAdmin/HRManager/Manager, manager không được gán phòng ban,
  write authorization trước mutation và response 403.

Kết quả kiểm thử:

- Targeted Phase 3: 12/12 pass.
- Full suite mặc định: 128 pass, 6 known-gap skipped, 0 fail.
- Characterization opt-in Phase 0 sau khi sửa:
  - 12 pass;
  - 6 fail còn lại;
  - hai failure `BE-LEAVE-01` đã chuyển sang pass.

---

## 8. Phase 4 — Scope Manager cho Manual Timesheet (BE-MGR-05)

### Mục tiêu

GET manual timesheets chỉ trả dữ liệu thuộc department Manager quản lý.

### Thay đổi dự kiến

Files:

- `SMEFLOWSystem.Application/Services/ManualTimesheetService.cs`
- `SMEFLOWSystem.Application/Interfaces/IRepositories/IManualMonthlyTimesheetRepository.cs`
- `SMEFLOWSystem.Infrastructure/Repositories/ManualMonthlyTimesheetRepository.cs`

1. Inject `IHrAuthorizationService` vào `ManualTimesheetService`.
2. Sau `EnsureHrAccess`, gọi `GetAccessibleDepartmentIdsAsync`.
3. Mở rộng repository method:

   ```csharp
   GetByTenantMonthYearAsync(
       Guid tenantId,
       int month,
       int year,
       IReadOnlyCollection<Guid>? departmentIds)
   ```

4. Lọc ngay tại database qua `ManualMonthlyTimesheet.Employee.DepartmentId`.
5. Manager chưa có department phải nhận danh sách rỗng.
6. Bản ghi của employee không có department không được hiển thị cho Manager.
7. Không thay đổi Upsert/Delete vì hai endpoint này đã giới hạn `AdminOrHr`.

### Test bắt buộc

- TenantAdmin/HRManager thấy toàn Tenant.
- Manager một department chỉ thấy department đó.
- Manager nhiều department thấy hợp đúng các department, không duplicate.
- Manager không có department nhận empty list.
- Không thấy Tenant khác.
- Filter tháng/năm vẫn đúng sau khi thêm department predicate.

### Tiêu chí hoàn thành

- Không còn client-side join/filter là lớp bảo vệ dữ liệu duy nhất.
- Không đổi response DTO.

### Trạng thái triển khai Phase 4 — hoàn thành 2026-07-27

Đã thay đổi:

- `IManualMonthlyTimesheetRepository.GetByTenantMonthYearAsync` nhận
  `IReadOnlyCollection<Guid>? departmentIds`.
- `ManualMonthlyTimesheetRepository` lọc tenant, tháng/năm và department ngay
  trong database query; `null` giữ quyền toàn Tenant, danh sách rỗng trả rỗng,
  còn danh sách có phần tử chỉ trả employee thuộc các department được giao.
- Timesheet của employee chưa có department chỉ hiển thị cho
  TenantAdmin/HRManager, không hiển thị cho Manager.
- `ManualTimesheetService` inject `IHrAuthorizationService`, lấy accessible
  department scope sau `EnsureHrAccess` và chuyển scope xuống repository.
- Hai luồng nội bộ trong `PayrollService` truyền `departmentIds: null` rõ ràng
  để tiếp tục tính trên toàn Tenant.
- Upsert/Delete và response DTO không thay đổi.
- Hai characterization test `BE-MGR-05` đã đổi từ `KnownBugFact` thành
  regression test `Fact`.
- Thêm `ManualTimesheetManagerScopeTests` bao phủ tenant/month/year query,
  quyền toàn Tenant, một/nhiều/không có department, employee chưa có
  department, không duplicate và việc service chuyển scope xuống repository.

Kết quả kiểm thử:

- Targeted Phase 4: 4/4 pass.
- Full suite mặc định: 132 pass, 4 known-gap skipped, 0 fail.
- Characterization opt-in Phase 0 sau khi sửa:
  - 14 pass;
  - 4 fail còn lại;
  - hai failure `BE-MGR-05` đã chuyển sang pass.

---

## 9. Phase 5 — Đồng nhất lỗi submit punch JSON và multipart (BE-ATT-02)

### Mục tiêu

Hai transport phải trả cùng status code và cùng message cho cùng một lỗi nghiệp vụ.

### Thay đổi dự kiến

File: `SMEFLOWSystem.WebAPI/Controllers/AttendanceController.cs`

1. Không bắt mọi `InvalidOperationException` thành 404 ở multipart.
2. Trích một helper dùng chung cho JSON và multipart, hoặc áp cùng mapping:
   - message bắt đầu bằng `Employee not found` → 404.
   - các business error còn lại → 400 và giữ `ex.Message`.
3. Đưa phần chuyển `IFormFile` sang base64 vào vùng xử lý lỗi phù hợp.
4. Giữ response shape tương thích tạm thời:

   ```json
   { "error": "message" }
   ```

5. Khi Phase 7 hoàn thành, chuyển cả hai endpoint sang global typed exception handler và bỏ catch lặp.

### Test contract bắt buộc

Chạy cùng test case cho JSON và multipart:

| Case | Status |
|---|---:|
| Không có employee record | 404 |
| Fake GPS | 400 |
| Thiếu GPS khi setting bắt buộc | 400 |
| Ngoài bán kính | 400 |
| Request hợp lệ | 200 |

Ngoài status, `error` phải giữ đúng message nghiệp vụ để Flutter hiển thị.

### Tiêu chí hoàn thành

- JSON và multipart không còn khác behavior.
- Không có business validation nào bị giả thành “Employee not found”.

### Trạng thái triển khai Phase 5 — hoàn thành 2026-07-27

Đã thay đổi:

- `AttendanceController.SubmitPunch` và `SubmitPunchForm` dùng chung
  `MapSubmitPunchError`.
- Lỗi có message bắt đầu bằng `Employee not found` trả 404 và giữ nguyên
  message từ service.
- Các `InvalidOperationException` nghiệp vụ còn lại như Fake GPS, bắt buộc GPS
  và ngoài vùng chấm công trả 400, không còn bị giả thành employee-not-found.
- Phần chuyển `IFormFile` sang base64 nằm trong khối xử lý request multipart.
- Response thành công và lỗi vẫn giữ shape tương thích hiện tại:
  `{ data, message }` và `{ error }`.
- Characterization multipart `BE-ATT-02` đã bỏ `KnownBugFact` và trở thành
  regression gate bắt buộc.
- Mở rộng `PhaseZeroAttendanceContractTests` để chạy cùng contract trên JSON
  và multipart cho employee-not-found, Fake GPS, thiếu GPS, ngoài bán kính và
  request hợp lệ; các test cũng xác nhận giữ nguyên message.

Kết quả kiểm thử:

- Targeted Phase 5: 10/10 pass.
- Full suite mặc định: 139 pass, 3 known-gap skipped, 0 fail.
- Characterization opt-in Phase 0 sau khi sửa:
  - 21 pass;
  - 3 fail còn lại;
  - failure `BE-ATT-02` đã chuyển sang pass.

---

## 10. Phase 6 — Idempotency cho submit punch (BE-ATT-01)

### Mục tiêu

Retry cùng một thao tác không tạo thêm `RawPunchLog`, kể cả hai request đến đồng thời.

### Quyết định thiết kế

Tách rõ hai khái niệm:

- **Idempotency:** dựa trên request key do client sinh và unique constraint ở DB.
- **Business dedup window:** loại các punch cùng loại quá gần nhau; đây chỉ là fallback cho client cũ, không thay thế idempotency.

Không dùng riêng cách “query log gần nhất rồi insert” vì hai request đồng thời vẫn có thể cùng vượt qua bước check.

### API contract đề xuất

1. Thêm `ClientRequestId` dạng UUID/string vào `SubmitPunchRequestDto`.
2. Cả JSON và multipart gửi cùng field này.
3. Flutter sinh một UUID cho mỗi thao tác người dùng và giữ nguyên UUID khi retry.
4. Giai đoạn chuyển tiếp:
   - field optional để app cũ không vỡ;
   - app mới bắt buộc gửi;
   - sau khi adoption đủ, cân nhắc bắt buộc server-side.
5. Khi retry cùng key:
   - trả 200 với chính `RawPunchLogDto` đã tạo;
   - không upload lại selfie nếu tìm thấy record trước khi upload;
   - không gửi realtime notification lần hai.

### Thay đổi domain/database

Files dự kiến:

- `SMEFLOWSystem.Core/Entities/RawPunchLog.cs`
- `SMEFLOWSystem.Infrastructure/Data/Configurations/RawPunchLogConfiguration.cs`
- migration mới trong `SMEFLOWSystem.Infrastructure/Migrations`

Thêm:

```csharp
public string? ClientRequestId { get; set; }
```

Unique index có filter cho dữ liệu mới:

```text
(TenantId, EmployeeId, ClientRequestId)
UNIQUE WHERE ClientRequestId IS NOT NULL
```

Yêu cầu:

- Chuẩn hóa UUID/string trước khi lưu.
- Giới hạn độ dài.
- Không backfill key giả cho log cũ.
- Migration `Down` chỉ xóa index/column mới.

### Thay đổi repository/service

Files:

- `IRawPunchLogRepository`
- `RawPunchLogRepository`
- `AttendanceService`

Luồng xử lý:

1. Resolve employee/tenant.
2. Nếu có `ClientRequestId`, tìm existing log cùng Tenant + Employee + key.
3. Nếu đã có, trả existing ngay.
4. Nếu chưa có, validate business rule và chuẩn bị log.
5. Insert.
6. Nếu insert gặp unique violation do concurrent retry:
   - repository bắt lỗi PostgreSQL unique violation đúng constraint;
   - đọc và trả record đã thắng race;
   - không đổi mọi `DbUpdateException` thành duplicate success.
7. Chỉ request tạo record mới mới phát realtime notification.

### Fallback cho client cũ

Trong thời gian `ClientRequestId` còn optional:

1. Có thể query punch gần nhất của cùng Employee + `PunchType`.
2. Dùng `AttendanceResolutionOptions.DedupWindowMinutes`.
3. Nếu nằm trong cửa sổ, trả log cũ thay vì insert.
4. Ghi metric/log để biết bao nhiêu request không có key.
5. Chấp nhận đây không bảo đảm chống race tuyệt đối; mục tiêu là bỏ fallback sau khi mobile rollout hoàn tất.

### Test bắt buộc

- Cùng key tuần tự 2 lần → một row, cùng response ID.
- Cùng key đồng thời N request → một row.
- Khác key → hai row.
- Cùng key nhưng khác employee → độc lập.
- Cùng key nhưng khác Tenant → độc lập.
- Unique violation ở constraint khác → không bị nuốt.
- Retry không phát notification lần hai.
- Retry không upload selfie lần hai trong đường xử lý không race.
- Client cũ không key tuân theo dedup window đã cấu hình.
- JSON và multipart dùng cùng idempotency behavior.

### Rollout migration

1. Deploy migration nullable column + filtered unique index.
2. Deploy backend chấp nhận optional key.
3. Deploy Flutter gửi UUID.
4. Theo dõi tỷ lệ request thiếu key.
5. Chỉ cân nhắc `required` sau khi version cũ đã hết thời gian hỗ trợ.

### Tiêu chí hoàn thành

- Concurrency test trên PostgreSQL thật chứng minh chỉ có một row cho một idempotency key.
- Không dựa vào cooldown phía client để bảo toàn dữ liệu.

### Trạng thái triển khai Phase 6 — code/migration hoàn thành, chờ PostgreSQL verification 2026-07-27

Đã thay đổi:

- `SubmitPunchRequestDto` và `RawPunchLogDto` có `ClientRequestId` optional để
  tương thích client cũ.
- `RawPunchLog.ClientRequestId` được map thành `varchar(100)` nullable.
- UUID được chuẩn hóa về dạng `D`; string khác được trim và từ chối nếu dài
  hơn 100 ký tự.
- Thêm filtered unique index
  `UX_RawPunchLogs_Tenant_Employee_ClientRequestId` trên
  `(TenantId, EmployeeId, ClientRequestId)` khi key khác null.
- Thêm migration `20260727035930_AddPunchIdempotency`; SQL Up thêm nullable
  column/index và SQL Down chỉ xóa index/column.
- `AttendanceService` lookup key trước business validation/selfie upload.
  Retry tuần tự trả đúng row cũ, không upload và không notification lần hai.
- `RawPunchLogRepository.AddIdempotentAsync` xử lý race bằng unique constraint;
  chỉ PostgreSQL unique violation đúng tên constraint idempotency được chuyển
  thành duplicate success. Constraint khác tiếp tục throw.
- Chỉ request thắng insert mới gửi realtime notification.
- Client cũ không có key dùng `AttendanceResolution:DedupWindowMinutes` theo
  Employee + PunchType và được ghi Information log để theo dõi adoption.
- Hai characterization test `BE-ATT-01` đã đổi từ `KnownBugFact` thành
  regression test `Fact`.
- Thêm `AttendanceIdempotencyTests` bao phủ retry tuần tự, race 8 request,
  khác key/employee/Tenant, constraint classifier, upload/notification,
  chuẩn hóa/giới hạn key và fallback client cũ.
- Thêm `AttendanceIdempotencyPostgreSqlTests`, chạy opt-in trên một PostgreSQL
  test database thật qua biến `PHASE6_POSTGRES_CONNECTION_STRING`.

Kết quả kiểm thử trong môi trường hiện tại:

- Targeted Phase 6: 11 pass, 1 PostgreSQL integration test skipped.
- Full suite mặc định: 150 pass, 2 skipped, 0 fail.
- Characterization opt-in Phase 0:
  - 23 pass;
  - 1 fail còn lại của Phase 7;
  - hai failure `BE-ATT-01` đã chuyển sang pass.
- Migration SQL Up/Down sinh thành công và EF báo không còn pending model
  changes.
- Chưa chạy được PostgreSQL concurrency test vì Docker daemon không khả dụng
  trong môi trường hiện tại. Vì vậy Definition of Done `BE-ATT-01` bên dưới
  chưa đánh dấu hoàn thành.

Chạy verification với database test dùng một lần:

```powershell
docker run --rm --name dodo-phase6-postgres `
  -e POSTGRES_DB=dodosystem_phase6_test `
  -e POSTGRES_USER=dodo_phase6 `
  -e POSTGRES_PASSWORD=phase6-local-only `
  -p 127.0.0.1:55432:5432 `
  -d postgres:16-alpine

$env:PHASE6_POSTGRES_CONNECTION_STRING='Host=localhost;Port=55432;Database=dodosystem_phase6_test;Username=dodo_phase6;Password=phase6-local-only'
dotnet test SMEFLOWSystem.Tests/SMEFLOWSystem.Tests.csproj `
  --no-restore `
  --filter "Phase=6"

docker stop dodo-phase6-postgres
```

Test sẽ tự apply migration và xóa các row fixture sau khi chạy. Không trỏ biến
này vào production database.

---

## 11. Phase 7 — Global exception contract và typed exceptions (BE-MGR-06)

### Mục tiêu

Không để lỗi nghiệp vụ rơi thành 500; đồng thời không che giấu lỗi lập trình/hạ tầng dưới mã 400.

### 11.1. Định nghĩa exception có kiểu

Thêm trong `SMEFLOWSystem.Application/Exceptions`:

- `BusinessRuleException` → 400.
- `ConflictException` → 409.
- Có thể dùng `KeyNotFoundException` → 404 trong giai đoạn đầu.
- `UnauthorizedAccessException`:
  - user chưa authenticated → 401;
  - user authenticated nhưng thiếu quyền → 403.
- Lỗi downstream payment/cloud provider nên có kiểu riêng → 502/503, không phải 400.

Không globally map mọi `InvalidOperationException` sang 400. Loại này còn có thể biểu thị lỗi code/configuration và phải lộ ra monitoring dưới dạng 500.

### 11.2. Global handler .NET 8

Files dự kiến:

- `SMEFLOWSystem.WebAPI/Exceptions/ApiExceptionHandler.cs`
- `SMEFLOWSystem.WebAPI/Extensions/DependencyInjection.cs`
- `SMEFLOWSystem.WebAPI/Validator/WebApplicationExtensions.cs`

Đăng ký:

```csharp
services.AddProblemDetails();
services.AddExceptionHandler<ApiExceptionHandler>();
```

Pipeline:

```csharp
app.UseExceptionHandler();
```

Đặt handler đủ sớm để bao cả controller và custom middleware.

### 11.3. Response contract

Dùng RFC 7807 `ProblemDetails`:

```json
{
  "type": "...",
  "title": "Business rule violation",
  "status": 400,
  "detail": "...",
  "instance": "/api/...",
  "traceId": "...",
  "errorCode": "PAYROLL_NOT_DRAFT",
  "error": "..."
}
```

Trong giai đoạn tương thích, giữ extension `error` để Flutter/Web cũ vẫn đọc được message. Với lỗi 500 ở production, không trả stack trace hoặc message nội bộ.

### 11.4. Chuyển exception theo từng cụm

Ưu tiên:

1. `PayrollService`
   - payroll không tồn tại → 404.
   - payroll không ở Draft → 409 với code ổn định.
   - ngoài scope Manager → 403.
2. `AttendanceService`
   - chuyển các business `InvalidOperationException` đã biết sang typed exception.
3. `ModuleSubscriptionService`
   - subscription không tồn tại → 404 hoặc business conflict theo contract đã chọn.
4. Rà soát các service hiện còn `throw new Exception(...)`:
   - `AuthService`
   - `BillingService`
   - `BillingOrderService`
   - `PaymentService`
   - `PayrollService`
   - `AttendanceService`
   - `ModuleSubscriptionService`
5. Phân loại từng chỗ trước khi thay; đặc biệt lỗi payment provider không được đổi nhầm thành 400.

Ví dụ `RoleController.GetById` và `InviteService` trong report không còn phản ánh chính xác source hiện tại, nên không dùng chúng làm tiêu chí duy nhất cho phase này.

### 11.5. Dọn controller catch blocks

1. Bỏ dần các `catch (Exception ex) { return BadRequest(...) }`.
2. Giữ catch cục bộ chỉ khi endpoint có recovery/fallback thật sự.
3. Không refactor toàn bộ controller trong một commit duy nhất.
4. Mỗi cụm controller được dọn sau khi typed exception và contract test của cụm đó đã có.

### Test bắt buộc

| Exception | HTTP |
|---|---:|
| validation/business rule | 400 |
| resource not found | 404 |
| authenticated nhưng ngoài scope | 403 |
| conflict trạng thái | 409 |
| downstream provider failure | 502/503 theo loại |
| exception không dự kiến | 500 |

Kiểm tra thêm:

- Mọi response có `traceId`.
- 500 production không lộ stack trace/secrets.
- Handler ghi log 5xx ở Error, 4xx nghiệp vụ ở mức phù hợp.
- Không ghi log cùng exception nhiều lần.
- JSON và multipart attendance dùng cùng contract sau khi bỏ catch cục bộ.

### Tiêu chí hoàn thành

- Không còn `throw new Exception(...)` trong các luồng nghiệp vụ đã liệt kê.
- Các endpoint Payroll quan trọng không trả 500 cho not-found/sai trạng thái/ngoài scope.
- Unknown exception vẫn là 500 và có trace để điều tra.

### Trạng thái triển khai Phase 7 — hoàn thành 2026-07-27

Đã thay đổi:

- Thêm `BusinessRuleException`, `ConflictException` và
  `DownstreamServiceException` với `errorCode` ổn định.
- Thêm `ApiExceptionHandler` và đăng ký `AddProblemDetails`,
  `AddExceptionHandler` cùng `UseExceptionHandler` sớm trong HTTP pipeline.
- Global handler trả RFC 7807, giữ extension `error` tương thích client cũ và
  luôn kèm `traceId`/`errorCode`.
- JWT challenge/forbidden, model validation và module access failures dùng
  cùng `ApiProblemDetails` runtime contract thay vì response JSON riêng lẻ.
- Mapping đã khóa: business/validation 400, not-found 404, anonymous 401,
  authenticated ngoài scope 403, conflict 409, downstream 502/503 và unknown
  500.
- Unknown 500 không trả exception message/stack trace; 5xx log một lần ở Error,
  còn 4xx được log ở Warning mà không lặp exception stack.
- Chuyển các lỗi đã phân loại trong `PayrollService`, `AttendanceService`,
  `ModuleSubscriptionService`, `AuthService`, `BillingService`,
  `BillingOrderService` và `PaymentService` sang typed exception hoặc giữ
  `InvalidOperationException` cho lỗi code/configuration cần hiện thành 500.
- Lỗi upload Cloudinary được phân loại là downstream unavailable 503.
- Bỏ catch/response mapping lặp ở Attendance, Auth và cụm Module. Callback VNPay
  vẫn giữ fallback redirect cục bộ vì đó là recovery contract của endpoint.
- JSON và multipart submit punch đều đi qua cùng ProblemDetails contract.
- Characterization `BE-MGR-06` đã đổi thành regression test bắt buộc.

Kết quả kiểm thử:

- Targeted Phase 7/attendance contract/idempotency: 29/29 pass.
- Full suite hiện tại: 167 pass, 1 PostgreSQL Phase 6 test skipped, 0 fail.
- Characterization opt-in Phase 0: 24/24 pass.
- `git diff --check` không phát hiện whitespace error.
- Phase 7 không thêm migration/database change.

---

## 12. Phase 8 — Regression, rollout và phối hợp Mobile

### Regression suite

1. Chạy:

   ```powershell
   dotnet test SMEFLOWSystem.sln --no-restore
   ```

2. Chạy security matrix trên staging với ít nhất hai Tenant.
3. Chạy concurrency test idempotency trên PostgreSQL.
4. Kiểm tra Swagger/contract:
   - authorization requirements;
   - `ClientRequestId`;
   - ProblemDetails responses.
5. Smoke test các luồng:
   - đọc/hủy module;
   - danh sách Shift/ShiftPattern include deleted;
   - list/approve/reject leave;
   - manual timesheets;
   - submit punch JSON và multipart;
   - payroll manual fields.

### Thứ tự deploy

1. Phase 1–2: security hotfix.
2. Phase 3–5: scope Manager và attendance error.
3. Phase 6 migration + backend optional idempotency key.
4. Flutter gửi `ClientRequestId`.
5. Phase 7 exception contract theo từng cụm.
6. Gỡ workaround client sau thời gian quan sát.

### Workaround phía Flutter chỉ được gỡ khi

- Backend version đã deploy production.
- Mobile test xác nhận response mới.
- Không còn user trên backend cũ trong môi trường mục tiêu.
- Monitoring không ghi nhận spike 401/403/409/500.

### Monitoring đề xuất

- Số request cancel module theo role và status.
- Số lần query include-deleted theo Tenant.
- Số lần Manager bị từ chối ngoài department.
- Tỷ lệ punch có/không có `ClientRequestId`.
- Số duplicate key được trả lại existing log.
- Phân bố HTTP 400/401/403/404/409/500 theo endpoint.

### Trạng thái triển khai Phase 8 — hoàn tất phần local 2026-07-27

Đã thay đổi:

- Thêm `ProblemDetailsResponseOperationFilter` để Swagger công bố
  `application/problem+json`, các status typed exception và 401/403 cho endpoint
  có authorization.
- Thêm `ApiProblemDetails` làm schema chính thức cho `traceId`, `errorCode` và
  `error`; response runtime vẫn giữ nguyên wire contract của Phase 7.
- Thêm `ReleaseReadinessContractTests` khóa OpenAPI auth/error contract và xác
  nhận JSON/multipart dùng chung `SubmitPunchRequestDto.ClientRequestId`.
- Thêm script `scripts/verify-mobile-remediation.ps1` để chạy build, full suite,
  Phase 0, Phase 7/8, PostgreSQL idempotency khi có connection string và kiểm
  tra SQL migration Up/Down.
- Thêm `mobile_app_phase8_rollout_runbook.md` mô tả contract mobile, staging
  security matrix hai Tenant, smoke test, deploy order, monitoring, rollback và
  release sign-off.

Kết quả verification local:

- Build: 0 warning, 0 error.
- Full suite: 167 pass, 1 PostgreSQL test skipped, 0 fail.
- Characterization Phase 0: 24/24 pass.
- Phase 7/8 contract suite: 14/14 pass.
- Migration Up SQL có column + filtered unique index; Down SQL xóa index +
  column đúng phạm vi.

Gate cần môi trường ngoài repository:

- PostgreSQL concurrency test chưa chạy vì Docker daemon/database test không
  khả dụng.
- Security matrix hai Tenant và migration Up trên staging chưa chạy vì không có
  staging credentials/endpoint trong workspace.
- Mobile sign-off, monitoring production và việc gỡ workaround vẫn phải thực
  hiện theo release runbook; không được tự đánh dấu hoàn thành từ unit test.

---

## 13. Xử lý riêng BE-MGR-02

### Kết luận

Không sửa theo đề xuất trong report ở đợt bugfix này vì:

1. `ApplySoftDeleteQueryFilters` đã tự ghép `!IsDeleted` với Tenant filter.
2. Repository query mặc định dùng global filter.
3. `HrEmployeeService.RestoreAsync` và `PATCH /api/hr/employees/{id}/restore` đã tồn tại.

### Product gap còn lại, nếu thật sự cần màn hình “Danh sách đã xóa”

Tạo ticket enhancement riêng:

- Thêm `includeDeleted` hoặc `deletionStatus` vào `EmployeeQueryDto`.
- Khi bỏ soft-delete filter phải tự re-apply `TenantId`, giống nguyên tắc Phase 2.
- Thêm `IsDeleted` vào DTO chỉ cho response cần phân biệt active/deleted.
- Quy định role được xem/restore deleted employee.
- Thêm paging và audit restore.

Không trộn enhancement này vào security hotfix để tránh mở thêm đường `IgnoreQueryFilters()` khi chưa có test tenant isolation.

---

## 14. Definition of Done toàn bộ

- [x] BE-AUTH-01: anonymous/header spoof/non-admin không thể hủy subscription.
- [x] BE-HR-01: include deleted không bao giờ trả Shift/ShiftPattern Tenant khác.
- [x] BE-LEAVE-01: Manager không xem hoặc xử lý leave ngoài department.
- [x] BE-MGR-05: Manager không xem manual timesheet ngoài department.
- [x] BE-ATT-02: JSON và multipart trả lỗi tương đương.
- [ ] BE-ATT-01: code/migration hoàn thành; chờ PostgreSQL concurrency verification.
- [x] BE-MGR-06: lỗi nghiệp vụ chính có typed status; lỗi bất ngờ vẫn 500 an toàn.
- [x] BE-MGR-02: có test chứng minh soft-delete filter hiện hoạt động.
- [ ] Tất cả test cũ và test mới pass.
- [ ] Migration Phase 6 có kiểm tra Up/Down và được thử trên staging.
- [ ] Flutter đã nhận contract `ClientRequestId` và ProblemDetails.
- [ ] Workaround client chỉ được gỡ sau production verification.

---

## 15. Cách chia commit/PR đề xuất

1. `test: characterize mobile backend bug report`
2. `fix(auth): protect module subscription endpoints`
3. `fix(tenant): preserve tenant scope when including deleted shifts`
4. `fix(leave): enforce manager department scope`
5. `fix(timesheet): scope manual timesheets by manager departments`
6. `fix(attendance): align multipart punch errors with json endpoint`
7. `feat(attendance): add atomic punch idempotency`
8. `feat(api): add global typed exception handling`
9. `test: add cross-tenant and end-to-end regression matrix`

Không squash các security phase với migration/idempotency phase trước khi staging đã xác nhận từng phần.
