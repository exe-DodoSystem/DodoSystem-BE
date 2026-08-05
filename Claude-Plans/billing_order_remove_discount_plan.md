# Kế hoạch bỏ discount khỏi BillingOrder và triển khai an toàn lên production

> Ngày tạo: 2026-08-03  
> Phạm vi: Backend, PostgreSQL production, CI/CD và AWS Lightsail  
> Trạng thái ban đầu: Chưa thực hiện  
> Tài liệu vận hành gốc: [`aws_deploy_guide.md`](./aws_deploy_guide.md)

## 1. Mục tiêu

- Bỏ hoàn toàn nghiệp vụ giảm giá theo số lượng module khỏi `BillingOrder`.
- `BillingOrder.TotalAmount` trở thành số tiền duy nhất của hóa đơn module và cũng là số tiền cần thanh toán.
- Công thức chuẩn sau thay đổi:

```text
BillingOrderModule.LineTotal = giá thực tế của từng dòng module
BillingOrder.TotalAmount      = SUM(BillingOrderModule.LineTotal)
PayableAmount                 = BillingOrder.TotalAmount
```

- Xóa `DiscountAmount` và `FinalAmount` khỏi entity, DTO, API, repository, analytics và cuối cùng khỏi bảng PostgreSQL `BillingOrders`.
- Giữ an toàn dữ liệu đã deploy trên production và giữ được khả năng rollback image.
- Không thay đổi discount của entity `Order` bán hàng thông thường.

## 2. Cách dùng checklist

- Chỉ đổi `[ ]` thành `[x]` sau khi đã thực hiện và kiểm tra kết quả thực tế.
- Ghi SHA release, thời gian, người thực hiện và bằng chứng không chứa secret vào mục Nhật ký cuối file.
- Nếu một mục không áp dụng, ghi rõ `N/A - lý do`; không tự đánh dấu hoàn thành.
- Không thực hiện phase contract/drop column nếu phase tương thích ngược chưa chạy ổn định trên production.
- Không chạy các lệnh production bằng cách copy mù. Luôn xác nhận đang ở đúng VPS, đúng thư mục `/opt/dodo` và đúng database.

## 3. Quyết định nghiệp vụ cần chốt trước khi code

### 3.1 Proration khi mua thêm module

Hiện tại mua module bổ sung đang tính theo số ngày còn lại của gói hiện tại:

```text
LineTotal = floor(MonthlyPrice / 30 * remainingDays)
```

Khuyến nghị giữ proration và chỉ bỏ discount. Khi đó khách vẫn trả đúng tổng các `LineTotal`, nhưng module mua giữa kỳ không bị thu nguyên tháng.

- [x] Chọn phương án A — giữ proration hiện tại (khuyến nghị).
- [ ] Hoặc chọn phương án B — luôn tính nguyên `MonthlyPrice` cho mỗi module.
- [ ] Nếu chọn B, đã bổ sung phạm vi xóa `prorateUntilUtc` khỏi `IBillingOrderService`, `BillingOrderService` và `BillingOrderController`.
- [x] Xác nhận `Quantity` vẫn luôn bằng `1`; nếu muốn mua nhiều quantity của cùng module thì phải lập kế hoạch khác.

### 3.2 Dữ liệu hóa đơn cũ

Khuyến nghị bảo toàn số tiền khách đã được báo/phải trả trước đây. Với hóa đơn cũ có discount, trước khi bỏ field sẽ chuyển số tiền net sang `TotalAmount`, sau đó đặt discount về `0`:

```text
TotalAmount mới = FinalAmount cũ
DiscountAmount  = 0
FinalAmount     = TotalAmount mới (do computed column tự tính lại)
```

- [ ] Xác nhận bảo toàn số tiền net của tất cả BillingOrder cũ (khuyến nghị).
- [ ] Nếu muốn hủy discount của cả hóa đơn pending cũ, đã có phê duyệt nghiệp vụ riêng và ghi rõ người phê duyệt.
- [x] Chọn reset toàn bộ PostgreSQL database trên server vì chỉ có dữ liệu test, chưa có dữ liệu khách hàng.
- [ ] Xác nhận frontend/admin sẽ ngừng đọc `discountAmount` và `finalAmount`, chỉ dùng `totalAmount`.

## 4. Chiến lược triển khai database

### 4.1 Phương án đã chọn — reset database test

Do database server chưa có dữ liệu khách hàng và người dùng đã chấp thuận xóa toàn bộ dữ liệu test, lần triển khai này dùng một release cuối cùng:

1. Sửa toàn bộ code để chỉ dùng `TotalAmount`.
2. Tạo migration mới `RemoveBillingOrderDiscountColumns`; không sửa migration cũ.
3. Build image mới trên feature branch nhưng chưa auto-deploy.
4. Trên VPS, backup lần cuối để có đường kiểm tra nếu cần.
5. Dừng API, xác định chính xác PostgreSQL named volume, xóa riêng volume PostgreSQL.
6. Deploy image mới; startup chạy toàn bộ migration trên database trống.
7. Kiểm tra schema mới không có hai cột BillingOrder và chạy smoke test.

Runbook đầy đủ nằm ở mục **12A — Reset PostgreSQL test database và deploy một release**. Với lựa chọn này, các phase backfill/two-release ở mục 9–12 được giữ làm phương án tham khảo nếu sau này môi trường có dữ liệu thật và được đánh dấu `N/A - dùng reset path 12A`.

### 4.2 Phương án bảo toàn dữ liệu — chỉ dùng khi không reset

Ứng dụng hiện gọi `Database.Migrate()` lúc khởi động. `deploy.sh` chỉ rollback image, không rollback PostgreSQL. Nếu code và migration drop column được deploy cùng một lần, rollback về image cũ sẽ lỗi vì image cũ vẫn query `DiscountAmount` và `FinalAmount`.

Kế hoạch dùng expand/contract:

1. **Release A — code tương thích ngược:** code ngừng map/đọc/ghi hai field nhưng database vẫn giữ hai cột. Không tạo migration drop column ở release này.
2. Backfill production để `TotalAmount` chứa đúng số tiền net lịch sử và `DiscountAmount = 0`.
3. Chạy Release A ổn định ít nhất 24–72 giờ hoặc qua một chu kỳ thanh toán sandbox/production có kiểm soát.
4. **Release B — contract:** tạo và áp dụng migration xóa hai cột. Rollback image chỉ được về Release A hoặc mới hơn.

