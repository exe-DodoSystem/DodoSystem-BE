# Mobile App ↔ Backend integration handoff

**Cập nhật:** 2026-07-27  
**Phạm vi:** Các thay đổi backend phục vụ Mobile App sau đợt remediation  
**Trạng thái production:** API healthy; migration
`20260727035930_AddPunchIdempotency` đã được áp dụng.

---

## 1. Backend đã sửa gì

### Authorization và tenant isolation

- Toàn bộ `ModuleSubscriptionsController` yêu cầu đăng nhập.
- Chỉ `TenantAdmin` được gọi:

  ```http
  DELETE /api/ModuleSubscriptions/me/cancel/{moduleId}
  ```

- Backend không dùng `X-Tenant-Id` làm bằng chứng phân quyền.
- Query Shift/ShiftPattern có `includeDeleted=true` vẫn bị giới hạn theo Tenant.
- Các navigation `Segments`, `Days`, `ScheduledShift` và nested `Segments`
  cũng được lọc theo Tenant.

### Manager department scope

- Manager chỉ xem và approve/reject leave request của nhân viên thuộc phòng ban
  được giao.
- Manager chỉ xem manual timesheet của nhân viên thuộc phòng ban được giao.
- Manager chưa được giao phòng ban nhận danh sách rỗng và không thể thao tác
  nhân viên ngoài scope.
- `TenantAdmin` và `HRManager` vẫn xem toàn Tenant.

### Attendance error contract

- JSON và multipart submit-punch dùng chung service và cùng error contract.
- Fake GPS, thiếu GPS và ngoài geofence trả `400`, không còn bị multipart đổi
  nhầm thành `404 Employee not found`.
- Employee không tồn tại trả `404`.
- Lỗi upload ảnh từ dịch vụ ngoài trả `503`.

### Attendance idempotency

- `SubmitPunchRequestDto` có field optional:

  ```text
  clientRequestId: string | null
  ```

- Backend chuẩn hóa UUID về dạng `xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx`.
- Chuỗi không phải UUID được trim và giới hạn tối đa 100 ký tự.
- Unique key ở database:

  ```text
  (TenantId, EmployeeId, ClientRequestId)
  ```

- Retry cùng key trả lại punch đã tạo trước đó:
  - response có cùng `data.id`;
  - không tạo thêm `RawPunchLog`;
  - không upload lại ảnh lên Cloudinary khi lookup thấy record;
  - không gửi realtime notification lần hai.
- Client cũ không có key vẫn được hỗ trợ tạm thời bằng dedup window, nhưng FE
  mới phải luôn gửi key.

### Global error contract

Các typed exception, lỗi authentication/authorization, model validation và
module access chính trả `application/problem+json`:

```json
{
  "type": "https://httpstatuses.com/400",
  "title": "Business rule violation",
  "status": 400,
  "detail": "Nội dung lỗi an toàn cho người dùng",
  "instance": "/api/v1/attendance/submit-punch",
  "traceId": "request-trace-id",
  "errorCode": "ATTENDANCE_FAKE_GPS",
  "error": "Nội dung lỗi an toàn cho người dùng"
}
```

Lỗi model validation có thể có thêm:

```json
{
  "errors": {
    "clientRequestId": [
      "The supplied value is invalid."
    ]
  }
}
```

Mapping status chính:

| HTTP | Ý nghĩa |
|---:|---|
| 400 | Validation hoặc business rule |
| 401 | Chưa đăng nhập/token không hợp lệ |
| 403 | Đã đăng nhập nhưng thiếu quyền/module/scope |
| 404 | Resource không tồn tại |
| 409 | Trạng thái resource xung đột |
| 502 | Downstream provider trả lỗi |
| 503 | Downstream provider tạm thời không khả dụng |
| 500 | Lỗi bất ngờ; response không lộ exception/stack trace |

Một số endpoint legacy ngoài phạm vi remediation vẫn có thể trả JSON dạng
`{ "error": "..." }` hoặc `{ "message": "..." }`. FE nên giữ fallback parser
trong giai đoạn chuyển tiếp.

---

## 2. FE bắt buộc phải sửa

### 2.1. Thêm `clientRequestId` vào model submit punch

Ví dụ model:

```dart
class SubmitPunchRequest {
  final String clientRequestId;
  final double? latitude;
  final double? longitude;
  final String? selfieBase64;
  final String? selfieUrl;
  final String? deviceId;
  final String punchType;
  final bool isMockLocation;
}
```

FE nên dùng UUID v4. Không dùng timestamp đơn thuần và không tái sử dụng key
cho hai thao tác chấm công khác nhau.

### 2.2. Quy tắc sinh và giữ key

1. Khi người dùng bắt đầu một thao tác chấm công mới, sinh đúng một UUID.
2. Lưu UUID cùng request đang pending.
3. Nếu timeout, mất mạng hoặc app retry cùng thao tác, giữ nguyên UUID.
4. Chỉ xóa request pending khi:
   - nhận response thành công; hoặc
   - nhận lỗi 4xx xác định thao tác không được chấp nhận.
5. Lần chấm công mới phải sinh UUID mới.

Sai:

```text
Tap → UUID A → timeout → retry bằng UUID B
```

Đúng:

```text
Tap → UUID A → timeout → retry vẫn UUID A
```

### 2.3. JSON submit-punch

Endpoint:

