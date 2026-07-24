# Kế hoạch bổ sung System Admin SaaS API không thay đổi Database

> Trạng thái: kế hoạch triển khai dựa trên source code và schema hiện có  
> Ngày rà soát: 24/07/2026  
> Plan gốc: `Claude-Plans/system_admin_saas_backend_api_plan.md`  
> Ràng buộc bắt buộc: không sửa database đã deploy, không tạo migration và không bật auto migration

---

## 1. Mục tiêu và phạm vi

Tài liệu này đối chiếu plan gốc với Backend hiện tại và chỉ lập kế hoạch cho các API:

1. Chưa tồn tại trong Backend.
2. Có thể trả dữ liệu có ý nghĩa từ schema hiện tại.
3. Không cần thêm bảng, cột, index, constraint hoặc backfill.
4. Không ghi dữ liệu analytics mới vào PostgreSQL.

Plan gốc được giữ nguyên. Tài liệu này là backlog triển khai rút gọn theo ràng buộc database hiện tại.

### 1.1. Database guardrail

Trong toàn bộ phạm vi này, **không được**:

- Sửa entity trong `SMEFLOWSystem.Core/Entities`.
- Sửa `SMEFLOWSystemContext` hoặc EF configuration để thay đổi model.
- Tạo/chỉnh file trong `SMEFLOWSystem.Infrastructure/Migrations`.
- Chạy `dotnet ef migrations add`, `dotnet ef database update` hoặc DDL thủ công.
- Thêm bảng aggregate, snapshot, audit, refund, export, usage hoặc incident.
- Backfill hay cập nhật lại dữ liệu production.
- Thay đổi cơ chế `Database.Migrate()` hiện tại hoặc bật thêm auto migration.

Được phép:

- Query read-only các bảng/cột hiện có bằng `AsNoTracking()`.
- Dùng `IgnoreQueryFilters()` trong repository System Admin, nhưng phải tự lọc tenant `SYSTEM`, tenant bị xóa và dữ liệu soft-delete theo đúng nghiệp vụ.
- Thêm controller, DTO, validator, application service, read repository, option và unit test.
- Dùng `IMemoryCache` đang được đăng ký sẵn để cache response ngắn hạn.
- Thêm cấu hình trong `appsettings`/environment variable; cấu hình không được làm thay đổi schema.

---

## 2. Kết quả rà soát Backend hiện tại

### 2.1. API đã có và tiếp tục giữ nguyên

Các controller hiện tại đã cung cấp 16 endpoint sau:

| Nhóm | Endpoint đã có |
|---|---|
| Dashboard | `GET /api/system/dashboard/overview` |
| Dashboard | `GET /api/system/dashboard/module-usage` |
| Dashboard | `GET /api/system/dashboard/module-cancellations` |
| Dashboard | `GET /api/system/dashboard/module-expirations` |
| Dashboard | `GET /api/system/dashboard/module-trends` |
| Tenant | `GET /api/system/tenants` |
| Tenant | `GET /api/system/tenants/{tenantId}` |
| Tenant | `GET /api/system/tenants/{tenantId}/users` |
| Tenant | `PATCH /api/system/tenants/{tenantId}/status` |
| Subscription | `GET /api/system/subscriptions` |
| Subscription | `POST /api/system/subscriptions/{subscriptionId}/extend` |
| Subscription | `POST /api/system/subscriptions/{subscriptionId}/suspend` |
| Subscription | `POST /api/system/subscriptions/{subscriptionId}/reactivate` |
| Billing | `GET /api/system/billing-orders` |
| Billing | `GET /api/system/billing-orders/{billingOrderId}` |
| Payment | `GET /api/system/payment-transactions` |

Tất cả nhóm trên đã dùng policy `SystemAdmin`. Chúng tiếp tục được dùng làm API drill-down và không bị thay đổi trong kế hoạch này.

Backend cũng đã có `GET /health` dựa trên ASP.NET Core Health Checks cho PostgreSQL, Redis và RabbitMQ. Endpoint này chưa phải contract `GET /api/system/operations/health-summary` vì chưa có authorization System Admin và response tổng hợp dành cho UI.

### 2.2. Dữ liệu hiện có có thể tái sử dụng

| Nguồn hiện tại | Dữ liệu dùng được |
|---|---|
| `Tenant` | `Id`, `Name`, `Status`, `SubscriptionEndDate`, `CreatedAt`, `UpdatedAt`, `IsDeleted` |
| `ModuleSubscription` | tenant/module, `StartDate`, `EndDate`, trạng thái hiện tại, `CreatedAt`, `UpdatedAt`, `IsDeleted` |
| `Module` | code, tên và `MonthlyPrice` hiện tại |
| `BillingOrder` | tenant, `BillingDate`, amount/discount/final amount, payment/order status |
| `BillingOrderModule` | module, quantity, unit price, line total; đủ để phân bổ hóa đơn nhiều module |
| `PaymentTransaction` | gateway, gateway transaction ID, response code, amount, status, `CreatedAt`, `ProcessedAt` |
| Health checks | trạng thái PostgreSQL, Redis, RabbitMQ |

`PaymentTransaction` đã có unique constraint `(Gateway, GatewayTransactionId)`, nên không đề xuất thay đổi constraint trong plan này.

### 2.3. Hạn chế dữ liệu phải công bố trong response

- Payment thành công hiện được lưu bằng chuỗi `Success`, không phải `Succeeded`/`Settled` như contract đề xuất.
- Không có refund ledger.
- Không có giá hợp đồng/commercial term theo lịch sử; chỉ có `Module.MonthlyPrice` hiện tại.
- Không có lịch sử trạng thái tenant/subscription.
- Không có thời điểm đến hạn riêng của billing order.
- Không có cờ chính thức để phân biệt tenant demo/test/sandbox.
- Không ghi nhận payment attempt ngay lúc tạo thanh toán; transaction chủ yếu được tạo khi có callback/webhook.
- Không có audit log System Admin dạng persistent/queryable. `ILogger` hiện tại không thay thế được audit API.
- Không có product usage event, operational incident, export metadata/file store.
- Không có `LastLoginAt`/`LastActivityAt` cho user/tenant.

Vì vậy:

