# Kế hoạch Backend API cho System Admin Analytics của DODO SaaS

> Trạng thái: kế hoạch đề xuất, chưa triển khai  
> Ngày lập: 24/07/2026  
> Phạm vi: API và dữ liệu Backend cần bổ sung để System Admin theo dõi doanh thu, tăng trưởng, subscription, thanh toán và sức khỏe vận hành  
> Tài liệu FE tích hợp tương ứng: `Codex-Plans/system_admin_saas_frontend_ui_plan.md`

---

## 1. Mục tiêu

Backend phải cung cấp một nguồn dữ liệu tổng hợp, thống nhất và có thể kiểm chứng để System Admin trả lời nhanh các câu hỏi:

1. Hôm nay, tháng này và kỳ đang chọn đã thu được bao nhiêu tiền?
2. Doanh thu tăng hay giảm so với kỳ trước?
3. MRR, ARR, ARPA, New MRR, Churned MRR và Net New MRR hiện tại là bao nhiêu?
4. Có bao nhiêu tenant trả phí, tenant mới, tenant rời bỏ và tenant có nguy cơ rời bỏ?
5. Module nào tạo ra nhiều doanh thu nhất?
6. Cổng thanh toán nào đang có tỷ lệ lỗi cao?
7. Bao nhiêu hóa đơn đang chờ, quá hạn hoặc thanh toán thất bại?
8. Tenant nào cần System Admin xử lý trước?
9. Ai đã thực hiện thao tác quản trị nhạy cảm và thực hiện lúc nào?
10. Số liệu được cập nhật đến thời điểm nào và có đầy đủ hay không?

Frontend không được tự tải toàn bộ billing order/payment/subscription rồi cộng hoặc suy diễn KPI. Mọi chỉ số tài chính và tỷ lệ phải do Backend tổng hợp.

---

## 2. Hiện trạng contract đã có

Frontend hiện đã tích hợp các API sau:

### 2.1. Dashboard vận hành

```text
GET /api/system/dashboard/overview
GET /api/system/dashboard/module-usage
GET /api/system/dashboard/module-cancellations
GET /api/system/dashboard/module-expirations
GET /api/system/dashboard/module-trends
```

Nhóm API này đang cung cấp số lượng tenant, subscription và xu hướng module. Chưa có doanh thu, MRR/ARR, churn, tỷ lệ thanh toán hoặc so sánh với kỳ trước.

### 2.2. Dữ liệu quản trị

```text
GET   /api/system/tenants
GET   /api/system/tenants/{tenantId}
GET   /api/system/tenants/{tenantId}/users
PATCH /api/system/tenants/{tenantId}/status

GET  /api/system/subscriptions
POST /api/system/subscriptions/{subscriptionId}/extend
POST /api/system/subscriptions/{subscriptionId}/suspend
POST /api/system/subscriptions/{subscriptionId}/reactivate

GET /api/system/billing-orders
GET /api/system/billing-orders/{billingOrderId}
GET /api/system/payment-transactions
```

Các API list/detail này tiếp tục được dùng cho drill-down. Không dùng chúng để tính KPI tổng quan ở FE.

---

## 3. Quyết định nghiệp vụ phải chốt trước khi code

### 3.1. Không gọi mọi số tiền là “doanh thu”

Dashboard phải phân biệt ít nhất ba khái niệm:

| Khái niệm | Nhãn UI đề xuất | Định nghĩa |
|---|---|---|
| Invoiced revenue | Giá trị hóa đơn | Tổng `finalAmount` của billing order hợp lệ phát sinh trong kỳ, chưa chắc đã thu được |
| Collected revenue | Doanh thu đã thu | Tổng tiền giao dịch đã được settlement/thành công trong kỳ, trừ tiền hoàn nếu có |
| Recurring revenue | Doanh thu định kỳ | Giá trị subscription định kỳ được chuẩn hóa thành MRR/ARR tại một thời điểm |