Trong Release A, model runtime và model snapshot sẽ tạm thời lệch nhau có chủ đích:

- Runtime model không còn map hai field nên vẫn chạy khi cột còn hoặc đã bị drop.
- `SMEFLOWSystemContextModelSnapshot` vẫn giữ hai field để Release B có thể scaffold đúng migration drop column.
- Không chạy/scaffold migration ngoài kế hoạch trong khoảng giữa Release A và Release B.
- `dotnet ef migrations has-pending-model-changes` có thể báo pending trong giai đoạn này; đây là trạng thái tạm thời đã được kiểm soát.

## 5. Danh sách phạm vi code

### 5.1 Bắt buộc thay đổi

| Nhóm | File | Thay đổi chính |
|---|---|---|
| Entity | `SMEFLOWSystem.Core/Entities/BillingOrder.cs` | Xóa `DiscountAmount`, `FinalAmount` |
| Tạo hóa đơn | `SMEFLOWSystem.Application/Services/BillingOrderService.cs` | Xóa discount theo số module, chỉ gán `TotalAmount` |
| DTO tenant | `SMEFLOWSystem.Application/DTOs/ModuleDtos/BillingOrderDto.cs` | Xóa hai field khỏi response |
| Payment | `SMEFLOWSystem.Application/Services/PaymentService.cs` | Dùng `TotalAmount` ở VNPay, SePay và callback |
| Email | `SMEFLOWSystem.Application/Services/BillingService.cs` | Dùng `TotalAmount`, bỏ dòng giảm giá |
| Dev simulation | `SMEFLOWSystem.WebAPI/Controllers/PaymentController.cs` | SePay simulation dùng `TotalAmount` |
| EF config | `SMEFLOWSystem.Infrastructure/Data/Configurations/BillingConfigurations.cs` | Xóa mapping computed/default của hai field chỉ trong `BillingOrderConfiguration` |
| Repository | `SMEFLOWSystem.Infrastructure/Repositories/BillingOrderRepository.cs` | Xóa copy hai field khi update |
| System billing | `SMEFLOWSystem.Infrastructure/Repositories/SystemBillingReadRepository.cs` | Chỉ project/sort theo `TotalAmount` |
| System DTO | `SMEFLOWSystem.Application/DTOs/SystemDtos/SystemBillingOrderDto.cs` | Xóa hai field ở list/detail |
| Analytics read | `SMEFLOWSystem.Infrastructure/Repositories/SystemAnalyticsReadRepository.cs` | Thay net/final bằng `TotalAmount` |
| Analytics contract | `SMEFLOWSystem.Application/Interfaces/IRepositories/ISystemAnalyticsReadRepository.cs` | Rename/remove các row field liên quan discount/final |
| Analytics helper | `SMEFLOWSystem.Application/Helpers/System/AnalyticsMetricCalculator.cs` | Xóa `CalculateFinalAmount`, đổi tên row field nếu cần |
| Analytics service | `SMEFLOWSystem.Application/Services/System/SystemAnalyticsService.cs` | Dùng tên amount mới sau khi đổi row model |
| Tests | `SMEFLOWSystem.Tests/*` | Bỏ fixture discount cũ, bổ sung test không discount |

### 5.2 Không được sửa nhầm

- `SMEFLOWSystem.Core/Entities/Order.cs` vẫn có `DiscountAmount` và `FinalAmount` cho nghiệp vụ Order khác.
- `OrderConfiguration` trong `BillingConfigurations.cs` vẫn giữ computed column của `Orders`.
- `OrderRepository.cs` không thuộc phạm vi này.
- `PaymentTransaction.Amount` vẫn giữ nguyên vì đây là số tiền gateway ghi nhận.
- `BillingOrderModule.LineTotal`, `UnitPrice`, `Quantity` vẫn giữ nguyên.
- `ModuleSubscriptionService` và `BillingOrderModuleService` không cần đổi nếu không xuất hiện phụ thuộc mới.

## 6. Phase 0 — Baseline và kiểm kê trước sửa

- [ ] Tạo branch riêng, ví dụ `refactor/billing-order-remove-discount`.
- [ ] Chạy `git status --short` và ghi nhận các thay đổi có sẵn của người dùng; không ghi đè file ngoài phạm vi.
- [ ] Ghi SHA production hiện tại từ VPS:

```bash
cd /opt/dodo
grep '^IMAGE_TAG=' .env
docker inspect dodo-webapi --format '{{.Config.Image}}'
```

- [ ] Ghi migration hiện tại trên production:

```bash
cd /opt/dodo
docker compose exec -T postgres sh -c \
  'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -c "SELECT * FROM \"__EFMigrationsHistory\" ORDER BY \"MigrationId\";"'
```

- [ ] Xác nhận health production đang tốt trước thay đổi:

```bash
cd /opt/dodo
docker compose ps
curl --fail --show-error http://127.0.0.1:8085/health
```

- [ ] Kiểm kê code lần cuối:

```powershell
rg -n -S "DiscountAmount|FinalAmount" `
  SMEFLOWSystem.Application `
  SMEFLOWSystem.Core `
  SMEFLOWSystem.Infrastructure `
  SMEFLOWSystem.WebAPI `
  SMEFLOWSystem.Tests `
  -g "!Migrations/**" -g "!bin" -g "!obj"
```

- [ ] Liệt kê endpoint/FE đang dùng `BillingOrderDto` và `SystemBillingOrderDto`.
- [ ] Thống nhất thay đổi API với frontend trước khi deploy Release A.
- [ ] Chụp mẫu response hiện tại của billing list/detail để so sánh sau sửa, không lưu token hoặc dữ liệu nhạy cảm.

## 7. Phase 1 — Release A: bỏ toàn bộ logic discount trong code

### 7.1 Entity và EF runtime model

- [x] Xóa `DiscountAmount` và `FinalAmount` khỏi `BillingOrder`.
- [x] Xóa hai cấu hình tương ứng khỏi `BillingOrderConfiguration`.
- [x] Kiểm tra `OrderConfiguration` vẫn còn hai cấu hình của entity `Order`.
- [x] N/A — không dùng Release A riêng vì đã chọn reset database test.
- [x] N/A — snapshot được cập nhật cùng migration cuối theo reset path.
- [ ] Ghi chú trong PR rằng snapshot lệch runtime model có chủ đích đến Release B.

