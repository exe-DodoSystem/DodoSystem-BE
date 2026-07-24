# Draft Contract API cho System Admin SaaS

Tài liệu này định nghĩa chi tiết request, response và các business rules cho 6 API bổ sung cho nhóm System Admin SaaS, không thay đổi Database theo Phase 0 của kế hoạch.

---

## 1. Các Quy ước Chung (Common Conventions)

### 1.1. Phân quyền và Lọc dữ liệu (Authorization & Multi-tenancy)
- Mọi endpoint phải đi kèm: `[Authorize(Policy = PolicyNames.SystemAdmin)]`.
- Tất cả các truy vấn dữ liệu toàn hệ thống bắt buộc:
  1. Sử dụng `.IgnoreQueryFilters()` để bypass filter mặc định của tenant hiện tại.
  2. Bỏ qua Tenant có tên là `SystemTenantConstants.Name` (tenant hệ thống).
  3. Bỏ qua các Tenant có `IsDeleted == true` (tenant đã bị xóa).

### 1.2. Múi giờ và Tiền tệ (Timezone & Currency)
- Múi giờ mặc định: `Asia/Ho_Chi_Minh` (UTC+7).
- Tiền tệ mặc định: `VND`. Mọi số tiền trả về là kiểu `decimal` biểu diễn cho VND.

### 1.3. Trạng thái Thanh toán & Thu nhập (Payment Status Rules)
- **Successful Payment** (Thanh toán thành công): các transaction có `Status` bằng `"Success"`, `"Succeeded"`, `"Settled"`, hoặc `"Paid"` (không phân biệt hoa thường).
- **Failed Payment** (Thanh toán thất bại): các transaction có `Status` bằng `"Failed"`.
- **Collected Revenue** (Doanh thu đã thu): chỉ tính từ các giao dịch thanh toán thành công và có `ProcessedAt != null`. Cột `ProcessedAt` được dùng để xác định mốc thời gian của doanh thu.
- **Invoiced Revenue** (Doanh thu hóa đơn): tổng `FinalAmount ?? (TotalAmount - DiscountAmount)` của các `BillingOrder` có trạng thái khác `Deleted`/`Cancelled` dựa trên `BillingDate`.

### 1.4. Ước tính MRR (Monthly Recurring Revenue)
Do không thay đổi database và không có lịch sử giá thương mại hoặc contract price riêng lẻ, MRR được ước tính (Estimated) tại thời điểm `asOf` bằng công thức:
```
SUM(Module.MonthlyPrice)
```
Áp dụng cho các `ModuleSubscription` thỏa mãn:
- `StartDate <= asOf` và `EndDate > asOf`
- `IsDeleted == false`
- `Status == "Active"` (hoặc theo hằng số trạng thái hoạt động)
- Tenant sở hữu không bị xóa, hoạt động bình thường và khác tenant hệ thống.
- **Trial subscriptions** không được cộng vào MRR.
- Response luôn trả về `mrrStatus = "Estimated"` và warning code `MRR_USES_CURRENT_CATALOG_PRICE`.

### 1.5. Cấu trúc Metadata Chung (`SystemAnalyticsMetaDto`)
Mọi endpoint trả về dữ liệu thống kê/analytics phải bao gồm đối tượng `meta` có cấu trúc như sau:

```json
{
  "from": "2026-07-01",
  "to": "2026-07-31",
  "previousFrom": "2026-06-01",
  "previousTo": "2026-06-30",
  "timezone": "Asia/Ho_Chi_Minh",
  "currency": "VND",
  "generatedAt": "2026-07-24T07:11:45Z",
  "dataThrough": "2026-07-24T07:00:00Z",
  "freshness": "Live",
  "excludesInternalTenant": true,
  "excludesTestTenants": false,
  "mrrStatus": "Estimated",
  "warnings": [
    "REFUND_DATA_UNAVAILABLE",
    "TEST_TENANT_FLAG_UNAVAILABLE",
    "MRR_USES_CURRENT_CATALOG_PRICE"
  ]
}
```

---

## 2. Chi tiết 6 Endpoint

### 2.1. GET `/api/system/analytics/revenue-series`
Lấy chuỗi dữ liệu doanh thu (Invoiced vs Collected) theo thời gian.

