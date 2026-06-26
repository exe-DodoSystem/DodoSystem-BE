# FE Implementation Plan — Invite Onboarding Flow

> Mục tiêu: nhân viên nhận email → click link `https://giaphu.xyz/onboard/<TOKEN>` → điền form → tài khoản được tạo → redirect về login.
> Tất cả đều derive từ contract API thực tế của backend. Đọc phần **Contract** trước khi code.

---

## Contract API (đọc kỹ trước khi code)

### 1. Validate token — kiểm tra link còn hiệu lực
```
GET https://giaphu.xyz/api/hr/invites/validate?token=<TOKEN>
Không cần Authorization header
```

**Response 200:**
```json
{
  "email": "nhanvien@gmail.com",
  "tenantId": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx",
  "roleId": 5,
  "departmentId": "yyyyyyyy-yyyy-yyyy-yyyy-yyyyyyyyyyyy",
  "positionId": "zzzzzzzz-zzzz-zzzz-zzzz-zzzzzzzzzzzz",
  "expiryDate": "2026-07-03T10:00:00Z",
  "isUsed": false
}
```

> ⚠️ Backend trả về 200 kể cả khi `isUsed: true` hoặc link đã hết hạn — **frontend phải tự check hai field này**.

**Response 400:**
```json
{ "error": "Token không hợp lệ hoặc đã hết hạn" }
```
→ Token không tồn tại trong DB (không phải hết hạn hay đã dùng, mà là không tìm thấy).

---

### 2. Complete onboarding — tạo tài khoản
```
POST https://giaphu.xyz/api/hr/invites/complete
Content-Type: application/json
Không cần Authorization header
```

**Request body:**
```json
{
  "token": "a3f8b2c1d4e5f6a7b8c9d0e1f2a3b4c5",
  "fullName": "Nguyễn Văn A",
  "password": "MatKhau@123",
  "phone": "0901234567"
}
```

> `phone` là optional — có thể bỏ trống hoặc truyền null.

**Response 200 (thành công):**
```json
{ "success": true }
```

**Response 400 (lỗi):**
```json
{ "error": "Email này đã được sử dụng!" }
{ "error": "Token không hợp lệ hoặc đã hết hạn" }
```

**Response 403 (module HR chưa active):**
```json
{ "error": "Bạn chưa đăng ký module HR" }
```

---

## Phase 0 — Cấu hình backend trên VPS (làm TRƯỚC khi test FE)

**File:** `.env` trên VPS hoặc `docker-compose.yml` environment

```dotenv
Invite__OnboardingUrl=https://giaphu.xyz/onboard
```

> Nếu bỏ qua bước này, email gửi cho nhân viên chỉ có token thô (`a3f8b2c1...`) thay vì link đầy đủ.

**Kiểm tra:** Gửi 1 invite test → xem email nhận được — phải thấy link `https://giaphu.xyz/onboard/...` thay vì chuỗi ký tự.

---

## Phase 1 — Thêm route `/onboard/:token` vào router

**Mục tiêu:** URL `https://giaphu.xyz/onboard/<TOKEN>` map vào `OnboardingPage`.

**Việc cần làm:**

1. Tạo file component `OnboardingPage` (hoặc tương đương tên trong dự án).

2. Đăng ký route — route này **không cần auth guard** (nhân viên chưa có tài khoản):

   ```
   /onboard/:token  →  OnboardingPage  (public, không redirect về login)
   ```

3. Đảm bảo auth guard/middleware hiện tại **không chặn** route này.
   - Nếu dự án dùng `PrivateRoute` wrapper → đừng bọc route này.
   - Nếu dùng middleware check token → whitelist `/onboard/*`.

**Định nghĩa hoàn thành:** Truy cập `https://giaphu.xyz/onboard/bất-kỳ-chuỗi` → vào được trang, không bị redirect về `/login`.

---

## Phase 2 — OnboardingPage: validate token khi load

**Mục tiêu:** Khi trang mount, lấy token từ URL → gọi API validate → xác định trạng thái link.

**Logic khi component mount:**

```
1. Lấy token từ URL params (/:token)
2. Nếu không có token → hiển thị lỗi "Link không hợp lệ"
3. Gọi GET /api/hr/invites/validate?token=<TOKEN>
4. Xử lý kết quả:
   - Lỗi mạng / 500 → "Đã có lỗi xảy ra, vui lòng thử lại"
   - 400 { error } → Hiển thị error từ backend (token không tồn tại)
   - 200 nhưng isUsed === true → "Link mời này đã được sử dụng"
   - 200 nhưng new Date(expiryDate) < new Date() → "Link mời đã hết hạn (7 ngày)"
   - 200, isUsed === false, chưa hết hạn → Lưu invite data vào state, hiển thị form
```

