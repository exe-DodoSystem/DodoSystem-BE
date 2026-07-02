# Danh sách vấn đề & Giải pháp

---

## Vấn đề 1: Hết hạn module → không đăng nhập → không thể thanh toán

**Mô tả:** Khi tất cả module của công ty hết hạn, `AuthService.LoginAsync()` chặn login hoàn toàn. Người dùng bị kẹt — không vào được để thanh toán gia hạn.

**Root cause (code thực tế):**

1. **`AuthService.cs` dòng 198-209** — Block login khi tất cả module hết hạn:
```csharp
// File: SMEFLOWSystem.Application/Services/AuthService.cs (dòng 198-210)
if (!isSystemAdmin)
{
    var subs = await _moduleSubscriptionRepo.GetByTenantIgnoreTenantAsync(tenant.Id);
    var now = DateTime.UtcNow;
    var hasValidModule = subs.Any(s => !s.IsDeleted
        && (string.Equals(s.Status, StatusEnum.ModuleActive, ...)
            || string.Equals(s.Status, StatusEnum.ModuleTrial, ...))
        && s.EndDate > now);
    if (!hasValidModule)
        throw new Exception("Hết hạn tất cả module, thanh toán để tiếp tục");
        // ← ĐÂY LÀ VẤN ĐỀ: throw Exception → user không login được
}
```

2. **`ModuleAccessMiddleware.cs`** — Đã kiểm tra, middleware CHỈ block các route module cụ thể:
```csharp
// File: SMEFLOWSystem.WebAPI/Middleware/ModuleAccessMiddleware.cs (dòng 21-37)
private static readonly (string Prefix, string ModuleCode)[] ProtectedPrefixes =
{
    ("/api/hr", "HR"),
    ("/api/v1/attendance", "ATTENDANCE"),
    ("/api/payrolls", "PAYROLL"),
    ("/api/customers", "SALES"),
    ("/api/orders", "SALES"),
    ("/api/tasks", "TASKS"),
    ("/api/projects", "TASKS"),
    ("/api/dashboard", "DASHBOARD"),
};
// ✅ /api/payment, /api/billing, /api/auth KHÔNG nằm trong danh sách
// → Middleware KHÔNG block các route thanh toán (đã đúng)
```

3. **Kết luận:** Vấn đề CHỈ nằm ở `AuthService.LoginAsync()` — cần cho phép login khi module hết hạn, và truyền trạng thái `isExpired` cho FE.

---

**Giải pháp:**

### Bước 1 — Cho phép login dù module hết hạn

**File sửa:** `SMEFLOWSystem.Application/Services/AuthService.cs`

**TRƯỚC (dòng 198-210):**
```csharp
if (!isSystemAdmin)
{
    var subs = await _moduleSubscriptionRepo.GetByTenantIgnoreTenantAsync(tenant.Id);
    var now = DateTime.UtcNow;
    var hasValidModule = subs.Any(s => !s.IsDeleted
        && (string.Equals(s.Status, StatusEnum.ModuleActive, StringComparison.OrdinalIgnoreCase)
            || string.Equals(s.Status, StatusEnum.ModuleTrial, StringComparison.OrdinalIgnoreCase))
        && s.EndDate > now);
    if (!hasValidModule)
        throw new Exception("Hết hạn tất cả module, thanh toán để tiếp tục");
}
```

**SAU — bỏ throw, lưu trạng thái expired:**
```csharp
bool isAllModulesExpired = false;
if (!isSystemAdmin)
{
    var subs = await _moduleSubscriptionRepo.GetByTenantIgnoreTenantAsync(tenant.Id);
    var now = DateTime.UtcNow;
    var hasValidModule = subs.Any(s => !s.IsDeleted
        && (string.Equals(s.Status, StatusEnum.ModuleActive, StringComparison.OrdinalIgnoreCase)
            || string.Equals(s.Status, StatusEnum.ModuleTrial, StringComparison.OrdinalIgnoreCase))
        && s.EndDate > now);
    isAllModulesExpired = !hasValidModule;
    // KHÔNG throw — cho phép login để user vào trang gia hạn
}
```

### Bước 2 — Thêm claim `isExpired` vào JWT token

**File sửa:** `SMEFLOWSystem.Application/Helpers/AuthHelper.cs`

Thêm parameter `isExpired` vào `GenerateJwtToken()`:
```csharp
// TRƯỚC:
public static string GenerateJwtToken(User user, IConfiguration config)

// SAU:
public static string GenerateJwtToken(User user, IConfiguration config, bool isExpired = false)
{
    var claims = new List<Claim>
    {
        new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
        new Claim(ClaimTypes.Email, user.Email),
        new Claim(ClaimTypes.Name, user.FullName),
        new Claim("tenantId", user.TenantId.ToString()),
        new Claim("isExpired", isExpired.ToString().ToLower())  // ← MỚI
    };
    // ... giữ nguyên phần còn lại ...
}
```

