# Danh sách vấn đề & Giải pháp (Backend & Frontend)

---

## Vấn đề 1: Hết hạn module → không đăng nhập → không thể thanh toán

**Mô tả:** Khi tất cả module của công ty hết hạn, `AuthService.LoginAsync()` chặn login hoàn toàn. Người dùng bị kẹt — không vào được để thanh toán gia hạn.

### Phía Backend đã sửa:

1. **`AuthService.cs`** — Bỏ việc ném ngoại lệ (`throw Exception`) khi hết hạn module. Thay vào đó, tính toán trạng thái `isAllModulesExpired` và:
   - Gán `isExpired = isAllModulesExpired` vào response của API `/api/auth/login`.
   - Truyền trạng thái hết hạn vào để tạo Token: `AuthHelper.GenerateJwtToken(user, _config, isAllModulesExpired)`.
2. **`AuthHelper.cs`** — Thêm claim `"isExpired"` (giá trị `"true"` hoặc `"false"`) vào JWT token.
3. **`LoginUserDto.cs`** — Bổ sung thuộc tính `IsExpired` (kiểu `bool`, mặc định `false`).

---

### Hướng dẫn tích hợp cho Frontend (FE):

#### 1. Xử lý phản hồi từ API Đăng nhập
Khi gửi request đăng nhập tới `POST /api/auth/login`, Backend sẽ trả về DTO có định dạng:
```json
{
  "fullName": "Nguyen Van A",
  "phone": "0987654321",
  "isActive": true,
  "isDeleted": false,
  "token": "eyJhbGciOi...",
  "isExpired": true, // <-- TRƯỜNG MỚI ĐỂ NHẬN BIẾT HẾT HẠN
  "refreshToken": "...",
  "tenantName": "Cong Ty TNHH Dodo"
}
```
**Nhiệm vụ của FE:**
- Lưu `token` và `refreshToken` vào bộ nhớ (`localStorage` / `cookies`) như bình thường để duy trì phiên đăng nhập.
- Kiểm tra thuộc tính `isExpired`:
  - Nếu `isExpired === true`: **KHÔNG** chuyển hướng user vào trang Dashboard chính. Ngay lập tức chuyển hướng sang trang gia hạn dịch vụ `/renew` (hoặc `/settings/billing`).
  - Nếu `isExpired === false`: Chuyển hướng vào Dashboard bình thường.

#### 2. Cài đặt Route Guard / Navigation Middleware
Để tránh trường hợp user cố tình gõ URL để vào các trang tính năng khi công ty đã hết hạn dịch vụ:
- Giải mã (decode) JWT token để kiểm tra claim `"isExpired"`.
- Nếu claim `"isExpired"` có giá trị `"true"`, chặn tất cả các route khác (như Quản lý nhân sự, Chấm công, Bảng lương,...) và tự động chuyển hướng người dùng về trang `/renew`.

#### 3. Xây dựng giao diện trang gia hạn `/renew`
- Khi user truy cập `/renew`, gọi API `GET /api/billingorder/me/billing-orders` để lấy danh sách hóa đơn của doanh nghiệp.
- Hệ thống đã có background job chạy hàng ngày tự động tạo hóa đơn gia hạn ở trạng thái chờ thanh toán (`paymentStatus === "Pending"`).
- Tìm hóa đơn `Pending` này trong danh sách, hiển thị thông tin các module cần gia hạn, tổng tiền và nút **"Thanh toán ngay"** (Sẽ kích hoạt luồng tích hợp SePay dưới đây).

---

## Vấn đề 2: Chuyển thanh toán thật — VNPay Sandbox → SePay

**Mô tả:** Hệ thống chuyển từ VNPay Sandbox sang **SePay** (thanh toán qua chuyển khoản ngân hàng thật, quét mã VietQR và xác nhận tự động qua webhook).

### Phía Backend đã sửa:
1. **`PaymentService.cs`** — Đọc cấu hình `Payment:Gateway`.
   - Nếu là `"VNPay"`: Trả về URL thanh toán VNPay Sandbox.
   - Nếu là `"SePay"`: Trả về chuỗi JSON chứa thông tin chuyển khoản ngân hàng và mã QR Code được khởi tạo từ `vietqr.app`.
2. **`PaymentController.cs`** — Thêm webhook `POST /api/payment/webhook/sepay` có tích hợp bảo mật kiểm tra API Key và xác thực chữ ký HMAC-SHA256 để khớp lệnh tự động. Bổ sung thêm API giả lập `/api/payment/simulate/sepay/success` để phục vụ dev test.
3. **`BillingOrderRepository.cs`** — Thêm phương thức truy vấn không lọc Tenant phục vụ cho Webhook.
4. **`BillingService.cs`** — Tự động định dạng email hóa đơn gửi tới khách hàng: Nếu dùng SePay sẽ hiển thị chi tiết số tài khoản ngân hàng và ảnh QR Code động thay vì nút chuyển hướng.

---

### Hướng dẫn tích hợp cho Frontend (FE):