- **Query Parameters**:
  - `from` (string, `YYYY-MM-DD`, optional): Ngày bắt đầu. Mặc định là 30 ngày trước.
  - `to` (string, `YYYY-MM-DD`, optional): Ngày kết thúc. Mặc định là hôm nay.
  - `timezone` (string, optional): Mặc định `"Asia/Ho_Chi_Minh"`.
  - `currency` (string, optional): Mặc định `"VND"`.
  - `compare` (string, optional): So sánh kỳ trước. Nhận `"previous_period"`, `"previous_year"`, `"none"`. Mặc định `"previous_period"`.
  - `moduleId` (int, optional): Lọc theo gói chức năng cụ thể.
  - `tenantSegment` (string, optional): Phân khúc tenant. Nhận `"all"`, `"paid"`, `"trial"`. Mặc định `"all"`.
  - `granularity` (string, optional): Độ chia nhỏ thời gian. Nhận `"day"`, `"week"`, `"month"`. Nếu không truyền, hệ thống tự động chọn:
    - Khoảng thời gian <= 31 ngày: `"day"`
    - Khoảng thời gian từ 32 đến 180 ngày: `"week"`
    - Khoảng thời gian > 180 ngày: `"month"`

- **Mô tả logic**:
  - Sinh đầy đủ các bucket (ngày/tuần/tháng) trong khoảng `from` đến `to`. Điền giá trị `0` cho các bucket trống.
  - Phân tích ranh giới ngày local thành `[fromUtc, toExclusiveUtc)` trước khi query.
  - `mrrSnapshot` biểu diễn ước tính MRR tại thời điểm kết thúc (cuối ngày/tuần/tháng) của bucket tương ứng.
  - Kết quả được cache tối đa 2 phút qua `IMemoryCache`.

- **Response (200 OK)**:
```json
{
  "points": [
    {
      "bucketStart": "2026-07-01",
      "invoicedRevenue": 15000000.0,
      "collectedRevenue": 12000000.0,
      "refundedAmount": null,
      "outstandingCreated": 3000000.0,
      "mrrSnapshot": 45000000.0
    }
  ],
  "previousPoints": [
    {
      "bucketStart": "2026-06-01",
      "invoicedRevenue": 14000000.0,
      "collectedRevenue": 11000000.0,
      "refundedAmount": null,
      "outstandingCreated": 3000000.0,
      "mrrSnapshot": 43000000.0
    }
  ],
  "meta": {
    "from": "2026-07-01",
    "to": "2026-07-31",
    "previousFrom": "2026-06-01",
    "previousTo": "2026-06-30",
    "timezone": "Asia/Ho_Chi_Minh",
    "currency": "VND",
    "generatedAt": "2026-07-24T07:11:45Z",
    "dataThrough": "2026-07-24T07:00:00Z",
    "freshness": "Live",
    "excludesInternalTenant": true,
    "excludesTestTenants": false,
    "mrrStatus": "Estimated",
    "warnings": [
      "REFUND_DATA_UNAVAILABLE",
      "TEST_TENANT_FLAG_UNAVAILABLE",
      "MRR_USES_CURRENT_CATALOG_PRICE"
    ]
  }
}
```

---

### 2.2. GET `/api/system/analytics/revenue-breakdown`
Phân bổ doanh thu thực thu theo Module, Tenant hoặc Gateway.

- **Query Parameters**:
  - Cùng các tham số của `SystemAnalyticsPeriodQueryDto` (`from`, `to`, `timezone`, `currency`, `compare`, `moduleId`, `tenantSegment`).
  - `dimension` (string, **required**): Chiều phân tích. Nhận `"module"`, `"tenant"`, `"gateway"`.
  - `limit` (int, optional): Giới hạn số phần tử đứng đầu. Nhận từ `5` đến `50`. Mặc định là `10`.

- **Mô tả logic**:
  - Tổng collected của breakdown phải khớp tuyệt đối với tổng collected trong revenue series cùng kỳ.
  - Phân bổ theo `module`:
    - Với các hóa đơn chứa nhiều module, sử dụng tỷ lệ `LineTotal / TotalLineAmount` của `BillingOrderModule` làm trọng số để phân bổ `DiscountAmount` và `CollectedAmount` của hóa đơn.
    - Phần dư khi làm tròn số decimal (rounding remainder) phải được cộng dồn vào phần tử cuối cùng để đảm bảo tổng tuyệt đối khớp.
  - Phân bổ theo `gateway`: gom nhóm theo cột `Gateway` của `PaymentTransaction`.
  - Phân bổ theo `tenant`: gom nhóm theo `TenantId` và map ra `Tenant.Name`.