**State cần quản lý:**
```
status: "loading" | "invalid" | "expired" | "used" | "ready"
inviteData: { email, tenantId, roleId, departmentId, positionId, expiryDate } | null
errorMessage: string | null
```

**UI theo từng status:**

| Status | Hiển thị |
|--------|---------|
| `loading` | Spinner / skeleton |
| `invalid` | Card lỗi: "Link không hợp lệ hoặc không tồn tại" |
| `expired` | Card lỗi: "Link mời đã hết hạn. Vui lòng liên hệ HR để được mời lại." |
| `used` | Card lỗi: "Link này đã được sử dụng. Vui lòng đăng nhập." + nút "Đăng nhập" → `/login` |
| `ready` | Form đăng ký (Phase 3) |

**Định nghĩa hoàn thành:** Các case dưới đây hoạt động đúng:
- Token không tồn tại → hiển thị lỗi phù hợp
- Token đã dùng → hiển thị thông báo + nút login
- Token hết hạn → hiển thị thông báo
- Token hợp lệ → hiển thị form

---

## Phase 3 — Form đăng ký

**Mục tiêu:** Form cho nhân viên điền thông tin để tạo tài khoản.

**Fields:**

| Field | Type | Required | Ghi chú |
|-------|------|----------|---------|
| Email | Text (readonly) | — | Lấy từ `inviteData.email`, không cho sửa |
| Họ và tên | Text input | ✅ | `fullName` |
| Mật khẩu | Password input | ✅ | `password` |
| Xác nhận mật khẩu | Password input | ✅ | Client-side check only, không gửi lên BE |
| Số điện thoại | Text input | ❌ | `phone` — optional |

**Validation client-side (trước khi gọi API):**

```
fullName:   không rỗng, ≥ 2 ký tự
password:   ≥ 8 ký tự (khuyến nghị thêm: có chữ hoa + số)
confirmPassword: phải === password
phone:      nếu có → chỉ chứa số, 10–11 ký tự (tuỳ chuẩn VN)
```

**UX lưu ý:**
- Email hiển thị dưới dạng text disabled/readonly, không phải input — tránh nhầm
- Nút submit disabled trong lúc đang gọi API
- Hiển thị spinner trên nút khi đang submit

**Định nghĩa hoàn thành:** Form render đúng với email pre-filled, validation hoạt động trước khi submit.

---

## Phase 4 — Submit: gọi API complete

**Mục tiêu:** Gửi dữ liệu form lên `POST /api/hr/invites/complete`, xử lý mọi case response.

**Request body gửi đi:**
```json
{
  "token": "<lấy từ URL params>",
  "fullName": "<từ form>",
  "password": "<từ form>",
  "phone": "<từ form, null nếu rỗng>"
}
```

> `token` lấy lại từ URL params, **không** lấy từ state — để tránh bug nếu state bị clear.

**Xử lý response:**

```
200 { success: true }
  → Hiển thị toast/banner "Tạo tài khoản thành công!"
  → Redirect về /login sau 2 giây
  → (optional) Truyền query param: /login?email=<email> để pre-fill email ở form login

400 { error: "Email này đã được sử dụng!" }
  → Hiển thị lỗi dưới form: "Email này đã có tài khoản. Vui lòng đăng nhập."

400 { error: "Token không hợp lệ hoặc đã hết hạn" }
  → Hiển thị lỗi: "Link không còn hiệu lực."

403 { error: "Bạn chưa đăng ký module HR" }
  → Hiển thị lỗi: "Hệ thống chưa kích hoạt, vui lòng liên hệ quản trị viên."

Lỗi mạng / 500
  → "Đã có lỗi xảy ra, vui lòng thử lại."
```

**Định nghĩa hoàn thành:**
- Flow thành công: điền form → submit → toast thành công → redirect `/login`
- Flow lỗi: từng error case hiện đúng thông báo, không crash trang

---

## Phase 5 — HR Admin UI: form gửi invite

**Mục tiêu:** Trong trang quản lý HR, TenantAdmin/HRManager có thể mời nhân viên mới.

> Kiểm tra trong HR docs: theo phân quyền, `POST /api/hr/invites/send` yêu cầu `[Authorize]` — chỉ cần có JWT hợp lệ. Nhưng về business logic, chỉ TenantAdmin nên thấy nút "Mời nhân viên" (xem ma trận phân quyền trong hr_flow_documentation.md).

