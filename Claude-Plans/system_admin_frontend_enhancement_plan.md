# Kế hoạch nâng cấp Frontend cho SystemAdmin — bám theo Backend Phase 0, 1 và 2

> Tài liệu này mô tả chi tiết FE cần thêm màn hình nào, route nào, API client nào, bảng/filter nào, nút nào và xử lý từng trạng thái ra sao.
>
> Phạm vi contract được đối chiếu trực tiếp với Backend hiện tại. FE không được tự giả định endpoint mutation ngoài những endpoint được ghi là **đã có**.

---

## 1. Mục tiêu

Sau khi hoàn thành, SystemAdmin có thể:

1. Xem tổng quan tình trạng toàn hệ thống.
2. Xem danh sách và chi tiết tenant.
3. Xem user, module, subscription, billing order và payment transaction của từng tenant.
4. Quản lý catalog module.
5. Quản lý role ở mức Backend hiện hỗ trợ.
6. Suspend/reactivate tenant có kiểm soát.
7. Extend/suspend/reactivate subscription có kiểm soát.
8. Theo dõi xu hướng sử dụng, hủy và hết hạn module.
9. Không nhìn thấy tenant nội bộ `SYSTEM`.
10. Không hiển thị secret, password hash, refresh token hoặc dữ liệu nhạy cảm.

---

## 2. Nguyên tắc triển khai

### 2.1. Backend là nguồn sự thật

- FE chỉ dùng các status và transition Backend cho phép.
- Không tự cập nhật optimistic đối với mutation nhạy cảm.
- Sau mutation thành công phải lấy response Backend và invalidate/refetch dữ liệu liên quan.
- Không tự tính lại số liệu dashboard từ dữ liệu list.
- Không tự suy diễn subscription usable chỉ từ `status`; Backend còn kiểm tra `isDeleted`, `startDate` và `endDate`.

### 2.2. Quyền truy cập

Toàn bộ màn hình trong tài liệu này chỉ dành cho user có role `SystemAdmin`.

Backend còn kiểm tra SystemAdmin:

- User tồn tại.
- User đang active.
- Thuộc tenant nội bộ hợp lệ.
- Có role SystemAdmin hợp lệ.

FE route guard giúp UX tốt hơn nhưng không thay thế authorization của Backend.

### 2.3. Feature flag cho Phase 2

Các nút mutation phải được bọc bởi feature flag:

```text
systemAdminMutationsEnabled
```

Giá trị mặc định khi lần đầu deploy production:

```text
false
```

Khi flag `false`:

- Không hiện nút suspend/reactivate tenant.
- Không hiện nút extend/suspend/reactivate subscription.
- Các màn hình read-only vẫn hoạt động.

Lưu ý: feature flag phía FE chỉ ẩn UI, không phải biện pháp bảo mật. Backend vẫn cần authorization và nên có feature flag riêng trước khi mở mutation production.

---

## 3. Cấu trúc route FE đề xuất

Nếu FE đang dùng tên route khác, có thể đổi path nhưng phải giữ nguyên cấu trúc chức năng.

| Route FE | Màn hình | Quyền |
|---|---|---|
| `/system-admin` | Redirect đến dashboard | SystemAdmin |
| `/system-admin/dashboard` | Dashboard hệ thống | SystemAdmin |
| `/system-admin/tenants` | Danh sách tenant | SystemAdmin |
| `/system-admin/tenants/:tenantId` | Chi tiết tenant | SystemAdmin |
| `/system-admin/subscriptions` | Subscription toàn hệ thống | SystemAdmin |
| `/system-admin/billing-orders` | Billing order toàn hệ thống | SystemAdmin |
| `/system-admin/billing-orders/:billingOrderId` | Chi tiết billing order | SystemAdmin |
| `/system-admin/payment-transactions` | Payment transaction toàn hệ thống | SystemAdmin |
| `/system-admin/modules` | Quản lý catalog module | SystemAdmin |
| `/system-admin/roles` | Quản lý role | SystemAdmin |
| `/system-admin/settings` | Cài đặt SystemAdmin | SystemAdmin |
| `/system-admin/settings/bootstrap-reset` | Danger zone reset bootstrap | Chỉ Development/Staging và có config |

### 3.1. Sidebar SystemAdmin

Thứ tự menu:

1. Tổng quan
2. Tenant
3. Subscription
4. Billing order
5. Payment transaction
6. Module
7. Role
8. Cài đặt

Mỗi item cần:

- Icon rõ nghĩa.
- Active state theo route.
- Tooltip khi sidebar thu gọn.
- Không render menu SystemAdmin cho role khác.

### 3.2. Route guard

Guard cần xử lý:

```text
Chưa đăng nhập
  -> redirect /login?returnUrl=<current-url>

Đã đăng nhập nhưng không có role SystemAdmin
  -> redirect /403 hoặc trang Forbidden

Có role trong token nhưng Backend trả 403
  -> hiển thị "Tài khoản SystemAdmin không còn hiệu lực"
  -> xóa cache dữ liệu SystemAdmin
  -> cho phép user đăng xuất/đăng nhập lại
```

Không dựa hoàn toàn vào role trong local storage vì Backend kiểm tra trạng thái user ở database.

---

## 4. API client FE cần bổ sung

Không hard-code domain. Dùng base URL từ environment hiện có.

### 4.1. Dashboard API

```text
GET /api/system/dashboard/overview
GET /api/system/dashboard/module-usage
GET /api/system/dashboard/module-cancellations
GET /api/system/dashboard/module-expirations
GET /api/system/dashboard/module-trends
```