- **Response (200 OK)**:
```json
{
  "totalCollectedRevenue": 12000000.0,
  "items": [
    {
      "id": "ATTENDANCE",
      "name": "Chấm công",
      "collectedRevenue": 6000000.0,
      "percentageOfTotal": 50.0
    },
    {
      "id": "PAYROLL",
      "name": "Tính lương",
      "collectedRevenue": 4000000.0,
      "percentageOfTotal": 33.33
    }
  ],
  "other": {
    "id": "OTHER",
    "name": "Khác",
    "collectedRevenue": 2000000.0,
    "percentageOfTotal": 16.67
  },
  "meta": {
    "from": "2026-07-01",
    "to": "2026-07-31",
    "previousFrom": "2026-06-01",
    "previousTo": "2026-06-30",
    "timezone": "Asia/Ho_Chi_Minh",
    "currency": "VND",
    "generatedAt": "2026-07-24T07:11:45Z",
    "dataThrough": "2026-07-24T07:00:00Z",
    "freshness": "Live",
    "excludesInternalTenant": true,
    "excludesTestTenants": false,
    "mrrStatus": "Unavailable",
    "warnings": [
      "REFUND_DATA_UNAVAILABLE",
      "TEST_TENANT_FLAG_UNAVAILABLE"
    ]
  }
}
```

---

### 2.3. GET `/api/system/analytics/action-center`
Cung cấp danh sách các sự kiện/hành động khẩn cấp cần System Admin xử lý dựa trên dữ liệu hiện tại.

- **Query Parameters**: Không có.
- **Mô tả logic**:
  - Quét hệ thống và thu thập các vấn đề theo 5 quy tắc dẫn xuất:
    1. `PaymentFailed`: Giao dịch thanh toán có trạng thái `Failed` trong 24 giờ qua.
    2. `OrderOverdue`: Hóa đơn có trạng thái `PaymentStatus == "Pending"` và thời điểm tạo `BillingDate <= Now - OrderOverdueGraceHours` (mặc định 24 giờ, cấu hình từ file `appsettings.json`).
    3. `SubscriptionExpiring`: Các gói đăng ký của tenant có `EndDate` trong vòng 7 ngày tới và đang `Active`.
    4. `TrialEnding`: Các gói trial có `EndDate` trong vòng 7 ngày tới và đang `Trial`.
    5. `TenantSuspended`: Các tenant có `Status == "Suspended"`.
  - Phân loại Severity:
    - `critical`: `PaymentFailed` mới hoặc `OrderOverdue`.
    - `warning`: `SubscriptionExpiring` hoặc `TrialEnding`.
    - `info`: `TenantSuspended`.
  - Hỗ trợ phân trang ảo hoặc giới hạn số lượng trả về tối đa qua option `ActionCenterMaxItems` (mặc định 100), tuy nhiên object `counts` ở đầu response vẫn hiển thị tổng số lượng thực tế.
  - Thuộc tính `targetPath` phải trỏ tới các route chuẩn của FE hệ thống (ví dụ: `/system/tenants/{tenantId}`).

- **Response (200 OK)**:
```json
{
  "counts": {
    "critical": 2,
    "warning": 5,
    "info": 1
  },
  "items": [
    {
      "id": "OrderOverdue_3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "type": "OrderOverdue",
      "severity": "critical",
      "title": "Hóa đơn quá hạn thanh toán",
      "description": "Hóa đơn số BO-100234 của Công ty A đã quá hạn thanh toán 24h.",
      "occurredAt": "2026-07-23T14:00:00Z",
      "entityId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "targetPath": "/system/tenants/3fa85f64-5717-4562-b3fc-2c963f66afa6"
    }
  ],
  "meta": {
    "from": "2026-07-24",
    "to": "2026-07-24",
    "timezone": "Asia/Ho_Chi_Minh",
    "currency": "VND",
    "generatedAt": "2026-07-24T07:11:45Z",
    "dataThrough": "2026-07-24T07:11:00Z",
    "freshness": "Live",
    "excludesInternalTenant": true,
    "excludesTestTenants": false,
    "mrrStatus": "Unavailable",
    "warnings": [
      "ORDER_OVERDUE_USES_CONFIGURED_GRACE_PERIOD",
      "TEST_TENANT_FLAG_UNAVAILABLE"
    ]
  }
}
```