### 7.2 Logic tạo BillingOrder

- [x] Trong `BillingOrderService`, giữ:

```csharp
var total = lines.Sum(line => line.LineTotal);
```

- [x] Chỉ gán `TotalAmount = total` khi tạo entity.
- [x] Xóa `discountPercent`, `discountAmount`, `discountDecimal`.
- [x] Xóa method `GetDiscountPercent`.
- [x] Không thay đổi validation module đang active/trùng subscription.
- [x] Không thay đổi logic trial ngoài phạm vi; lưu ý hiện `isTrialOrder` còn quyết định `LineTotal = 0` và `Notes = "TRIAL"`.
- [x] Nếu chọn giữ proration, giữ công thức proration và test riêng.
- [ ] Nếu chọn bỏ proration, xóa tham số và các call-site theo quyết định ở mục 3.1.

### 7.3 PaymentService

Thay tất cả công thức `TotalAmount - discount` thành `TotalAmount`:

- [x] Validate `vnp_Amount` trong callback VNPay.
- [x] Build query giả lập VNPay success.
- [x] Tạo request/URL VNPay.
- [x] Tạo amount và QR URL SePay.
- [x] Validate `TransferAmount` trong webhook SePay.
- [x] Giữ validation `TotalAmount > 0`.
- [x] Giữ quy tắc làm tròn minor unit VNPay hiện tại.
- [x] Quyết định riêng việc SePay có tiếp tục chấp nhận overpayment; không đổi hành vi này trong refactor discount nếu chưa có yêu cầu.

Kết quả mong đợi ở mọi gateway:

```csharp
var payable = order.TotalAmount;
```

### 7.4 BillingService và email

- [x] Xóa biến `discount`.
- [x] Dùng `payable = order.TotalAmount` hoặc dùng trực tiếp `order.TotalAmount`.
- [x] Xóa dòng HTML “Giảm giá” khỏi email đăng ký mới.
- [x] Xóa dòng HTML “Giảm giá” khỏi email gia hạn.
- [x] Xóa dòng HTML “Giảm giá” khỏi email mua thêm module.
- [x] Xóa dòng HTML “Giảm giá” khỏi các email type còn lại.
- [x] Chỉ hiển thị một dòng tổng/cần thanh toán để tránh hai dòng cùng giá trị.
- [x] QR/SePay email hiển thị đúng `TotalAmount`.

### 7.5 Controller simulation

- [x] `PaymentController.SimulateSePaySuccess` dùng `order.TotalAmount` làm `TransferAmount`.
- [x] Endpoint simulation vẫn chỉ hoạt động trong Development.

### 7.6 Repository và DTO tenant

- [x] Xóa hai assignment trong `BillingOrderRepository.UpdateAsync`.
- [x] Xóa hai assignment trong `BillingOrderRepository.UpdateIgnoreTenantAsync`.
- [x] Xóa hai property khỏi `BillingOrderDto`.
- [x] Kiểm tra AutoMapper build thành công sau khi DTO thay đổi.
- [x] Xác nhận response tenant billing chỉ còn `totalAmount` cho phần tiền.

### 7.7 System billing/admin

- [x] Xóa `DiscountAmount`, `FinalAmount` khỏi `SystemBillingOrderListItemDto`.
- [x] Xóa `DiscountAmount`, `FinalAmount` khỏi `SystemBillingOrderDetailDto`.
- [x] `SystemBillingReadRepository` chỉ project `TotalAmount`.
- [x] Xóa field tương ứng khỏi `BillingOrderRow` nội bộ.
- [x] Đổi sort key `finalAmount` sang `totalAmount`.
- [ ] Nếu cần tương thích FE ngắn hạn, document alias `finalAmount` nhưng cho sort theo `TotalAmount`; đặt ngày xóa alias.
- [ ] Cập nhật Swagger/API contract.
- [ ] Frontend admin đã đổi cột “Final amount”/“Discount” thành một cột “Total amount”.

### 7.8 System analytics

- [x] Mọi projection BillingOrder revenue dùng `order.TotalAmount`.
- [x] Rename `InvoicedOrderRow.FinalAmount` thành `TotalAmount` hoặc tên rõ nghĩa tương đương.
- [x] Rename `PendingOutstandingOrderRow.FinalAmount` thành `TotalAmount`.
- [x] Xóa `BillingOrderModuleAllocationRow.OrderFinalAmount` vì field không được dùng.
- [x] Xóa `BillingOrderModuleAllocationRow.OrderDiscountAmount`.
- [x] Xóa `AnalyticsMetricCalculator.CalculateFinalAmount` nếu không còn caller production.
- [x] `SumInvoiced` cộng field amount mới.
- [x] Revenue series `InvoicedRevenue` dùng `TotalAmount`.
- [x] Outstanding series dùng `TotalAmount`.
- [x] Revenue module allocation vẫn dựa trên `LineTotal`; tổng payment allocation không thay đổi.
- [x] Kiểm tra analytics không dùng nhầm entity `Order`.

### 7.9 Search gate của Release A

- [x] Chạy lại search:

```powershell
rg -n -S "DiscountAmount|FinalAmount" `
  SMEFLOWSystem.Application `
  SMEFLOWSystem.Core `
  SMEFLOWSystem.Infrastructure `
  SMEFLOWSystem.WebAPI `
  SMEFLOWSystem.Tests `
  -g "!Migrations/**" -g "!bin" -g "!obj"
```

- [x] Mọi kết quả còn lại đã được phân loại rõ: chỉ thuộc entity `Order` hoặc historical migration.
- [x] Search `GetDiscountPercent` không còn kết quả.
- [x] Search `TotalAmount - discount` không còn kết quả trong billing/payment module.

## 8. Phase 2 — Test Release A

### 8.1 Unit/integration test cần sửa hoặc thêm

