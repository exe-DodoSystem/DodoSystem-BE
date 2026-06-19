# 📖 Sổ tay khái niệm — Hiểu trước khi làm (dành cho người mới hoàn toàn)

> Đây là phần **ĐỌC HIỂU**, chưa cần gõ lệnh nào cả. Mục tiêu: đọc xong, khi mở [deploy_plan.md](deploy_plan.md) ra làm bạn sẽ **không bị ngợp** vì đã biết mỗi thứ là gì và tại sao cần nó.
>
> Cách đọc: mỗi khái niệm có **1 câu định nghĩa đơn giản → 1 ví dụ đời thường → liên hệ tới dự án DodoSystem**. Đọc chậm, không cần nhớ hết, cần thì quay lại tra.

---

## Phần 1 — Câu hỏi lớn nhất: "Deploy" là gì?

**Định nghĩa:** Deploy = đưa phần mềm của bạn lên một máy chạy 24/7 để **người khác trên internet truy cập được**.

**Ví dụ đời thường:**
- Bạn nấu ăn trong bếp nhà mình, chỉ mình bạn ăn → đó là chạy app **trên máy của bạn** (localhost).
- Bạn mở một **quán ăn** có địa chỉ, ai cũng tới ăn được, mở cửa cả ngày → đó là **deploy**.

**Liên hệ dự án:** Hiện tại DodoSystem chỉ chạy trên máy bạn (qua Visual Studio / Docker ở nhà). Muốn người dùng thật, app điện thoại, hay cổng thanh toán VNPay "gọi" tới được, bạn phải đưa nó lên một máy chạy liên tục có địa chỉ công khai. Đó là việc cả plan này hướng tới.

> 🔑 Ý chính: deploy = chuyển từ "chạy cho mình xem" sang "chạy cho cả thế giới dùng".

---

## Phần 2 — Server & Máy ảo (VM)

### Server là gì?
**Định nghĩa:** Server = một máy tính **luôn bật, luôn nối internet**, chuyên để phục vụ người khác.

**Ví dụ:** Máy tính ở nhà bạn tắt khi ngủ → không ai dùng app lúc đó được. Server giống cái **quầy lễ tân khách sạn** — lúc nào cũng có người trực, 3 giờ sáng vẫn mở.

### Máy ảo (Virtual Machine - VM) là gì?
**Định nghĩa:** VM = một "máy tính giả" chạy **bên trong** một máy tính thật, có hệ điều hành riêng, tách biệt với máy thật.

**Ví dụ:** Như **một căn hộ trong chung cư**. Tòa nhà (máy thật) chia ra nhiều căn hộ (VM), mỗi căn có cửa riêng, bếp riêng, người ở căn này không đụng tới căn kia. Một máy chủ vật lý mạnh có thể chia ra hàng chục VM.

### VPS là gì?
**Định nghĩa:** VPS (Virtual Private Server) = một cái VM mà bạn **thuê** của nhà cung cấp trên mạng, có địa chỉ internet công khai.

**Ví dụ:** Thay vì tự xây nhà, bạn **thuê một căn hộ** đã có sẵn điện nước, có địa chỉ. Trả tiền theo tháng (vài đô đến vài chục đô).

### WSL là gì? (cái bạn đang có)
**Định nghĩa:** WSL = một bản Linux chạy **ké bên trong Windows** của bạn, để bạn tập Linux mà không cần máy riêng.

**Ví dụ:** Như **góc bếp giả lập để tập nấu** ngay trong nhà bạn — tiện để học, nhưng **không phải quán ăn thật** (khách ngoài đường không vào được).

> 🔑 Ý chính của bạn: **WSL để TẬP (Tuần 1–3), VPS để CHẠY THẬT (Tuần 4)**. WSL không có "địa chỉ công khai" nên VNPay không gọi vào được — đó là lý do tuần 4 phải chuyển sang VPS thật.

---

## Phần 3 — Linux & Dòng lệnh (Terminal)

### Linux là gì?
**Định nghĩa:** Linux = một hệ điều hành (giống Windows, macOS) nhưng **miễn phí và phổ biến nhất cho server**.

**Ví dụ:** Windows giống xe số tự động (dễ dùng, nhiều nút bấm). Linux server giống xe số sàn (ít màu mè, nhưng nhẹ, mạnh, kiểm soát tốt — dân chuyên dùng). 9/10 server trên thế giới chạy Linux.