### Bước 3 — Truyền `isExpired` khi gọi GenerateJwtToken

**File sửa:** `AuthService.cs` — trong `LoginAsync()`, dòng 217:
```csharp
// TRƯỚC:
var token = AuthHelper.GenerateJwtToken(user, _config);

// SAU:
var token = AuthHelper.GenerateJwtToken(user, _config, isAllModulesExpired);
```

### Bước 4 — Thêm field `IsExpired` vào `LoginUserDto`

**File sửa:** `SMEFLOWSystem.Application/DTOs/UserDtos/LoginUserDto.cs`
```csharp
public bool IsExpired { get; set; } = false;
```

**File sửa:** `AuthService.cs` — set giá trị:
```csharp
var userDto = _mapper.Map<LoginUserDto>(user);
userDto.Token = token;
userDto.IsExpired = isAllModulesExpired;  // ← MỚI
```

### Bước 5 — (Không cần) Middleware đã đúng

`ModuleAccessMiddleware` đã KHÔNG protect `/api/payment`, `/api/billing`, `/api/billingorder`.
Chỉ block: `/api/hr`, `/api/v1/attendance`, `/api/payrolls`, `/api/dashboard`.
→ **Không cần sửa middleware.**

### Bước 6 — Frontend xử lý `isExpired`

Khi FE nhận response login với `isExpired: true`:
1. Lưu token (user vẫn authenticated)
2. Redirect sang trang `/renew` hiển thị:
   - Thông báo: "Tất cả module đã hết hạn"
   - Danh sách module + ngày hết hạn
   - Nút "Gia hạn ngay" → gọi `POST /api/billingorder/renewal` → hiển thị QR thanh toán SePay
3. Sau khi thanh toán thành công → redirect về dashboard

---

### Tóm tắt file cần sửa

| # | File | Hành động | Mô tả |
|---|------|-----------|-------|
| 1 | `Application/Services/AuthService.cs` | **SỬA** | Bỏ throw khi expired, lưu biến `isAllModulesExpired` |
| 2 | `Application/Helpers/AuthHelper.cs` | **SỬA** | Thêm param `isExpired`, thêm claim vào JWT |
| 3 | `Application/DTOs/UserDtos/LoginUserDto.cs` | **SỬA** | Thêm field `IsExpired` |
| 4 | Frontend | **SỬA** | Xử lý redirect khi `isExpired = true` |

---

## Vấn đề 2: Chuyển thanh toán thật — VNPay Sandbox → SePay

**Mô tả:** Hiện tại hệ thống dùng VNPay Sandbox (tiền ảo). Cần chuyển sang **SePay** — cổng thanh toán qua chuyển khoản ngân hàng thật, xác nhận tự động qua webhook.

**Tại sao chọn SePay thay vì VNPay Production?**
- VNPay production yêu cầu hợp đồng doanh nghiệp + phí giao dịch
- SePay miễn phí, tích hợp đơn giản, dùng webhook xác nhận thanh toán qua chuyển khoản
- Hỗ trợ QR Code VietQR (user quét bằng app ngân hàng bất kỳ)

**Root cause hiện tại:**
- `PaymentService.CreatePaymentUrlAsync()` chỉ hỗ trợ VNPay → trả về redirect URL
- `PaymentController` chỉ có callback GET từ VNPay, chưa có webhook POST cho SePay
- `appsettings.json` → `Payment:Gateway = "VNPay"`, chưa có config SePay

---

### Luồng thanh toán mới (SePay)