Không hiển thị “lợi nhuận” nếu hệ thống chưa có dữ liệu chi phí, thuế, phí cổng thanh toán và hoàn tiền đầy đủ.

### 3.2. Nguồn sự thật cho tiền đã thu

Đề xuất:

```text
collectedRevenue
  = SUM(successful settled payment amount)
  - SUM(successful refund amount)
```

Quy tắc:

- Chỉ tính giao dịch có trạng thái canonical `Succeeded` hoặc `Settled`.
- Dùng `processedAt`/`settledAt` để xác định kỳ, không dùng `createdAt` của lần thử thanh toán.
- Một giao dịch gateway chỉ được tính một lần.
- Retry hoặc webhook lặp phải idempotent.
- Loại bỏ transaction bị void/cancelled.
- Loại bỏ tenant nội bộ `SYSTEM`.
- Có cờ loại bỏ tenant demo/test/sandbox khỏi analytics.
- Nếu chưa có refund model, API phải trả `refundDataAvailable=false`; không mặc định refund bằng 0 rồi khiến người dùng hiểu nhầm.

### 3.3. Nguồn sự thật cho MRR

Không lấy `Module.monthlyPrice` hiện tại để tính ngược MRR lịch sử, vì giá catalog có thể thay đổi.

Mỗi subscription/contract cần lưu snapshot thương mại:

```text
unitPrice
quantity
currency
billingInterval        // Monthly, Quarterly, Yearly...
billingIntervalCount
discountType
discountValue
discountStartAt
discountEndAt
trialEndAt
cancelledAt
cancelReasonCode
effectiveFrom
effectiveTo
```

Công thức chuẩn hóa:

```text
Monthly          -> recurringAmount
Quarterly        -> recurringAmount / 3
Yearly           -> recurringAmount / 12
N tháng một lần  -> recurringAmount / N
```

MRR chỉ gồm subscription:

- Đang có hiệu lực tại `asOf`.
- Không bị soft-delete/cancelled tại `asOf`.
- Không thuộc trial miễn phí.
- Không thuộc tenant `SYSTEM`, test hoặc sandbox.
- Dùng giá contract/snapshot, không dùng giá catalog mới nhất.

Nếu schema hiện chưa có đủ dữ liệu, Backend phải trả `mrrStatus="Estimated"` và mô tả `estimationReason`. Không trình bày MRR ước tính như số kế toán chính xác.

### 3.4. Tiền tệ và timezone

- Currency mặc định: `VND`.
- Tiền dùng `decimal`, không dùng `float/double`.
- JSON trả amount dạng number nếu toàn hệ thống đã thống nhất decimal serialization; không nhân/chia 100 ở FE.
- Business timezone mặc định: `Asia/Ho_Chi_Minh`.
- `from` là đầu ngày và `to` là cuối ngày theo timezone yêu cầu.
- API luôn trả lại timezone, currency và khoảng thời gian đã áp dụng trong `meta`.
- So sánh kỳ trước phải dùng khoảng thời gian có cùng số ngày.

### 3.5. Định nghĩa churn

Phải tách:

- `tenantChurnRate`: số paying tenant rời bỏ trong kỳ / paying tenant đầu kỳ.
- `grossRevenueChurnRate`: `(churnedMRR + contractionMRR) / startingMRR`.
- `netRevenueRetention`: `(startingMRR + expansionMRR - contractionMRR - churnedMRR) / startingMRR`.

Không lấy số subscription/module bị hủy chia cho tổng subscription rồi gọi là customer churn.

---

## 4. Quy ước chung cho Analytics API

### 4.1. Namespace

Thêm namespace mới:

```text
/api/system/analytics/*
```

Giữ `/api/system/dashboard/*` trong giai đoạn chuyển tiếp để không làm hỏng FE hiện tại. Khi FE mới ổn định mới đánh dấu endpoint cũ deprecated.

### 4.2. Query chuẩn