#### 1. Xử lý phản hồi khi bấm nút "Thanh toán"
Khi người dùng bấm nút thanh toán đơn hàng (hoặc gia hạn), FE thực hiện gọi:
`POST /api/payment/create?orderId={orderId}`

**LƯU Ý QUAN TRỌNG VỀ RESPONSE:**
Nội dung phản hồi trả về là một **chuỗi văn bản (string)**. FE cần kiểm tra định dạng của chuỗi này:
- **Trường hợp 1 (VNPay):** Chuỗi là một URL thông thường (bắt đầu bằng `http` hoặc `https`).
  - **Hành động:** Chuyển hướng người dùng trực tiếp tới URL đó (mở tab mới hoặc redirect trình duyệt).
- **Trường hợp 2 (SePay):** Chuỗi là một chuỗi JSON (bắt đầu bằng ký tự `{`).
  - **Hành động:** Thực hiện parse chuỗi này thành đối tượng JSON (`JSON.parse(response)`) theo cấu trúc `SePayPaymentInfoDto` để tự vẽ giao diện chuyển khoản trên Web App.

Cấu trúc đối tượng `SePayPaymentInfoDto` ở FE:
```typescript
interface SePayPaymentInfoDto {
  transferContent: string;      // Nội dung CK bắt buộc: e.g. "DODO SUB-123456"
  bankAccountNumber: string;    // Số tài khoản nhận tiền
  bankAccountName: string;      // Tên chủ tài khoản nhận
  bankCode: string;             // Mã ngân hàng (e.g. "MB", "VCB", "TCB", ...)
  amount: number;               // Số tiền cần chuyển (VND)
  qrCodeUrl: string;            // URL ảnh QR động từ VietQR.app
  orderId: string;              // ID đơn hàng
}
```

#### 2. Hiển thị UI chuyển khoản SePay
Vẽ một Modal hoặc vùng hiển thị thông tin chuyển khoản trực quan bao gồm:
1. **Ảnh QR Code:** Thẻ `<img src={qrCodeUrl} alt="VietQR" />` cho phép khách hàng mở app ngân hàng quét mã nhanh (mã này đã chứa sẵn số tiền và nội dung chuyển khoản).
2. **Thông tin ngân hàng** kèm nút **"Copy" (Sao chép)** bên cạnh mỗi dòng:
   - Ngân hàng nhận: `{bankCode}`
   - Số tài khoản: `{bankAccountNumber}` 📋
   - Chủ tài khoản: `{bankAccountName}`
   - Số tiền cần chuyển: `{amount.toLocaleString('vi-VN')} VND` 📋
   - Nội dung chuyển khoản: `{transferContent}` 📋 (Bắt buộc làm nổi bật bằng chữ màu đỏ, in đậm).
3. **Cảnh báo nghiệp vụ:** *"Vui lòng nhập chính xác nội dung chuyển khoản phía trên để hệ thống tự động kích hoạt dịch vụ sau 1-3 phút."*

#### 3. Polling kiểm tra trạng thái thanh toán thành công
Vì SePay xác nhận qua webhook bất đồng bộ và không có redirect trực tiếp về web khi thành công như VNPay:
- FE thiết lập cơ chế chạy ngầm (ví dụ: `setInterval` cứ **5 giây một lần**) gọi API `GET /api/billingorder/me/billing-orders`.
- Kiểm tra đơn hàng có `orderId` tương ứng. Nếu trạng thái chuyển từ `"Pending"` sang `"Paid"` (hoặc `"Success"`):
  - Dừng polling.
  - Hiển thị thông báo: *"Thanh toán thành công! Dịch vụ đã được kích hoạt."*
  - Yêu cầu người dùng đăng nhập lại hoặc tự động làm mới JWT token (gọi refresh token) để cập nhật claim `isExpired` về `false`, sau đó chuyển hướng về trang Dashboard.

---

## Vấn đề 3: Chuyển trạng thái "Đã Nghỉ Việc" → xóa nhân viên khỏi hệ thống

**Mô tả:** Backend đã tách biệt rõ hai thao tác:
- **Đánh dấu nghỉ việc (Resigned):** Chỉ đổi trạng thái (`Status = Resigned`) để lưu vết lịch sử nhân viên. Nhân sự vẫn tồn tại trong DB, không bị soft delete.
- **Xóa nhân viên (Delete):** Thực hiện soft-delete thực sự (`IsDeleted = true`), ẩn hoàn toàn khỏi danh sách chung và giải phóng email tài khoản.

### Phía Backend đã sửa:
1. **`HrEmployeeService.cs`:**
   - Sửa `DeleteAsync()`: Chỉ set `IsDeleted = true` và giải phóng email user. Không thay đổi status thành `Resigned`.
   - Sửa `UpdateAsync()`: Khi cập nhật nhân viên sang `Status = Resigned`, bắt buộc phải có `ResignationDate` (nếu không có sẽ báo lỗi `400 Bad Request`). Hệ thống sẽ tự động khóa tài khoản user liên kết (`IsActive = false`). Nếu đổi ngược lại từ `Resigned` về trạng thái làm việc, tài khoản user sẽ được kích hoạt lại (`IsActive = true`).
   - Thêm `RestoreAsync()`: Hỗ trợ khôi phục nhân viên bị xóa nhầm, kích hoạt lại user và khôi phục email cũ nếu có thể.