Method gợi ý:

```ts
getSystemOverview(params)
getSystemModuleUsage(params)
getSystemModuleCancellations(params)
getSystemModuleExpirations(params)
getSystemModuleTrends(params)
```

### 4.2. Tenant API

```text
GET   /api/system/tenants
GET   /api/system/tenants/{tenantId}
GET   /api/system/tenants/{tenantId}/users
PATCH /api/system/tenants/{tenantId}/status
```

Method gợi ý:

```ts
getSystemTenants(query)
getSystemTenantDetail(tenantId)
getSystemTenantUsers(tenantId, query)
changeSystemTenantStatus(tenantId, payload)
```

### 4.3. Subscription API

```text
GET  /api/system/subscriptions
POST /api/system/subscriptions/{subscriptionId}/extend
POST /api/system/subscriptions/{subscriptionId}/suspend
POST /api/system/subscriptions/{subscriptionId}/reactivate
```

Method gợi ý:

```ts
getSystemSubscriptions(query)
extendSystemSubscription(subscriptionId, payload)
suspendSystemSubscription(subscriptionId, payload?)
reactivateSystemSubscription(subscriptionId, payload?)
```

### 4.4. Billing API

```text
GET /api/system/billing-orders
GET /api/system/billing-orders/{billingOrderId}
GET /api/system/payment-transactions
```

Method gợi ý:

```ts
getSystemBillingOrders(query)
getSystemBillingOrderDetail(billingOrderId)
getSystemPaymentTransactions(query)
```

### 4.5. Module API

```text
GET  /api/Modules/all
POST /api/Modules
PUT  /api/Modules/{id}
PUT  /api/Modules/{id}/activate
PUT  /api/Modules/{id}/deactivate
```

Method gợi ý:

```ts
getAllModules()
createModule(payload)
updateModule(moduleId, payload)
activateModule(moduleId)
deactivateModule(moduleId)
```

### 4.6. Role API

```text
GET  /api/Role/all
GET  /api/Role/{id}
GET  /api/Role/all/page
POST /api/Role
PUT  /api/Role/{id}
```

Method gợi ý:

```ts
getAllRoles()
getRoleById(roleId)
getRolesPaged(query)
createRole(payload)
updateRole(roleId, payload)
```

Backend hiện không có API xóa role. FE không được thêm nút Xóa.

### 4.7. Bootstrap reset API

```text
DELETE /api/system/bootstrap/reset
```

Chỉ hiển thị trong FE ở Development/Staging khi `SystemBootstrap:AllowReset=true`.
Backend có maintenance gate production riêng cho tình huống khẩn cấp, nhưng FE production
không được render chức năng này.

---

## 5. Type dùng chung phía FE

### 5.1. Response phân trang

Các API list SystemAdmin trả cấu trúc:

```ts
type PagedResult<T> = {
  items: T[];
  totalCount: number;
  pageNumber: number;
  pageSize: number;
  totalPages: number;
  hasPrevious: boolean;
  hasNext: boolean;
};
```

Không dùng cấu trúc pagination cũ nếu tên field khác.

### 5.2. ProblemDetails và lỗi cũ

Backend hiện có hai dạng lỗi:

```ts
type ProblemDetails = {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  instance?: string;
  errors?: Record<string, string[]>;
};
```

và một số endpoint cũ trả:

```ts
type LegacyApiError = {
  error: string;
};
```

Tạo một hàm normalize duy nhất:

```ts
normalizeApiError(error): {
  status?: number;
  message: string;
  fieldErrors?: Record<string, string[]>;
}
```

Thứ tự lấy message:

1. `detail`
2. `error`
3. `title`
4. Message mặc định theo status

### 5.3. Date/time

- DateTime Backend trả UTC: hiển thị theo `Asia/Ho_Chi_Minh`.
- Khi extend subscription, gửi ISO UTC có hậu tố `Z`.
- `subscriptionEndDate` của tenant là `DateOnly`; hiển thị trực tiếp theo ngày, không convert timezone gây lệch ngày.
- Không lấy `new Date("YYYY-MM-DD")` rồi format lại nếu thư viện có thể đổi timezone.

### 5.4. Tiền

Hiển thị bằng:

```ts
new Intl.NumberFormat("vi-VN", {
  style: "currency",
  currency: "VND",
  maximumFractionDigits: 0
})
```

Không tự chia cho 100 hoặc nhân 100.

---

## 6. Phase FE-0 — Nền tảng SystemAdmin

### 6.1. Tạo layout riêng

Thêm:

- `SystemAdminLayout`
- `SystemAdminSidebar`
- `SystemAdminHeader`
- Breadcrumb
- Khu vực page title và page actions

Header hiển thị:

- Tên SystemAdmin.
- Role badge.
- Nút refresh trang hiện tại.
- Menu tài khoản/đăng xuất.

### 6.2. Shared components

Các component nên dùng lại:

- `SystemStatusBadge`
- `SystemPageHeader`
- `SystemFilterBar`
- `SystemDataTable`
- `SystemPagination`
- `SystemEmptyState`
- `SystemErrorState`
- `ConfirmActionDialog`
- `ReasonTextarea`
- `DateRangePicker`
- `MonthYearPicker`
- `MoneyText`
- `UtcDateTimeText`
- `CopyableId`

### 6.3. Status badge

Tenant:

| Status | Nhãn | Màu |
|---|---|---|
| `Active` | Hoạt động | Xanh lá |
| `Trial` | Dùng thử | Xanh dương |
| `PendingPayment` | Chờ thanh toán | Vàng/cam |
| `Suspended` | Tạm ngưng | Đỏ |