- [x] Test tạo hóa đơn 1 module: `TotalAmount = LineTotal`.
- [x] Test tạo hóa đơn 2 module: tổng đúng và không giảm 10%.
- [x] Test tạo hóa đơn 3 module: tổng đúng và không giảm 15%.
- [x] Test tạo hóa đơn 4 module: tổng đúng và không giảm 20%.
- [ ] Test duplicate/invalid module vẫn bị reject như trước.
- [ ] Test module đang active vẫn bị reject như trước.
- [x] Test proration đúng nếu chọn giữ proration.
- [ ] Test renewal không proration vẫn bằng tổng monthly price.
- [ ] Test VNPay request dùng đúng `TotalAmount`.
- [ ] Test VNPay callback reject amount khác `TotalAmount`.
- [ ] Test SePay QR/payment info dùng đúng `TotalAmount`.
- [ ] Test SePay webhook reject chuyển thiếu so với `TotalAmount`.
- [ ] Test email không còn text “Giảm giá”.
- [x] Test entity/tenant/system DTO không còn property `DiscountAmount`/`FinalAmount`.
- [x] Test system billing list/detail chỉ còn contract `TotalAmount` cho phần tiền.
- [x] Test system billing chấp nhận sort `totalAmount` và reject alias cũ `finalAmount`.
- [x] Cập nhật `SystemRevenueCalculatorTests`.
- [x] Cập nhật `SystemAnalyticsReadRepositoryTests`.
- [x] Cập nhật `SystemRevenueSeriesTests`.
- [x] Cập nhật `SystemRevenueBreakdownTests`.

### 8.2 Build gate local

Chạy tại repository trên local:

```powershell
dotnet restore SMEFLOWSystem.sln -p:WarningsAsErrors=NU1901%3BNU1902%3BNU1903%3BNU1904
dotnet build SMEFLOWSystem.sln -c Release --no-restore
dotnet test SMEFLOWSystem.sln -c Release --no-build --verbosity normal
```

- [x] Restore thành công và không có advisory NU1901–NU1904.
- [x] Build Release thành công, không warning mới liên quan mapping/nullable.
- [x] Toàn bộ test thành công: 218 passed, 1 PostgreSQL integration test skipped do thiếu connection string disposable.
- [ ] Swagger khởi động được.
- [ ] Local PostgreSQL/Redis/RabbitMQ/webapi healthy nếu chạy Compose.
- [ ] Smoke test mua module bằng VNPay sandbox hoặc SePay sandbox thành công.

## 9. Phase 3 — Chuẩn bị production cho Release A

### 9.1 Chuẩn bị image nhưng chưa tự deploy

Do workflow hiện deploy tự động khi push/merge `main`, cần chuẩn bị trước để phối hợp maintenance:

1. Push branch Release A.
2. Mở GitHub Actions và chạy `workflow_dispatch` trên branch đó.
3. Workflow sẽ build/test/push image SHA; job deploy bị skip vì ref không phải `main`.
4. Copy đủ SHA 40 ký tự của image Release A.

- [ ] PR Release A đã review.
- [ ] CI build/test xanh.
- [ ] Image Release A theo SHA 40 ký tự đã tồn tại trên GHCR.
- [ ] Ghi `RELEASE_A_SHA` vào Nhật ký, không dùng `latest` hoặc SHA rút gọn.
- [ ] Xác nhận frontend tương thích đã sẵn sàng deploy cùng thời điểm.
- [ ] Chọn maintenance window tải thấp.
- [ ] Thông báo downtime ngắn cho người liên quan.

### 9.2 Audit dữ liệu trước backfill

Chạy read-only trên VPS:

```bash
cd /opt/dodo
docker compose exec -T postgres sh -c \
  'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB"' <<'SQL'
SELECT
  COUNT(*) AS total_orders,
  COUNT(*) FILTER (WHERE COALESCE("DiscountAmount", 0) <> 0) AS discounted_orders,
  COALESCE(SUM("TotalAmount"), 0) AS gross_total,
  COALESCE(SUM(COALESCE("FinalAmount", "TotalAmount" - COALESCE("DiscountAmount", 0))), 0) AS net_total
FROM "BillingOrders";
SQL
```

- [ ] Lưu kết quả audit không chứa thông tin khách hàng.
- [ ] Đối chiếu số lượng discounted order với kỳ vọng.
- [ ] Nếu `net_total` âm hoặc có amount bất thường, dừng deploy và điều tra.

### 9.3 Backup bắt buộc

- [ ] Chạy backup off-site S3 theo guide:

```bash
cd /opt/dodo
/opt/dodo/backup-postgres.sh
tail -n 20 /opt/dodo/backups/backup.log
```

- [ ] Xác nhận object S3 mới có kích thước lớn hơn 0.
- [ ] Xác nhận đã từng restore test thành công theo phần 8.3 của AWS guide.
- [ ] Tạo thêm dump local trước thay đổi:

```bash
cd /opt/dodo
PRECHANGE_BACKUP="/opt/dodo/backups/billing-discount-prechange-$(date -u +%Y-%m-%dT%H-%M-%SZ).dump"
docker compose exec -T postgres sh -c \
  'pg_dump -U "$POSTGRES_USER" -d "$POSTGRES_DB" -Fc' > "$PRECHANGE_BACKUP"
test -s "$PRECHANGE_BACKUP" && echo prechange-backup-ok
echo "$PRECHANGE_BACKUP"
```

- [ ] Ghi đúng đường dẫn dump vào Nhật ký.

## 10. Phase 4 — Backfill production và deploy Release A

### 10.1 Vào maintenance và dừng writer

```bash
cd /opt/dodo
pwd
docker compose stop webapi
docker compose ps
```

- [ ] `pwd` là `/opt/dodo`.
- [ ] `webapi` đã dừng; PostgreSQL vẫn healthy.
- [ ] Không có deploy CI khác đang chạy.

### 10.2 Chạy backfill trong transaction

Đoạn SQL dưới đây bảo toàn số tiền net cũ, sau đó đưa discount về 0. `FinalAmount` là computed column nên sẽ tự trở thành bằng `TotalAmount` mới.

```bash
cd /opt/dodo
docker compose exec -T postgres sh -c \
  'psql -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d "$POSTGRES_DB"' <<'SQL'
BEGIN;

UPDATE "BillingOrders"
SET
  "TotalAmount" = COALESCE(
    "FinalAmount",
    "TotalAmount" - COALESCE("DiscountAmount", 0)
  ),
  "DiscountAmount" = 0
WHERE COALESCE("DiscountAmount", 0) <> 0
   OR "FinalAmount" IS DISTINCT FROM "TotalAmount";

DO $$
BEGIN
  IF EXISTS (
    SELECT 1
    FROM "BillingOrders"
    WHERE COALESCE("DiscountAmount", 0) <> 0
       OR "FinalAmount" IS DISTINCT FROM "TotalAmount"
  ) THEN
    RAISE EXCEPTION 'BillingOrder amount backfill verification failed';
  END IF;
END $$;

COMMIT;
SQL
```