### Terminal / Dòng lệnh là gì? (quan trọng nhất với người mới)
**Định nghĩa:** Terminal = cửa sổ để bạn **ra lệnh cho máy bằng cách GÕ CHỮ**, thay vì click chuột.

**Ví dụ:** Trên Windows bạn mở thư mục bằng cách **double-click** vào icon. Trên server **không có chuột, không có màn hình đồ họa** — bạn phải gõ chữ để ra lệnh, kiểu như nhắn tin cho máy: "mở thư mục này", "chạy cái này".

**Vì sao server không có giao diện chuột?** Vì server đặt ở trung tâm dữ liệu xa xôi, bạn điều khiển nó từ xa qua mạng. Gửi chữ qua mạng thì nhẹ và nhanh; gửi cả màn hình đồ họa thì nặng và chậm.

**Một câu lệnh trông như thế nào?**
```
docker  logs  webapi
  │       │      │
 lệnh   việc   đối tượng
chính  cần làm  áp dụng
```
Đọc là: "docker ơi, cho tôi xem **logs** (nhật ký) của cái **webapi**". Hầu hết lệnh có dạng `công-cụ  hành-động  thứ-cần-tác-động`.

**Vài lệnh "đi lại" cơ bản** (giống như mở các ngăn tủ):
| Lệnh | Nghĩa đời thường |
|------|------------------|
| `pwd` | "Tôi đang đứng ở thư mục nào?" |
| `ls` | "Liệt kê đồ trong thư mục này ra xem" |
| `cd dodo` | "Đi vào thư mục tên dodo" |
| `cd ..` | "Lùi ra thư mục cha (đi lên 1 cấp)" |

> 🔑 Đừng sợ terminal. Nó chỉ là cách "nhắn tin ra lệnh" cho máy. Lúc đầu lạ, gõ vài chục lần là quen như gõ tin nhắn.

### SSH là gì?
**Định nghĩa:** SSH = cách **đăng nhập vào server từ xa** một cách an toàn (có mã hóa) để gõ lệnh trên đó.

**Ví dụ:** Server ở xa, bạn không tới tận nơi cắm bàn phím được. SSH giống **điều khiển từ xa qua một đường dây bảo mật** — bạn ngồi nhà, gõ lệnh, lệnh chạy trên server.

**"Key" (chìa khóa) thay cho mật khẩu:** Thay vì gõ mật khẩu (dễ bị đoán, dễ lộ), SSH dùng một cặp **chìa khóa**: một nửa giữ trên máy bạn (chìa riêng — giữ kín), một nửa để trên server (ổ khóa — công khai). Chỉ đúng chìa mới mở được ổ. An toàn hơn mật khẩu nhiều.

---

## Phần 4 — Docker: ngôi sao chính của cả dự án

> Dự án này dùng Docker cho mọi thứ, nên hiểu Docker là hiểu 70% công việc.

### Vấn đề Docker sinh ra để giải quyết
Câu nói kinh điển của lập trình viên: **"Trên máy tôi chạy được mà!"** — code chạy ngon trên máy bạn, copy sang máy khác lại lỗi, vì máy kia thiếu thư viện, khác phiên bản, khác cấu hình...

Docker giải quyết bằng cách: **đóng gói app KÈM THEO mọi thứ nó cần** vào một cái "hộp" tiêu chuẩn. Hộp này chạy ở đâu cũng y hệt nhau.

### Image vs Container (dễ nhầm nhất — đọc kỹ)
- **Image (ảnh)** = **công thức nấu ăn + nguyên liệu đóng gói sẵn**. Nó nằm im, đọc-only, không tự chạy.
- **Container (thùng chứa)** = **món ăn được nấu ra từ công thức đó**, đang nóng hổi, đang phục vụ.

**Ví dụ:** Từ 1 công thức bánh (image), bạn nướng ra 5 cái bánh (5 container) giống hệt nhau. Image là bản thiết kế; container là vật chạy thật.

**Liên hệ dự án:** [Dockerfile](../SMEFLOWSystem.WebAPI/Dockerfile) chính là **công thức** để tạo ra image của WebAPI. Khi `docker compose up`, Docker "nấu" image đó thành container đang chạy.