**Fields trong form gửi invite:**

| Field | Type | Required | Nguồn data |
|-------|------|----------|-----------|
| Email nhân viên | Text | ✅ | Nhập tay |
| Vai trò (Role) | Select | ✅ | Hardcode: Employee=5, Manager=4, HRManager=3 |
| Phòng ban | Select | Tùy | Gọi `GET /api/hr/departments` |
| Chức vụ | Select | Tùy | Gọi `GET /api/hr/positions?departmentId=<id>` (sau khi chọn phòng ban) |
| Lời nhắn | Textarea | ❌ | Nhập tay |

**Logic phòng ban ↔ chức vụ:**
```
- departmentId và positionId phải đi cùng nhau hoặc cùng để trống
- Khi chọn Department → reset positionId → load lại danh sách Position theo departmentId mới
- Khi xóa Department → xóa positionId luôn
```

**Request gửi đi:**
```json
{
  "email": "nv@cty.com",
  "roleId": 5,
  "departmentId": "uuid hoặc null",
  "positionId": "uuid hoặc null",
  "message": "Chào mừng bạn!"
}
```

**Xử lý response:**

```
200 { success: true }
  → Toast: "Đã gửi lời mời tới <email>"
  → Reset form

400 { error: "Email này đã được sử dụng!" }
  → Lỗi dưới field email

400 { error: "DepartmentId và PositionId phải đi cùng nhau" }
  → Lỗi chọn không đầy đủ (thường bị bắt client-side trước)

400 { error: "Role không tồn tại" }
  → Hiếm gặp — roleId sai
```

**Định nghĩa hoàn thành:** Gửi invite thành công → email đến hộp thư nhân viên chứa link đầy đủ.

---

## Phase 6 — End-to-end test

**Test checklist:**

```
□ Phase 0: Email nhận được có link https://giaphu.xyz/onboard/<token>, không phải token thô
□ Phase 1: URL /onboard/anything không bị redirect về /login
□ Phase 2:
    □ Token hợp lệ → hiển thị form với email pre-filled
    □ Token không tồn tại → hiển thị lỗi phù hợp
    □ Token đã dùng (IsUsed=true) → hiển thị "đã sử dụng"
    □ Token hết hạn (expiryDate < now) → hiển thị "đã hết hạn"
□ Phase 3: Validation form hoạt động (rỗng, password không khớp, phone sai format)
□ Phase 4:
    □ Submit thành công → toast → redirect /login sau 2s
    □ Submit lỗi email đã dùng → hiện thông báo, không crash
□ Phase 5:
    □ Chọn department → dropdown position load theo đúng department đó
    □ Gửi invite không có dept/pos → backend nhận (null, null) — hợp lệ
    □ Gửi invite có dept nhưng không có pos → backend trả 400, FE hiển thị lỗi
```

---

## Sơ đồ tổng quan FE flow

```
URL: giaphu.xyz/onboard/<TOKEN>
        │
        ▼
[OnboardingPage mount]
        │
        ▼
GET /api/hr/invites/validate?token=TOKEN
        │
   ┌────┴────┐
  400       200
   │         │
  lỗi    check isUsed + expiryDate
   │         │
  show    ┌──┴──┐
  error  true  false + chưa hết hạn
          │         │
        show      show Form
        "đã dùng"    │
                     ▼
              [User điền form]
              fullName, password,
              confirmPassword, phone
                     │
                     ▼
            POST /api/hr/invites/complete
            { token, fullName, password, phone }
                     │
              ┌──────┴──────┐
             200            4xx
              │              │
         toast success    show error
              │
              ▼
        redirect → /login
        (optional: ?email=xxx)
```

---

## Files cần tạo / chỉnh sửa

| Việc | File (tham khảo) |
|------|-----------------|
| Tạo trang onboarding | `src/pages/OnboardingPage.tsx` (hoặc tên tương đương) |
| Đăng ký route public | `src/router/index.tsx` hoặc `App.tsx` |
| Service gọi API validate | `src/services/inviteService.ts` hoặc thêm vào api client chung |
| Service gọi API complete | Cùng file với validate |
| Form gửi invite (HR) | `src/pages/hr/InvitePage.tsx` hoặc modal trong trang HR employees |
| Config VPS | `.env` trên VPS: `Invite__OnboardingUrl=https://giaphu.xyz/onboard` |