```http
POST /api/v1/attendance/submit-punch
Content-Type: application/json
Authorization: Bearer <token>
```

Body:

```json
{
  "clientRequestId": "46dd4741-9915-4aaa-9016-f22f93bbc321",
  "latitude": 10.7769,
  "longitude": 106.7009,
  "deviceId": "mobile-device-id",
  "punchType": "Auto",
  "isMockLocation": false,
  "selfieBase64": "data:image/jpeg;base64,..."
}
```

### 2.4. Multipart submit-punch

Endpoint:

```http
POST /api/v1/attendance/submit-punch-form
Content-Type: multipart/form-data
Authorization: Bearer <token>
```

Các field text:

```text
clientRequestId
latitude
longitude
deviceId
punchType
isMockLocation
```

File:

```text
selfie
```

`clientRequestId` phải là text field của multipart, không đặt trong header tùy
biến.

### 2.5. Success response và retry

Success:

```json
{
  "data": {
    "id": "raw-punch-log-id",
    "employeeId": "employee-id",
    "timestamp": "2026-07-27T12:00:00Z",
    "deviceId": "mobile-device-id",
    "isProcessed": false,
    "punchType": "Auto",
    "clientRequestId": "46dd4741-9915-4aaa-9016-f22f93bbc321"
  },
  "message": "Punch submitted successfully"
}
```

Retry thành công có thể trả record cũ. FE phải coi response `200` là thành
công dù timestamp/ID được tạo từ request đầu tiên.

### 2.6. Error parser

Thứ tự lấy message đề xuất:

1. `error`
2. `detail`
3. `message`
4. message mặc định theo HTTP status

Luôn lưu `traceId` vào log/crash report để backend tra cứu.

Pseudo-code:

```dart
final message =
    json['error'] ??
    json['detail'] ??
    json['message'] ??
    defaultMessageForStatus(statusCode);

final errorCode = json['errorCode'];
final traceId = json['traceId'];
```

Không parse business rule bằng cách so khớp toàn bộ chuỗi tiếng Việt. Ưu tiên
`errorCode`.

Các attendance error code hiện có:

| `errorCode` | FE behavior đề xuất |
|---|---|
| `ATTENDANCE_FAKE_GPS` | Báo người dùng tắt Fake GPS |
| `ATTENDANCE_GPS_REQUIRED` | Yêu cầu bật quyền/GPS |
| `ATTENDANCE_OUTSIDE_GEOFENCE` | Hiện thông báo ngoài vùng |
| `ATTENDANCE_INVALID_CLIENT_REQUEST_ID` | Tạo request hợp lệ; không retry vô hạn |
| `ATTENDANCE_IMAGE_UPLOAD_UNAVAILABLE` | Cho retry cùng key sau backoff |

### 2.7. Xử lý status

- `400`: hiện business/validation message; không tự retry liên tục.
- `401`: refresh token hoặc đưa về login.
- `403`: hiện thiếu quyền/module/scope; không đổi thành lỗi đăng nhập.
- `404`: hiện resource không tồn tại.
- `409`: refresh dữ liệu và hiện xung đột trạng thái.
- `502/503`: retry có backoff, giữ nguyên `clientRequestId`.
- `500`: hiện lỗi chung và gửi `traceId` vào monitoring.

---

## 3. Workaround FE có thể gỡ

Sau khi xác nhận app đang gọi đúng production backend:

- Gỡ logic coi mọi lỗi multipart attendance là `Employee not found`.
- Không dùng cooldown/debounce phía client làm cơ chế chống duplicate duy nhất.
  Có thể giữ debounce để UX tốt hơn.
- Không dựa vào client-side filtering để bảo vệ dữ liệu Manager; backend đã
  enforce scope. FE vẫn có thể filter để trình bày.

Chưa nên gỡ fallback parser cho `{ error }`/`{ message }` vì một số endpoint
legacy ngoài đợt remediation vẫn dùng contract cũ.

---

## 4. Checklist tích hợp FE

- [ ] Model JSON có `clientRequestId`.
- [ ] Multipart có text field `clientRequestId`.
- [ ] Mỗi thao tác mới sinh UUID mới.
- [ ] Retry cùng thao tác giữ nguyên UUID.
- [ ] Pending request sống qua timeout/mất mạng.
- [ ] Parser đọc `errorCode`, `error`, `detail`, `traceId`.
- [ ] 401 và 403 được xử lý khác nhau.
- [ ] 409 refresh state trước khi cho thao tác lại.
- [ ] 502/503 retry backoff và giữ nguyên UUID.
- [ ] Test Fake GPS/GPS required/outside geofence cho cả JSON và multipart.
- [ ] Test hai request tuần tự cùng UUID trả cùng `data.id`.
- [ ] Test app restart khi còn request pending.
- [ ] Xác nhận Manager UI chỉ hiển thị dữ liệu department được giao.

---

## 5. Backend verification đã thực hiện

- Local build/test: 167 pass, 1 PostgreSQL integration test skip.
- Phase 0 characterization: 24/24 pass.
- Phase 7/8 contract tests: 14/14 pass.
- Migration production đã có trong `__EFMigrationsHistory`:

  ```text
  20260712123427_InitialPostgreSql
  20260727035930_AddPunchIdempotency
  ```

- Production health endpoint trả:

  ```text
  Healthy
  ```

FE vẫn cần hoàn thành checklist phía trên trước khi phát hành bản mobile mới.