### Volume (ổ cứng gắn ngoài)
**Vấn đề:** Container giống hộp dùng-một-lần — xóa hộp là **mất sạch dữ liệu bên trong**. Nếu database nằm trong container và bạn xóa container → mất hết data người dùng. Thảm họa.

**Định nghĩa:** Volume = một kho lưu trữ **nằm NGOÀI container**, container chết thì volume vẫn còn.

**Ví dụ:** Container là **cái laptop có thể đập bỏ bất cứ lúc nào**. Volume là **ổ cứng gắn ngoài USB** — laptop hỏng thì rút ổ cứng cắm sang máy mới, dữ liệu còn nguyên.

**Liên hệ dự án:** Trong [docker-compose.yml](../docker-compose.yml), `sqlserver_data`, `redis_data`, `rabbitmq_data` chính là các volume giữ dữ liệu an toàn. Đây là lý do `docker compose down` (tắt) khác hẳn `down -v` (tắt + **đập luôn ổ cứng** = mất data).

### Port — Cổng / Cánh cửa đánh số
**Định nghĩa:** Port = một "cánh cửa" đánh số trên máy, để biết gõ cửa nào thì gặp dịch vụ nào.

**Ví dụ:** Một tòa nhà (máy) có nhiều cửa đánh số. Cửa **8080** dẫn tới WebAPI, cửa **1433** dẫn tới SQL Server, cửa **6379** tới Redis. Khách muốn gặp ai thì gõ đúng cửa đó.

**Cách đọc `8085:8080` trong compose:** số bên trái là cửa **ngoài máy** (host), số bên phải là cửa **trong container**. Nghĩa là: "ai gõ cửa 8085 của máy → tôi dẫn vào cửa 8080 bên trong hộp WebAPI".

### Network — Các hộp nói chuyện với nhau
**Định nghĩa:** Khi nhiều container chạy chung, Docker tạo một "mạng nội bộ" để chúng gọi nhau **bằng TÊN** thay vì địa chỉ số.

**Ví dụ:** Trong một văn phòng, bạn gọi đồng nghiệp bằng **tên** ("gọi anh Sơn giúp"), không cần biết anh ấy ngồi bàn số mấy. Tương tự, WebAPI gọi database bằng tên `sqlserver`, gọi cache bằng tên `redis`.

> Đây là lý do connection string ghi `Server=sqlserver` chứ **không phải** `localhost`. Vì `localhost` nghĩa là "chính cái hộp WebAPI này", còn database nằm ở **hộp khác** tên `sqlserver`.

### docker-compose — Nhạc trưởng
**Vấn đề:** Dự án có tới 4 hộp (WebAPI, SQL, Redis, RabbitMQ). Bật từng cái bằng tay, đúng thứ tự, đúng cấu hình → cực và dễ sai.

**Định nghĩa:** `docker-compose.yml` = một file mô tả **toàn bộ dàn hộp** + cách chúng nối với nhau. Một lệnh `docker compose up` là cả dàn cùng chạy.

**Ví dụ:** Nhạc trưởng (compose) vẫy tay một cái → cả dàn nhạc (4 service) cùng vào đúng lúc, đúng bè. Bạn không phải chỉ huy từng nhạc công.

> 🔑 Tổng kết Docker bằng 1 hình: **Dockerfile (công thức) → Image (đồ đóng gói) → Container (đang chạy), data để trong Volume, vào ra qua Port, các container gọi nhau qua Network, và compose là nhạc trưởng điều phối tất cả.**

---

## Phần 5 — Đưa app ra internet cho cả thế giới

### Domain & DNS — Địa chỉ nhà & Danh bạ
- **Domain** = tên dễ nhớ của trang, ví dụ `api.dodo.com`.
- **IP** = địa chỉ thật bằng số của server, ví dụ `123.45.67.89`.
- **DNS** = "danh bạ điện thoại" của internet, dịch tên domain → địa chỉ IP.

**Ví dụ:** Bạn lưu danh bạ "Mẹ" thay vì nhớ số điện thoại. Bấm gọi "Mẹ" → điện thoại tự tra ra số thật. DNS làm y vậy: gõ `api.dodo.com` → tra ra IP server để kết nối.