- [ ] Lệnh kết thúc với `COMMIT`, không có exception.
- [ ] Nếu lỗi trước `COMMIT`, transaction đã rollback; không tiếp tục deploy trước khi tìm nguyên nhân.

### 10.3 Verify dữ liệu sau backfill

```bash
cd /opt/dodo
docker compose exec -T postgres sh -c \
  'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB"' <<'SQL'
SELECT
  COUNT(*) AS total_orders,
  COUNT(*) FILTER (WHERE COALESCE("DiscountAmount", 0) <> 0) AS non_zero_discount_orders,
  COUNT(*) FILTER (WHERE "FinalAmount" IS DISTINCT FROM "TotalAmount") AS mismatched_amount_orders,
  COALESCE(SUM("TotalAmount"), 0) AS payable_total
FROM "BillingOrders";
SQL
```

- [ ] `non_zero_discount_orders = 0`.
- [ ] `mismatched_amount_orders = 0`.
- [ ] `payable_total` khớp `net_total` đã audit trước backfill.

### 10.4 Deploy image Release A trong maintenance

Thay placeholder bằng SHA 40 ký tự thật đã build:

```bash
cd /opt/dodo
./deploy.sh RELEASE_A_40_CHARACTER_SHA
docker compose ps
docker compose logs --since=15m --tail=300 webapi
curl --fail --show-error http://127.0.0.1:8085/health
```

- [ ] `deploy.sh` báo thành công.
- [ ] `IMAGE_TAG` là đúng Release A SHA.
- [ ] Tất cả container Up/healthy.
- [ ] Health local HTTP 200.
- [ ] Health HTTPS domain HTTP 200.
- [ ] Log không có lỗi column `DiscountAmount`/`FinalAmount`.
- [ ] Log không có migration ngoài dự kiến.

Nếu Release A lỗi và `deploy.sh` rollback về image cũ:

- Image cũ vẫn chạy được vì dữ liệu đã được chuẩn hóa thành discount 0.
- Giữ maintenance hoặc hạn chế tạo BillingOrder mới vì code cũ có thể tiếp tục sinh discount mới.
- Sửa Release A, build SHA mới và lặp lại audit/backfill trước lần deploy tiếp theo.
- Không restore database tự động.

### 10.5 Smoke test Release A

- [ ] Login tenant admin thành công.
- [ ] Billing list/detail trả `totalAmount`, không còn hai field cũ.
- [ ] Với staging/sandbox hoặc tenant test được phê duyệt, tạo đơn 2+ module và xác nhận không discount.
- [ ] `TotalAmount = SUM(LineTotal)`.
- [ ] VNPay/SePay request amount đúng `TotalAmount`.
- [ ] Callback/webhook cập nhật trạng thái đúng.
- [ ] Email không còn dòng giảm giá.
- [ ] Module subscription được kích hoạt sau payment success.
- [ ] System admin billing/analytics không lỗi.
- [ ] Collected revenue vẫn khớp `PaymentTransaction.Amount`.

### 10.6 Hoàn tất Release A

- [ ] Merge Release A vào `main` sau khi manual deploy ổn định.
- [ ] Theo dõi workflow main; lần deploy SHA main tiếp theo vẫn healthy.
- [ ] Ghi SHA main tương ứng.
- [ ] Theo dõi 24–72 giờ: health, 5xx, payment callback, webhook, email và analytics.
- [ ] Không scaffold migration khác trong thời gian chờ contract.
- [ ] Xác nhận không có code/service nào còn cần hai cột cũ.

## 11. Phase 5 — Release B: tạo migration contract xóa cột

Chỉ bắt đầu khi toàn bộ Release A đã đạt.

### 11.1 Tạo migration local

```powershell
dotnet ef migrations add RemoveBillingOrderDiscountColumns `
  --project SMEFLOWSystem.Infrastructure `
  --startup-project SMEFLOWSystem.WebAPI `
  --output-dir Migrations
```

- [x] Migration mới chỉ thay đổi bảng `BillingOrders`.
- [x] `Up()` drop `FinalAmount` trước.
- [x] `Up()` drop `DiscountAmount` sau.
- [x] Migration không drop hai cột của bảng `Orders`.
- [x] `Down()` add `DiscountAmount` trước, rồi add computed `FinalAmount` nếu cần cho môi trường dev/test.
- [x] Hiểu rằng `Down()` không khôi phục giá trị discount lịch sử đã mất.
- [x] Snapshot không còn hai property của `BillingOrder`.
- [x] Snapshot vẫn còn hai property của entity `Order`.
- [x] Không sửa migration `InitialPostgreSql` hoặc migration đã apply.

### 11.2 Review migration script

```powershell
dotnet ef migrations script --idempotent `
  --project SMEFLOWSystem.Infrastructure `
  --startup-project SMEFLOWSystem.WebAPI `
  --output migration-review.sql

rg -n -i 'DROP TABLE|DROP COLUMN|ALTER COLUMN|DELETE FROM|TRUNCATE' migration-review.sql
```

- [x] Đã tạo và review phần contract của idempotent migration script.
- [x] Chỉ có hai `DROP COLUMN` dự kiến trên `BillingOrders`.
- [x] Thứ tự drop đúng dependency computed column.
- [x] Script không chứa connection string/secret.
- [x] File review tạm đã được xóa, không commit vào repository.

### 11.3 Test migration trên bản restore production

- [ ] Restore bản backup production vào database test/staging, không đè production.
- [ ] Chạy Release A trên database restore và smoke test.
- [ ] Apply migration Release B.
- [ ] Xác nhận hai cột đã mất:

```sql
SELECT column_name
FROM information_schema.columns
WHERE table_schema = 'public'
  AND table_name = 'BillingOrders'
  AND column_name IN ('DiscountAmount', 'FinalAmount');
```