Subscription:

| Status | Nhãn | Màu |
|---|---|---|
| `Active` | Hoạt động | Xanh lá |
| `Trial` | Dùng thử | Xanh dương |
| `Suspended` | Tạm ngưng | Đỏ |
| `isDeleted=true` | Đã hủy | Xám đậm |

Billing/payment:

- `Pending`: vàng
- `Paid`/`Success`/`Completed`: xanh lá
- `Failed`: đỏ
- `Cancelled`: xám

### 6.4. Query state

Filter, sort, page và page size phải đồng bộ lên URL query string.

Ví dụ:

```text
/system-admin/tenants?pageNumber=2&pageSize=20&status=Active&sortBy=createdAt&sortDirection=desc
```

Lợi ích:

- Reload không mất filter.
- Back/forward hoạt động.
- Có thể copy link gửi cho người khác.

Search input debounce khoảng 300–500 ms.

Khi filter thay đổi:

- Reset `pageNumber=1`.
- Hủy request cũ nếu thư viện hỗ trợ AbortSignal.

---

## 7. Phase FE-1 — Dashboard SystemAdmin

### 7.1. Route

```text
/system-admin/dashboard
```

### 7.2. Thanh điều khiển

Thêm:

- Month selector.
- Year selector.
- Nút `Tháng hiện tại`.
- Nút `Làm mới`.
- Trend range selector riêng, mặc định 6 tháng gần nhất.

Điều kiện:

- Month từ 1 đến 12.
- Year từ 2020 đến năm hiện tại + 1.
- Trend tối đa 24 tháng.
- From không được sau To.

### 7.3. KPI cards

Gọi:

```text
GET /api/system/dashboard/overview?month={month}&year={year}
```

Hiển thị các card:

1. Tổng tenant — `totalTenants`
2. Tenant hoạt động — `activeTenants`
3. Tenant dùng thử — `trialTenants`
4. Tenant chờ thanh toán — `pendingPaymentTenants`
5. Tenant tạm ngưng — `suspendedTenants`
6. Tenant mới trong kỳ — `newTenantsInPeriod`
7. Hết hạn trong 7 ngày — `expiringIn7Days`
8. Hết hạn trong 30 ngày — `expiringIn30Days`
9. Subscription hoạt động — `activeSubscriptions`
10. Subscription bị hủy trong kỳ — `cancelledSubscriptionsInPeriod`
11. Billing order chờ xử lý — `pendingBillingOrders`
12. Payment thất bại trong kỳ — `failedPaymentsInPeriod`

Click-through đề xuất:

- Active tenant -> `/system-admin/tenants?status=Active`
- Trial tenant -> `/system-admin/tenants?status=Trial`
- Pending payment -> `/system-admin/tenants?status=PendingPayment`
- Suspended -> `/system-admin/tenants?status=Suspended`
- Expiring 7/30 ngày -> tenant list với `expiringInDays`
- Pending billing -> billing list với `paymentStatus=Pending`
- Failed payment -> payment list với `status=Failed`

### 7.4. Module usage chart

API:

```text
GET /api/system/dashboard/module-usage?month={month}&year={year}
```

UI:

- Bar chart theo module.
- Trục X: `moduleName`.
- Trục Y: `activeCompaniesCount`.
- Tooltip hiển thị module và số công ty.
- Bên dưới có bảng fallback cho accessibility.

### 7.5. Cancellation chart

API:

```text
GET /api/system/dashboard/module-cancellations?month={month}&year={year}
```

Hiển thị:

- Bar chart hoặc donut chart.
- Dữ liệu: `cancelledCompaniesCount`.
- Ghi chú rõ đây là số liệu suy ra từ dữ liệu cancellation hiện có, không phải audit lịch sử tuyệt đối.

### 7.6. Expiration chart

API:

```text
GET /api/system/dashboard/module-expirations?month={month}&year={year}
```

Hiển thị `expiredCompaniesCount` theo module.

### 7.7. Trend chart

API:

```text
GET /api/system/dashboard/module-trends
  ?fromMonth=
  &fromYear=
  &toMonth=
  &toYear=
```

UI:

- Multi-series line chart.
- Cho phép bật/tắt từng module trong legend.
- Mỗi module có ba metric:
  - `activeCompanies`
  - `cancellations`
  - `expirations`
- Nếu quá nhiều module, mặc định chọn 3–5 module đầu và cho user chọn thêm.

### 7.8. Loading/error

- KPI dùng skeleton card.
- Mỗi chart có error boundary riêng.
- Một chart lỗi không làm toàn dashboard trắng.
- Có nút `Thử lại` từng khu vực.
- Empty data hiển thị `Không có dữ liệu trong kỳ đã chọn`, không hiển thị chart rỗng khó hiểu.

---

## 8. Phase FE-2 — Danh sách tenant

### 8.1. Route

```text
/system-admin/tenants
```

### 8.2. API

```text
GET /api/system/tenants
```

Query:

```ts
{
  pageNumber: number;          // >= 1
  pageSize: number;            // 1..100
  search?: string;
  status?: "Active" | "Trial" | "PendingPayment" | "Suspended";
  moduleId?: number;
  expiringInDays?: number;     // 1..365
  sortBy: "name" | "status" | "createdAt" | "subscriptionEndDate";
  sortDirection: "asc" | "desc";
}
```

### 8.3. Filter bar

Thêm:

- Search theo tên tenant/owner/email theo khả năng Backend hiện tại.
- Select trạng thái.
- Select module, lấy options từ `GET /api/Modules/all`.
- Select sắp hết hạn:
  - 7 ngày
  - 14 ngày
  - 30 ngày
  - 60 ngày
  - 90 ngày