```ts
interface AnalyticsPeriodQuery {
  from: string;             // YYYY-MM-DD
  to: string;               // YYYY-MM-DD, inclusive
  timezone?: string;        // default Asia/Ho_Chi_Minh
  currency?: "VND";         // chuẩn bị cho multi-currency
  compare?: "previous_period" | "previous_year" | "none";
  moduleId?: number;
  tenantSegment?: "all" | "paid" | "trial";
}
```

Quy tắc validate:

- `from <= to`.
- Khoảng mặc định: 30 ngày gần nhất.
- Range online tối đa: 24 tháng.
- Với range trên 24 tháng, dùng export/report async.
- Timezone chỉ nhận IANA timezone trong allow-list.
- `moduleId` không tồn tại trả `400 ProblemDetails`, không âm thầm trả mảng rỗng.

### 4.3. Metadata chung

Mọi response analytics phải có:

```ts
interface AnalyticsMeta {
  from: string;
  to: string;
  previousFrom?: string;
  previousTo?: string;
  timezone: string;
  currency: string;
  generatedAt: string;        // UTC ISO 8601
  dataThrough: string;         // dữ liệu đầy đủ đến thời điểm nào
  freshness: "Live" | "NearRealTime" | "DailySnapshot";
  excludesInternalTenant: true;
  excludesTestTenants: boolean;
  mrrStatus: "Exact" | "Estimated" | "Unavailable";
  warnings: string[];
}
```

`warnings` dùng cho trường hợp refund chưa có, dữ liệu gateway chưa đồng bộ hoặc một số contract thiếu price snapshot.

### 4.4. Delta chuẩn

```ts
interface MetricValue {
  value: number;
  previousValue: number | null;
  absoluteChange: number | null;
  percentageChange: number | null;
  trend: "up" | "down" | "flat" | "not_comparable";
}
```

Khi `previousValue=0`, `percentageChange=null`, không trả Infinity.

### 4.5. Error

Tất cả endpoint mới dùng RFC 7807 `ProblemDetails`:

```json
{
  "type": "https://dodo.vn/problems/invalid-analytics-period",
  "title": "Khoảng thời gian không hợp lệ",
  "status": 400,
  "detail": "from phải nhỏ hơn hoặc bằng to",
  "traceId": "..."
}
```

---

## 5. API bắt buộc — Phase BE-P0

Đây là các API tối thiểu để dựng dashboard SaaS có doanh thu rõ ràng.

### BE-P0.1 — Executive summary

```text
GET /api/system/analytics/summary
```

Query: `AnalyticsPeriodQuery`.

Response:

```ts
interface SystemAnalyticsSummary {
  meta: AnalyticsMeta;
  financial: {
    collectedRevenue: MetricValue;
    invoicedRevenue: MetricValue;
    outstandingAmount: MetricValue;
    refundedAmount: MetricValue | null;
    mrr: MetricValue;
    arr: MetricValue;
    arpa: MetricValue;
    newMrr: MetricValue;
    expansionMrr: MetricValue;
    contractionMrr: MetricValue;
    churnedMrr: MetricValue;
    netNewMrr: MetricValue;
  };
  customers: {
    activePayingTenants: MetricValue;
    trialTenants: MetricValue;
    newTenants: MetricValue;
    activatedTenants: MetricValue;
    churnedTenants: MetricValue;
    tenantChurnRate: MetricValue;
    trialConversionRate: MetricValue;
    netRevenueRetention: MetricValue;
  };
  payments: {
    successfulAmount: MetricValue;
    failedAmount: MetricValue;
    paymentAttemptSuccessRate: MetricValue;
    orderCollectionRate: MetricValue;
    pendingOrderCount: MetricValue;
    overdueOrderCount: MetricValue;
    failedTransactionCount: MetricValue;
  };
}
```

Lưu ý:

- `outstandingAmount`: tổng giá trị order hợp lệ chưa thu tại cuối kỳ.
- `paymentAttemptSuccessRate`: successful attempts / total terminal attempts.
- `orderCollectionRate`: số order đã thu đủ / số order đến hạn. Đây là tỷ lệ quan trọng hơn khi retry nhiều lần.
- `arpa = MRR / activePayingTenants`; nếu mẫu số bằng 0 trả 0 và warning.
- `ARR = MRR * 12`.
- `netNewMrr = newMrr + expansionMrr - contractionMrr - churnedMrr`.

### BE-P0.2 — Chuỗi thời gian doanh thu

```text
GET /api/system/analytics/revenue-series
```

Query bổ sung:

```text
granularity=day|week|month
```

Response:

```ts
interface RevenueSeriesResponse {
  meta: AnalyticsMeta;
  granularity: "day" | "week" | "month";
  points: Array<{
    periodStart: string;
    periodEnd: string;
    invoicedRevenue: number;
    collectedRevenue: number;
    refundedAmount: number | null;
    outstandingCreated: number;
    mrrSnapshot: number;
  }>;
  previousPoints?: Array<{
    periodStart: string;
    periodEnd: string;
    collectedRevenue: number;
    mrrSnapshot: number;
  }>;
}
```

Quy tắc granularity mặc định:

- Tối đa 31 ngày: `day`.
- 32–180 ngày: `week`.
- Trên 180 ngày: `month`.

Backend phải điền các bucket không có giao dịch bằng 0 để biểu đồ không bị đứt.

### BE-P0.3 — Phân bổ doanh thu

```text
GET /api/system/analytics/revenue-breakdown
```

Query bổ sung:

```text
dimension=module|tenant|gateway
limit=5..50
```

Response:

```ts
interface RevenueBreakdownResponse {
  meta: AnalyticsMeta;
  dimension: "module" | "tenant" | "gateway";
  totalCollectedRevenue: number;
  items: Array<{
    key: string;
    name: string;
    collectedRevenue: number;
    invoicedRevenue: number;
    percentageOfTotal: number;
    paymentCount: number;
    activeSubscriptionCount?: number;
  }>;
  other: {
    collectedRevenue: number;
    percentageOfTotal: number;
  } | null;
}
```

Yêu cầu:

- Với module, phân bổ theo billing order lines hoặc allocation ledger.
- Không gán toàn bộ tiền order nhiều module cho từng module.
- Tổng các item + `other` phải bằng tổng, sai số decimal cho phép phải được test.

### BE-P0.4 — Tăng trưởng tenant

```text
GET /api/system/analytics/tenant-growth
```

Response:

```ts
interface TenantGrowthResponse {
  meta: AnalyticsMeta;
  granularity: "day" | "week" | "month";
  points: Array<{
    periodStart: string;
    newTenants: number;
    activatedTenants: number;
    churnedTenants: number;
    activePayingTenantsAtEnd: number;
    trialTenantsAtEnd: number;
  }>;
}
```

Cần có business event hoặc lịch sử trạng thái để biết tenant “activated/churned” tại thời điểm nào. Không suy diễn chính xác lịch sử chỉ từ trạng thái hiện tại.

### BE-P0.5 — Subscription metrics

```text
GET /api/system/analytics/subscription-metrics
```

Response:

```ts
interface SubscriptionMetricsResponse {
  meta: AnalyticsMeta;
  summary: {
    active: number;
    trial: number;
    suspended: number;
    cancelledInPeriod: number;
    expiringIn7Days: number;
    expiringIn30Days: number;
    trialConversionRate: number;
    tenantChurnRate: number;
    grossRevenueChurnRate: number;
    netRevenueRetention: number;
  };
  byModule: Array<{
    moduleId: number;
    moduleCode: string;
    moduleName: string;
    activeSubscriptions: number;
    trials: number;
    cancellations: number;
    mrr: number;
    mrrShare: number;
    churnedMrr: number;
  }>;
}
```