---

### 2.4. GET `/api/system/analytics/tenants/{tenantId}/financial-summary`
Lấy báo cáo tài chính tóm tắt của một Tenant cụ thể.

- **Path Parameters**:
  - `tenantId` (Guid, **required**): ID của Tenant cần truy vấn.

- **Query Parameters**:
  - Tương tự như `SystemAnalyticsPeriodQueryDto` (để lọc thông số thu nhập trong kỳ).

- **Mô tả logic**:
  - Trả về `404 Not Found` nếu Tenant không tồn tại, đã bị soft-delete, hoặc là Tenant hệ thống (`SYSTEM`).
  - `averagePaymentDelayDays`: Tính trung bình số ngày chênh lệch từ lúc xuất hóa đơn tới lúc thanh toán thành công (`ProcessedAt - BillingDate`). Nếu có chênh lệch âm (do dữ liệu lỗi), loại bỏ bản ghi đó ra khỏi phép tính trung bình và đính kèm warning.

- **Response (200 OK)**:
```json
{
  "tenantId": "8fa85f64-5717-4562-b3fc-2c963f66afa6",
  "tenantName": "Công ty Cổ phần Công nghệ ABC",
  "status": "Active",
  "currentMrr": 2500000.0,
  "lifetimeCollectedRevenue": 45000000.0,
  "collectedRevenueInPeriod": 5000000.0,
  "outstandingAmount": 0.0,
  "lastSuccessfulPaymentAt": "2026-07-15T08:30:00Z",
  "lastFailedPaymentAt": "2026-07-14T09:15:00Z",
  "averagePaymentDelayDays": 1.5,
  "subscriptions": {
    "active": 2,
    "trial": 0,
    "expiringIn30Days": 1
  },
  "meta": {
    "from": "2026-07-01",
    "to": "2026-07-31",
    "timezone": "Asia/Ho_Chi_Minh",
    "currency": "VND",
    "generatedAt": "2026-07-24T07:11:45Z",
    "dataThrough": "2026-07-24T07:00:00Z",
    "freshness": "Live",
    "excludesInternalTenant": true,
    "excludesTestTenants": false,
    "mrrStatus": "Estimated",
    "warnings": [
      "REFUND_DATA_UNAVAILABLE",
      "MRR_USES_CURRENT_CATALOG_PRICE"
    ]
  }
}
```

---

### 2.5. GET `/api/system/operations/health-summary`
Báo cáo sức khỏe tổng quan của hệ thống (PostgreSQL, Redis, RabbitMQ) dành riêng cho System Admin.

- **Query Parameters**: Không có.
- **Mô tả logic**:
  - Gọi dịch vụ `HealthCheckService` nội bộ của ASP.NET Core để lấy trạng thái thực tế của các dependency.
  - **Không** được phép trả ra connection string, hostname, username, stack trace hoặc các dữ liệu nhạy cảm. Map exception hoặc lỗi thô thành các mô tả an toàn cho người dùng cuối (ví dụ: `"PostgreSQL database is not reachable."`).
  - Cache response tối đa 15 giây để tránh DDOS database check.

- **Response (200 OK)**:
```json
{
  "status": "Healthy",
  "checkedAt": "2026-07-24T07:11:45Z",
  "durationMs": 45,
  "components": [
    {
      "name": "postgres",
      "status": "Healthy",
      "durationMs": 12,
      "description": "Database connection is healthy."
    },
    {
      "name": "redis",
      "status": "Healthy",
      "durationMs": 8,
      "description": "Redis cache store is reachable."
    },
    {
      "name": "rabbitmq",
      "status": "Healthy",
      "durationMs": 25,
      "description": "Message broker is operational."
    }
  ]
}
```

---

### 2.6. GET `/api/system/analytics/revenue-forecast`
Dự báo doanh thu thực thu trong tương lai (từ 1 đến 6 tháng tiếp theo).