- Nút `Xóa bộ lọc`.
- Nút `Làm mới`.

### 8.4. Bảng tenant

Columns:

1. Tên công ty
2. Trạng thái
3. Owner
4. Email owner
5. Số user
6. Module active
7. Ngày hết hạn tổng
8. Số ngày còn lại
9. Ngày tạo
10. Thao tác

Quy tắc hiển thị:

- `remainingDays < 0`: `Đã hết hạn N ngày`.
- `remainingDays = 0`: `Hết hạn hôm nay`.
- `remainingDays <= 7`: màu đỏ.
- `remainingDays <= 30`: màu cam.
- Null end date: `Chưa có`.

### 8.5. Nút trên mỗi row

Luôn có:

- `Xem chi tiết`
- Menu `...`

Nếu mutation flag bật:

| Tenant status | Nút |
|---|---|
| `Active` | `Tạm ngưng` |
| `Trial` | `Tạm ngưng` |
| `Suspended` | `Kích hoạt lại` |
| `PendingPayment` | Không có nút đổi trạng thái |

Không hiển thị tenant `SYSTEM`; Backend đã loại tenant này nhưng FE vẫn không được hard-code link tới nó.

### 8.6. Suspend tenant dialog

Tiêu đề:

```text
Tạm ngưng tenant “{tenantName}”?
```

Nội dung cảnh báo:

- User tenant có thể mất quyền truy cập module.
- Thao tác có hiệu lực ngay.
- Đây không phải xóa dữ liệu.

Field:

- `Lý do` — textarea bắt buộc, tối đa 500 ký tự.

Nút:

- `Hủy`
- `Xác nhận tạm ngưng`

API:

```http
PATCH /api/system/tenants/{tenantId}/status
Content-Type: application/json

{
  "status": "Suspended",
  "reason": "Lý do do SystemAdmin nhập"
}
```

### 8.7. Reactivate tenant dialog

Tiêu đề:

```text
Kích hoạt lại tenant “{tenantName}”?
```

Lý do optional, tối đa 500 ký tự.

Payload:

```json
{
  "status": "Active",
  "reason": "Optional"
}
```

### 8.8. Xử lý response

```ts
{
  tenantId: string;
  status: string;
  updatedAt?: string;
  changed: boolean;
}
```

- `changed=true`: toast thành công và refetch.
- `changed=false`: toast info `Tenant đã ở trạng thái này`.
- `409`: giữ dialog mở, hiển thị `detail`, refetch row vì dữ liệu có thể đã đổi.
- `404`: đóng dialog, thông báo tenant không còn tồn tại, refetch list.
- `403`: báo không đủ quyền.

---

## 9. Phase FE-3 — Chi tiết tenant

### 9.1. Route

```text
/system-admin/tenants/:tenantId
```

### 9.2. Header

Hiển thị:

- Tên tenant.
- Status badge.
- Tenant ID có nút copy.
- Ngày tạo/cập nhật.
- Nút `Quay lại danh sách`.
- Nút suspend/reactivate theo cùng rule ở tenant list.

### 9.3. Summary cards

- Ngày hết hạn tổng.
- Số ngày còn lại.
- Tổng user.
- Tổng module đang gắn.
- Owner.

### 9.4. Tabs

#### Tab Tổng quan

API:

```text
GET /api/system/tenants/{tenantId}
```

Owner card:

- Họ tên.
- Email.
- Số điện thoại.
- Active/Inactive.
- Verified/Not verified.

Không hiện password, password hash hoặc token.

#### Tab Module

Từ `detail.modules`:

| Column | Field |
|---|---|
| Module | `moduleName` |
| Bắt đầu | `startDate` |
| Kết thúc | `endDate` |
| Trạng thái | `status` |

Click một module có thể mở trang subscription với filter:

```text
/system-admin/subscriptions?tenantId={tenantId}&moduleId={moduleId}
```

#### Tab Người dùng

API:

```text
GET /api/system/tenants/{tenantId}/users
```

Filter:

- Search tên/email/phone.
- Role.
- Trạng thái active:
  - Tất cả
  - Active
  - Inactive

Columns:

- Họ tên
- Email
- Số điện thoại
- Roles
- Active
- Verified
- Ngày tạo

Backend hiện chỉ cung cấp read-only. Không thêm nút khóa, kích hoạt hay đổi role user ở màn hình này.

#### Tab Subscription

Dùng API global với tenant filter:

```text
GET /api/system/subscriptions?tenantId={tenantId}
```

Cho phép các action subscription giống trang subscription toàn hệ thống.

#### Tab Billing

```text
GET /api/system/billing-orders?tenantId={tenantId}
```

Hiển thị bảng rút gọn và link sang billing detail.

#### Tab Payment

```text
GET /api/system/payment-transactions?tenantId={tenantId}
```

Hiển thị read-only.

### 9.5. Deep-link bằng query

Cho phép:

```text
/system-admin/tenants/:tenantId?tab=users
/system-admin/tenants/:tenantId?tab=subscriptions
/system-admin/tenants/:tenantId?tab=billing
```

Reload phải giữ đúng tab.

---

## 10. Phase FE-4 — Subscription toàn hệ thống

### 10.1. Route

```text
/system-admin/subscriptions
```

### 10.2. Filter

Query được Backend hỗ trợ:

```ts
{
  pageNumber: number;
  pageSize: number;
  searchTenant?: string;
  tenantId?: string;
  moduleId?: number;
  status?: "Active" | "Trial" | "Suspended";
  includeCancelled?: boolean;
  expiringFrom?: string;
  expiringTo?: string;
}
```

UI filter:

- Search tenant.
- Module.
- Status.
- Date range hết hạn.
- Checkbox `Bao gồm subscription đã hủy`.
- Nút xóa filter.

Date range phải đảm bảo `expiringFrom <= expiringTo`.

### 10.3. Bảng subscription

Columns:

1. Tenant
2. Module
3. Module code
4. Trạng thái
5. Ngày bắt đầu
6. Ngày kết thúc
7. Số ngày còn lại
8. Đã hủy
9. Ngày cập nhật
10. Thao tác

Tenant name là link đến tenant detail.

### 10.4. Nút thao tác

Khi mutation flag bật:

| Điều kiện | Nút |
|---|---|
| `isDeleted=true` | Không có mutation; chỉ xem |
| Status `Active` hoặc `Trial` | `Gia hạn`, `Tạm ngưng` |
| Status `Suspended`, end date còn hạn | `Gia hạn`, `Kích hoạt lại` |
| Status `Suspended`, đã hết hạn | `Gia hạn`; sau khi gia hạn/refetch mới cho reactivate |

FE chỉ dùng rule trên để ẩn/hiện hợp lý. Backend vẫn là nơi quyết định cuối cùng.

### 10.5. Extend dialog

Hiển thị:

- Tenant.
- Module.
- End date hiện tại.
- New end date.
- Chênh lệch số ngày.
- Reason optional, tối đa 500 ký tự.

Validation:

- New end date bắt buộc.
- Phải sau end date hiện tại.
- Convert cuối ngày hoặc thời điểm được chọn thành UTC rõ ràng.
- Không gửi timestamp không có timezone.

Payload:

```json
{
  "newEndDate": "2026-12-31T16:59:59.000Z",
  "reason": "Gia hạn theo yêu cầu hỗ trợ"
}
```

API:

```text
POST /api/system/subscriptions/{subscriptionId}/extend
```

Extend không tự đổi status. Nếu đang Suspended thì vẫn Suspended sau khi extend.

### 10.6. Suspend dialog

Cảnh báo:

- Tenant mất quyền dùng module này ngay.
- Không xóa subscription.
- Có thể reactivate nếu còn hạn và catalog module còn active.

Reason optional theo Backend, tối đa 500 ký tự. FE nên khuyến nghị nhập để log có ngữ cảnh.

API:

```text
POST /api/system/subscriptions/{subscriptionId}/suspend
```

Payload:

```json
{
  "reason": "Tạm khóa để kiểm tra"
}
```

### 10.7. Reactivate dialog

Trước khi bật nút, FE kiểm tra sơ bộ:

- `isDeleted=false`
- status `Suspended`
- end date > hiện tại

Backend còn kiểm tra catalog module active và tenant hợp lệ.

API:

```text
POST /api/system/subscriptions/{subscriptionId}/reactivate
```

### 10.8. Response mutation

```ts
type SystemSubscriptionCommandResult = {
  id: string;
  tenantId: string;
  moduleId: number;
  status: string;
  endDate: string;
  updatedAt?: string;
  isDeleted: boolean;
  changed: boolean;
};
```

Sau thành công invalidate:

- Subscription list hiện tại.
- Tenant detail tương ứng.
- Tenant list.
- Dashboard overview.
- Module usage/cancellation/expiration/trend có liên quan.

Không cần refetch tất cả đồng thời nếu query library hỗ trợ stale/invalidate.

---

## 11. Phase FE-5 — Billing order

### 11.1. Danh sách

Route:

```text
/system-admin/billing-orders
```

API:

```text
GET /api/system/billing-orders
```

Filter:

- Search billing order number/tenant.
- Tenant ID khi đi từ tenant detail.
- Payment status:
  - `Pending`
  - `Paid`
  - `Failed`
  - `Cancelled`
- Order status:
  - `Pending`
  - `Paid`
  - `Cancelled`
  - `Failed`
  - `Completed`
- Date range.

Sort:

- `billingDate`
- `createdAt`
- `finalAmount`
- `billingOrderNumber`
- Direction asc/desc.

Columns:

1. Mã billing order
2. Tenant
3. Billing date
4. Tổng tiền
5. Giảm giá
6. Thành tiền
7. Payment status
8. Order status
9. Số module
10. Ngày tạo
11. Nút `Xem chi tiết`

### 11.2. Chi tiết billing order

Route:

```text
/system-admin/billing-orders/:billingOrderId
```

API:

```text
GET /api/system/billing-orders/{billingOrderId}
```

Sections:

1. Header order number và status.
2. Tenant có link sang tenant detail.
3. Billing date.
4. Tổng tiền/giảm giá/thành tiền.
5. Notes.
6. Bảng module lines.
7. Bảng payment transactions liên quan.

Module line columns:

- Module code/name
- Quantity
- Unit price
- Proration days
- Line total
- Created at

Màn hình read-only. Không thêm nút mark paid, cancel hoặc refund vì Backend chưa có API SystemAdmin tương ứng.

---

## 12. Phase FE-6 — Payment transaction

### 12.1. Route

```text
/system-admin/payment-transactions
```

### 12.2. Filter

- Tenant.
- Billing order ID.
- Gateway:
  - `VNPay`
  - `SePay`
- Status:
  - `Success`
  - `Failed`
- Date range.

### 12.3. Bảng

Columns:

1. Transaction ID
2. Tenant
3. Billing order
4. Gateway
5. Gateway transaction ID
6. Response code
7. Amount
8. Status
9. Created at
10. Processed at

