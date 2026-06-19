# Deploy Plan — DodoSystem Backend (Lộ trình 6 tuần: WSL → VPS)

> 👉 **Người mới hoàn toàn? ĐỌC [deploy_00_so_tay_khai_niem.md](deploy_00_so_tay_khai_niem.md) TRƯỚC.** Sổ tay đó giải thích mọi khái niệm (Docker, container, port, server, HTTPS...) bằng ví dụ đời thường, không cần gõ lệnh. Hiểu sổ tay rồi mới quay lại file này làm thật sẽ không bị ngợp.

> **Mục tiêu gốc:** Deploy dự án lên Ubuntu để học cách máy ảo (VM) chạy và DevOps cơ bản.
> **Triết lý:** học theo tuần · tập trên cái nhỏ trước · WSL cho giai đoạn dev (rẻ, nhanh) → **VPS thật từ Tuần 4** (để học VM/SSH thật + có IP public cho VNPay/HTTPS).
> Cập nhật: 2026-06-19

---

## 0. Bức tranh tổng thể — Ta đang deploy cái gì?

Đây **không phải** một app đơn lẻ mà là một **hệ thống nhiều service**. Hiểu kiến trúc trước khi deploy là bước quan trọng nhất.

| Thành phần | Công nghệ | Cổng (container) | Vai trò | Public ra internet? |
|------------|-----------|------------------|---------|---------------------|
| **WebAPI** | ASP.NET Core 8 | 8080 | API chính + SignalR Hub realtime | ✅ (qua reverse proxy) |
| **SQL Server** | MSSQL 2022 | 1433 | Database chính | ❌ chỉ nội bộ |
| **Redis** | Redis 7 | 6379 | Cache + **Hangfire job storage** (background jobs!) | ❌ chỉ nội bộ |
| **RabbitMQ** | RabbitMQ 3 | 5672 / 15672 | Message queue (email, payroll, payment, attendance) | ❌ (15672 chỉ mở khi debug) |

**5 đặc thù của dự án cần nhớ suốt quá trình deploy:**

1. **VNPay callback** (`/api/payment/callback/vnpay`) → cổng thanh toán gọi ngược về server, **bắt buộc URL public + HTTPS**. ⚠️ **WSL không có IP public** → không test callback thật trên WSL được, phải đợi Tuần 4 (VPS hoặc tunnel).
2. **SignalR (WebSocket)** → reverse proxy phải bật header `Upgrade`/`Connection`, nếu không realtime sẽ rớt. Test-from-internet cũng cần IP public.
3. **Secrets** → [appsettings.json](../SMEFLOWSystem.WebAPI/appsettings.json) đang có nhiều `SET-IN-USER-SECRETS` (Cloudinary, Face++, SMTP) + JWT secret mẫu. **Không bao giờ commit secret thật**; nạp qua `.env`.
4. **SQL Server ngốn RAM** → container MSSQL cần ~2GB. WSL2/VM nên cấp **≥ 4–6GB RAM** nếu không sẽ crash hoặc swap nặng.
5. **Background jobs** → Hangfire (storage trên **Redis**) chạy cron `attendance-resolution` (`*/15`), `monthly-payroll` (01:00 ngày 1), `tenant-expiration` (00:00 hằng ngày) + consumer RabbitMQ, tất cả chạy nền trong chính WebAPI → `restart: always` (đã có) để tự lên lại sau restart.