- **Query Parameters**:
  - Tương tự như `SystemAnalyticsPeriodQueryDto` (chủ yếu dùng để xác định khoảng thời gian huấn luyện dữ liệu lịch sử - training data).
  - `forecastPeriods` (int, optional): Số tháng cần dự báo tiếp theo. Từ `1` đến `6`. Mặc định là `3`.
  - `granularity` (string, optional): Chỉ chấp nhận `"month"`.

- **Mô tả logic**:
  - Yêu cầu bắt buộc phải có ít nhất **6 tháng lịch sử doanh thu** thực thu (khuyến nghị 12 tháng). Nếu không đủ, trả về lỗi `422 Unprocessable Entity` kèm mô tả chi tiết, **không** tự giả lập dữ liệu bằng số `0`.
  - Sử dụng phương pháp hồi quy tuyến tính đơn giản (Linear Trend Regression) thuần túy để tính toán. Các công thức toán học phải deterministic và chạy hoàn toàn trên RAM (không ghi thông số mô hình hay kết quả dự báo xuống DB).
  - Tính toán khoảng tin cậy (Confidence Interval) dựa trên độ lệch (residuals) của dữ liệu quá khứ.
  - Các giá trị dự đoán tối thiểu phải lớn hơn hoặc bằng `0` (nếu âm phải clamp về `0`).
  - Response phải trả về các warning codes tương ứng.

- **Response (200 OK)**:
```json
{
  "method": "LinearTrend",
  "trainingFrom": "2025-07-01",
  "trainingTo": "2026-06-30",
  "currency": "VND",
  "granularity": "month",
  "actualPoints": [
    {
      "bucketStart": "2026-06-01",
      "value": 11500000.0
    }
  ],
  "forecastPoints": [
    {
      "bucketStart": "2026-07-01",
      "value": 12000000.0,
      "lowerBound": 10500000.0,
      "upperBound": 13500000.0
    },
    {
      "bucketStart": "2026-08-01",
      "value": 12200000.0,
      "lowerBound": 10600000.0,
      "upperBound": 13800000.0
    }
  ],
  "meta": {
    "from": "2025-07-01",
    "to": "2026-08-31",
    "timezone": "Asia/Ho_Chi_Minh",
    "currency": "VND",
    "generatedAt": "2026-07-24T07:11:45Z",
    "dataThrough": "2026-07-24T07:00:00Z",
    "freshness": "Live",
    "excludesInternalTenant": true,
    "excludesTestTenants": false,
    "mrrStatus": "Unavailable",
    "warnings": [
      "FORECAST_EXCLUDES_REFUNDS",
      "FORECAST_BASED_ON_AVAILABLE_PAYMENT_HISTORY",
      "TEST_TENANT_FLAG_UNAVAILABLE"
    ]
  }
}
```

---

## 3. Các Phản hồi lỗi Chuẩn (ProblemDetails - RFC 7807)

Các endpoint mới khi gặp lỗi nghiệp vụ hoặc tham số không hợp lệ sẽ phản hồi bằng định dạng JSON ProblemDetails chuẩn.

### 3.1. Lỗi validation (400 Bad Request)
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "traceId": "00-84a1d48c8b417e4bb7e3ff6835261546-24e5f76269273c52-00",
  "errors": {
    "From": [
      "The 'From' date must be before or equal to the 'To' date."
    ],
    "Timezone": [
      "The timezone 'America/New_York' is not supported. Only 'Asia/Ho_Chi_Minh' is allowed."
    ]
  }
}
```

### 3.2. Không đủ lịch sử dự báo (422 Unprocessable Entity)
```json
{
  "type": "https://tools.ietf.org/html/rfc4918#section-11.2",
  "title": "Insufficient historical data for forecasting.",
  "status": 422,
  "detail": "At least 6 months of historical collected revenue are required to generate a forecast. The system only detected 3 months of history.",
  "traceId": "00-84a1d48c8b417e4bb7e3ff6835261546-24e5f76269273c52-00"
}
```

### 3.3. Tenant hoặc Module không tồn tại (404 Not Found)
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.4",
  "title": "The specified resource was not found.",
  "status": 404,
  "detail": "Tenant with ID '8fa85f64-5717-4562-b3fc-2c963f66afa6' does not exist or has been soft-deleted.",
  "traceId": "00-84a1d48c8b417e4bb7e3ff6835261546-24e5f76269273c52-00"
}
```