ID dài:

- Hiển thị rút gọn.
- Tooltip toàn bộ.
- Có nút copy.

Không log hoặc gửi gateway secret lên analytics phía FE.

Không có mutation payment ở phạm vi này.

---

## 13. Phase FE-7 — Quản lý module

### 13.1. Route

```text
/system-admin/modules
```

### 13.2. Header actions

- Nút `Thêm module`.
- Nút `Làm mới`.
- Filter local theo active/inactive nếu danh sách nhỏ.

### 13.3. Bảng module

Columns:

- Code
- Short code
- Name
- Description
- Monthly price
- Active
- Created at
- Updated at
- Actions

### 13.4. Tạo module

Nút:

```text
Thêm module
```

Form:

| Field | Bắt buộc | Ghi chú |
|---|---|---|
| Code | Có | Sau khi tạo không có API đổi code |
| Short code | Có | Sau khi tạo không có API đổi short code |
| Name | Có | Tối đa hợp lý theo UI |
| Description | Không | Textarea |
| Monthly price | Có | >= 0 |
| Is active | Có | Mặc định true |

Payload:

```json
{
  "code": "MODULE_CODE",
  "shortCode": "SHORT",
  "name": "Tên module",
  "description": "Mô tả",
  "monthlyPrice": 150000,
  "isActive": true
}
```

### 13.5. Sửa module

Form edit chỉ có:

- Name
- Description
- Monthly price

Code và short code hiển thị readonly vì Backend update không nhận hai field này.

API:

```text
PUT /api/Modules/{id}
```

Payload:

```json
{
  "name": "Tên mới",
  "description": "Mô tả mới",
  "monthlyPrice": 180000
}
```

### 13.6. Activate/deactivate

Nút theo trạng thái:

- Active -> `Ngừng hoạt động`
- Inactive -> `Kích hoạt`

Deactivate confirmation phải ghi rõ:

- Subscription đang suspended sẽ không reactivate được khi catalog module inactive.
- Việc deactivate có thể ảnh hưởng quyền truy cập module.

Không dùng toggle cập nhật ngay lập tức. Phải có confirm dialog vì ảnh hưởng toàn hệ thống.

---

## 14. Phase FE-8 — Quản lý role

### 14.1. Route

```text
/system-admin/roles
```

### 14.2. Bảng role

Columns:

- ID
- Name
- Description
- System role
- Actions

Badge:

- `isSystemRole=true`: `Vai trò hệ thống`
- false/null: `Vai trò tùy chỉnh`

### 14.3. Tạo role

Form:

- Name: bắt buộc, tối đa 100.
- Description: tối đa 500.

Không có checkbox `System role`; client không được tự tạo system role.

### 14.4. Sửa role

Custom role:

- Cho sửa name.
- Cho sửa description.

System role:

- Name readonly.
- Chỉ cho sửa description.
- Hiển thị cảnh báo `Không thể đổi tên vai trò hệ thống`.

Backend sẽ trả `409` nếu duplicate hoặc vi phạm bảo vệ system role.

### 14.5. Không có chức năng

Không thêm:

- Nút xóa role.
- Permission matrix.
- Gán permission chi tiết.
- Đổi cờ system role.

Backend hiện chưa hỗ trợ các chức năng này.

---

## 15. Phase FE-9 — Settings và bootstrap reset

### 15.1. Production

Không render route hoặc menu bootstrap reset trên production, kể cả khi đội vận hành
tạm bật maintenance gate phía Backend. Reset production phải được thực hiện trực tiếp
qua công cụ vận hành được kiểm soát, không qua UI thông thường.

### 15.2. Development/Staging

Chỉ render khi:

```text
environment != production
AND frontend config allowBootstrapReset == true
```

UI Danger Zone:

- Cảnh báo màu đỏ.
- Giải thích reset sẽ xóa bootstrap identity hợp lệ để cho phép bootstrap lại.
- Input confirmation phrase chính xác:

```text
RESET_SYSTEM_BOOTSTRAP
```

- Input current password.
- Checkbox xác nhận đã hiểu.
- Nút `Reset bootstrap identity`.

API:

```http
DELETE /api/system/bootstrap/reset
Content-Type: application/json

{
  "confirmation": "RESET_SYSTEM_BOOTSTRAP",
  "currentPassword": "current password"
}
```

Sau thành công:

1. Không lưu current password vào state lâu hơn cần thiết.
2. Xóa input ngay.
3. Xóa token/session hiện tại.
4. Redirect đến màn hình bootstrap/login phù hợp.

Không log request body vào console, monitoring hoặc analytics.

---

## 16. Error handling theo HTTP status

| Status | Xử lý FE |
|---|---|
| `400` | Hiển thị validation/detail; map field errors vào form |
| `401` | Thử refresh token theo flow hiện có; nếu thất bại về login |
| `403` | Hiển thị không đủ quyền hoặc SystemAdmin không còn active |
| `404` | Hiển thị resource không tồn tại; cho quay lại list |
| `409` | Hiển thị conflict; giữ dialog/form và refetch dữ liệu hiện tại |
| `429` | Hiển thị thao tác quá nhanh; disable retry ngắn hạn |
| `500+` | Thông báo lỗi hệ thống, có nút thử lại |

Mutation dialog:

- Không đóng khi request lỗi.
- Không mất nội dung reason khi lỗi.
- Disable nút submit khi đang gọi.
- Chống double click.

List:

- Request lỗi không xóa dữ liệu cũ ngay nếu đang refetch.
- Có banner `Không thể cập nhật dữ liệu mới nhất`.