2. **`HrEmployeesController.cs`:** Bổ sung endpoint khôi phục nhân viên: `PATCH /api/hr/employees/{id}/restore`.

---

### Hướng dẫn tích hợp cho Frontend (FE):

#### 1. Giao diện Danh sách Nhân sự (`/hr/employees`)
- **Bộ lọc (Filter):** Thêm một nút toggle hoặc checkbox mang tên **"Hiển thị nhân sự đã nghỉ việc"**.
  - Khi checkbox này được tích chọn, FE truyền tham số query `IncludeResigned=true` vào API:
    `GET /api/hr/employees?IncludeResigned=true&pageNumber=1&pageSize=20`.
- **Trực quan hóa:** Các nhân sự có `status === "Resigned"` hiển thị với dòng chữ mờ hoặc badge màu xám **"Đã nghỉ việc"**.
- **Khóa hành động:** Chặn các nút chỉnh sửa bảng công, bảng lương đối với nhân viên đã nghỉ việc (chỉ cho phép xem chi tiết).

#### 2. Giao diện Cập nhật thông tin nhân viên (Edit/Update Form)
- Trong dropdown chọn Trạng thái (Status) của nhân viên:
  - Nếu người dùng chọn trạng thái là `"Resigned"`:
    - Hiển thị thêm trường nhập ngày **"Ngày nghỉ việc"** (`resignationDate` dưới dạng date picker, định dạng gửi lên là `YYYY-MM-DD`).
    - Bắt buộc người dùng phải chọn ngày này (validate ở client để tránh lỗi `400 Bad Request` từ API).
  - Nếu chọn các trạng thái khác (ví dụ: `Working`, `Probation`), ẩn trường nhập ngày nghỉ việc đi.

#### 3. Xử lý chức năng Xóa và Khôi phục
- **Xóa (Delete):** Nút xóa (biểu tượng thùng rác) gọi API `DELETE /api/hr/employees/{id}`.
  - Hiển thị cảnh báo rõ ràng cho người dùng trước khi thực hiện: *"Hành động này sẽ ẩn vĩnh viễn nhân sự khỏi danh sách và vô hiệu hóa hoàn toàn tài khoản đăng nhập của họ. Bạn có chắc chắn muốn xóa?"*
- **Khôi phục (Restore):** 
  - Tại trang quản trị/thùng rác hoặc lịch sử hệ thống, nếu hiển thị nhân viên đã bị xóa (đáp ứng quyền admin), cung cấp nút **"Khôi phục nhân sự"**.
  - Khi click, thực hiện gọi API: `PATCH /api/hr/employees/{id}/restore`.
  - Sau khi khôi phục thành công, hiển thị thông báo: *"Nhân sự và tài khoản đăng nhập liên kết đã được khôi phục trạng thái hoạt động."* và làm mới danh sách.

---

## Tóm tắt công việc tích hợp cho Frontend

| Trang màn hình | Chức năng cần làm | API sử dụng | Chi tiết hiển thị |
|---|---|---|---|
| **Đăng nhập (Login)** | Điều hướng hết hạn | `POST /api/auth/login` | Lưu token, nếu `isExpired: true` chuyển hướng thẳng đến `/renew`. |
| **Route Guard / Router** | Chặn trang | Phân tích JWT Claim | Nếu `isExpired === "true"` chặn toàn bộ các trang trừ trang gia hạn. |
| **Gia hạn (`/renew`)** | Hiển thị & Tạo đơn | `GET /api/billingorder/me/billing-orders` | Hiển thị đơn hàng gia hạn ở trạng thái `Pending` và danh sách module. |
| **Thanh toán (Payment)** | Hiển thị QR / Chuyển khoản | `POST /api/payment/create?orderId={orderId}` | Nhận diện JSON, hiển thị QR VietQR và thông tin CK, có nút sao chép (Copy) tiện lợi. |
| **Thanh toán (Payment)** | Polling kiểm tra kết quả | `GET /api/billingorder/me/billing-orders` | Cứ mỗi 5 giây gọi API kiểm tra trạng thái đơn hàng đã đổi sang `Paid` hay chưa. |
| **Nhân sự (`/employees`)** | Toggle hiện nhân viên đã nghỉ | `GET /api/hr/employees?IncludeResigned=true` | Thêm toggle ẩn/hiện nhân viên đã nghỉ việc với badge xám. |
| **Chỉnh sửa nhân viên** | Form cập nhật trạng thái nghỉ | `PUT /api/hr/employees/{id}` | Nếu chọn Status = Resigned, hiển thị thêm field `ResignationDate` bắt buộc. |
| **Khôi phục nhân viên** | Nút Khôi phục | `PATCH /api/hr/employees/{id}/restore` | Cho phép khôi phục nhân sự đã bị xóa khỏi hệ thống kèm tài khoản liên kết. |