- [ ] Query trả 0 dòng.
- [ ] Chạy lại Release A trên schema đã contract và xác nhận vẫn healthy; đây là bài test rollback image bắt buộc.
- [ ] Chạy Release B và toàn bộ smoke test.
- [ ] Kiểm tra `TotalAmount = SUM(LineTotal)` cho các đơn mới không proration hoặc theo quy tắc proration đã chốt.
- [ ] Kiểm tra dữ liệu paid/history và analytics không bị tăng lại gross amount.

### 11.4 Build gate Release B

```powershell
dotnet restore SMEFLOWSystem.sln -p:WarningsAsErrors=NU1901%3BNU1902%3BNU1903%3BNU1904
dotnet build SMEFLOWSystem.sln -c Release --no-restore
dotnet test SMEFLOWSystem.sln -c Release --no-build --verbosity normal
```

- [x] Restore/build/test đều thành công.
- [x] `dotnet ef migrations has-pending-model-changes` không còn báo pending.
- [x] Search xác nhận chỉ entity `Order` và historical migration cũ còn `DiscountAmount`/`FinalAmount`.

## 12. Phase 6 — Controlled deploy Release B lên server

Migration này có `DROP COLUMN`, vì vậy không để merge `main` tự động deploy trước khi backup và maintenance sẵn sàng.

### 12.1 Chuẩn bị image contract

- [ ] Push branch Release B.
- [ ] Chạy `workflow_dispatch` trên branch để build/push image nhưng không deploy.
- [ ] Ghi `RELEASE_B_SHA` đủ 40 ký tự.
- [ ] Xác nhận `RELEASE_A_SHA` vẫn còn trên GHCR để rollback.
- [ ] Chọn maintenance window.
- [ ] Tạm dừng/không merge thay đổi production khác.

### 12.2 Pre-deploy production checks

```bash
cd /opt/dodo
pwd
docker compose ps
grep '^IMAGE_TAG=' .env
curl --fail --show-error http://127.0.0.1:8085/health
docker compose exec -T postgres sh -c \
  'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -c "SELECT * FROM \"__EFMigrationsHistory\" ORDER BY \"MigrationId\";"'
```

- [ ] Image đang chạy là Release A hoặc SHA mới hơn tương thích.
- [ ] Không được contract nếu image đang chạy là bản trước Release A.
- [ ] Health đang tốt trước maintenance.
- [ ] Không có migration contract trong history trước khi deploy.

### 12.3 Backup trước contract

- [ ] Chạy backup S3 mới và xác nhận object:

```bash
cd /opt/dodo
/opt/dodo/backup-postgres.sh
```

- [ ] Tạo/ghi nhận dump local trước deploy; `deploy.sh` cũng sẽ tạo `predeploy-*.dump`.
- [ ] Xác nhận dung lượng disk đủ cho dump và image mới bằng `df -h` và `docker system df`.
- [ ] Xác nhận restore test gần nhất thành công.

### 12.4 Apply contract qua controlled startup

Ứng dụng chạy `Database.Migrate()` khi startup. Vì vậy lệnh deploy dưới đây cũng là thời điểm migration drop column được apply.

```bash
cd /opt/dodo
docker compose stop webapi
./deploy.sh RELEASE_B_40_CHARACTER_SHA
docker compose ps
docker compose logs --since=20m --tail=400 webapi
curl --fail --show-error http://127.0.0.1:8085/health
```

- [ ] `deploy.sh` tạo `predeploy-*.dump` lớn hơn 0.
- [ ] Startup log cho thấy migration `RemoveBillingOrderDiscountColumns` thành công.
- [ ] WebAPI healthy.
- [ ] Không có retry migration vô hạn.

### 12.5 Verify schema và migration history

```bash
cd /opt/dodo
docker compose exec -T postgres sh -c \
  'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB"' <<'SQL'
SELECT "MigrationId", "ProductVersion"
FROM "__EFMigrationsHistory"
ORDER BY "MigrationId";

SELECT column_name
FROM information_schema.columns
WHERE table_schema = 'public'
  AND table_name = 'BillingOrders'
  AND column_name IN ('DiscountAmount', 'FinalAmount');
SQL
```

- [ ] Migration contract có trong history.
- [ ] Query column trả 0 dòng.
- [ ] Bảng `Orders` vẫn còn hai column của nghiệp vụ Order nếu thiết kế cũ yêu cầu.
- [ ] `BillingOrders.TotalAmount` và dữ liệu thanh toán vẫn còn.

### 12.6 Smoke test sau contract

- [ ] Health local và HTTPS đều 200.
- [ ] Login/refresh token hoạt động.
- [ ] Tenant billing list/detail hoạt động.
- [ ] System billing list/detail và sort hoạt động.
- [ ] Dashboard/analytics hoạt động.
- [ ] Tạo BillingOrder sandbox/control thành công.
- [ ] Payment URL/QR amount đúng.
- [ ] Callback/webhook thành công.
- [ ] Payment success consumer và module subscription hoạt động.
- [ ] Không có lỗi column missing trong log.
- [ ] Theo dõi CPU/RAM/disk và restart count sau deploy.

### 12.7 Hoàn tất Release B

- [ ] Merge Release B vào `main` sau khi controlled deploy thành công.
- [ ] Workflow main deploy SHA mới; migration đã apply nên startup chỉ verify history.
- [ ] Theo dõi production ít nhất 24 giờ.
- [ ] Cập nhật API contract/frontend documentation.
- [ ] Đánh dấu toàn bộ task code/database hoàn thành.

## 12A. Reset PostgreSQL test database và deploy một release — phương án đã chọn

Mục này thay thế Phase 3–6 cho lần triển khai hiện tại. Thao tác xóa volume sẽ xóa toàn bộ dữ liệu PostgreSQL trên server. Chỉ thực hiện vì người dùng đã xác nhận database chỉ có dữ liệu test và chưa có dữ liệu khách hàng.

### 12A.1 Chuẩn bị image mới trước maintenance

1. Push feature branch chứa code và migration cuối cùng.
2. Mở GitHub Actions → `Build and deploy production` → `Run workflow`.
3. Chọn đúng feature branch.
4. Job build/test/push image phải xanh; job deploy phải skip vì ref không phải `main`.
5. Copy đủ SHA 40 ký tự của image.

- [ ] CI restore/build/test xanh.
- [ ] Image SHA mới có trên GHCR.
- [ ] Ghi SHA hiện tại và SHA mới vào Nhật ký.
- [ ] Không dùng tag `latest`.
- [ ] Frontend tương thích với response chỉ còn `totalAmount` đã sẵn sàng.