- `excludesTestTenants` phải trả `false`.
- `refundDataAvailable` phải được phản ánh bằng warning và amount refund phải là `null`.
- MRR chỉ được trả với `mrrStatus="Estimated"`.
- Không được trả số `0` để giả lập metric mà source hiện tại không chứng minh được.

---

## 3. Ma trận 20 API còn thiếu trong plan gốc

### 3.1. API sẽ triển khai, không đổi database

| Mức | Endpoint | Khả năng triển khai |
|---|---|---|
| P0 | `GET /api/system/analytics/revenue-series` | Đủ dữ liệu; `refundedAmount=null`, MRR là estimated |
| P0 | `GET /api/system/analytics/revenue-breakdown` | Đủ billing line để phân bổ theo module/tenant/gateway |
| P0 | `GET /api/system/analytics/action-center` | Dùng rule overdue dẫn xuất từ `BillingDate` và cấu hình grace period |
| P1 | `GET /api/system/analytics/tenants/{tenantId}/financial-summary` | Đủ dữ liệu; `currentMrr` là estimated |
| P2 | `GET /api/system/operations/health-summary` | Tái sử dụng health checks hiện có |
| P2 | `GET /api/system/analytics/revenue-forecast` | Tính tại request từ collected revenue; từ chối nếu lịch sử chưa đủ |

### 3.2. API tạm loại vì cần dữ liệu/schema chưa có

| Endpoint | Lý do không triển khai trong phạm vi này |
|---|---|
| `GET /api/system/analytics/summary` | Thiếu expansion/contraction MRR, activation/churn history, trial conversion, NRR và refund |
| `GET /api/system/analytics/tenant-growth` | Không thể dựng lại activated/churned tenant và trạng thái cuối từng kỳ |
| `GET /api/system/analytics/subscription-metrics` | Không tính đúng trial conversion, tenant churn, revenue churn và NRR |
| `GET /api/system/analytics/payment-health` | Không lưu đầy đủ mọi payment attempt và pending attempt |
| `GET /api/system/analytics/tenant-health` | Không có login/activity/usage history đáng tin cậy |
| `GET /api/system/analytics/tenants/{tenantId}/health` | Cùng thiếu hụt dữ liệu tenant health |
| `GET /api/system/audit-logs` | Không có nguồn audit persistent có thể query |
| `GET /api/system/audit-logs/{auditLogId}` | Không có nguồn audit persistent có thể query |
| `POST /api/system/analytics/exports` | Không có export metadata và file/object storage phù hợp |
| `GET /api/system/analytics/exports/{exportId}` | Không có trạng thái export persistent |
| `GET /api/system/analytics/exports/{exportId}/download` | Không có file store/token store phù hợp |
| `GET /api/system/analytics/product-usage` | Không có product usage event |
| `GET /api/system/analytics/feature-adoption` | Không có feature event taxonomy/data |
| `GET /api/system/operations/incidents` | Không có incident source/store |

Không tạo endpoint stub trả `200` với số giả cho các API bị loại. Chỉ đưa chúng trở lại backlog khi có nguồn dữ liệu hợp lệ mà không vi phạm ràng buộc database.

---

## 4. Quy ước chung cho 6 API sẽ làm

### 4.1. Authorization

- Tất cả controller mới dùng `[Authorize(Policy = PolicyNames.SystemAdmin)]`.
- Không dựa vào tenant query filter mặc định để tổng hợp toàn hệ thống.
- Mọi query cross-tenant phải:
  1. Dùng `IgnoreQueryFilters()`.
  2. Join về `Tenant`.
  3. Loại `Tenant.IsDeleted`.
  4. Loại tenant có `Name == SystemTenantConstants.Name`.
- Không trả `RawData`, password, token hoặc gateway secret.

### 4.2. Query kỳ analytics

Tạo `SystemAnalyticsPeriodQueryDto`:

```csharp
public sealed class SystemAnalyticsPeriodQueryDto
{
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public string Timezone { get; set; } = "Asia/Ho_Chi_Minh";
    public string Currency { get; set; } = "VND";
    public string Compare { get; set; } = "previous_period";
    public int? ModuleId { get; set; }
    public string TenantSegment { get; set; } = "all";
}
```

Validation:

- Mặc định 30 ngày gần nhất theo `Asia/Ho_Chi_Minh`.
- `from <= to`.
- Range tối đa 24 tháng.
- Currency chỉ nhận `VND`.
- Timezone chỉ nhận allow-list ban đầu: `Asia/Ho_Chi_Minh`.
- `compare`: `previous_period`, `previous_year`, `none`.
- `tenantSegment`: `all`, `paid`, `trial`.
- `moduleId` phải tồn tại; nếu không trả `400 ProblemDetails`.
- Chuyển ranh giới ngày local thành `[fromUtc, toExclusiveUtc)` trước khi query.

Không dùng `DateTime <= endOfDay`; dùng cận trên exclusive để tránh mất dữ liệu có precision cao.

### 4.3. Analytics metadata

Mọi response analytics dùng cùng `SystemAnalyticsMetaDto`:

```text
from
to
previousFrom
previousTo
timezone = Asia/Ho_Chi_Minh
currency = VND
generatedAt
dataThrough
freshness = Live
excludesInternalTenant = true
excludesTestTenants = false
mrrStatus = Estimated hoặc Unavailable
warnings[]
```

Warnings chuẩn trong phạm vi hiện tại:

- `REFUND_DATA_UNAVAILABLE`
- `TEST_TENANT_FLAG_UNAVAILABLE`
- `MRR_USES_CURRENT_CATALOG_PRICE`
- `PAYMENT_WITHOUT_PROCESSED_AT_EXCLUDED`
- `ORDER_OVERDUE_USES_CONFIGURED_GRACE_PERIOD`
- `FORECAST_EXCLUDES_REFUNDS`

### 4.4. Payment status adapter

Không sửa giá trị trong database. Tạo helper read-only:

```text
IsSuccessfulPayment(status)
  = equals "Success", "Succeeded", "Settled" hoặc "Paid"

IsFailedPayment(status)
  = equals "Failed"
```

So sánh không phân biệt hoa/thường. Collected revenue chỉ lấy payment thành công có `ProcessedAt != null`, dùng `ProcessedAt` để xác định kỳ.