### Reverse Proxy (Nginx) — Lễ tân đứng cửa
**Định nghĩa:** Reverse proxy = một anh "lễ tân" đứng ngoài cùng, nhận mọi khách từ internet rồi **dẫn vào đúng dịch vụ bên trong**. Khách không bao giờ chạm trực tiếp vào hộp app.

**Ví dụ:** Khách vào khách sạn không tự đi lung tung vào phòng bếp/phòng máy. Họ gặp **lễ tân** (Nginx), lễ tân mới dẫn tới đúng nơi. Lễ tân cũng kiểm tra giấy tờ, chặn kẻ lạ.

**Vì sao cần:** App của bạn chạy ở cửa 8085 nội bộ. Internet chỉ quen gõ cửa 80 (http) và 443 (https). Nginx đứng ở 80/443, nhận khách rồi chuyển vào 8085. Ngoài ra nó lo luôn việc khóa bảo mật (HTTPS) bên dưới.

### HTTPS / SSL — Ổ khóa & Thư niêm phong
**Định nghĩa:** HTTPS = phiên bản có mã hóa của kết nối web. Dữ liệu đi giữa người dùng và server bị **khóa kín**, kẻ giữa đường không đọc được.

**Ví dụ:** HTTP là gửi **bưu thiếp** (ai cầm cũng đọc được nội dung). HTTPS là gửi **thư niêm phong trong phong bì khóa** (chỉ người nhận mở được). Cái ổ khóa 🔒 bạn thấy trên trình duyệt chính là nó.

**Liên hệ dự án:** VNPay **từ chối** gửi tiền/callback qua kết nối không khóa (http). Bắt buộc phải có HTTPS → đó là việc Certbot/Let's Encrypt làm ở Tuần 4 (cấp "ổ khóa" miễn phí, tự động).

### Cửa công khai vs cửa nội bộ (Firewall)
Không phải cửa nào cũng nên mở cho khách. Database (cửa 1433), Redis (6379) chỉ dùng nội bộ → **đóng kín với internet**, nếu mở ra hacker sẽ vào lấy data.

**Firewall** = hàng rào quanh nhà, mặc định **chặn hết**, chỉ mở vài cửa cần thiết (80, 443 cho web, 22 cho SSH).

---

## Phần 6 — Tự động hóa & Vận hành

### CI/CD — Dây chuyền tự động
**Định nghĩa:** Mỗi lần bạn sửa code và đẩy lên (git push), một "dây chuyền" tự động **kiểm tra → đóng gói → đưa lên server** mà bạn không phải làm tay.

**Ví dụ:** Thay vì mỗi lần sửa món lại tự tay bê ra quán, bạn lắp **băng chuyền**: bỏ món vào đầu băng → tự động chạy ra tới bàn khách. Sửa code xong, phần còn lại máy lo.

### Log — Nhật ký
**Định nghĩa:** Log = dòng ghi chép app in ra trong lúc chạy ("đã nhận request", "lỗi kết nối DB"...). Khi có sự cố, đây là nơi đầu tiên để xem chuyện gì xảy ra.

**Ví dụ:** Như **hộp đen máy bay** — khi có vấn đề, mở log ra đọc để biết nguyên nhân.

### Backup — Sao lưu
**Định nghĩa:** Backup = sao chép dữ liệu database ra một bản dự phòng định kỳ, để khi sự cố (xóa nhầm, server hỏng) còn khôi phục được.

**Ví dụ:** Photo giấy tờ quan trọng cất một bản riêng. ⚠️ Quy tắc vàng: **backup chưa thử khôi phục = chưa phải backup** — phải test khôi phục thật mới chắc nó dùng được.

---

## Phần 7 — Ráp lại: Bản đồ DodoSystem bằng ngôn ngữ vừa học

Giờ đọc lại sơ đồ này, bạn sẽ hiểu hết:

```
        🌍 Người dùng / App điện thoại / VNPay (từ internet)
                          │  gõ https://api.dodo.com
                          ▼
              🔒 NGINX (lễ tân + ổ khóa HTTPS)        ← cửa 443, công khai
                          │  dẫn vào nội bộ
                          ▼
        ┌─────────────── MÁY ẢO / VPS (Ubuntu Linux) ───────────────┐
        │                                                            │
        │   📦 WebAPI (ASP.NET)   ← hộp app chính, cửa 8085          │
        │      │       │      │                                      │
        │      ▼       ▼      ▼                                      │
        │  📦 SQL   📦 Redis  📦 RabbitMQ   ← 3 hộp phục vụ, NỘI BỘ  │
        │  (data)  (cache +   (hàng đợi      (KHÔNG mở ra internet)  │
        │   ⬇      job chạy    email/lương)                          │
        │  💾volume  nền)⬇                                           │
        │           💾volume                                         │
        │                                                            │
        │   tất cả do 🎼 docker-compose điều phối                    │
        └────────────────────────────────────────────────────────────┘
```