### BE-P0.6 — Payment health

```text
GET /api/system/analytics/payment-health
```

Response:

```ts
interface PaymentHealthResponse {
  meta: AnalyticsMeta;
  summary: {
    attempts: number;
    successfulAttempts: number;
    failedAttempts: number;
    pendingAttempts: number;
    attemptSuccessRate: number;
    orderCollectionRate: number;
    medianTimeToPayMinutes: number | null;
  };
  byGateway: Array<{
    gateway: string;
    attempts: number;
    successes: number;
    failures: number;
    pending: number;
    successRate: number;
    successfulAmount: number;
    failedAmount: number;
  }>;
  topFailureCodes: Array<{
    gateway: string;
    responseCode: string;
    displayMessage: string;
    count: number;
    amount: number;
  }>;
  daily: Array<{
    date: string;
    successes: number;
    failures: number;
    successRate: number;
  }>;
}
```

Không trả raw gateway message có thể chứa dữ liệu nhạy cảm. Backend ánh xạ `responseCode` sang thông điệp an toàn.

### BE-P0.7 — Action center

```text
GET /api/system/analytics/action-center
```

Response:

```ts
interface ActionCenterResponse {
  meta: AnalyticsMeta;
  counts: {
    failedPayments24h: number;
    overdueOrders: number;
    expiringSubscriptions7d: number;
    trialsEnding7d: number;
    suspendedTenants: number;
  };
  items: Array<{
    id: string;
    type:
      | "PaymentFailed"
      | "OrderOverdue"
      | "SubscriptionExpiring"
      | "TrialEnding"
      | "TenantSuspended";
    severity: "critical" | "warning" | "info";
    title: string;
    description: string;
    tenantId?: string;
    tenantName?: string;
    entityId?: string;
    occurredAt: string;
    targetPath: string;
  }>;
}
```

`targetPath` chỉ là internal application path trong allow-list. Không trả URL ngoài tùy ý.

---

## 6. API mở rộng — Phase BE-P1

### BE-P1.1 — Financial summary theo tenant

```text
GET /api/system/analytics/tenants/{tenantId}/financial-summary
```

Response:

```ts
interface TenantFinancialSummaryResponse {
  meta: AnalyticsMeta;
  tenant: {
    id: string;
    name: string;
    status: string;
  };
  financial: {
    currentMrr: number;
    lifetimeCollectedRevenue: number;
    collectedRevenueInPeriod: number;
    outstandingAmount: number;
    lastSuccessfulPaymentAt: string | null;
    lastFailedPaymentAt: string | null;
    averagePaymentDelayDays: number | null;
  };
  subscriptions: {
    active: number;
    trial: number;
    expiringIn30Days: number;
  };
}
```

API này phục vụ tab “Tài chính” trong tenant detail.

### BE-P1.2 — Tenant health/risk

```text
GET /api/system/analytics/tenant-health
GET /api/system/analytics/tenants/{tenantId}/health
```

Health score chỉ nên triển khai khi có dữ liệu usage đáng tin cậy.

Đầu vào đề xuất:

- Ngày từ lần đăng nhập/hoạt động gần nhất.
- Số active user 7/30 ngày.
- Tần suất sử dụng module.
- Payment failures.
- Subscription sắp hết hạn.
- Số ticket hỗ trợ mở nếu sau này có support module.

Response list:

```ts
interface TenantHealthItem {
  tenantId: string;
  tenantName: string;
  score: number; // 0..100
  level: "healthy" | "watch" | "at_risk";
  reasons: Array<{
    code: string;
    label: string;
    impact: number;
  }>;
  currentMrr: number;
  lastActivityAt: string | null;
}
```

Backend phải trả cả `reasons`; FE không hiển thị một điểm số “hộp đen”.

### BE-P1.3 — Audit log

```text
GET /api/system/audit-logs
GET /api/system/audit-logs/{auditLogId}
```