Nếu phát hiện status ngoài allow-list:

- Không cộng vào collected revenue.
- Ghi structured log theo số lượng, không log raw payload.
- Thêm warning về trạng thái không nhận diện.

### 4.5. MRR estimated

Không sửa subscription và không tạo commercial snapshot.

MRR estimated tại một thời điểm:

```text
SUM(Module.MonthlyPrice)
```

cho các `ModuleSubscription`:

- `StartDate <= asOf`.
- `EndDate > asOf`.
- `IsDeleted == false`.
- `Status` là `Active`.
- Tenant hợp lệ và không phải `SYSTEM`.

Trial không được cộng vào MRR. Vì giá dùng catalog hiện tại thay vì contract price lịch sử, response luôn ghi:

```text
mrrStatus = "Estimated"
warnings += "MRR_USES_CURRENT_CATALOG_PRICE"
```

### 4.6. Error contract

API mới trả RFC 7807 `ProblemDetails`:

- `400`: query/range/dimension/granularity không hợp lệ.
- `403`: không phải active System Admin.
- `404`: tenant/module không tồn tại.
- `422`: forecast không đủ lịch sử để tính.
- `500`: lỗi không mong đợi, không lộ SQL/raw gateway payload.

Mỗi error có `traceId`.

---

## 5. Contract và cách triển khai từng API

### 5.1. Revenue series

```text
GET /api/system/analytics/revenue-series
```

Query bổ sung:

```text
granularity=day|week|month
```

Mặc định:

- Tối đa 31 ngày: `day`.
- 32–180 ngày: `week`.
- Trên 180 ngày: `month`.

Nguồn từng field:

| Field | Nguồn/cách tính |
|---|---|
| `invoicedRevenue` | Tổng `BillingOrder.FinalAmount ?? (TotalAmount - DiscountAmount)` theo `BillingDate`, loại order deleted/cancelled |
| `collectedRevenue` | Tổng `PaymentTransaction.Amount` của status thành công theo `ProcessedAt` |
| `refundedAmount` | Luôn `null`, kèm `REFUND_DATA_UNAVAILABLE` |
| `outstandingCreated` | Tổng final amount của order được tạo trong bucket và đang `PaymentStatus=Pending` tại lúc query |
| `mrrSnapshot` | Estimated MRR tại cuối bucket từ active subscription và catalog price hiện tại |

Yêu cầu:

- Sinh toàn bộ bucket trong application service và điền `0` cho bucket không có dữ liệu.
- Week bắt đầu thứ Hai theo timezone đã chọn.
- `previousPoints` chỉ trả khi `compare != none`.
- `dataThrough` lấy timestamp lớn nhất trong các nguồn hợp lệ đã dùng.
- Không dùng `CreatedAt` thay cho `ProcessedAt` cho collected revenue.
- Cache normalized query tối đa 2 phút bằng `IMemoryCache`.

Acceptance criteria:

- Tổng collected của các point bằng query source trong cùng kỳ.
- Bucket qua ranh giới UTC/+07 không bị lệch ngày.
- Empty range vẫn trả đủ bucket với số `0`.
- Payment trùng gateway ID không thể bị cộng hai lần do constraint hiện tại.

### 5.2. Revenue breakdown

```text
GET /api/system/analytics/revenue-breakdown
```

Query bổ sung:

```text
dimension=module|tenant|gateway
limit=5..50
```

Quy tắc:

- `tenant`: group payment/order theo `TenantId`.
- `gateway`: group successful payment theo `PaymentTransaction.Gateway`.
- `module`: dùng `BillingOrderModule.LineTotal` làm trọng số phân bổ.
- Discount của order được phân bổ tỷ lệ theo line total.
- Collected amount của order nhiều module được phân bổ theo cùng trọng số.
- Nếu phép chia decimal có phần dư, cộng phần dư vào item cuối để tổng tuyệt đối khớp.
- Order không có billing line không được tự gán cho một module; đưa vào warning/unallocated amount nội bộ và không làm sai `totalCollectedRevenue`.
- `items + other` phải khớp tổng sau rounding.
- `percentageOfTotal=0` khi tổng bằng 0.

Acceptance criteria:

- Order hai module không bị cộng toàn bộ amount hai lần.
- Breakdown theo tenant/gateway khớp tổng collected của revenue series.
- Limit ngoài `5..50` trả `400`.
- Không trả raw gateway message.

### 5.3. Action center

```text
GET /api/system/analytics/action-center
```

Nguồn:

| Loại | Rule dùng schema hiện tại |
|---|---|
| `PaymentFailed` | Payment status `Failed`, ưu tiên 24 giờ gần nhất |
| `OrderOverdue` | Order `PaymentStatus=Pending` và `BillingDate <= now - overdueGraceHours` |
| `SubscriptionExpiring` | Active subscription có `EndDate` trong 7 ngày |
| `TrialEnding` | Trial subscription có `EndDate` trong 7 ngày |
| `TenantSuspended` | Tenant status `Suspended` |

Thêm option không liên quan database:

```json
{
  "SystemAnalytics": {
    "OrderOverdueGraceHours": 24,
    "ActionCenterMaxItems": 100
  }
}
```

Response phải thêm warning `ORDER_OVERDUE_USES_CONFIGURED_GRACE_PERIOD` để tránh hiểu `BillingDate` là due date chính thức.

Quy tắc item:

- Sắp xếp severity rồi `occurredAt` giảm dần.
- `critical`: failed payment mới hoặc order overdue.
- `warning`: subscription/trial sắp hết hạn.
- `info`: tenant suspended.
- `id` ổn định theo `type + entityId`.
- `targetPath` tạo từ route constant allow-list, không lấy từ database/user input.
- Không quá `ActionCenterMaxItems`; counts vẫn là tổng đầy đủ.

Acceptance criteria:

- Không có URL ngoài.
- Tenant `SYSTEM` không xuất hiện.
- Không có duplicate item cho cùng `type + entityId`.
- Đổi grace period chỉ cần config/redeploy, không có migration.

### 5.4. Tenant financial summary

```text
GET /api/system/analytics/tenants/{tenantId}/financial-summary
```