```
1. User chọn gói → POST /api/billingorder/buy-additional-modules
   → Tạo BillingOrder (status: Pending)

2. Frontend gọi POST /api/payment/create?orderId=xxx
   → Backend trả về JSON:
     {
       "transferContent": "DODO BO-2026-001",
       "bankAccountNumber": "1234567890",
       "bankAccountName": "CONG TY DODO",
       "bankCode": "MB",
       "amount": 500000,
       "qrCodeUrl": "https://vietqr.app/img?acc=1234567890&bank=MB&amount=500000&des=DODO%20BO-2026-001&template=compact",
       "orderId": "guid-xxx"
     }

3. Frontend hiển thị QR Code + thông tin chuyển khoản
   → User mở app ngân hàng, quét QR hoặc CK thủ công

4. SePay phát hiện tiền vào → gọi POST /api/payment/webhook/sepay
   Payload:
   {
     "id": 12345,
     "gateway": "MBBank",
     "transactionDate": "2026-07-01 13:00:00",
     "accountNumber": "1234567890",
     "transferAmount": 500000,
     "accumulated": 15000000,
     "code": "TXN123456",
     "content": "DODO BO-2026-001",
     "referenceCode": "FT26182xxxxx",
     "description": "...",
     "transferType": "in"
   }

5. Backend xử lý webhook:
   a. Verify API Key từ header (SePay gửi Authorization header)
   b. Chỉ xử lý transferType == "in" (tiền vào)
   c. Parse content → tìm "DODO BO-2026-001" → match BillingOrder
   d. Validate số tiền khớp
   e. Idempotency: check GatewayTransactionId đã xử lý chưa
   f. Lưu PaymentTransaction record
   g. Publish PaymentSucceededEvent qua Outbox (KHÔNG ĐỔI gì)

6. PaymentSucceededConsumer xử lý (KHÔNG ĐỔI):
   → Activate subscription
   → Update tenant status → Active
```

---

### Bước 1 — Tạo DTOs cho SePay

**File mới:** `SMEFLOWSystem.Application/DTOs/PaymentDtos/SePayDtos.cs`

```csharp
namespace SMEFLOWSystem.Application.DTOs.PaymentDtos;

/// <summary>Response trả về FE khi tạo payment SePay</summary>
public record SePayPaymentInfoDto(
    string TransferContent,      // "DODO BO-2026-001"
    string BankAccountNumber,    // STK ngân hàng nhận tiền
    string BankAccountName,      // Tên chủ tài khoản
    string BankCode,             // Mã ngân hàng (MB, VCB, TCB, ...)
    decimal Amount,              // Số tiền cần chuyển khoản
    string QrCodeUrl,            // URL ảnh QR từ vietqr.app
    Guid OrderId                 // ID đơn hàng
);

/// <summary>Payload SePay gửi qua webhook khi có giao dịch</summary>
public record SePayWebhookPayload(
    int Id,                      // Transaction ID trên SePay
    string Gateway,              // Tên ngân hàng (vd: "MBBank")
    string TransactionDate,      // "2026-07-01 13:00:00"
    string AccountNumber,        // STK nhận tiền
    string? SubAccount,          // Tài khoản phụ (nullable)
    decimal TransferAmount,      // Số tiền giao dịch
    decimal Accumulated,         // Số dư tích lũy
    string Code,                 // Mã giao dịch trên SePay
    string Content,              // Nội dung CK — dùng để match order
    string? ReferenceCode,       // Mã tham chiếu ngân hàng
    string Description,          // Mô tả
    string TransferType          // "in" = tiền vào, "out" = tiền ra
);
```

---

### Bước 2 — Sửa `PaymentService.cs` — Thêm nhánh SePay

**File sửa:** `SMEFLOWSystem.Application/Services/PaymentService.cs`

**2a. Thêm constant:**
```csharp
private const string GatewaySePay = "SePay";
```

**2b. Sửa `CreatePaymentUrlAsync()` — thêm nhánh SePay:**
```csharp
public async Task<string> CreatePaymentUrlAsync(Guid orderId, string? clientIp = null)
{
    var billingOrder = await _billingOrderRepo.GetByIdIgnoreTenantAsync(orderId);
    if (billingOrder == null) throw new Exception("Không tìm thấy đơn thanh toán");

    // ... giữ nguyên validation status ...

    var gateway = _config["Payment:Gateway"] ?? throw new Exception("Missing config");
    if (gateway == "VNPay")
    {
        return CreateVNPayUrl(billingOrder, clientIp);
    }
    if (gateway == "SePay")
    {
        return CreateSePayPaymentInfo(billingOrder);  // ← MỚI
    }
    throw new Exception($"Unsupported payment gateway: {gateway}");
}
```