### 12A.2 Kiểm tra đúng server và backup lần cuối

SSH vào VPS rồi chạy:

```bash
cd /opt/dodo
pwd
docker compose ps
grep '^IMAGE_TAG=' .env
curl --fail --show-error http://127.0.0.1:8085/health
```

- [ ] `pwd` chính xác là `/opt/dodo`.
- [ ] Đã ghi lại image SHA cũ.
- [ ] PostgreSQL hiện healthy.

Mặc dù dữ liệu chỉ là test, vẫn tạo một backup cuối để có bằng chứng và đường kiểm tra:

```bash
cd /opt/dodo
/opt/dodo/backup-postgres.sh
tail -n 20 /opt/dodo/backups/backup.log
```

- [ ] Script báo backup thành công.
- [ ] Object mới xuất hiện trên S3 và có kích thước lớn hơn 0.

### 12A.3 Xác định chính xác PostgreSQL volume

Không đoán tên volume và không dùng `docker compose down -v`.

```bash
cd /opt/dodo
POSTGRES_VOLUME="$(docker inspect dodo-postgres --format '{{range .Mounts}}{{if eq .Destination "/var/lib/postgresql/data"}}{{.Name}}{{end}}{{end}}')"
echo "PostgreSQL volume: ${POSTGRES_VOLUME}"
test -n "${POSTGRES_VOLUME}"

case "${POSTGRES_VOLUME}" in
  postgres_data|*_postgres_data) ;;
  *) echo "Tên volume không đúng phạm vi postgres_data; dừng lại." >&2; exit 1 ;;
esac

docker volume inspect "${POSTGRES_VOLUME}"
```

- [ ] Biến không rỗng.
- [ ] Volume mount đúng destination `/var/lib/postgresql/data` của container `dodo-postgres`.
- [ ] Tên volume kết thúc bằng `_postgres_data` hoặc đúng `postgres_data`.
- [ ] Không phải volume Redis hoặc RabbitMQ.

### 12A.4 Dừng API và xóa riêng PostgreSQL volume

Giữ nguyên cùng phiên SSH để biến `POSTGRES_VOLUME` không bị mất:

```bash
cd /opt/dodo
docker compose stop webapi
docker compose stop postgres
docker compose rm -f postgres
docker volume rm "${POSTGRES_VOLUME}"

if docker volume inspect "${POSTGRES_VOLUME}" >/dev/null 2>&1; then
  echo "Volume PostgreSQL vẫn còn; dừng lại." >&2
  exit 1
else
  echo "Đã xóa đúng PostgreSQL volume: ${POSTGRES_VOLUME}"
fi

docker compose ps
```

- [ ] Chỉ container PostgreSQL bị remove; Redis/RabbitMQ volume không bị xóa.
- [ ] Lệnh `docker volume rm` in đúng tên đã xác minh.
- [ ] PostgreSQL volume cũ không còn tồn tại.

Thao tác trên không thể hoàn tác nếu không restore backup. Tuyệt đối không thay bằng:

```text
docker compose down -v
docker volume prune
docker system prune --volumes
```

### 12A.5 Vô hiệu hóa rollback về image cũ và deploy image mới

Sau reset, image cũ không tương thích với schema mới vì còn query hai cột đã bỏ. Đặt tag tạm về bootstrap để `deploy.sh` không tự rollback về image cũ khi health thất bại.

Thay `NEW_40_CHARACTER_SHA` bằng SHA thật:

```bash
cd /opt/dodo
NEW_TAG="NEW_40_CHARACTER_SHA"

if [[ ! "${NEW_TAG}" =~ ^[0-9a-f]{40}$ ]]; then
  echo "NEW_TAG phải là Git SHA đủ 40 ký tự." >&2
  exit 1
fi

sed -i 's/^IMAGE_TAG=.*/IMAGE_TAG=bootstrap-not-deployed/' .env
grep '^IMAGE_TAG=' .env

./deploy.sh "${NEW_TAG}"
```

`deploy.sh` sẽ:

1. Tạo lại PostgreSQL container và named volume trống.
2. Chờ PostgreSQL/Redis/RabbitMQ healthy.
3. Pull image SHA mới.
4. Start WebAPI.
5. WebAPI chạy `Database.Migrate()` và áp dụng toàn bộ migration, gồm `RemoveBillingOrderDiscountColumns`.
6. Seed role/module hệ thống.
7. Chờ `/health`.

- [ ] `deploy.sh` báo thành công.
- [ ] Nếu deploy lỗi, không chạy image cũ; giữ API dừng và đọc log để sửa image mới.
- [ ] Không restore database test cũ trừ khi quyết định hủy toàn bộ thay đổi.

### 12A.6 Verify container, migration và schema mới

```bash
cd /opt/dodo
docker compose ps
grep '^IMAGE_TAG=' .env
docker compose logs --since=20m --tail=400 webapi
curl --fail --show-error http://127.0.0.1:8085/health

docker compose exec -T postgres sh -c \
  'psql -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d "$POSTGRES_DB"' <<'SQL'
SELECT "MigrationId", "ProductVersion"
FROM "__EFMigrationsHistory"
ORDER BY "MigrationId";

SELECT column_name
FROM information_schema.columns
WHERE table_schema = 'public'
  AND table_name = 'BillingOrders'
  AND column_name IN ('DiscountAmount', 'FinalAmount');

SELECT COUNT(*) AS billing_order_count FROM "BillingOrders";
SQL
```

- [ ] `IMAGE_TAG` là SHA mới đủ 40 ký tự.
- [ ] Tất cả service Up/healthy.
- [ ] Migration `RemoveBillingOrderDiscountColumns` có trong history.
- [ ] Query column trả 0 dòng.
- [ ] `BillingOrders` đang rỗng như kỳ vọng sau reset.
- [ ] `Orders` vẫn còn discount fields nếu nghiệp vụ Order vẫn yêu cầu.
- [ ] Log không có migration retry, missing column hoặc exception.
- [ ] Health local trả HTTP 200.
- [ ] Health HTTPS domain trả HTTP 200.

### 12A.7 Smoke test chức năng sau reset