Nguồn:

| Field | Cách tính |
|---|---|
| `currentMrr` | Estimated MRR của active subscription thuộc tenant |
| `lifetimeCollectedRevenue` | Tổng successful payment có `ProcessedAt` của tenant |
| `collectedRevenueInPeriod` | Tổng successful payment trong kỳ |
| `outstandingAmount` | Tổng final amount order đang pending |
| `lastSuccessfulPaymentAt` | Max `ProcessedAt` của successful payment |
| `lastFailedPaymentAt` | Max `ProcessedAt` của failed payment |
| `averagePaymentDelayDays` | Trung bình `ProcessedAt - BillingDate` của successful payment |
| `subscriptions.active` | Active, chưa hết hạn, không deleted |
| `subscriptions.trial` | Trial, chưa hết hạn, không deleted |
| `subscriptions.expiringIn30Days` | Active/trial hết hạn trong 30 ngày |

Quy tắc:

- Tenant không tồn tại, deleted hoặc `SYSTEM` trả `404`.
- Khoảng delay âm do dữ liệu sai bị loại và thêm warning.
- Không suy diễn refund.
- `currentMrr` luôn đi kèm `mrrStatus=Estimated`.

Acceptance criteria:

- Summary khớp drill-down billing/payment hiện có cho cùng tenant.
- Không N+1 query theo subscription.
- Tenant khác không thể tác động route để xem secret/raw data.

### 5.5. Operations health summary

```text
GET /api/system/operations/health-summary
```

Triển khai bằng `HealthCheckService` hiện có; không query bảng mới.

Response đề xuất:

```ts
interface SystemOperationsHealthSummary {
  status: "Healthy" | "Degraded" | "Unhealthy";
  checkedAt: string;
  durationMs: number;
  components: Array<{
    name: "postgres" | "redis" | "rabbitmq" | string;
    status: "Healthy" | "Degraded" | "Unhealthy";
    durationMs: number;
    description: string | null;
  }>;
}
```

Quy tắc:

- Chỉ System Admin được gọi.
- Không trả connection string, host, username, exception stack hoặc secret.
- Map exception thành mô tả an toàn.
- Không thay thế `GET /health`; giữ endpoint cũ cho infrastructure probe.
- Không cache lâu hơn 15 giây.

Acceptance criteria:

- Khi một dependency fail, overall status phản ánh đúng.
- Response không lộ cấu hình.
- `/health` hiện tại không bị đổi behavior.

### 5.6. Revenue forecast

```text
GET /api/system/analytics/revenue-forecast
```

Query:

```text
from
to
forecastPeriods=1..6
granularity=month
```

Phương án không lưu model vào database:

1. Lấy collected revenue theo tháng từ cùng query core của revenue series.
2. Yêu cầu tối thiểu 6 tháng đầy đủ, khuyến nghị 12 tháng.
3. Dùng linear trend đơn giản cho bản đầu; toàn bộ phép tính deterministic.
4. Tính confidence interval từ residual của training data.
5. Clamp lower bound về `0`.
6. Không trộn point forecast vào actual series.

Response luôn có:

```text
method
trainingFrom
trainingTo
generatedAt
currency
granularity
actualPoints
forecastPoints(value, lowerBound, upperBound)
warnings
```

Nếu có ít hơn 6 tháng đầy đủ:

- Trả `422 ProblemDetails`.
- Không trả forecast giả bằng `0`.

Warnings bắt buộc:

- `FORECAST_EXCLUDES_REFUNDS`.
- `FORECAST_BASED_ON_AVAILABLE_PAYMENT_HISTORY`.

Acceptance criteria:

- Cùng input/source cho cùng output, trừ `generatedAt`.
- Confidence interval hợp lệ và `lowerBound >= 0`.
- Không forecast vượt quá 6 tháng.
- Không ghi model/result xuống database.

---

## 6. Thiết kế code theo kiến trúc repo hiện tại

### 6.1. WebAPI

Tạo:

```text
SMEFLOWSystem.WebAPI/Controllers/System/SystemAnalyticsController.cs
SMEFLOWSystem.WebAPI/Controllers/System/SystemTenantAnalyticsController.cs
SMEFLOWSystem.WebAPI/Controllers/System/SystemOperationsController.cs
```

Phân route:

- `SystemAnalyticsController`: revenue series, revenue breakdown, action center, forecast.
- `SystemTenantAnalyticsController`: tenant financial summary.
- `SystemOperationsController`: health summary.

Controller chỉ:

- Bind/validate request.
- Gọi service.
- Map lỗi nghiệp vụ đã biết sang `ProblemDetails`.
- Không chứa EF query hoặc công thức tài chính.

### 6.2. Application

Tạo DTO theo nhóm, tránh một file quá lớn:

```text
SMEFLOWSystem.Application/DTOs/SystemAnalyticsDtos/
  SystemAnalyticsCommonDto.cs
  SystemRevenueSeriesDto.cs
  SystemRevenueBreakdownDto.cs
  SystemActionCenterDto.cs
  SystemTenantFinancialSummaryDto.cs
  SystemRevenueForecastDto.cs
  SystemOperationsHealthDto.cs
```

Tạo interface/service:

```text
Interfaces/IServices/System/ISystemAnalyticsService.cs
Interfaces/IServices/System/ISystemTenantAnalyticsService.cs
Services/System/SystemAnalyticsService.cs
Services/System/SystemTenantAnalyticsService.cs
```

Tạo helper thuần application:

```text
AnalyticsPeriodResolver
AnalyticsMetricCalculator
PaymentStatusClassifier
RevenueBucketBuilder
RevenueAllocationCalculator
RevenueForecastCalculator
```

Các calculator phải là pure function để unit test mà không cần database.

### 6.3. Infrastructure

Tạo read repository:

```text
SMEFLOWSystem.Application/Interfaces/IRepositories/ISystemAnalyticsReadRepository.cs
SMEFLOWSystem.Infrastructure/Repositories/SystemAnalyticsReadRepository.cs
```

Repository trả row/projection cần thiết, không trả EF entity ra service.

Nguyên tắc query:

- `AsNoTracking()`.
- Query theo khoảng UTC đã normalize.
- Aggregate tại PostgreSQL khi phù hợp; bucket fill/allocation có thể hoàn tất ở application.
- Không load toàn bộ lịch sử không giới hạn.
- Luôn truyền `CancellationToken`.
- Không thay đổi repository/entity/configuration hiện có nếu không cần.

Đăng ký DI trong:

```text
SMEFLOWSystem.Application/Extensions/DependencyInjection.cs
SMEFLOWSystem.Infrastructure/Extensions/DependencyInjection.cs
SMEFLOWSystem.WebAPI/Extensions/DependencyInjection.cs
```

Chỉ thêm service registration/options; không thêm database registration hoặc migration.

### 6.4. Validation/options

Tạo:

```text
Validation/SystemValidation/SystemAnalyticsPeriodQueryDtoValidator.cs
Validation/SystemValidation/SystemRevenueSeriesQueryDtoValidator.cs
Validation/SystemValidation/SystemRevenueBreakdownQueryDtoValidator.cs
Validation/SystemValidation/SystemRevenueForecastQueryDtoValidator.cs
Options/SystemAnalyticsOptions.cs
```

Option tối thiểu:

```text
BusinessTimezone
DefaultRangeDays
MaxRangeMonths
CacheSeconds
OrderOverdueGraceHours
ActionCenterMaxItems
ForecastMinimumMonths
ForecastMaximumPeriods
```

---

## 7. Kế hoạch triển khai theo phase

Các phase dưới đây thực hiện **tuần tự từ Phase 0 đến Phase 9**. Không bắt đầu phase tiếp theo khi exit criteria của phase hiện tại chưa đạt.

### Phase 0 — Khóa phạm vi và contract

**Mục tiêu:** chốt chính xác thứ sẽ làm trước khi viết code.

#### Bước 0.1 — Khóa danh sách endpoint

Chỉ đưa 6 endpoint sau vào release:

```text
GET /api/system/analytics/revenue-series
GET /api/system/analytics/revenue-breakdown
GET /api/system/analytics/action-center
GET /api/system/analytics/tenants/{tenantId}/financial-summary
GET /api/system/operations/health-summary
GET /api/system/analytics/revenue-forecast
```

Đánh dấu 14 endpoint tại mục 3.2 là `Out of scope - insufficient current data`.

#### Bước 0.2 — Khóa business rule

Chốt bằng văn bản:

1. Successful payment nhận các status `Success`, `Succeeded`, `Settled`, `Paid`.
2. Failed payment nhận status `Failed`.
3. Collected revenue dùng `ProcessedAt`; record thiếu `ProcessedAt` bị loại.
4. Overdue order là pending quá `BillingDate + OrderOverdueGraceHours`.
5. MRR dùng `Module.MonthlyPrice` hiện tại và luôn là `Estimated`.
6. Refund luôn unavailable/null.
7. Chưa loại được tenant test/sandbox nên `excludesTestTenants=false`.

#### Bước 0.3 — Viết OpenAPI draft

Chuẩn bị request/response example cho 6 endpoint, bao gồm:

- Query, default và giới hạn.
- Nullability.
- Warning code.
- `ProblemDetails` 400/403/404/422.
- Amount dùng VND/decimal.

**Đầu ra Phase 0:**

- Contract được Backend và FE xác nhận.
- Không có code/schema thay đổi.

**Exit criteria:**

- [ ] Không còn field hoặc công thức chưa chốt.
- [ ] FE xác nhận route và field name.
- [ ] Diff không có entity/DbContext/migration.

---

### Phase 1 — Dựng nền tảng analytics dùng chung

**Mục tiêu:** tạo DTO, validation và helper dùng chung; chưa triển khai endpoint nghiệp vụ.

#### Bước 1.1 — Tạo DTO và options

Tạo:

```text
SMEFLOWSystem.Application/DTOs/SystemAnalyticsDtos/SystemAnalyticsCommonDto.cs
SMEFLOWSystem.Application/Options/SystemAnalyticsOptions.cs
```

Bao gồm:

- `SystemAnalyticsPeriodQueryDto`.
- `SystemAnalyticsMetaDto`.
- Enum/string constant cho granularity, compare, dimension, warning code.
- Option timezone, range, cache, overdue và forecast.

#### Bước 1.2 — Tạo period resolver

Tạo `AnalyticsPeriodResolver`:

1. Nhận `DateOnly from/to`.
2. Validate range tối đa 24 tháng.
3. Chuyển đầu ngày Việt Nam sang UTC.
4. Trả cận `[fromUtc, toExclusiveUtc)`.
5. Tính previous period hoặc previous year.
6. Chọn granularity mặc định.

#### Bước 1.3 — Tạo payment status classifier

Tạo `PaymentStatusClassifier` dạng pure function:

- `IsSuccessful`.
- `IsFailed`.
- `IsKnownTerminalStatus`.
- Không sửa status trong database.

#### Bước 1.4 — Tạo validator

Tạo:

```text
SystemAnalyticsPeriodQueryDtoValidator.cs
SystemRevenueSeriesQueryDtoValidator.cs
SystemRevenueBreakdownQueryDtoValidator.cs
SystemRevenueForecastQueryDtoValidator.cs
```

#### Bước 1.5 — Chuẩn hóa lỗi cho endpoint mới

- Map lỗi validation thành `400 ProblemDetails`.
- Tenant/module không tồn tại thành `404`.
- Forecast thiếu lịch sử thành `422`.
- Thêm `traceId`.
- Không thay behavior endpoint cũ.

#### Bước 1.6 — Đăng ký DI/options

Chỉ thêm registration vào Application/WebAPI DI. Không đăng ký entity, DbSet hoặc migration.

**Test Phase 1:**

- Range mặc định và range đảo.
- Boundary đầu/cuối ngày UTC+7.
- Previous period có cùng số ngày.
- Unknown timezone/currency/status.
- Granularity mặc định.

**Đầu ra Phase 1:**

- Foundation compile được.
- Unit test helper pass.
- Chưa có EF query hoặc controller mới public.

**Exit criteria:**

- [ ] `dotnet build` pass.
- [ ] Unit test Phase 1 pass.
- [ ] Không có file database/schema bị sửa.