Đọc thành lời: "Người dùng gõ địa chỉ `api.dodo.com`, DNS dịch ra IP của VPS. Nginx (lễ tân, có ổ khóa HTTPS) nhận họ ở cửa 443, dẫn vào hộp WebAPI ở cửa 8085. WebAPI gọi 3 hộp nội bộ (SQL lưu data, Redis làm cache + chạy job nền, RabbitMQ xếp hàng email/lương) bằng tên. Data nằm an toàn trong volume. Tất cả do compose điều phối, chạy trên một máy ảo Ubuntu."

> Nếu đọc tới đây bạn hình dung được sơ đồ trên = bạn đã sẵn sàng vào [deploy_plan.md](deploy_plan.md) làm thật.

---

## Phần 8 — Bảng tra nhanh thuật ngữ

| Thuật ngữ | Một câu giải thích |
|-----------|--------------------|
| **Deploy** | Đưa app lên máy chạy 24/7 cho người ngoài dùng |
| **Server** | Máy tính luôn bật, phục vụ người khác |
| **VM (máy ảo)** | Máy tính giả chạy trong máy thật |
| **VPS** | Máy ảo bạn thuê trên mạng, có IP công khai |
| **WSL** | Linux chạy ké trong Windows để tập (không phải máy thật) |
| **Linux / Ubuntu** | Hệ điều hành phổ biến cho server (Ubuntu là một bản Linux) |
| **Terminal** | Cửa sổ ra lệnh cho máy bằng cách gõ chữ |
| **SSH** | Đăng nhập điều khiển server từ xa, có mã hóa |
| **Docker** | Công cụ đóng gói app vào "hộp" chạy đâu cũng giống nhau |
| **Image** | Công thức + nguyên liệu đóng gói (nằm im) |
| **Container** | App đang chạy thật, nấu ra từ image |
| **Volume** | Ổ cứng ngoài giữ data khi container bị xóa |
| **Port** | Cánh cửa đánh số để vào đúng dịch vụ |
| **docker-compose** | Nhạc trưởng bật cả dàn nhiều container một lệnh |
| **Domain** | Tên dễ nhớ của trang (api.dodo.com) |
| **DNS** | Danh bạ dịch domain → địa chỉ IP |
| **Nginx / Reverse proxy** | Lễ tân đứng cửa, dẫn khách vào đúng app |
| **HTTPS / SSL** | Kết nối có khóa mã hóa (ổ khóa 🔒 trên trình duyệt) |
| **Firewall (UFW)** | Hàng rào, chặn hết trừ vài cửa cho phép |
| **CI/CD** | Băng chuyền tự động: push code → tự đưa lên server |
| **Log** | Nhật ký app in ra, để xem khi có sự cố |
| **Backup** | Bản sao lưu data để khôi phục khi hỏng |

---

## ✅ Tự kiểm tra (đọc xong nên trả lời được)

Không cần học thuộc, chỉ cần **hiểu ý**:
1. Vì sao chạy trên máy bạn thì được mà vẫn phải "deploy"?
2. WSL khác VPS ở chỗ nào? Vì sao Tuần 4 phải chuyển sang VPS?
3. Image và Container khác nhau thế nào? (gợi ý: công thức vs món ăn)
4. Nếu lỡ xóa container chứa database thì có mất data không? Nhờ cái gì mà không mất?
5. Vì sao WebAPI gọi database bằng `sqlserver` chứ không phải `localhost`?
6. Nginx đóng vai trò gì? Vì sao cần HTTPS cho VNPay?

> Trả lời được 6 câu này (bằng lời của bạn, không cần chính xác từng chữ) → **mở [deploy_plan.md](deploy_plan.md) và bắt đầu Tuần 1**. Khi làm mà quên khái niệm nào, quay lại đây tra Phần 8.