**2c. Thêm method `CreateSePayPaymentInfo()` — tạo QR Code + thông tin CK:**
```csharp
private string CreateSePayPaymentInfo(BillingOrder order)
{
    var bankAccount = _config["Payment:SePay:BankAccountNumber"]
        ?? throw new Exception("Missing config: Payment:SePay:BankAccountNumber");
    var bankName = _config["Payment:SePay:BankAccountName"]
        ?? throw new Exception("Missing config: Payment:SePay:BankAccountName");
    var bankCode = _config["Payment:SePay:BankCode"]
        ?? throw new Exception("Missing config: Payment:SePay:BankCode");
    var prefix = _config["Payment:SePay:PaymentContentPrefix"] ?? "DODO";

    var discount = order.DiscountAmount ?? 0m;
    var payable = order.TotalAmount - discount;
    if (payable <= 0m)
        throw new Exception("Đơn thanh toán không hợp lệ (số tiền phải > 0)");

    // Nội dung CK: "DODO BO-2026-001" (dùng BillingOrderNumber)
    var transferContent = $"{prefix} {order.BillingOrderNumber}";

    // QR Code URL qua vietqr.app (miễn phí, không cần API key)
    var encodedContent = Uri.EscapeDataString(transferContent);
    var qrCodeUrl = $"https://vietqr.app/img?acc={bankAccount}&bank={bankCode}"
        + $"&amount={payable:0}&des={encodedContent}&template=compact";

    var paymentInfo = new SePayPaymentInfoDto(
        TransferContent: transferContent,
        BankAccountNumber: bankAccount,
        BankAccountName: bankName,
        BankCode: bankCode,
        Amount: payable,
        QrCodeUrl: qrCodeUrl,
        OrderId: order.Id
    );

    return JsonConvert.SerializeObject(paymentInfo);
}
```

**2d. Thêm method `ProcessSePayWebhookAsync()` — xử lý webhook từ SePay:**
```csharp
public async Task<bool> ProcessSePayWebhookAsync(SePayWebhookPayload payload)
{
    // 1. Chỉ xử lý tiền VÀO
    if (payload.TransferType != "in")
        return false;

    // 2. Parse nội dung CK để tìm BillingOrderNumber
    //    Format: "DODO BO-2026-001" hoặc chứa "BO-2026-001"
    var prefix = _config["Payment:SePay:PaymentContentPrefix"] ?? "DODO";
    var content = payload.Content?.Trim() ?? "";

    // Tìm BillingOrderNumber trong nội dung CK
    // Nội dung CK có thể bị ngân hàng thêm prefix/suffix
    var orderNumberPattern = @"BO-\d{4}-\d+";
    var match = Regex.Match(content, orderNumberPattern);
    if (!match.Success)
        return false;

    var billingOrderNumber = match.Value;

    // 3. Tìm đơn hàng
    var order = await _billingOrderRepo.GetByOrderNumberIgnoreTenantAsync(billingOrderNumber);
    if (order == null) return false;

    // 4. Validate đơn hàng đang ở trạng thái chờ thanh toán
    if (!string.Equals(order.PaymentStatus, StatusEnum.PaymentPending, StringComparison.OrdinalIgnoreCase))
        return false;

    // 5. Validate số tiền
    var discount = order.DiscountAmount ?? 0m;
    var expectedPayable = order.TotalAmount - discount;
    if (payload.TransferAmount < expectedPayable)
        return false;  // Số tiền CK ít hơn cần thanh toán

    var gatewayTransactionId = payload.Code;

    // 6. Xử lý trong transaction (giữ nguyên pattern cũ)
    await _transaction.ExecuteAsync(async () =>
    {
        // Idempotency check
        var existing = await _paymentTransactionRepo.GetByGatewayTransactionIdAsync(
            gateway: GatewaySePay,
            gatewayTransactionId: gatewayTransactionId,
            ignoreTenantFilter: true);
        if (existing != null) return;

        var freshOrder = await _billingOrderRepo.GetByIdIgnoreTenantAsync(order.Id);
        if (freshOrder == null) return;

        // Tạo PaymentTransaction record
        var paymentTransaction = new PaymentTransaction
        {
            Id = Guid.NewGuid(),
            TenantId = freshOrder.TenantId,
            BillingOrderId = freshOrder.Id,
            Gateway = GatewaySePay,
            GatewayTransactionId = gatewayTransactionId,
            GatewayResponseCode = "00",  // SePay webhook = success
            Amount = payload.TransferAmount,
            Status = "Success",
            RawData = JsonConvert.SerializeObject(payload),
            CreatedAt = DateTime.UtcNow,
            ProcessedAt = DateTime.UtcNow
        };

        await _paymentTransactionRepo.AddAsync(paymentTransaction);

        // Cập nhật trạng thái đơn hàng
        if (BillingStateMachine.CanSetPaymentToPaid(freshOrder.PaymentStatus)
            && BillingStateMachine.CanSetOrderToCompleted(freshOrder.Status))
        {
            freshOrder.PaymentStatus = StatusEnum.PaymentPaid;
            freshOrder.Status = StatusEnum.OrderCompleted;

            // Publish PaymentSucceededEvent (GIỐNG HỆT logic VNPay cũ)
            var paymentSucceededEvent = new PaymentSucceededEvent
            {
                BillingOrderId = freshOrder.Id,
                TenantId = freshOrder.TenantId,
                Gateway = GatewaySePay,
                GatewayTransactionId = gatewayTransactionId,
                Amount = payload.TransferAmount,
                Currency = "VND",
                CorrelationId = freshOrder.Id.ToString()
            };

            var exchange = _config["RabbitMQ:Exchange"] ?? "smeflow.exchange";
            var routingKey = _config["RabbitMQ:RoutingKeys:PaymentSucceeded"]
                ?? "payment.succeeded";

            var outboxMessage = new OutboxMessage
            {
                Id = Guid.NewGuid(),
                TenantId = freshOrder.TenantId,
                EventId = paymentSucceededEvent.EventId,
                EventType = nameof(PaymentSucceededEvent),
                Exchange = exchange,
                RoutingKey = routingKey,
                Payload = JsonConvert.SerializeObject(paymentSucceededEvent),
                CorrelationId = paymentSucceededEvent.CorrelationId,
                Status = StatusEnum.OutboxPending,
                RetryCount = 0,
                OccurredOnUtc = DateTime.UtcNow,
                NextAttemptOnUtc = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            };

            await _outboxMessageRepo.AddAsync(outboxMessage);
        }

        await _billingOrderRepo.UpdateIgnoreTenantAsync(freshOrder);
    });

    return true;
}
```