Query:

```text
pageNumber
pageSize
actorUserId
action
entityType
entityId
from
to
result=Succeeded|Failed
```

Mỗi mutation System Admin phải ghi:

```ts
interface SystemAuditLog {
  id: string;
  actorUserId: string;
  actorEmail: string;
  action: string;
  entityType: string;
  entityId: string;
  tenantId?: string;
  reason?: string;
  result: "Succeeded" | "Failed";
  before?: Record<string, unknown>;
  after?: Record<string, unknown>;
  traceId: string;
  ipAddressMasked?: string;
  occurredAt: string;
}
```

Không ghi token, password, secret, full payment payload hoặc PII không cần thiết.

### BE-P1.4 — Analytics export

Nếu cần tải báo cáo:

```text
POST /api/system/analytics/exports
GET  /api/system/analytics/exports/{exportId}
GET  /api/system/analytics/exports/{exportId}/download
```

Export chạy nền, có expiry, kiểm tra quyền ở cả lúc tạo và lúc tải. Không tạo CSV đồng bộ cho range lớn.

---

## 7. API mở rộng — Phase BE-P2

Chỉ triển khai sau khi dashboard tài chính P0/P1 ổn định.

### 7.1. Product usage

```text
GET /api/system/analytics/product-usage
GET /api/system/analytics/feature-adoption
```

Cần event taxonomy rõ ràng, không dựa trên log request thô. Event tối thiểu:

```text
TenantActivated
UserInvited
UserActivated
ModuleOpened
CoreActionCompleted
SubscriptionStarted
SubscriptionRenewed
SubscriptionCancelled
PaymentSucceeded
PaymentFailed
```

### 7.2. System health

```text
GET /api/system/operations/health-summary
GET /api/system/operations/incidents
```

Nên lấy từ hệ thống observability/APM thay vì query database nghiệp vụ. Không đưa CPU/RAM giả lập vào dashboard nếu chưa có nguồn monitoring thật.

### 7.3. Forecast

```text
GET /api/system/analytics/revenue-forecast
```

Chỉ hiển thị forecast khi có đủ lịch sử và luôn trả:

- Phương pháp.
- Confidence interval.
- Training window.
- Generated time.

Không trộn forecast với doanh thu thực tế trong cùng một số KPI.

---

## 8. Thay đổi schema và dữ liệu nền cần cân nhắc

### 8.1. Subscription commercial snapshot

Nếu subscription hiện chỉ giữ `moduleId`, `startDate`, `endDate`, `status`, cần thêm price snapshot như mục 3.3. Đây là điều kiện để MRR lịch sử chính xác.

### 8.2. Payment canonicalization

Chuẩn hóa trạng thái nội bộ:

```text
Pending
Succeeded
Failed
Cancelled
Settled
Refunded
PartiallyRefunded
```

Thêm unique constraint phù hợp cho:

```text
(gateway, gatewayTransactionId)
```

Webhook phải có idempotency key/event id.

### 8.3. Refund

Nếu sản phẩm hỗ trợ hoàn tiền, tạo entity/ledger riêng:

```text
RefundId
PaymentTransactionId
GatewayRefundId
Amount
Status
Reason
ProcessedAt
```

Không sửa amount giao dịch gốc làm mất audit trail.

### 8.4. Status history

Để tính activation/churn theo lịch sử:

```text
TenantStatusHistory
SubscriptionStatusHistory
```

Tối thiểu có `fromStatus`, `toStatus`, `effectiveAt`, `reasonCode`, `actor`.

### 8.5. Aggregate/snapshot

Khi dữ liệu lớn, thêm:

```text
AnalyticsDailyRevenue
AnalyticsDailyTenantSnapshot
AnalyticsDailySubscriptionSnapshot
AnalyticsDailyPaymentHealth
```

Nguyên tắc:

- Payment/revenue near-real-time có thể aggregate theo event.
- MRR và tenant count chụp snapshot cuối ngày.
- Có job backfill idempotent.
- Có reconciliation job so sánh aggregate với source tables.
- `dataThrough` phản ánh lần chạy thành công cuối.

---

## 9. Index và hiệu năng

Index cần đánh giá theo DB thực tế:

```text
PaymentTransactions(Status, ProcessedAt)
PaymentTransactions(Gateway, GatewayTransactionId) UNIQUE
BillingOrders(PaymentStatus, BillingDate)
BillingOrders(TenantId, CreatedAt)
Subscriptions(Status, StartDate, EndDate, IsDeleted)
Subscriptions(TenantId, ModuleId, EndDate)
TenantStatusHistory(EffectiveAt, ToStatus)
SubscriptionStatusHistory(EffectiveAt, ToStatus)
```

Mục tiêu:

- `summary`: p95 dưới 800 ms với range 30 ngày.
- Series 12 tháng: p95 dưới 1.5 giây.
- Action center: p95 dưới 800 ms.
- Không N+1 query theo tenant/module.
- Hỗ trợ cancellation token.
- Cache theo normalized query trong 1–5 phút.
- Cache key phải gồm timezone, currency, filter và quyền/phạm vi.
- Mutation payment/subscription không cần xóa cache tức thời nếu response công bố freshness near-real-time; nếu có event invalidation thì càng tốt.

---

## 10. Bảo mật và kiểm soát truy cập

1. Tất cả endpoint chỉ cho `SystemAdmin` hợp lệ thuộc tenant nội bộ.
2. Backend authorization là bắt buộc; ẩn menu ở FE không phải bảo mật.
3. Không trả password hash, token, secret gateway hoặc payload webhook đầy đủ.
4. Tenant nội bộ `SYSTEM` luôn bị loại khỏi analytics.
5. Tenant demo/test dùng cờ dữ liệu chính thức, không lọc bằng tên.
6. Export cần signed token ngắn hạn hoặc authenticated download.
7. Mọi mutation System Admin ghi audit log cả thành công lẫn thất bại.
8. Rate limit endpoint export và truy vấn range lớn.
9. Log query analytics có trace id nhưng không log PII dư thừa.

---

## 11. Kế hoạch triển khai Backend theo ticket

### BE-0 — Chốt glossary và data audit

- Chốt định nghĩa collected/invoiced/MRR/churn.
- Lập mapping trạng thái payment từ VNPay, SePay sang canonical status.
- Kiểm tra duplicate gateway transaction.
- Kiểm tra subscription có price snapshot hay chưa.
- Xác định tenant test/sandbox.
- Xác định có refund hay không.
- Viết ADR cho timezone, currency và revenue source of truth.

**Đầu ra:** glossary được Product/Backend/Finance duyệt và báo cáo chất lượng dữ liệu.

### BE-1 — Chuẩn hóa schema tài chính

- Bổ sung commercial snapshot cho subscription nếu thiếu.
- Thêm status history/event cần thiết.
- Thêm payment uniqueness/idempotency.
- Bổ sung refund ledger nếu sản phẩm có refund.
- Viết migration và backfill.

**Phụ thuộc:** BE-0.

### BE-2 — Analytics core

- Xây query/service dùng chung.
- Triển khai `summary`.
- Triển khai `revenue-series`.
- Triển khai `revenue-breakdown`.
- Trả `AnalyticsMeta` và warnings đầy đủ.

**Phụ thuộc:** BE-1 hoặc có phương án `Estimated` rõ ràng.

### BE-3 — Customer và subscription analytics

- Triển khai `tenant-growth`.
- Triển khai `subscription-metrics`.
- Test activation/churn theo status history.

**Phụ thuộc:** BE-1.

### BE-4 — Payment health và action center

- Triển khai `payment-health`.
- Chuẩn hóa failure code.
- Triển khai `action-center`.
- Link target phải nằm trong allow-list route FE.

