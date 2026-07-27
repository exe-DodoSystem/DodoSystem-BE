# System Admin SaaS API — Phase 9 release runbook

Runbook này áp dụng cho 6 endpoint:

```text
GET /api/system/analytics/revenue-series
GET /api/system/analytics/revenue-breakdown
GET /api/system/analytics/action-center
GET /api/system/analytics/tenants/{tenantId}/financial-summary
GET /api/system/operations/health-summary
GET /api/system/analytics/revenue-forecast
```

## 1. Điều kiện trước khi deploy staging

- Ghi lại application version/commit SHA hiện tại và version sẽ deploy.
- Full test suite pass.
- Contract/security/reconciliation tests của 6 endpoint pass.
- `git diff --check` pass.
- Diff không chứa thay đổi trong:

```text
SMEFLOWSystem.Core/Entities/
SMEFLOWSystem.Infrastructure/Data/SMEFLOWSystemContext.cs
SMEFLOWSystem.Infrastructure/Data/Configurations/
SMEFLOWSystem.Infrastructure/Migrations/
```

- Không chạy `dotnet ef database update`, DDL, DML hoặc backfill.
- Xác nhận build không chứa migration mới. Cơ chế `Database.Migrate()` hiện hữu của
  ứng dụng không được sửa trong feature này và phải không có pending migration để áp dụng.
- Secret, token và connection string chỉ lấy từ secret store của môi trường.

## 2. Deploy staging

1. Deploy đúng application artifact đã qua test; ghi lại image tag/version.
2. Không chạy migration job.
3. Xác nhận `/health` cũ không đổi behavior.
4. Dùng một active System Admin để smoke test đủ 6 endpoint.
5. Kiểm tra một tài khoản chưa đăng nhập nhận `401`.
6. Kiểm tra một tài khoản không phải active System Admin nhận `403`.
7. Lưu `traceId`, status code và latency; không lưu token hoặc response chứa dữ liệu tenant.

## 3. Reconciliation staging

- `revenue-series`: tổng collected khớp payment thành công trong cùng UTC boundary.
- `revenue-breakdown`: `items + other` khớp tổng collected của series.
- `financial-summary`: lifetime/period/outstanding khớp billing/payment drill-down.
- `action-center`: counts khớp tập candidate read-only theo từng loại.
- `revenue-forecast`: có ít nhất 6 tháng liên tục thì trả forecast; nếu thiếu trả
  `422 application/problem+json`.
- `health-summary`: chỉ trả trạng thái canonical, không lộ host, secret hoặc exception.
- SYSTEM tenant không xuất hiện trong kết quả.

FE ghi nhận sign-off bằng application version và thời điểm kiểm tra.

## 4. Performance staging

Đo cold request và warm request cho:

- 30 ngày.
- 12 tháng.
- 24 tháng.

Với từng request, ghi p50/p95, status code và số query. Kiểm tra query plan PostgreSQL
cho các query chậm; không dùng cache để che số liệu sai. Hủy request phía client và xác
nhận server dừng công việc theo cancellation token.

Không đưa số liệu performance local/in-memory vào staging sign-off.

## 5. Production rollout

1. Chỉ dùng đúng artifact đã được staging sign-off.
2. Ghi lại production version trước deploy.
3. Deploy application, không chạy migration job.
4. Smoke test `/health` và 6 endpoint bằng dữ liệu không nhạy cảm.
5. Theo dõi error rate, p95 latency, `401/403/422/500` và dependency health.

## 6. Rollback application

Rollback khi error rate/latency vượt ngưỡng vận hành, contract sai, reconciliation lệch
hoặc dependency health bị ảnh hưởng:

1. Dừng rollout và giữ lại traceId/log liên quan.
2. Chuyển application về image tag/version đã ghi ở bước pre-deploy.
3. Không rollback database vì release này không có database change.
4. Smoke test `/health` cũ và các endpoint quan trọng.
5. Xác nhận error rate/latency trở về baseline.
6. Ghi incident, version lỗi, version rollback và bằng chứng reconciliation.

Không retry deploy version lỗi cho đến khi nguyên nhân đã được xác định và full test pass lại.