---

### Bước 3 — Sửa interfaces

**File sửa:** `IPaymentService.cs` — thêm:
```csharp
Task<bool> ProcessSePayWebhookAsync(SePayWebhookPayload payload);
```

**File sửa:** `IBillingService.cs` — thêm:
```csharp
Task<bool> ProcessSePayWebhookAsync(SePayWebhookPayload payload);
```

**File sửa:** `BillingService.cs` — thêm delegate:
```csharp
public Task<bool> ProcessSePayWebhookAsync(SePayWebhookPayload payload)
    => _paymentService.ProcessSePayWebhookAsync(payload);
```

---

### Bước 4 — Thêm webhook endpoint trong `PaymentController.cs`

**File sửa:** `SMEFLOWSystem.WebAPI/Controllers/PaymentController.cs`

```csharp
/// <summary>Webhook từ SePay — xác nhận thanh toán chuyển khoản</summary>
[HttpPost("webhook/sepay")]
[AllowAnonymous]  // SePay gọi từ ngoài, không có JWT
public async Task<IActionResult> SePayWebhook([FromBody] SePayWebhookPayload payload)
{
    // Verify API Key — SePay gửi trong header Authorization
    var expectedApiKey = _config["Payment:SePay:ApiKey"];
    if (!string.IsNullOrEmpty(expectedApiKey))
    {
        var authHeader = Request.Headers["Authorization"].ToString();
        // SePay gửi: "Sepay <API_KEY>" hoặc chỉ "<API_KEY>"
        var providedKey = authHeader
            .Replace("Sepay ", "", StringComparison.OrdinalIgnoreCase)
            .Replace("Bearer ", "", StringComparison.OrdinalIgnoreCase)
            .Trim();

        if (providedKey != expectedApiKey)
            return Unauthorized(new { success = false, message = "Invalid API Key" });
    }

    var result = await _billingService.ProcessSePayWebhookAsync(payload);
    // SePay yêu cầu trả về 200 + {"success": true} để không retry
    return Ok(new { success = result });
}
```

**Giữ nguyên** các endpoint VNPay cũ (`callback/vnpay`, `simulate/vnpay/success`) để fallback.

---

### Bước 5 — Thêm repository method tìm order theo BillingOrderNumber

**File sửa:** `IBillingOrderRepository.cs` — thêm:
```csharp
Task<BillingOrder?> GetByOrderNumberIgnoreTenantAsync(string billingOrderNumber);
```

**File sửa:** `BillingOrderRepository.cs` — implement:
```csharp
public async Task<BillingOrder?> GetByOrderNumberIgnoreTenantAsync(string billingOrderNumber)
{
    return await _context.BillingOrders
        .IgnoreQueryFilters()
        .FirstOrDefaultAsync(x => x.BillingOrderNumber == billingOrderNumber);
}
```

---

### Bước 6 — Cấu hình `appsettings.json`

```json
"Payment": {
    "Mode": "Production",
    "Gateway": "SePay",
    "FrontendUrl": "https://<frontend-domain>",
    "SePay": {
        "ApiKey": "SET-IN-USER-SECRETS",
        "BankAccountNumber": "<SỐ TÀI KHOẢN NGÂN HÀNG>",
        "BankAccountName": "<TÊN CHỦ TÀI KHOẢN>",
        "BankCode": "<MB|VCB|TCB|...>",
        "PaymentContentPrefix": "DODO"
    },
    "VNPay": {
        "TmnCode": "7BD2ILMB",
        "HashSecret": "BCJSBUREQ9UN22CDL8HHYOLXG30X3VI1",
        "BaseUrl": "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html",
        "CallbackUrl": "/api/payment/callback/vnpay"
    }
}
```