**Tin tốt khi deploy Linux:**
- **Migration tự chạy** lúc startup (có retry chờ SQL), tự seed Roles/Modules → **không cần migrate tay**. Chi tiết ở Tuần 3.
- **Timezone đã xử lý cross-platform**: code thử `SE Asia Standard Time` (Windows) rồi `Asia/Ho_Chi_Minh` (Linux) ([WebApplicationExtensions.cs:167-176](../SMEFLOWSystem.WebAPI/Validator/WebApplicationExtensions.cs#L167-L176)) → giờ cron chạy đúng UTC+7 trên Ubuntu, không cần chỉnh.
- **Fail-fast config**: thiếu `Jwt:Secret` hoặc `ConnectionStrings:Redis` là app **throw ngay lúc startup** → nếu container WebAPI exit liền sau khi lên, kiểm tra `.env` thiếu 2 giá trị này trước tiên.

---

## ⚠️ WSL ≠ Máy ảo (đọc trước khi bắt đầu)

Bạn đang dùng WSL nên ta tận dụng nó cho Tuần 1–3 (dev nhanh, miễn phí). Nhưng phải hiểu rõ giới hạn:

- WSL **chia sẻ kernel với Windows**, không boot riêng, **không có IP public** → từ internet không chạm tới được.
- Vì vậy **VNPay callback + SignalR-from-internet + HTTPS thật KHÔNG test được trên WSL.**
- Để học đúng "máy ảo chạy như nào" (yêu cầu gốc) → **Tuần 4 chuyển sang VPS Ubuntu thật** (học SSH, firewall, IP public). Đây cũng là lúc bắt buộc cần IP public cho VNPay.

**3 gotcha kỹ thuật của WSL với stack này:**
1. **RAM:** WSL2 có thể ăn hết RAM Windows khi SQL Server chạy. Tạo `C:\Users\<bạn>\.wslconfig`:
   ```ini
   [wsl2]
   memory=6GB
   processors=4
   ```
   Rồi `wsl --shutdown` để áp dụng.
2. **IP WSL đổi mỗi lần khởi động** → đừng hardcode IP; trong compose luôn gọi nhau bằng **tên service** (`sqlserver`, `redis`).
3. **systemd mặc định tắt** trong WSL → bật nếu cần, trong `/etc/wsl.conf`:
   ```ini
   [boot]
   systemd=true
   ```

---

## Lộ trình 6 tuần — tổng quan

```
Tuần 1: Linux + Docker (trên WSL)        ← làm chủ docker run/compose/logs/exec
Tuần 2: Deploy 1 API ĐƠN GIẢN            ← API + SQL, tập nhỏ trước khi đụng dự án thật
Tuần 3: Deploy DodoSystem (full stack)   ← WebAPI + SQL + Redis + RabbitMQ trên WSL
Tuần 4: VPS THẬT + Nginx + Domain + HTTPS ← học VM/SSH, VNPay callback, SignalR WebSocket
Tuần 5: CI/CD tự động                     ← GitHub Actions → VPS
Tuần 6: Vận hành: Log, Backup, Hardening  ← monitoring, sao lưu DB, bảo mật
```

Mỗi tuần có: **🎯 Mục tiêu** · **📖 Lý thuyết tối thiểu** · **⌨️ Thực hành** · **📝 Bài tập** · **✅ Định nghĩa Hoàn thành (DoD)**.

---

## Tuần 1 — Linux + Docker (trên WSL)

### 🎯 Mục tiêu
Làm chủ Docker & docker-compose. Chưa đụng Nginx/SSL/CI/CD.

### 📖 Lý thuyết tối thiểu
- **Image vs Container:** image là "ảnh đóng băng" read-only; container là instance đang chạy.
- **Volume:** lưu data bền vững ngoài container (DB không mất khi container chết).
- **Network:** service trong cùng compose gọi nhau **bằng tên service**, không phải `localhost`.
- **Dockerfile multi-stage:** dự án dùng `sdk:8.0` để build → `aspnet:8.0` để chạy, xem [SMEFLOWSystem.WebAPI/Dockerfile](../SMEFLOWSystem.WebAPI/Dockerfile).

### ⌨️ Thực hành — cài Docker trong WSL Ubuntu
```bash
sudo apt update && sudo apt upgrade -y
curl -fsSL https://get.docker.com | sudo sh
sudo usermod -aG docker $USER        # chạy docker không cần sudo (mở lại terminal)
docker --version && docker compose version

# 5 lệnh xương sống phải thuộc lòng:
docker run -d --name web -p 8080:80 nginx   # run
curl localhost:8080                          # nginx chào
docker logs web                              # logs
docker exec -it web bash                     # exec (gõ exit để ra)
docker rm -f web                             # dọn
```

### 📝 Bài tập
1. Chạy nginx, sửa file `index.html` bằng `docker exec`, reload thấy đổi.
2. Chạy `docker run redis`, dùng `docker exec` vào gõ `redis-cli ping` → `PONG`.
3. Giải thích bằng lời của bạn: image vs container vs volume vs network.

### ✅ DoD
- [ ] Docker chạy không cần sudo trong WSL.
- [ ] Thuộc 5 lệnh: `run`, `compose up/down`, `logs`, `exec`, `ps`.
- [ ] Đã đặt `.wslconfig` giới hạn RAM.

---

## Tuần 2 — Deploy một API ĐƠN GIẢN (tập nhỏ trước)

> Chưa đụng DodoSystem. Tập trên một API tối giản để hiểu pattern "API + DB qua compose" mà không bị nhiễu bởi Redis/RabbitMQ/SignalR.

### 🎯 Mục tiêu
Tự viết một compose 2 service (ASP.NET + SQL Server) và hiểu cách chúng kết nối, dữ liệu bền vững.

### 📖 Lý thuyết tối thiểu
- Connection string trỏ **tên service** (`Server=db`) chứ không phải `localhost`.
- Volume giữ data DB sau `down`/`up`.
- `depends_on` + healthcheck: thứ tự khởi động.

### ⌨️ Thực hành
Tạo một Todo/Weather API ASP.NET tối giản (`dotnet new webapi`), kiến trúc:
```
ASP.NET  →  SQL Server
```
`docker-compose.yml` mẫu:
```yaml
services:
  api:
    build: .
    ports: ["5000:8080"]
    environment:
      - ConnectionStrings__Default=Server=db;Database=Todo;User ID=sa;Password=${SA_PASS};TrustServerCertificate=True
    depends_on: [db]
  db:
    image: mcr.microsoft.com/mssql/server:2022-latest
    environment: [ACCEPT_EULA=Y, MSSQL_SA_PASSWORD=${SA_PASS}]
    volumes: [todo_data:/var/opt/mssql]
volumes: { todo_data: }
```

### 📝 Bài tập
1. API chạy, gọi được endpoint, lưu được 1 record xuống SQL.
2. `docker compose down` rồi `up` → **dữ liệu còn nguyên** (chứng minh volume hoạt động).
3. Đổi `Server=db` thành `Server=localhost` → quan sát nó **fail**, hiểu vì sao.

### ✅ DoD
- [ ] API + SQL chạy bằng compose, kết nối được.
- [ ] Data sống sót qua `down`/`up`.
- [ ] Hiểu vì sao dùng tên service thay cho localhost.

---

## Tuần 3 — Deploy DodoSystem (full stack trên WSL)

> Bây giờ mới chuyển sang dự án thật. Vẫn chạy trên WSL.

### 🎯 Mục tiêu
Chạy được toàn bộ stack DodoSystem trên WSL và hiểu vai trò từng service. **Tách secret ra `.env`** (chuẩn bị cho Tuần 4).

### 📖 Lý thuyết tối thiểu
- ASP.NET Core đọc config theo thứ tự: `appsettings.json` → `appsettings.{Env}.json` → **biến môi trường (ghi đè)**. Dùng `__` cho cấp lồng nhau: `ConnectionStrings__DefaultConnection`.
- 12-Factor: config nằm trong environment, không trong code.

### ⌨️ Thực hành

**Bước 1 — Tạo `.env` (KHÔNG commit, thêm vào `.gitignore`):**
```dotenv
MSSQL_SA_PASSWORD=<mật-khẩu-mạnh-ngẫu-nhiên>
Jwt__Secret=<chuỗi-bí-mật-≥32-ký-tự>
EmailSettings__SmtpPassword=<app-password-gmail>
Cloudinary__CloudName=...
Cloudinary__ApiKey=...
Cloudinary__ApiSecret=...
FacePlusPlus__ApiKey=...
FacePlusPlus__ApiSecret=...
Payment__VNPay__TmnCode=...
Payment__VNPay__HashSecret=...
```

**Bước 2 — Sửa [docker-compose.yml](../docker-compose.yml) bỏ secret hardcode:**
```yaml
  sqlserver:
    environment:
      - ACCEPT_EULA=Y
      - MSSQL_SA_PASSWORD=${MSSQL_SA_PASSWORD}
  webapi:
    env_file: [.env]
    environment:
      - ASPNETCORE_ENVIRONMENT=Development     # WSL còn để Development cho dễ debug; Production từ Tuần 4
      - ConnectionStrings__DefaultConnection=Server=sqlserver;Database=SMEFLOWSystem;User ID=sa;Password=${MSSQL_SA_PASSWORD};Encrypt=False;TrustServerCertificate=True
      - ConnectionStrings__Redis=redis:6379
      - RabbitMQ__Host=rabbitmq
```

**Bước 3 — Chạy & kiểm tra:**
```bash
docker compose up -d --build
docker compose ps              # tất cả Up, sqlserver healthy
docker compose logs -f webapi  # theo dõi khởi động
curl http://localhost:8085/swagger   # hoặc /health
docker stats --no-stream             # xem RAM thật (chú ý SQL Server)
```

### 📝 Bài tập
1. **Migration chạy thế nào? → ĐÃ KIỂM TRA: tự động, KHÔNG cần `dotnet ef database update` tay.**
   - `app.UseWebApi()` gọi `InitializeDatabase()` → `db.Database.Migrate()` ([WebApplicationExtensions.cs:50-73](../SMEFLOWSystem.WebAPI/Validator/WebApplicationExtensions.cs#L50-L73)). DB tự tạo/cập nhật schema khi container WebAPI khởi động.
   - Có **retry 12 lần × 5 giây** (chờ tối đa 60s) → an toàn với Docker khi SQL Server container chưa kịp sẵn sàng. Đây là lý do `up` lần đầu vẫn chạy được dù SQL khởi động chậm.
   - Sau migration **tự seed** Roles (TenantAdmin, Manager, HRManager, SystemAdmin, Employee) + Modules (HR, ATTENDANCE, PAYROLL, DASHBOARD) — chỉ seed khi bảng rỗng.
   - ✅ **Hệ quả khi deploy:** chỉ cần `docker compose up`, không có bước migrate thủ công.
2. Tắt riêng `redis` → xem log WebAPI phản ứng. ⚠️ **Lưu ý: Redis KHÔNG chỉ là cache** — Hangfire dùng Redis làm storage cho background jobs ([DependencyInjection.cs:138](../SMEFLOWSystem.WebAPI/Extensions/DependencyInjection.cs#L138)). Redis chết → payroll hằng tháng, tenant-expiration, attendance-resolution chết theo. Redis là service **bắt buộc**, không optional.
3. Mở tạm RabbitMQ UI `http://localhost:15672` (guest/guest), xem queue email/payroll/attendance.

### ✅ DoD
- [ ] `docker compose ps` tất cả Up; API trả lời.
- [ ] Secret đã ra `.env`, `.env` không bị git theo dõi.
- [ ] Hiểu vai trò từng service và cách migration chạy.

---

## Tuần 4 — VPS thật + Nginx + Domain + HTTPS

> 🎓 Đây là lúc học **máy ảo thật** (đúng mục tiêu gốc) và là lúc **bắt buộc** rời WSL vì VNPay/SignalR/HTTPS cần **IP public**.

### 🎯 Mục tiêu
Dựng VPS Ubuntu, làm chủ SSH/firewall, đặt reverse proxy + domain + SSL, làm VNPay callback và SignalR WebSocket chạy thật.

### 📖 Lý thuyết tối thiểu
- **VPS = VM thật** chạy trên hypervisor của nhà cung cấp, có IP public, boot riêng — khác hẳn WSL.
- **SSH key:** đăng nhập từ xa an toàn, không dùng password.
- **Reverse proxy (Nginx):** nhận 80/443 từ internet → chuyển vào container WebAPI. Người dùng không chạm trực tiếp container.
- **Let's Encrypt/Certbot:** SSL miễn phí, tự gia hạn.
- **WebSocket proxy:** SignalR cần header `Upgrade`/`Connection`.

### ⌨️ Thực hành

**Bước 1 — Thuê VPS Ubuntu 22.04 (≥ 4GB RAM, 2 vCPU)** rồi học VM/SSH:
```bash
ssh-keygen -t ed25519 -C "dodo-deploy"
ssh-copy-id deploy@<VPS_IP>
ssh deploy@<VPS_IP>
# Hardening cơ bản: tạo user sudo, tắt SSH bằng password & root login
```

**Bước 2 — Cài Docker + firewall (UFW):**
```bash
curl -fsSL https://get.docker.com | sudo sh
sudo usermod -aG docker $USER
sudo apt install -y ufw nginx certbot python3-certbot-nginx
sudo ufw default deny incoming && sudo ufw default allow outgoing
sudo ufw allow OpenSSH && sudo ufw allow 80/tcp && sudo ufw allow 443/tcp
sudo ufw enable
# KHÔNG mở 1433/6379/5672/15672 ra internet
```

**Bước 3 — Copy dự án sang VPS** (câu "chỉ cần copy docker-compose.yml" thành hiện thực):
```bash
git clone <repo-url> dodo && cd dodo
# tạo lại .env trên VPS, ĐỔI ASPNETCORE_ENVIRONMENT=Production
docker compose up -d --build
```

**Bước 4 — Domain + Nginx + HTTPS** (trỏ A record `api.dodo.com` → IP VPS):
```nginx
server {
    server_name api.dodo.com;
    location / {
        proxy_pass http://localhost:8085;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;       # SignalR WebSocket
        proxy_set_header Connection "upgrade";        # SignalR WebSocket
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;   # để ASP.NET sinh callback https://
    }
}
```
```bash
sudo ln -s /etc/nginx/sites-available/dodo /etc/nginx/sites-enabled/
sudo nginx -t && sudo systemctl reload nginx
sudo certbot --nginx -d api.dodo.com    # tự cấu hình HTTPS + redirect
```
> Đảm bảo ASP.NET có **ForwardedHeaders middleware** để đọc đúng `X-Forwarded-Proto` → callback VNPay mới ra `https://`.

> 💡 **Không muốn thuê VPS ngay?** Có thể dùng tunnel (`cloudflared`/`ngrok`) hở tạm WSL ra internet để test VNPay/SignalR — nhưng vẫn nên làm VPS để học VM/SSH thật.

### 📝 Bài tập
1. `https://api.dodo.com/swagger` → ổ khoá xanh, chứng chỉ hợp lệ.
2. Client SignalR kết nối `wss://api.dodo.com/...` → giữ kết nối, không rớt.
3. VNPay sandbox end-to-end: tạo payment → thanh toán thử → callback `https://api.dodo.com/api/payment/callback/vnpay` thành công.

### ✅ DoD
- [ ] SSH key-only, root/password login tắt; UFW chỉ 22/80/443.
- [ ] API chạy HTTPS qua domain; SignalR `wss://` ổn định.
- [ ] VNPay callback về tới server thành công.

---

## Tuần 5 — CI/CD tự động (GitHub Actions → VPS)

> Dự án đã có skeleton [.github/workflows/ci-cd.yml](../.github/workflows/ci-cd.yml) (job build + "Deploy Placeholder"). Tuần này biến placeholder thành deploy thật.

### 🎯 Mục tiêu
`git push main` → tự build → SSH vào VPS → `docker compose up`.

### 📖 Lý thuyết tối thiểu
- **CI:** build + test mỗi commit (đã có job `build-and-test`).
- **CD:** SSH vào VPS chạy script deploy.
- **GitHub Secrets:** `VM_HOST`, `VM_USER`, `SSH_PRIVATE_KEY` lưu trong repo Settings, không để lộ trong YAML. Tạo **deploy key riêng cho CI**.

### ⌨️ Thực hành — thay job `deploy`
```yaml
  deploy:
    needs: build-and-test
    if: github.ref == 'refs/heads/main'
    runs-on: ubuntu-latest
    steps:
      - name: Deploy to VPS over SSH
        uses: appleboy/ssh-action@v1
        with:
          host: ${{ secrets.VM_HOST }}
          username: ${{ secrets.VM_USER }}
          key: ${{ secrets.SSH_PRIVATE_KEY }}
          script: |
            cd ~/dodo
            git pull origin main
            docker compose up -d --build
            docker image prune -f
```

### 📝 Bài tập
1. Cấu hình 3 secrets, đổi job deploy.
2. Push 1 commit nhỏ lên `main` → xác minh VPS tự cập nhật.
3. Cho build fail có chủ đích → xác minh **không** deploy code hỏng.

### ✅ DoD
- [ ] Push `main` → app trên VPS tự cập nhật, không SSH tay.
- [ ] Không secret nào lộ trong YAML.
- [ ] Build fail thì pipeline dừng, không deploy.

---

## Tuần 6 — Vận hành: Log, Backup, Bảo mật

### 🎯 Mục tiêu
Biết hệ thống khoẻ không, cứu được dữ liệu khi sự cố, khoá lỗ hổng cơ bản.

### ⌨️ Thực hành

**Log & giám sát:**
```bash
docker compose logs -f --tail=100 webapi
docker stats
# Giới hạn log Docker khỏi đầy ổ — /etc/docker/daemon.json:
# { "log-driver": "json-file", "log-opts": { "max-size": "10m", "max-file": "3" } }
```

**Backup SQL Server (cron hằng đêm):**
```bash
docker exec smeflow-sqlserver /opt/mssql-tools/bin/sqlcmd \
  -S localhost -U sa -P "$MSSQL_SA_PASSWORD" \
  -Q "BACKUP DATABASE [SMEFLOWSystem] TO DISK='/var/opt/mssql/backup/dodo_$(date +%F).bak'"
# crontab: 2h sáng mỗi ngày, giữ 7 ngày gần nhất.
```

**Hardening:**
- [ ] SSH key-only (Tuần 4). UFW chỉ 22/80/443.
- [ ] Đổi **toàn bộ** secret mẫu: JWT secret, SA password, RabbitMQ `guest/guest` → user riêng.
- [ ] `unattended-upgrades` tự vá bảo mật OS.
- [ ] `fail2ban` chống brute-force SSH.

### 📝 Bài tập
1. Cron backup DB + **test restore** vào DB tạm (backup không test = không có backup).
2. Xoá container WebAPI → đo thời gian phục hồi bằng `docker compose up -d`.
3. Đổi RabbitMQ sang user riêng, cập nhật `.env`.

### ✅ DoD
- [ ] Backup DB tự động + đã test restore thành công.
- [ ] Secret mẫu đã thay hết.
- [ ] Log Docker giới hạn dung lượng, OS tự vá.

---

## 🎓 Bài tập tổng kết (nghiệm thu)

**Dựng lại toàn bộ hệ thống từ một VPS trắng trong < 60 phút**, chỉ dùng ghi chú bạn tự viết trong plan này:
- [ ] `https://api.dodo.com/swagger` truy cập được (HTTPS hợp lệ).
- [ ] Đăng nhập lấy JWT → gọi 1 API có auth thành công.
- [ ] SignalR realtime nhận event (vd `dashboard.refresh`).
- [ ] VNPay sandbox → callback thành công.
- [ ] Push `main` → CI/CD tự deploy.
- [ ] Backup DB tự động + restore được.

---

## 📚 Phụ lục — Cheat sheet

```bash
# Docker compose
docker compose up -d --build     # build + chạy nền
docker compose ps                # trạng thái
docker compose logs -f <svc>     # log
docker compose down              # dừng (GIỮ volume)
docker compose down -v           # dừng + XOÁ volume (MẤT DATA!)
docker exec -it <ct> bash        # vào container

# Hệ thống (VPS)
htop ; df -h ; sudo ufw status
journalctl -u nginx -f

# WSL
wsl --shutdown                   # áp dụng .wslconfig
```

## ⚠️ Sai lầm thường gặp
1. Tưởng WSL là VM → cố test VNPay callback trên WSL (không có IP public → bất khả thi).
2. Để `ASPNETCORE_ENVIRONMENT=Development` trên VPS production → lộ stack trace.
3. Quên block WebSocket trong Nginx → SignalR rớt liên tục.
4. Quên `X-Forwarded-Proto` → callback VNPay ra `http://` → VNPay từ chối.
5. WSL/VM < 4GB RAM → SQL Server crash.
6. Commit `.env` lên git → lộ mật khẩu. Luôn `git status` trước khi commit.
7. `docker compose down -v` nhầm → mất sạch database. Phân biệt rõ `down` vs `down -v`.
```
