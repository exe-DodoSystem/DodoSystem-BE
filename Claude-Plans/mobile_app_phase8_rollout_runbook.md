# Mobile backend remediation — Phase 8 rollout runbook

## Phạm vi release

Release gồm các thay đổi Phase 1–7:

- khóa quyền hủy module và tenant/header spoofing;
- tenant isolation cho Shift/ShiftPattern khi `includeDeleted=true`;
- department scope cho leave và manual timesheet;
- đồng nhất submit punch JSON/multipart;
- idempotency bằng `ClientRequestId`;
- RFC 7807 `ProblemDetails` và typed exceptions.

Phase 7–8 không thêm database migration. Migration cần deploy là
`20260727035930_AddPunchIdempotency` của Phase 6.

## Contract Mobile

### `ClientRequestId`

- Field: `clientRequestId`, string, tối đa 100 ký tự.
- Khuyến nghị gửi UUID v4 dạng chuẩn.
- Sinh một ID mới khi người dùng bắt đầu một thao tác punch mới.
- Giữ nguyên ID khi retry do timeout, mất mạng hoặc app gửi lại.
- Không tái sử dụng ID cho một thao tác punch khác.
- Cả JSON và multipart gửi cùng field.

Ví dụ JSON:

```json
{
  "latitude": 10.7769,
  "longitude": 106.7009,
  "punchType": "Auto",
  "clientRequestId": "46dd4741-9915-4aaa-9016-f22f93bbc321"
}
```

Multipart dùng field text `clientRequestId` bên cạnh `selfie`.

### `ProblemDetails`

Client đọc lỗi theo thứ tự:

1. `error`;
2. `detail`;
3. message fallback cục bộ.

```json
{
  "type": "https://httpstatuses.com/409",
  "title": "Resource conflict",
  "status": 409,
  "detail": "Phiếu lương đã chốt.",
  "instance": "/api/payrolls/...",
  "traceId": "0HN...",
  "errorCode": "PAYROLL_NOT_DRAFT",
  "error": "Phiếu lương đã chốt."
}
```

Không suy luận lỗi chỉ từ message. Dùng `status` và `errorCode` cho logic:

- 400: dữ liệu/business rule không hợp lệ;
- 401: cần đăng nhập lại;
- 403: đã đăng nhập nhưng không có quyền;
- 404: tài nguyên không tồn tại hoặc ngoài tenant scope;
- 409: trạng thái hiện tại xung đột;
- 502/503: provider tạm lỗi, có thể cho phép retry;
- 500: lỗi bất ngờ; hiển thị thông báo chung và ghi nhận `traceId`.

## Verification tự động

Từ repository root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass `
  -File scripts/verify-mobile-remediation.ps1
```

Để chạy concurrency test trên PostgreSQL test dùng một lần:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass `
  -File scripts/verify-mobile-remediation.ps1 `
  -PostgresConnectionString 'Host=localhost;Port=55432;Database=dodosystem_phase8_test;Username=dodo_phase8;Password=local-only'
```

Không dùng production database cho script verification.

Script kiểm tra:

- build và full test suite;
- Phase 0 characterization;
- Phase 7/8 contract tests;
- PostgreSQL idempotency nếu có connection string;
- SQL Up/Down của migration Phase 6.

## Staging security matrix

Chuẩn bị Tenant A và Tenant B, mỗi tenant có TenantAdmin, HRManager,
Manager và Employee. Manager A1 chỉ quản lý Department A1.

| Luồng | Caller | Target | Kỳ vọng |
|---|---|---|---|
| Cancel module | Anonymous, có/không `X-Tenant-Id` | Tenant A | 401, không mutate |
| Cancel module | Employee/Manager/HRManager A | Tenant A | 403, không mutate |
| Cancel module | TenantAdmin A + header Tenant B | Module A | Chỉ claim Tenant A có hiệu lực |
| Shift include deleted | HR A | Tenant A/B | Có active/deleted A, không có B |
| ShiftPattern include deleted | HR A | Tenant A/B | Có active/deleted A, không có B |
| Leave list | Manager A1 | Department A1/A2 | Chỉ thấy A1 |
| Leave approve/reject | Manager A1 | Request A2 | 403/404, không mutate |
| Manual timesheet | Manager A1 | Department A1/A2 | Chỉ thấy A1 |
| Punch retry | Employee A | Cùng `ClientRequestId` | Cùng response ID, một DB row |
| Payroll manual fields | Manager A1 | Employee A2 | 403, payroll không đổi |

Sau mỗi ca bị từ chối, kiểm tra trực tiếp database/audit để xác nhận không có
mutation ngoài ý muốn.

## Smoke test API

1. Đọc và hủy module bằng đúng/sai role.
2. Query Shift và ShiftPattern với `includeDeleted=false/true`.
3. List, approve và reject leave ở trong/ngoài department.
4. List manual timesheet với Manager có nhiều/không có department.
5. Submit punch hợp lệ và lỗi bằng cả JSON/multipart.
6. Gửi đồng thời cùng `ClientRequestId`.
7. Cập nhật payroll Draft; thử lại khi Published để nhận 409.
8. Xác nhận mọi lỗi API có `traceId`, `errorCode`, `error`.
9. Mở Swagger và xác nhận request punch cùng các response
   `application/problem+json`.

## Thứ tự deploy

1. Backup database và ghi nhận migration hiện tại.
2. Deploy backend có Phase 1–7.
3. Apply migration `AddPunchIdempotency`.
4. Chạy health check và smoke test backend.
5. Phát hành mobile gửi `ClientRequestId` nhưng vẫn giữ error fallback cũ.
6. Theo dõi tối thiểu một chu kỳ release.
7. Chỉ gỡ workaround mobile khi toàn bộ traffic dùng backend mới.

## Monitoring và rollback

Theo dõi theo endpoint/tenant:

- phân bố 400/401/403/404/409/500/502/503;
- cancel module bị từ chối theo role;
- Manager bị từ chối ngoài department;
- tỷ lệ punch có `ClientRequestId`;
- số retry trả existing punch;
- duplicate-key/database error;
- spike 500 và `traceId` liên quan.

Nếu backend cần rollback, field `ClientRequestId` đang optional nên mobile mới
vẫn tương thích với backend cũ ở mức request payload. Không rollback migration
trong lúc còn backend mới đang chạy. Chỉ chạy migration Down khi đã dừng toàn bộ
instance dùng column/index mới và đã backup dữ liệu.

## Release sign-off

- [ ] Automated verification pass.
- [ ] PostgreSQL concurrency test pass.
- [ ] Migration Up/Down được review và Up được chạy trên staging.
- [ ] Security matrix hai Tenant pass.
- [ ] Swagger/ProblemDetails contract được mobile xác nhận.
- [ ] Mobile retry giữ nguyên `ClientRequestId`.
- [ ] Monitoring dashboard/alert đã sẵn sàng.
- [ ] Production smoke test pass.
- [ ] Workaround mobile chỉ gỡ sau thời gian quan sát.