---

## 17. Cache/query key và invalidation

Query key gợi ý:

```ts
["system", "dashboard", "overview", period]
["system", "dashboard", "module-usage", period]
["system", "dashboard", "module-cancellations", period]
["system", "dashboard", "module-expirations", period]
["system", "dashboard", "module-trends", range]
["system", "tenants", query]
["system", "tenant", tenantId]
["system", "tenant-users", tenantId, query]
["system", "subscriptions", query]
["system", "billing-orders", query]
["system", "billing-order", billingOrderId]
["system", "payments", query]
["system", "modules"]
["system", "roles", query]
```

### 17.1. Tenant status mutation

Invalidate:

- Tenant list.
- Tenant detail.
- Dashboard overview.
- Các query có liên quan đến tenant hiện tại.

### 17.2. Subscription mutation

Invalidate:

- Subscription list.
- Tenant detail.
- Tenant list.
- Dashboard và module statistics.

### 17.3. Module mutation

Invalidate:

- Module list.
- Module options trong tenant/subscription filter.
- Dashboard module charts.
- Subscription list nếu module active state có thể ảnh hưởng action.

### 17.4. Role mutation

Invalidate:

- Role list/paging.
- Role detail.
- Role options ở các form khác nếu có dùng chung.

---

## 18. UX chi tiết cho bảng

Mọi bảng cần:

- Skeleton khi load lần đầu.
- Empty state có nội dung phù hợp filter.
- Header cố định nếu bảng dài.
- Horizontal scroll trên màn hình nhỏ.
- Page size: 10, 20, 50, 100.
- Hiển thị `Tổng N kết quả`.
- Nút previous/next dựa trên `hasPrevious` và `hasNext`.
- Không cho page vượt `totalPages`; nếu xóa/filter làm page hiện tại mất thì quay về page cuối hợp lệ.

Responsive:

- Desktop: table đầy đủ.
- Tablet: ẩn bớt ID/date phụ vào expandable row.
- Mobile: card list hoặc horizontal table; action vẫn phải dễ bấm.

Accessibility:

- Dialog có focus trap.
- Nút icon có `aria-label`.
- Badge không chỉ phân biệt bằng màu.
- Chart có bảng dữ liệu hoặc mô tả thay thế.

---

## 19. Các thành phần FE không được làm sai

1. Không dùng tenant endpoint cũ để dựng SystemAdmin list.
2. Không hiển thị tenant `SYSTEM`.
3. Không cho client gửi `isSystemRole`.
4. Không cho edit module code/short code sau khi tạo.
5. Không coi subscription `Active` là usable nếu đã hết hạn.
6. Không reactivate subscription đã canceled.
7. Không tự đổi subscription từ Suspended sang Active sau khi extend.
8. Không gửi local datetime thiếu timezone cho `newEndDate`.
9. Không đóng mutation dialog khi Backend trả `409`.
10. Không lưu reason/current password vào persistent storage.
11. Không log response chứa dữ liệu user lên console production.
12. Không thêm nút delete/cancel/refund khi Backend chưa có endpoint.

---

## 20. File/folder FE đề xuất

Tên thực tế có thể đổi theo framework hiện tại:

```text
src/
  features/
    system-admin/
      api/
        systemDashboardApi.ts
        systemTenantsApi.ts
        systemSubscriptionsApi.ts
        systemBillingApi.ts
        systemModulesApi.ts
        systemRolesApi.ts
      components/
        SystemAdminLayout.tsx
        SystemStatusBadge.tsx
        SystemFilterBar.tsx
        ConfirmActionDialog.tsx
        TenantStatusDialog.tsx
        SubscriptionExtendDialog.tsx
        SubscriptionStatusDialog.tsx
      pages/
        SystemDashboardPage.tsx
        SystemTenantsPage.tsx
        SystemTenantDetailPage.tsx
        SystemSubscriptionsPage.tsx
        SystemBillingOrdersPage.tsx
        SystemBillingOrderDetailPage.tsx
        SystemPaymentTransactionsPage.tsx
        SystemModulesPage.tsx
        SystemRolesPage.tsx
        SystemAdminSettingsPage.tsx
      hooks/
        useSystemDashboard.ts
        useSystemTenants.ts
        useSystemSubscriptions.ts
        useSystemBilling.ts
      types/
        dashboard.ts
        tenant.ts
        subscription.ts
        billing.ts
        module.ts
        role.ts
        common.ts
      utils/
        status.ts
        dates.ts
        money.ts
        errors.ts
      routes.tsx
```

Nếu FE dùng Vue/Angular, giữ cùng separation: API, types, pages, components, composables/services và utils.

---

## 21. Test plan FE

### 21.1. Unit test

Test:

- `normalizeApiError`.
- DateOnly không lệch ngày.
- UTC conversion cho extend.
- Money formatter.
- Status mapping.
- Action eligibility tenant.
- Action eligibility subscription.
- Query serialization bỏ field rỗng.

### 21.2. Component test

Dashboard:

- Render đủ KPI.
- Chart empty/error/loading.
- Period invalid không gọi API.

Tenant:

- Filter đổi reset page.
- Suspend bắt buộc reason.
- PendingPayment không có nút activate.
- `changed=false` hiển thị info.

Subscription:

- Canceled không có action.
- End date cũ không submit extend.
- Suspended hết hạn không hiện reactivate.
- `409` giữ dialog.

Module:

- Edit không cho sửa code.
- Deactivate có confirmation.

Role:

- System role name readonly.
- Không có nút delete.