- [ ] Tạo lại tài khoản/tenant test cần thiết.
- [ ] Login và refresh token thành công.
- [ ] Chọn 1 module: `TotalAmount` bằng giá dòng module.
- [ ] Chọn 2–4 module: `TotalAmount = SUM(LineTotal)`, không discount.
- [ ] Mua module giữa kỳ vẫn áp dụng proration.
- [ ] API billing chỉ trả `totalAmount`.
- [ ] VNPay/SePay amount bằng `TotalAmount`.
- [ ] Callback/webhook thành công.
- [ ] Email không có dòng giảm giá.
- [ ] Payment success kích hoạt subscription.
- [ ] System billing và analytics không lỗi.

### 12A.8 Hoàn tất deploy

- [ ] Merge feature branch vào `main` sau khi manual deploy SHA thành công.
- [ ] Theo dõi workflow main; migration đã apply nên không chạy lại thay đổi schema.
- [ ] Xác nhận SHA main mới healthy.
- [ ] Theo dõi log/payment trong ít nhất 24 giờ.
- [ ] Không rollback về image trước thay đổi discount.
- [ ] Nếu cần rollback chức năng, tạo fix-forward trên model mới hoặc reset database test lại bằng image tương thích.

## 13. Rollback và xử lý sự cố

### 13.1 Release A lỗi trước khi drop column

- Có thể rollback về SHA production trước Release A.
- Database vẫn còn hai cột.
- Nếu backfill đã chạy, old image vẫn đọc được vì discount đã về 0 và final bằng total.
- Old image có thể tạo discount mới; trước lần deploy tiếp theo phải audit/backfill lại.

Checklist:

- [ ] Ghi SHA lỗi và SHA rollback.
- [ ] Đọc log đầu tiên gây lỗi, không restart lặp vô hạn.
- [ ] Không restore database chỉ vì code lỗi.

### 13.2 Release B lỗi sau khi drop column

Chỉ rollback image về `RELEASE_A_SHA` hoặc một SHA mới hơn đã được kiểm tra không map hai field:

```bash
cd /opt/dodo
./deploy.sh RELEASE_A_40_CHARACTER_SHA
docker compose ps
docker compose logs --since=15m --tail=300 webapi
curl --fail --show-error http://127.0.0.1:8085/health
```

- [ ] Không rollback về image trước Release A vì image đó query các column đã bị drop.
- [ ] Không chạy migration `Down()` trên production chỉ để rollback image.
- [ ] Không tự `pg_restore` vào production đang có write mới.
- [ ] Nếu Release A cũng không chạy, dừng writer, đánh giá migration history và dump trước deploy rồi mới quyết định restore.
- [ ] Khi restore thật sự cần thiết, ghi rõ RPO và chấp nhận mất các write sau thời điểm dump.

### 13.3 Lệnh bị cấm trên production

Không chạy:

```text
docker compose down -v
docker volume prune
docker system prune --volumes
rm -rf /var/lib/docker/volumes/...
dotnet ef database drop
DROP DATABASE
pg_restore --clean vào database production đang hoạt động
```

## 14. Definition of Done

### Code

- [x] BillingOrder chỉ có `TotalAmount` cho số tiền hóa đơn.
- [x] Không còn discount theo số module.
- [x] Payment VNPay và SePay dùng `TotalAmount`.
- [x] Email không hiển thị discount.
- [x] Tenant/system API chỉ trả `TotalAmount`.
- [x] Analytics dùng `TotalAmount`.
- [x] Entity `Order` khác không bị ảnh hưởng.

### Test

- [x] Restore/build/test local thành công.
- [x] Test 1–4 module chứng minh không discount.
- [ ] Payment sandbox end-to-end thành công.
- [x] Analytics test và contract admin DTO thành công.
- [ ] Migration đã test trên database restore/staging.
- [ ] Release A đã được test trên schema sau contract để đảm bảo rollback.

### Database

- [ ] Dữ liệu lịch sử đã backfill theo quyết định nghiệp vụ.
- [ ] Backup S3 và local dump có trước mỗi thay đổi production.
- [ ] `BillingOrders.DiscountAmount` đã bị drop.
- [ ] `BillingOrders.FinalAmount` đã bị drop.
- [ ] `BillingOrders.TotalAmount` còn nguyên và đúng payable amount.
- [ ] `Orders` không bị thay đổi ngoài phạm vi.

### Production

- [ ] CI/CD xanh và image chạy bằng SHA 40 ký tự.
- [ ] Tất cả container Up/healthy.
- [ ] Health local và HTTPS 200.
- [ ] Không có lỗi migration/column/payment trong log.
- [ ] Có SHA Release A để rollback Release B.
- [ ] Đã theo dõi production tối thiểu 24 giờ sau contract.

## 15. Nhật ký thực hiện

| Thời gian UTC | Phase | Người thực hiện | Commit/Image SHA | Backup/S3 object | Kết quả/Ghi chú |
|---|---|---|---|---|---|
|  | Baseline |  |  |  |  |
|  | Release A build |  |  | N/A |  |
|  | Backfill |  |  |  |  |
|  | Release A deploy |  |  |  |  |
|  | Release A observation |  |  | N/A |  |
|  | Release B migration test |  |  | Restore test |  |
|  | Release B deploy |  |  |  |  |
|  | Post-deploy observation |  |  | N/A |  |

## 16. Ghi chú phát hiện trong repository tại thời điểm lập plan

- Production startup hiện chạy `db.Database.Migrate()` trong `WebApplicationExtensions.InitializeDatabase`.
- Workflow `.github/workflows/ci-cd.yml` tự deploy khi ref là `main`; `workflow_dispatch` trên feature branch có thể dùng để build image SHA mà không chạy job deploy.
- `docker-compose.yml` dùng PostgreSQL named volume `postgres_data`; tuyệt đối không dùng `down -v`.
- Migration hiện có tại thời điểm lập plan gồm `InitialPostgreSql` và `AddPunchIdempotency`; phải tạo migration mới, không sửa migration đã apply.
- `BillingOrderService` hiện áp dụng discount 10%/15%/20% cho 2/3/4 module.
- Mua module bổ sung hiện có proration theo ngày; đây là quyết định độc lập với việc bỏ discount.
- `AuthService` và recurring renewal job gọi chung `CreateModuleBillingOrderAsync`; sau khi sửa service, cả đăng ký mới, gia hạn và mua thêm đều tự dùng công thức không discount.