---

### Phase 2 — Dựng read repository và revenue query core

**Mục tiêu:** có một nguồn query dùng chung trước khi tạo API đầu tiên.

#### Bước 2.1 — Tạo repository interface

Tạo:

```text
SMEFLOWSystem.Application/Interfaces/IRepositories/ISystemAnalyticsReadRepository.cs
```

Định nghĩa các query nhỏ:

- Invoiced order rows/aggregate.
- Successful/failed payment rows/aggregate.
- Billing order module allocation rows.
- Active subscription price rows.
- Tenant financial aggregate.
- Action center candidates.

Không tạo một method tải toàn bộ bảng.

#### Bước 2.2 — Tạo repository implementation

Tạo:

```text
SMEFLOWSystem.Infrastructure/Repositories/SystemAnalyticsReadRepository.cs
```

Mọi query phải:

1. Dùng `AsNoTracking()`.
2. Dùng `[fromUtc, toExclusiveUtc)`.
3. Join `Tenant`.
4. Loại tenant deleted và `SYSTEM`.
5. Tự xử lý soft-delete phù hợp.
6. Chỉ select field cần thiết.
7. Nhận `CancellationToken`.

#### Bước 2.3 — Tạo revenue calculation core

Tạo:

```text
RevenueBucketBuilder
RevenueAllocationCalculator
AnalyticsMetricCalculator
```

Calculator không phụ thuộc EF để có thể unit test riêng.

#### Bước 2.4 — Đăng ký repository

Thêm:

```csharp
services.AddScoped<ISystemAnalyticsReadRepository, SystemAnalyticsReadRepository>();
```

**Test Phase 2:**

- SYSTEM/deleted tenant bị loại.
- Payment thành công/thất bại được phân loại đúng.
- `ProcessedAt=null` không được cộng collected revenue.
- Discount/final amount được tính đúng.
- Query projection không chứa `RawData`.

**Đầu ra Phase 2:**

- Repository/core sẵn sàng cho các endpoint.
- Chưa cần cache và chưa cần forecast.

**Exit criteria:**

- [ ] Query core được test bằng dữ liệu theo schema hiện tại.
- [ ] Không N+1.
- [ ] Không query raw payment payload.
- [ ] Không có migration.

---

### Phase 3 — Làm API `revenue-series` trước

**Mục tiêu:** hoàn thành vertical slice đầu tiên và dùng nó làm nguồn chuẩn cho các API doanh thu sau.

#### Bước 3.1 — Tạo DTO

Tạo `SystemRevenueSeriesDto.cs` gồm:

- Query DTO.
- Point hiện tại.
- Previous point.
- Response có `meta`.

#### Bước 3.2 — Tạo service

Trong `SystemAnalyticsService`:

1. Resolve kỳ.
2. Query invoiced/collected/outstanding.
3. Sinh bucket day/week/month.
4. Fill bucket trống bằng `0`.
5. Tính estimated MRR cuối bucket.
6. Tính previous points nếu được yêu cầu.
7. Thêm warnings và `dataThrough`.

#### Bước 3.3 — Tạo controller action

Thêm:

```text
GET /api/system/analytics/revenue-series
```

Controller dùng policy `SystemAdmin`, không chứa công thức.

#### Bước 3.4 — Thêm cache

- Dùng `IMemoryCache`.
- TTL mặc định 120 giây.
- Cache key gồm from/to/timezone/currency/compare/module/segment/granularity.

#### Bước 3.5 — Test và reconciliation

- Bucket trống.
- Range qua ranh giới UTC.
- Payment success/failed/unknown.
- Discount.
- Previous period.
- Tổng point khớp payment/order drill-down.

**Đầu ra Phase 3:**

- Endpoint đầu tiên sẵn sàng cho FE staging.

**Exit criteria:**

- [ ] Contract `revenue-series` pass.
- [ ] Tổng collected khớp source read-only.
- [ ] `refundedAmount=null`.
- [ ] `mrrStatus=Estimated`.
- [ ] p95 mục tiêu đạt hoặc có query plan giải thích được.

---

### Phase 4 — Làm API `revenue-breakdown`

**Mục tiêu:** phân bổ doanh thu nhưng vẫn reconcile tuyệt đối với Phase 3.

#### Bước 4.1 — Tạo DTO/validator

Tạo `SystemRevenueBreakdownDto.cs` và validate:

- Dimension `module|tenant|gateway`.
- Limit `5..50`.

#### Bước 4.2 — Làm dimension tenant và gateway

1. Tái sử dụng successful payment rule từ Phase 2.
2. Group theo tenant hoặc gateway.
3. Sort theo collected revenue.
4. Cắt top N và tạo `other`.

#### Bước 4.3 — Làm dimension module

1. Lấy billing lines.
2. Tính trọng số `LineTotal / totalLineAmount`.
3. Phân bổ discount/final amount.
4. Phân bổ collected payment.
5. Đưa decimal remainder vào item cuối.
6. Không nhân toàn bộ order amount cho từng module.

#### Bước 4.4 — Tạo controller action

Thêm:

```text
GET /api/system/analytics/revenue-breakdown
```

#### Bước 4.5 — Reconcile

Với cùng query:

```text
breakdown.totalCollectedRevenue
= revenue-series.points.sum(collectedRevenue)
```

**Test Phase 4:**

- Order một module.
- Order nhiều module.
- Order có discount.
- Decimal remainder.
- Top N/other.
- Tổng module/tenant/gateway khớp revenue series.

**Đầu ra Phase 4:**

- FE có biểu đồ phân bổ theo ba dimension.

**Exit criteria:**

- [ ] Không double count.
- [ ] `items + other = total`.
- [ ] Reconciliation Phase 3 pass.

---

### Phase 5 — Làm API `action-center`

**Mục tiêu:** cung cấp danh sách việc cần xử lý từ dữ liệu hiện có.

#### Bước 5.1 — Chốt option runtime

Thêm config:

```text
SystemAnalytics:OrderOverdueGraceHours
SystemAnalytics:ActionCenterMaxItems
```

Không thêm cột due date.

#### Bước 5.2 — Query từng loại action

Làm lần lượt:

1. `PaymentFailed`.
2. `OrderOverdue`.
3. `SubscriptionExpiring`.
4. `TrialEnding`.
5. `TenantSuspended`.

#### Bước 5.3 — Chuẩn hóa item

- Stable ID = `type + entityId`.
- Severity.
- Title/description an toàn.
- Deduplicate.
- Sort.
- Limit item nhưng không limit counts.
- `targetPath` chỉ lấy từ route constant allow-list.

#### Bước 5.4 — Tạo controller action

Thêm:

```text
GET /api/system/analytics/action-center
```

#### Bước 5.5 — Test

- Boundary 24 giờ/7 ngày.
- Không duplicate.
- Counts không bị item limit ảnh hưởng.
- Không có external URL.
- SYSTEM tenant bị loại.

**Đầu ra Phase 5:**

- Action center sẵn sàng FE.

**Exit criteria:**

- [ ] Năm loại action trả đúng.
- [ ] Warning overdue grace có trong meta.
- [ ] Target path đã được FE xác nhận.

---

### Phase 6 — Làm API tenant `financial-summary`

**Mục tiêu:** hoàn thành dữ liệu tài chính cho tenant detail.

#### Bước 6.1 — Tạo DTO/service

Tạo `SystemTenantFinancialSummaryDto.cs` và `ISystemTenantAnalyticsService`.

#### Bước 6.2 — Query tenant

1. Kiểm tra tenant tồn tại.
2. Loại tenant deleted và `SYSTEM`.
3. Query payment aggregate.
4. Query pending billing amount.
5. Query subscription counts.
6. Tính estimated current MRR.
7. Tính average payment delay.

#### Bước 6.3 — Tạo controller

Thêm:

```text
GET /api/system/analytics/tenants/{tenantId}/financial-summary
```

#### Bước 6.4 — Đối chiếu drill-down

So sánh response với:

```text
GET /api/system/billing-orders?tenantId=...
GET /api/system/payment-transactions?tenantId=...
GET /api/system/subscriptions?tenantId=...
```

**Test Phase 6:**

- Tenant không tồn tại/deleted/SYSTEM.
- Không có payment.
- Successful và failed payment.
- Pending order.
- Subscription active/trial/expiring.
- Delay âm/null.

**Đầu ra Phase 6:**

- Tab tài chính tenant có backend contract đầy đủ trong khả năng schema hiện tại.

**Exit criteria:**

- [ ] 404 đúng cho tenant không hợp lệ.
- [ ] Amount khớp drill-down.
- [ ] MRR luôn được đánh dấu estimated.

---

### Phase 7 — Làm API `operations/health-summary`

**Mục tiêu:** bọc health check hiện tại thành response an toàn cho System Admin.

#### Bước 7.1 — Tạo DTO/service WebAPI

- Tạo `SystemOperationsHealthDto.cs`.
- Tạo service gọi `HealthCheckService`.
- Map PostgreSQL/Redis/RabbitMQ result.

#### Bước 7.2 — Sanitize

- Không trả connection string.
- Không trả hostname/username/stack trace.
- Chỉ trả component, status, duration và description an toàn.

#### Bước 7.3 — Tạo controller

Thêm:

```text
GET /api/system/operations/health-summary
```

Giữ nguyên `GET /health`.

#### Bước 7.4 — Test

- All healthy.
- Một dependency unhealthy.
- Authorization.
- Response không có secret.

**Đầu ra Phase 7:**

- FE System Admin đọc được health summary.

**Exit criteria:**

- [ ] Endpoint mới có policy SystemAdmin.
- [ ] `/health` cũ không đổi.
- [ ] Không lộ thông tin kết nối.

---

### Phase 8 — Làm API `revenue-forecast` sau cùng

**Mục tiêu:** chỉ forecast sau khi revenue series đã được staging xác nhận đúng.

#### Bước 8.1 — Kiểm tra dữ liệu đầu vào

- Reuse monthly collected revenue từ Phase 3.
- Có ít nhất 6 tháng đầy đủ.
- Thiếu dữ liệu trả `422`, không giả `0`.

#### Bước 8.2 — Tạo calculator

Tạo `RevenueForecastCalculator`:

1. Linear trend deterministic.
2. Residual.
3. Confidence interval.
4. Lower bound không âm.
5. Tối đa 6 tháng tương lai.

#### Bước 8.3 — Tạo DTO/controller

Thêm:

```text
GET /api/system/analytics/revenue-forecast
```

Response tách actual và forecast, có method/training window/warnings.

#### Bước 8.4 — Test

- Dưới 6 tháng.
- Chuỗi bằng 0.
- Chuỗi tăng.
- Chuỗi giảm.
- Confidence interval.
- Forecast period ngoài giới hạn.

**Đầu ra Phase 8:**

- Forecast có thể giải thích và không persist xuống database.

**Exit criteria:**

- [ ] Phase 3 đã reconciliation pass.
- [ ] Dữ liệu staging đủ lịch sử hoặc endpoint trả 422 đúng contract.
- [ ] Không lưu model/result.

---

### Phase 9 — Hardening, staging và release

**Mục tiêu:** kiểm tra toàn bộ 6 endpoint trước khi deploy.

#### Bước 9.1 — Contract test

- Field name.
- Nullability.
- Enum/string value.
- ProblemDetails.
- Meta/warnings.
- Swagger example.

#### Bước 9.2 — Security test

- 401 khi không login.
- 403 khi không phải active System Admin.
- SYSTEM tenant bị loại.
- Không có `RawData`, secret hoặc external URL.

#### Bước 9.3 — Reconciliation

- Series khớp payment/order source.
- Breakdown khớp series.
- Tenant financial khớp drill-down.
- Action counts khớp read-only query.

#### Bước 9.4 — Performance

- Test 30 ngày.
- Test 12 tháng.
- Test 24 tháng.
- Kiểm tra N+1.
- Kiểm tra cancellation token.
- Chỉ dùng cache ngắn hạn sau khi số liệu gốc đã đúng.

#### Bước 9.5 — Database safety review

Trước merge/deploy, xác nhận diff không chứa:

```text
SMEFLOWSystem.Core/Entities/*
SMEFLOWSystem.Infrastructure/Data/SMEFLOWSystemContext.cs
SMEFLOWSystem.Infrastructure/Data/Configurations/*
SMEFLOWSystem.Infrastructure/Migrations/*
```

Không có:

- DDL/DML script production.
- `dotnet ef database update`.
- Backfill.
- Thay đổi `Database.Migrate()`.

#### Bước 9.6 — Deploy

1. Deploy code lên staging, không chạy migration.
2. Smoke test 6 endpoint.
3. FE xác nhận số liệu/drill-down.
4. Deploy production, không chạy migration.
5. Theo dõi error rate và latency.
6. Nếu lỗi, rollback application version; database không cần rollback.

**Exit criteria cuối:**

- [ ] 6 endpoint pass contract/security/reconciliation.
- [ ] FE staging sign-off.
- [ ] Không có database change.
- [ ] Có application rollback plan.

---

## 8. Thứ tự thực hiện và bàn giao bắt buộc

### 8.1. Thứ tự Backend

| Thứ tự | Phase | Chỉ bắt đầu khi | Kết quả bàn giao |
|---:|---|---|---|
| 1 | Phase 0 — Contract | Bắt đầu dự án | Contract 6 endpoint được chốt |
| 2 | Phase 1 — Foundation | Phase 0 pass | DTO, validator, period/status helper |
| 3 | Phase 2 — Read core | Phase 1 pass | Read repository và calculator core |
| 4 | Phase 3 — Revenue series | Phase 2 pass | API doanh thu theo thời gian |
| 5 | Phase 4 — Breakdown | Phase 3 reconcile pass | API phân bổ doanh thu |
| 6 | Phase 5 — Action center | Phase 4 pass | API hành động cần xử lý |
| 7 | Phase 6 — Tenant finance | Phase 5 pass | API tài chính tenant |
| 8 | Phase 7 — Health | Phase 6 pass | API health cho System Admin |
| 9 | Phase 8 — Forecast | Phase 3 đúng và Phase 7 pass | API forecast hoặc 422 hợp lệ |
| 10 | Phase 9 — Release | Phase 3–8 pass | Staging sign-off và production release |

### 8.2. Thứ tự FE tích hợp

1. Nhận common contract/meta sau Phase 1.
2. Tích hợp `revenue-series` sau Phase 3.
3. Tích hợp `revenue-breakdown` sau Phase 4.
4. Tích hợp `action-center` sau Phase 5.
5. Tích hợp tenant `financial-summary` sau Phase 6.
6. Tích hợp `health-summary` sau Phase 7.
7. Tích hợp `revenue-forecast` sau Phase 8.

### 8.3. Quy tắc không nhảy phase

- Không làm breakdown trước khi revenue series reconcile đúng.
- Không làm forecast trước khi monthly collected revenue được xác nhận.
- Không thêm cache để che query/số liệu sai.
- Không bàn giao FE endpoint chưa có contract test.
- Không deploy nếu diff chứa bất kỳ thay đổi database nào.

---

## 9. Test strategy

### 9.1. Unit test trong project hiện tại

Thêm các file:

```text
SMEFLOWSystem.Tests/SystemAnalyticsPeriodTests.cs
SMEFLOWSystem.Tests/SystemRevenueCalculatorTests.cs
SMEFLOWSystem.Tests/SystemRevenueAllocationTests.cs
SMEFLOWSystem.Tests/SystemActionCenterTests.cs
SMEFLOWSystem.Tests/SystemTenantFinancialTests.cs
SMEFLOWSystem.Tests/SystemRevenueForecastTests.cs
```

Ưu tiên test application service/calculator bằng fake repository như các test System hiện có.

### 9.2. Integration/contract test

Nếu bổ sung integration test:

- Chỉ dùng database test tách biệt, dựng từ migration **hiện có**.
- Không tạo migration mới.
- Không trỏ test vào production/staging database.
- Test authorization 401/403, ProblemDetails, serialization và query thật.
- Seed dữ liệu nhỏ theo schema hiện tại.

### 9.3. Reconciliation bắt buộc

Trên staging, dùng truy vấn read-only để xác nhận:

- Revenue series collected = tổng successful payment theo `ProcessedAt`.
- Breakdown tenant/gateway = revenue series trong cùng kỳ.
- Breakdown module không double count.
- Financial summary tenant = drill-down payment/billing của tenant.
- Tenant `SYSTEM` không xuất hiện.

---

## 10. Definition of Done

- [ ] Chỉ 6 endpoint ở mục 3.1 được bổ sung.
- [ ] 14 endpoint thiếu nguồn dữ liệu không có stub/fake response.
- [ ] Không sửa entity, DbContext, EF configuration hoặc migration.
- [ ] Không chạy DDL, backfill hoặc auto migration mới.
- [ ] Tất cả endpoint mới dùng policy `SystemAdmin`.
- [ ] Query cross-tenant loại tenant `SYSTEM` và dữ liệu deleted.
- [ ] Amount dùng `decimal`.
- [ ] Revenue dùng `ProcessedAt`; không dùng payment `CreatedAt` thay thế.
- [ ] Refund trả unavailable/null, không giả bằng `0`.
- [ ] MRR được ghi rõ `Estimated`.
- [ ] `excludesTestTenants=false` được công bố.
- [ ] Error dùng `ProblemDetails` và có `traceId`.
- [ ] Unit test công thức, timezone, rounding và warning đầy đủ.
- [ ] Contract Swagger khớp response thực tế.
- [ ] Reconciliation staging khớp drill-down hiện có.
- [ ] Endpoint cũ không bị sửa/xóa.
- [ ] Production deploy không chạy migration.

---

## 11. Điều kiện để mở lại các API bị loại

Các API bị loại chỉ được xem xét trong plan khác khi có nguồn dữ liệu sẵn mà không yêu cầu thay đổi database production, ví dụ:

- Audit/incident/usage lấy từ một observability provider đã cấu hình và có API query.
- Export dùng object storage và job metadata service bên ngoài đã có.
- Tenant health lấy từ analytics provider có last activity/active user.
- Churn/MRR lấy từ billing/subscription provider có commercial và status history.

Nếu không có các nguồn trên, tiếp tục không triển khai để tránh dashboard hiển thị số liệu sai.