> ⚠️ **Bảo mật:** `ApiKey` PHẢI lưu trong User Secrets hoặc Environment Variables, KHÔNG commit vào git.

---

### Bước 7 — Validate config trong `DependencyInjection.cs` (WebAPI)

```csharp
// Thêm vào ValidateConfiguration()
if (paymentGateway == "SePay")
{
    _ = GetRequiredConfig(configuration, "Payment:SePay:ApiKey");
    _ = GetRequiredConfig(configuration, "Payment:SePay:BankAccountNumber");
    _ = GetRequiredConfig(configuration, "Payment:SePay:BankAccountName");
    _ = GetRequiredConfig(configuration, "Payment:SePay:BankCode");
}
```

---

### Bước 8 — Cập nhật email thanh toán trong `BillingService.cs`

Trong `EnqueuePaymentLinkEmailAsync()`, khi Gateway = SePay, thay link VNPay bằng thông tin CK:

```csharp
// TRƯỚC: <a href='{paymentUrl}'>THANH TOÁN</a>
// SAU (khi SePay):
var gateway = _config["Payment:Gateway"];
if (gateway == "SePay")
{
    // paymentUrl lúc này là JSON string chứa SePayPaymentInfoDto
    var paymentInfo = JsonConvert.DeserializeObject<SePayPaymentInfoDto>(paymentUrl);
    // Thay phần link thanh toán bằng thông tin chuyển khoản + ảnh QR
    // bankAccountNumber, bankAccountName, transferContent, qrCodeUrl
}
```

---

### Bước 9 — Setup SePay Dashboard

