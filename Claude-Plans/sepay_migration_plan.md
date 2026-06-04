# Kế hoạch Migration: VNPay Sandbox → SePay (Tiền thật)

**Ngày tạo**: 2026-06-03  
**Tác giả**: Truong Hoang Long  
**Trạng thái**: Đang lên kế hoạch

---

## 1. Tổng quan

### Tại sao chuyển sang SePay?

| Tiêu chí | VNPay Sandbox | SePay (Production) |
|---|---|---|
| Môi trường | Test (tiền ảo) | Thật (tiền thật) |
| Tích hợp | Redirect tới cổng VNPay | Webhook + QR Code chuyển khoản |
| Phí giao dịch | Không | Theo hợp đồng SePay |
| Xác nhận | Callback URL | Webhook realtime |
| Yêu cầu | Tài khoản sandbox | Tài khoản doanh nghiệp/cá nhân SePay |

### SePay hoạt động như thế nào?

```
Frontend                  Backend                    SePay
   |                         |                          |
   |-- POST /payment/create ->|                          |
   |                         |-- Tạo đơn hàng + mã QR ->|
   |<-- Trả về QR Code / STK--|                          |
   |                         |                          |
   | (User chuyển khoản qua app ngân hàng)               |
   |                         |<-- Webhook notification --|
   |                         |   (xác nhận thanh toán)  |
   |                         |-- Xử lý đơn hàng         |
   |<-- Realtime update ------|                          |
```

SePay không redirect như VNPay — thay vào đó trả về thông tin chuyển khoản (STK, số tài khoản, nội dung chuyển khoản) và confirm qua **webhook**.

---

## 2. Điều kiện tiên quyết

### 2.1 Đăng ký SePay