**Phụ thuộc:** BE-0.

### BE-5 — Aggregate, cache và reconciliation

- Thêm snapshot/aggregate nếu query trực tiếp không đạt SLO.
- Backfill dữ liệu.
- Reconciliation job.
- Monitoring data freshness.

**Phụ thuộc:** BE-2 đến BE-4.

### BE-6 — Tenant financial detail và audit

- Triển khai financial summary theo tenant.
- Ghi và đọc audit log.
- Bảo vệ dữ liệu nhạy cảm.

**Phụ thuộc:** BE-2.

### BE-7 — Export, usage, health và forecast

- Thực hiện từng phần khi có nhu cầu và nguồn dữ liệu thật.
- Không chặn release dashboard P0.

---

## 12. Test bắt buộc

### 12.1. Unit test công thức

- Kỳ trước có giá trị 0.
- Partial refund và full refund.
- Payment retry nhiều lần, chỉ một lần thành công.
- Subscription monthly/quarterly/yearly.
- Discount có và hết hiệu lực giữa kỳ.
- Trial miễn phí chuyển sang paid.
- Upgrade/downgrade giữa kỳ.
- Churn và reactivate.
- Tenant `SYSTEM`, test, sandbox bị loại.
- Khoảng ngày qua ranh giới UTC nhưng cùng ngày Việt Nam.

### 12.2. Integration test

- Authorization 401/403.
- Invalid range trả ProblemDetails 400.
- Bucket thiếu được điền 0.
- Tổng breakdown khớp summary.
- Summary khớp source transaction đã seed.
- Soft-deleted/cancelled subscription không lọt vào MRR.
- Duplicate webhook không làm tăng doanh thu.

### 12.3. Contract test với FE

- Field name, nullable và enum đúng tài liệu.
- Amount không bị đổi đơn vị.
- Date/time có timezone rõ ràng.
- Empty dataset trả cấu trúc hợp lệ thay vì `null` toàn response.
- `generatedAt`, `dataThrough`, `warnings` luôn có.

### 12.4. Performance test

- 30 ngày, 12 tháng và 24 tháng.
- Dữ liệu lớn theo số tenant/payment dự kiến 2–3 năm.
- Kiểm tra execution plan và N+1.
- Kiểm tra nhiều System Admin mở dashboard đồng thời.

---

## 13. Definition of Done cho Backend

- [ ] Glossary tài chính được chốt bằng văn bản.
- [ ] Không có transaction trùng bị cộng hai lần.
- [ ] MRR dùng contract price snapshot hoặc được đánh dấu `Estimated`.
- [ ] API P0 có OpenAPI/Swagger và example response.
- [ ] Tất cả API có `AnalyticsMeta`.
- [ ] Tất cả error mới dùng ProblemDetails.
- [ ] Authorization và loại tenant nội bộ được test.
- [ ] Có unit, integration, contract và performance test.
- [ ] Có SLO và dashboard theo dõi data freshness.
- [ ] Có reconciliation giữa aggregate và source data.
- [ ] FE staging xác nhận số liệu drill-down khớp.
- [ ] Endpoint cũ chưa bị xóa trong cùng release.

---

## 14. Thứ tự bàn giao cho Frontend

Backend nên bàn giao theo lát dọc để FE tích hợp sớm:

1. OpenAPI + mock JSON của `summary`, `revenue-series`.
2. API thật `summary`, `revenue-series`, kèm bộ dữ liệu staging.
3. `revenue-breakdown`, `tenant-growth`.
4. `subscription-metrics`, `payment-health`.
5. `action-center`.
6. Tenant financial summary và audit log.
7. Các API P2 nếu được duyệt.

Mỗi lần bàn giao cần kèm:

- Endpoint và example.
- Ý nghĩa từng field.
- Công thức.
- Freshness.
- Known data-quality warnings.
- Một bộ ID tenant/order/transaction staging để FE kiểm tra drill-down.