1. Đăng ký tài khoản tại [sepay.vn](https://sepay.vn)
2. Liên kết tài khoản ngân hàng
3. Vào menu **WebHooks** → Thêm mới:
   - URL: `https://<your-domain>/api/payment/webhook/sepay`
   - Event: Tiền vào (Inbound)
   - Chứng thực: API Key
4. Lấy API Key → lưu vào User Secrets
5. Test bằng **Giả lập giao dịch** trên SePay Dashboard

---

### Bước 10 — Verify end-to-end

1. Đổi `Payment:Gateway` = `"SePay"` trong config
2. Tạo BillingOrder → gọi `POST /api/payment/create` → verify response JSON có QR URL
3. Mở QR URL trong trình duyệt → thấy ảnh QR Code
4. Dùng SePay Dashboard → Giả lập giao dịch → verify webhook được gọi
5. Kiểm tra DB: `PaymentTransaction` được tạo, `BillingOrder.PaymentStatus = "Paid"`
6. Kiểm tra subscription được activate
7. Test idempotency: webhook gọi 2 lần → chỉ xử lý 1 lần

---

### Tóm tắt file cần sửa

| # | File | Hành động | Mô tả |
|---|------|-----------|-------|
| 1 | `Application/DTOs/PaymentDtos/SePayDtos.cs` | **TẠO MỚI** | DTOs cho payment info + webhook payload |
| 2 | `Application/Services/PaymentService.cs` | **SỬA** | Thêm SePay branch + webhook handler |
| 3 | `Application/Interfaces/IServices/IPaymentService.cs` | **SỬA** | Thêm `ProcessSePayWebhookAsync` |
| 4 | `Application/Interfaces/IServices/IBillingService.cs` | **SỬA** | Thêm `ProcessSePayWebhookAsync` |
| 5 | `Application/Services/BillingService.cs` | **SỬA** | Delegate webhook + sửa email |
| 6 | `WebAPI/Controllers/PaymentController.cs` | **SỬA** | Thêm webhook endpoint |
| 7 | `Application/Interfaces/IRepositories/IBillingOrderRepository.cs` | **SỬA** | Thêm `GetByOrderNumberIgnoreTenantAsync` |
| 8 | `Infrastructure/Repositories/BillingOrderRepository.cs` | **SỬA** | Implement method trên |
| 9 | `WebAPI/appsettings.json` | **SỬA** | Thêm SePay config |
| 10 | `WebAPI/Extensions/DependencyInjection.cs` | **SỬA** | Validate SePay config |

---

## Vấn đề 3: Chuyển trạng thái "Đã Nghỉ Việc" → xóa nhân viên khỏi hệ thống

**Mô tả:** Trong `HrEmployeeService`, khi Admin/HR xóa nhân viên (`DELETE /api/hr/employees/{id}`), code set cả `Status = Resigned` VÀ `IsDeleted = true` cùng lúc. Điều này gộp 2 hành động nghiệp vụ khác nhau ("đánh dấu nghỉ việc" vs "xóa khỏi hệ thống") làm một.

**Kiểm tra code thực tế:**

✅ **`UpdateAsync()` đã ĐÚNG** — khi update status sang Resigned, KHÔNG set `IsDeleted`:
```csharp
// File: HrEmployeeService.cs dòng 203-212 — ĐÃ ĐÚNG
if (string.Equals(emp.Status, StatusEnum.EmployeeResigned, ...))
{
    if (!request.ResignationDate.HasValue)
        throw new ArgumentException("ResignationDate is required when Status=Resigned");
    emp.ResignationDate = request.ResignationDate.Value;
}
// → Chỉ cập nhật status + ResignationDate, KHÔNG gọi IsDeleted = true ✅
```

⚠️ **`DeleteAsync()` CÓ VẤN ĐỀ** — gộp "nghỉ việc" + "xóa" + "vô hiệu hóa User":
```csharp
// File: HrEmployeeService.cs dòng 221-237 — VẤN ĐỀ Ở ĐÂY
public async Task DeleteAsync(Guid id)
{
    var emp = await _employeeRepo.GetByIdAsync(id) ?? throw ...;
    
    emp.Status = StatusEnum.EmployeeResigned;     // 1. Set trạng thái nghỉ việc
    emp.ResignationDate ??= DateOnly.FromDateTime(DateTime.UtcNow);
    emp.IsDeleted = true;                          // 2. Soft delete (ẩn khỏi query)
    emp.UpdatedAt = DateTime.UtcNow;
    await _employeeRepo.UpdateAsync(emp);

    if (emp.UserId.HasValue)
    {
        await _userRepo.SoftDeleteUserAndFreeEmailAsync(emp.UserId.Value);  // 3. Vô hiệu hóa User account
    }
}
```

**Vấn đề nghiệp vụ:**
- Khi gọi `DELETE /api/hr/employees/{id}`, nhân viên bị ẩn hoàn toàn (global query filter `IsDeleted`)
- HR Manager không thể xem lịch sử nhân viên đã nghỉ trong danh sách
- Không có cách "đánh dấu nghỉ việc" mà vẫn giữ nhân viên hiển thị trong danh sách
- User account bị vô hiệu hóa ngay lập tức — nhưng có khi cần giữ account cho nhân viên chuyển sang trạng thái khác

---

**Giải pháp:**

### Bước 1 — Tách `DeleteAsync()` thành 2 hành động riêng biệt

**File sửa:** `SMEFLOWSystem.Application/Services/HrEmployeeService.cs`

**1a. Sửa `DeleteAsync()` — chỉ soft delete, KHÔNG đổi status:**
```csharp
public async Task DeleteAsync(Guid id)
{
    _currentUser.EnsureHrAccess();
    var emp = await _employeeRepo.GetByIdAsync(id) 
        ?? throw new KeyNotFoundException("Employee not found");
    await _hrAuth.EnsureEmployeeAccessAsync(emp);

    // Chỉ soft delete — KHÔNG tự động đổi status sang Resigned
    emp.IsDeleted = true;
    emp.UpdatedAt = DateTime.UtcNow;
    await _employeeRepo.UpdateAsync(emp);

    // Vô hiệu hóa user account nếu có
    if (emp.UserId.HasValue)
    {
        await _userRepo.SoftDeleteUserAndFreeEmailAsync(emp.UserId.Value);
    }
}
```

**1b. `UpdateAsync()` đã đúng — giữ nguyên:**
Khi `status = Resigned` → chỉ cập nhật `Status` + `ResignationDate`, KHÔNG set `IsDeleted`.
Nhân viên vẫn hiển thị trong danh sách (với filter `includeResigned`).

### Bước 2 — Đảm bảo `GetPagedAsync()` hỗ trợ filter nhân viên đã nghỉ

**Kiểm tra:** `GetPagedAsync()` đã có param `includeResigned` ✅

```csharp
// File: HrEmployeeService.cs dòng 96
includeResigned: query.IncludeResigned,
```

**FE cần làm:**
- Mặc định: `includeResigned = false` → chỉ hiện nhân viên đang làm
- HR Manager bật toggle "Hiện nhân viên đã nghỉ" → `includeResigned = true`
- Nhân viên status `Resigned` hiển thị với tag/badge "Đã nghỉ việc" (màu xám)

### Bước 3 — Thêm endpoint "Khôi phục nhân viên" (tùy chọn)

**File mới hoặc sửa:** `HrEmployeeService.cs` + `HrEmployeesController.cs`

Nếu Admin muốn khôi phục nhân viên đã xóa nhầm:
```csharp
// HrEmployeeService.cs — thêm method mới
public async Task<EmployeeDto> RestoreAsync(Guid id)
{
    _currentUser.EnsureHrAccess();
    // Cần query IgnoreQueryFilters để tìm employee đã bị IsDeleted
    var emp = await _employeeRepo.GetByIdIncludeDeletedAsync(id)
        ?? throw new KeyNotFoundException("Employee not found");
    
    emp.IsDeleted = false;
    emp.Status = StatusEnum.EmployeeActive;  // hoặc giữ Resigned tùy nghiệp vụ
    emp.UpdatedAt = DateTime.UtcNow;
    await _employeeRepo.UpdateAsync(emp);
    
    return _mapper.Map<EmployeeDto>(emp);
}
```

```csharp
// HrEmployeesController.cs — thêm endpoint
/// <summary>[TenantAdmin, HRManager] Khôi phục nhân viên đã xóa</summary>
[HttpPatch("{id:guid}/restore")]
[Authorize(Policy = PolicyNames.AdminOrHr)]
public async Task<ActionResult<EmployeeDto>> Restore([FromRoute] Guid id)
{
    return Ok(await _service.RestoreAsync(id));
}
```

### Bước 4 — Kiểm tra dữ liệu đã bị xóa nhầm

Nếu DB đã có nhân viên bị `IsDeleted = true` mà thực ra chỉ cần đánh dấu nghỉ việc:
```sql
-- Kiểm tra trước
SELECT Id, FullName, Status, IsDeleted, ResignationDate 
FROM Employees 
WHERE IsDeleted = 1 AND Status = 'Resigned';

-- Nếu cần khôi phục (chỉ chạy nếu chắc chắn)
UPDATE Employees 
SET IsDeleted = 0 
WHERE Status = 'Resigned' AND IsDeleted = 1 
  AND ResignationDate IS NOT NULL;
```

### Bước 5 — Xem xét vô hiệu hóa User account khi nghỉ việc

Hiện tại, `SoftDeleteUserAndFreeEmailAsync()` chỉ được gọi trong `DeleteAsync()` (xóa).
Cân nhắc: Khi `UpdateAsync()` set status = Resigned, có nên vô hiệu hóa User account không?

```csharp
// Tùy chọn — thêm vào UpdateAsync() nếu cần:
if (string.Equals(emp.Status, StatusEnum.EmployeeResigned, ...)
    && emp.UserId.HasValue)
{
    // Vô hiệu hóa user → nhân viên không login được nữa
    var user = await _userRepo.GetByIdAsync(emp.UserId.Value);
    if (user != null)
    {
        user.IsActive = false;
        await _userRepo.UpdateUserAsync(user);
    }
}
```

> ⚠️ **Quyết định nghiệp vụ:** Có nên tự động vô hiệu hóa account khi đánh dấu nghỉ việc?
> - **Có:** Nhân viên không login được nữa → an toàn hơn
> - **Không:** Giữ account active để nhân viên có thể xem lương cuối, phiếu lương tháng cuối

---

### Tóm tắt file cần sửa

| # | File | Hành động | Mô tả |
|---|------|-----------|-------|
| 1 | `Application/Services/HrEmployeeService.cs` | **SỬA** | Tách logic DeleteAsync, bỏ set Status=Resigned |
| 2 | `Application/Services/HrEmployeeService.cs` | **THÊM** | Method RestoreAsync (tùy chọn) |
| 3 | `Application/Interfaces/IServices/IHrEmployeeService.cs` | **SỬA** | Thêm RestoreAsync vào interface |
| 4 | `WebAPI/Controllers/Hr/HrEmployeesController.cs` | **THÊM** | Endpoint PATCH restore (tùy chọn) |
| 5 | `Infrastructure/Repositories/EmployeeRepository.cs` | **THÊM** | GetByIdIncludeDeletedAsync |

---

## Tóm tắt ưu tiên

| # | Vấn đề | Mức độ | Effort |
|---|--------|--------|--------|
| 2 | Chuyển VNPay → SePay (thanh toán thật) | 🔴 Cao | Trung bình — 10 file, ~4-6h code + test |
| 1 | Hết hạn → không login được | 🔴 Cao | Thấp — sửa AuthService + AuthHelper + DTO |
| 3 | Delete employee gộp "nghỉ việc" + "xóa" | 🟠 Trung bình | Thấp — tách logic trong DeleteAsync |