- [ ] Truy cập [https://sepay.vn](https://sepay.vn) và đăng ký tài khoản
- [ ] Liên kết tài khoản ngân hàng (Vietcombank, Techcombank, MB Bank, v.v.)
- [ ] Lấy thông tin:
  - `API_KEY` — dùng để gọi SePay API
  - `BANK_ACCOUNT_NUMBER` — số tài khoản ngân hàng nhận tiền
  - `BANK_ACCOUNT_NAME` — tên chủ tài khoản
  - `BANK_CODE` — mã ngân hàng (VCB, TCB, MB, v.v.)
  - `WEBHOOK_SECRET` — để verify webhook từ SePay

### 2.2 Cấu hình Webhook trên SePay Dashboard

- [ ] Vào SePay Dashboard → Webhook
- [ ] Nhập URL: `https://<your-domain>/api/payment/webhook/sepay`
- [ ] Chọn event: `payment.success`
- [ ] Lưu `WEBHOOK_SECRET`

### 2.3 Cấu hình server (nếu test local)

- [ ] Dùng **ngrok** hoặc **localtunnel** để expose localhost cho SePay webhook:
  ```bash
  ngrok http 5000
  # Copy URL: https://xxxx.ngrok.io
  # Dùng: https://xxxx.ngrok.io/api/payment/webhook/sepay
  ```

---

## 3. Thay đổi cần làm

### 3.1 Cấu hình (appsettings.json)

**Hiện tại:**
```json
"Payment": {
  "Mode": "Sandbox",
  "Gateway": "VNPay",
  "FrontendUrl": "http://localhost:3000",
  "VNPay": {
    "TmnCode": "7BD2ILMB",
    "HashSecret": "BCJSBUREQ9UN22CDL8HHYOLXG30X3VI1",
    "BaseUrl": "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html",
    "CallbackUrl": "/api/payment/callback/vnpay"
  }
}
```

**Sau khi đổi:**
```json
"Payment": {
  "Mode": "Production",
  "Gateway": "SePay",
  "FrontendUrl": "https://<your-frontend-domain>",
  "SePay": {
    "ApiKey": "<SEPAY_API_KEY>",
    "WebhookSecret": "<SEPAY_WEBHOOK_SECRET>",
    "BankAccountNumber": "<YOUR_BANK_ACCOUNT>",
    "BankAccountName": "<YOUR_BANK_NAME>",
    "BankCode": "<VCB|TCB|MB|...>",
    "WebhookUrl": "/api/payment/webhook/sepay",
    "PaymentContentPrefix": "DODO"
  }
}
```

> **Bảo mật**: Không commit `ApiKey` và `WebhookSecret` lên git. Dùng **User Secrets** hoặc **Environment Variables** trên production.

---

### 3.2 Các file cần tạo mới

#### A. `SMEFLOWSystem.Application/Interfaces/IServices/ISePayService.cs`
```csharp
public interface ISePayService
{
    // Tạo thông tin thanh toán (QR + nội dung chuyển khoản)
    Task<SePayPaymentInfo> CreatePaymentInfoAsync(Guid orderId);
    
    // Xác minh và xử lý webhook từ SePay
    Task<bool> ProcessWebhookAsync(SePayWebhookPayload payload, string rawBody, string signature);
}
```

#### B. `SMEFLOWSystem.Application/DTOs/Payment/SePayDtos.cs`
```csharp
// Thông tin trả về cho frontend
public record SePayPaymentInfo(
    string TransferContent,      // Nội dung CK: "DODO <OrderId>"
    string BankAccountNumber,    // STK ngân hàng
    string BankAccountName,      // Tên chủ TK
    string BankCode,             // Mã ngân hàng
    decimal Amount,              // Số tiền
    string QrCodeUrl             // URL ảnh QR (tùy chọn - SePay cung cấp)
);

// Payload webhook SePay gửi về
public record SePayWebhookPayload(
    string Gateway,              // "sepay"
    string TransactionDate,
    string AccountNumber,
    string SubAccount,
    decimal TransferAmount,
    decimal Accumulated,
    string Code,                 // Mã giao dịch SePay
    string TransactionContent,   // Nội dung chuyển khoản
    string ReferenceCode,
    string Description,
    string TransferType          // "in" (nhận tiền)
);
```

#### C. `SMEFLOWSystem.Application/Services/SePayService.cs`
Logic chính:
- `CreatePaymentInfoAsync`: Tạo nội dung chuyển khoản duy nhất từ OrderId, trả về thông tin STK
- `ProcessWebhookAsync`:
  1. Verify chữ ký webhook (HMAC-SHA256 với `WebhookSecret`)
  2. Kiểm tra `TransferType == "in"` (chỉ xử lý tiền đến)
  3. Parse `TransactionContent` để tìm OrderId
  4. Kiểm tra số tiền khớp với đơn hàng
  5. Idempotency: bỏ qua nếu đã xử lý
  6. Lưu `PaymentTransaction` record
  7. Publish `PaymentSucceededEvent` (giữ nguyên event cũ)

---

### 3.3 Các file cần chỉnh sửa

#### A. `PaymentController.cs` — Thêm endpoint mới

```csharp
// THÊM: Endpoint tạo thông tin thanh toán SePay
[HttpPost("create")]  // Đổi response shape
public async Task<IActionResult> CreatePayment([FromBody] CreatePaymentRequest request)
{
    // Thay vì trả về URL redirect VNPay
    // → Trả về: STK, tên TK, nội dung CK, QR URL
}

// THÊM: Webhook từ SePay
[HttpPost("webhook/sepay")]
[AllowAnonymous]  // SePay gọi từ ngoài, không có JWT
public async Task<IActionResult> SePayWebhook([FromBody] SePayWebhookPayload payload)
{
    // Verify signature từ header
    // Xử lý thanh toán
    // Trả về 200 OK để SePay không retry
}

// GIỮ NGUYÊN (để không break): VNPay callback cũ
// [HttpGet("callback/vnpay")] — có thể xóa sau khi confirm SePay hoạt động

// XÓA (môi trường production):
// [HttpPost("simulate/vnpay/success")] — chỉ dùng dev
```

#### B. `SMEFLOWSystem.Application/Extensions/DependencyInjection.cs`

```csharp
// Thêm:
services.AddScoped<ISePayService, SePayService>();

// Giữ tạm (hoặc xóa nếu bỏ hẳn VNPay):
// services.AddSingleton<IVnpay, Vnpay>();
// services.AddScoped<IPaymentService, PaymentService>();
```

#### C. `SMEFLOWSystem.WebAPI/Extensions/DependencyInjection.cs`

Cập nhật validation config:
```csharp
// Thêm validation cho SePay config
var sePayConfig = builder.Configuration.GetSection("Payment:SePay");
if (string.IsNullOrEmpty(sePayConfig["ApiKey"]))
    throw new InvalidOperationException("SePay ApiKey is required");
```

---

### 3.4 Logic nội dung chuyển khoản (quan trọng)

SePay match payment dựa vào **nội dung chuyển khoản**. Cần tạo nội dung duy nhất:

```
Format: "DODO {OrderNumber}"
Ví dụ: "DODO BO-2024-001"
```

- `OrderNumber` phải là field có sẵn trong `BillingOrder.BillingOrderNumber`
- Trong webhook handler: parse `TransactionContent` để extract `BillingOrderNumber`
- Match với DB để tìm đúng đơn hàng

---

## 4. Luồng xử lý mới (SePay)

```
1. User chọn gói → POST /api/billingorder/buy-additional-modules
   → Tạo BillingOrder (status: Pending)

2. Frontend gọi POST /api/payment/create { orderId }
   → Backend trả về:
      {
        "transferContent": "DODO BO-2024-001",
        "bankAccountNumber": "1234567890",
        "bankAccountName": "CONG TY ABC",
        "bankCode": "VCB",
        "amount": 500000,
        "qrCodeUrl": "https://..."  // optional
      }

3. Frontend hiển thị thông tin chuyển khoản / QR code
   → User mở app ngân hàng và chuyển tiền

4. SePay nhận tiền → gọi POST /api/payment/webhook/sepay
   {
     "transferAmount": 500000,
     "transactionContent": "DODO BO-2024-001",
     "transferType": "in",
     "code": "SEPAY_TXN_001",
     ...
   }

5. Backend xử lý webhook:
   a. Verify signature
   b. Find BillingOrder by content "DODO BO-2024-001"
   c. Validate amount
   d. Lưu PaymentTransaction
   e. Publish PaymentSucceededEvent → RabbitMQ (không đổi)

6. PaymentSucceededConsumer xử lý (không đổi):
   → Activate subscription
   → Update tenant status → Active

7. Frontend nhận realtime update qua SignalR (nếu đã tích hợp)
```

---

## 5. Thứ tự thực hiện

### Phase 1: Setup & Config (1-2 giờ)
- [ ] Đăng ký tài khoản SePay
- [ ] Lấy credentials (ApiKey, WebhookSecret, thông tin ngân hàng)
- [ ] Cập nhật `appsettings.json` (KHÔNG commit credentials)
- [ ] Setup ngrok cho dev/test webhook

### Phase 2: Core Implementation (4-6 giờ)
- [ ] Tạo `SePayDtos.cs` (PaymentInfo + WebhookPayload)
- [ ] Tạo `ISePayService.cs` interface
- [ ] Implement `SePayService.cs`:
  - [ ] `CreatePaymentInfoAsync`
  - [ ] `ProcessWebhookAsync` với signature verification
- [ ] Đăng ký DI

### Phase 3: Controller & API (2-3 giờ)
- [ ] Cập nhật `POST /api/payment/create` → trả về SePay payment info
- [ ] Thêm `POST /api/payment/webhook/sepay`
- [ ] Kiểm tra `[AllowAnonymous]` cho webhook endpoint
- [ ] Bỏ simulate endpoint (hoặc giữ cho dev)

### Phase 4: Testing (2-4 giờ)
- [ ] Test `CreatePaymentInfoAsync` trả đúng format
- [ ] Dùng ngrok để test webhook thật từ SePay
- [ ] Chuyển khoản thật (số tiền nhỏ, ví dụ 10,000 VND) để verify luồng
- [ ] Kiểm tra subscription được activate sau thanh toán
- [ ] Kiểm tra idempotency (webhook đến 2 lần không xử lý 2 lần)

### Phase 5: Cleanup (1 giờ)
- [ ] Xóa VNPay code (hoặc feature flag nếu muốn giữ fallback)
- [ ] Xóa VNPAY.NET project (nếu không còn dùng)
- [ ] Cập nhật Swagger docs
- [ ] Thêm secrets vào CI/CD pipeline (GitHub Actions Secrets / Azure Key Vault)

---

## 6. Rủi ro & lưu ý

### Rủi ro bảo mật
- **Không verify webhook signature** → attacker có thể fake thanh toán. Phải verify HMAC-SHA256.
- **Không check `transferType == "in"`** → có thể xử lý cả giao dịch ra.
- **Expose ApiKey** → không commit lên git, dùng environment variable.

### Rủi ro logic
- **Trùng nội dung chuyển khoản**: Nếu `BillingOrderNumber` không đủ unique, dùng thêm timestamp hoặc random suffix.
- **User nhập sai nội dung CK**: SePay sẽ không match → cần có cơ chế manual confirm (admin panel).
- **Webhook đến trước khi BillingOrder tồn tại**: Implement retry queue hoặc pending webhook table.

### Lưu ý frontend
- Frontend cần **polling** hoặc **SignalR** để biết khi nào thanh toán thành công (vì không redirect như VNPay).
- Hiển thị countdown timer (ví dụ 15 phút) cho user chuyển khoản.
- Hiển thị QR code nếu SePay cung cấp.

---

## 7. Cấu trúc file sau khi hoàn thành

```
SMEFLOWSystem.Application/
├── Interfaces/IServices/
│   └── ISePayService.cs          [MỚI]
├── Services/
│   └── SePayService.cs           [MỚI]
├── DTOs/Payment/
│   └── SePayDtos.cs              [MỚI]

SMEFLOWSystem.WebAPI/
├── Controllers/
│   └── PaymentController.cs      [SỬA - thêm webhook endpoint]
└── appsettings.json              [SỬA - thêm SePay config]

VNPAY.NET/                        [XÓA - sau khi xác nhận SePay ok]
```

---

## 8. Câu hỏi cần xác nhận

1. **Ngân hàng nào** sẽ dùng để nhận tiền qua SePay? (Vietcombank, Techcombank, MB...)
2. **Frontend** đã có SignalR để nhận realtime update chưa, hay cần polling?
3. **Nếu user nhập sai nội dung CK** — muốn có admin panel để manual confirm không?
4. **Có muốn giữ VNPay** như fallback (feature flag) hay xóa hẳn?
5. **Domain production** là gì để cấu hình webhook URL trên SePay Dashboard?