### 21.3. Integration/API mock test

Mock:

- 200 list rỗng.
- 200 list nhiều page.
- 400 validation.
- 401 token hết hạn.
- 403 SystemAdmin inactive.
- 404 detail.
- 409 mutation conflict.
- 500 retry.

### 21.4. E2E staging

Kịch bản:

1. Login SystemAdmin.
2. Mở dashboard.
3. Đổi tháng/năm.
4. Mở tenant list, filter và sort.
5. Xác nhận không thấy tenant `SYSTEM`.
6. Mở tenant detail và từng tab.
7. Mở subscription list.
8. Xem billing detail.
9. Xem payment transactions.
10. Tạo/sửa module test.
11. Activate/deactivate module test.
12. Tạo/sửa custom role test.
13. Bật mutation flag trên staging.
14. Suspend/reactivate tenant test.
15. Extend/suspend/reactivate subscription test.
16. Xác nhận access module thay đổi ngay.

Không chạy mutation E2E trên tenant production thật.

---

## 22. Thứ tự triển khai FE an toàn

### Sprint FE-0 — Foundation

- Route guard.
- Layout/sidebar.
- API error normalizer.
- Shared types/components.
- Feature flags.

### Sprint FE-1 — Read-only Phase 0

- Dashboard overview.
- Module usage/cancellation/expiration.
- Tenant list/detail.
- Module catalog list.

### Sprint FE-2 — Read-only Phase 1

- Tenant users.
- Subscription list.
- Billing list/detail.
- Payment list.
- Module trends.

### Sprint FE-3 — Catalog management

- Create/update/activate/deactivate module.
- Create/update role.

### Sprint FE-4 — Mutation Phase 2 trên staging

- Tenant suspend/reactivate.
- Subscription extend/suspend/reactivate.
- Confirmation dialogs.
- Invalidation.
- Structured error handling.

### Sprint FE-5 — Production rollout

1. Deploy toàn bộ read-only UI.
2. Giữ `systemAdminMutationsEnabled=false`.
3. Theo dõi 401/403/500 và performance query.
4. Chạy smoke test.
5. Bật mutation cho nhóm SystemAdmin được xác nhận.
6. Theo dõi log EventId `5101–5105`.

---

## 23. API Backend chưa có — không được gọi trong bản FE hiện tại

Các chức năng sau cần Backend bổ sung ở phase tương lai:

1. Audit log screen lưu lịch sử lâu dài.
2. Export CSV/Excel server-side.
3. Create/cancel subscription trực tiếp bởi SystemAdmin.
4. Refund/retry/mark-paid payment.
5. Suspend/activate user từ màn hình SystemAdmin.
6. Delete role.
7. Permission matrix cho custom role.
8. Revenue analytics chính xác.
9. Persist và đọc lại reason của mutation.
10. Capability endpoint trả feature flags từ server.

Endpoint gợi ý cho backlog, **chưa tồn tại**:

```text
GET  /api/system/capabilities
GET  /api/system/audit-events
GET  /api/system/exports/tenants
GET  /api/system/exports/subscriptions
GET  /api/system/exports/billing-orders
GET  /api/system/exports/payment-transactions
```

Không implement API client cho danh sách này cho đến khi Backend có contract chính thức.

---

## 24. Definition of Done

FE chỉ được coi là hoàn thành khi:

- SystemAdmin route không truy cập được bởi role khác.
- Dashboard hiển thị đúng toàn bộ KPI và chart.
- Tenant list có filter/sort/paging và không có `SYSTEM`.
- Tenant detail có owner, module, user, subscription, billing và payment.
- Subscription list hỗ trợ filter và canceled view.
- Billing/payment read-only đúng contract.
- Module create/update/activate/deactivate hoạt động.
- Role create/update tuân thủ system role protection.
- Mutation có feature flag.
- Suspend tenant bắt buộc reason.
- Subscription mutation tuân thủ transition.
- Mọi mutation có confirmation.
- Không có optimistic update sai.
- 400/401/403/404/409/500 được xử lý rõ ràng.
- Date UTC và DateOnly không lệch ngày.
- Tiền hiển thị đúng VND.
- Không lộ secret trên UI, log hoặc analytics.
- Unit/component tests pass.
- E2E staging pass.
- Production triển khai read-only trước, mutation sau.

---

## 25. Checklist bàn giao cho FE developer

- [ ] Tạo SystemAdmin route guard.
- [ ] Tạo layout và sidebar.
- [ ] Thêm API client Dashboard.
- [ ] Thêm API client Tenant.
- [ ] Thêm API client Subscription.
- [ ] Thêm API client Billing/Payment.
- [ ] Thêm API client Module/Role.
- [ ] Tạo types từ DTO Backend.
- [ ] Tạo error normalizer.
- [ ] Tạo status badge mapping.
- [ ] Tạo Dashboard page.
- [ ] Tạo Tenant list.
- [ ] Tạo Tenant detail và các tab.
- [ ] Tạo Subscription list.
- [ ] Tạo Billing list/detail.
- [ ] Tạo Payment list.
- [ ] Tạo Module management.
- [ ] Tạo Role management.
- [ ] Tạo tenant mutation dialogs.
- [ ] Tạo subscription mutation dialogs.
- [ ] Thêm feature flag.
- [ ] Thêm query invalidation.
- [ ] Thêm loading/empty/error states.
- [ ] Thêm responsive/accessibility.
- [ ] Viết unit/component tests.
- [ ] Chạy E2E staging.
- [ ] Deploy read-only trước.
- [ ] Chỉ bật mutation sau khi xác nhận.
