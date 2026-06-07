# SMEFLOW Backend — Kế hoạch Deploy lên Azure App Service

> **Môi trường mục tiêu:** Azure App Service (Linux Docker) + Azure SQL Database + RabbitMQ (Azure Container Instance)
> **CI/CD:** GitHub Actions → Docker Hub → Azure App Service

---

## Kiến trúc sau khi deploy

```
Developer push lên branch `main`
        ↓
GitHub Actions
  ├─ Build Docker image (Dockerfile)
  ├─ Push lên Docker Hub (free)
  └─ Trigger Azure App Service restart
        ↓
Azure App Service (Linux, Single Container)
  └─ Pull image mới từ Docker Hub
        ↓
  ┌─────────────────────────────────────────┐
  │  smeflow-webapi container               │
  │  - ASP.NET Core 8 API                   │
  │  - Hangfire (SQL Server storage)        │
  │  - SignalR                              │
  │  - RabbitMQ consumer/publisher          │
  └────────┬──────────────┬────────────────┘
           │              │
    Azure SQL DB    Azure Container Instance
    (managed)       (RabbitMQ container)
```

> **Lưu ý quan trọng về Redis:** Hiện tại Redis chỉ dùng để lưu Hangfire jobs.
> Plan này sẽ **chuyển Hangfire sang dùng SQL Server** (1 dòng code) → loại bỏ hoàn toàn dependency vào Redis → đơn giản hóa deployment đáng kể.

---

## Checklist tổng quan

- [ ] **Phase 1** — Thay đổi code (4 việc)
- [ ] **Phase 2** — Tạo Azure resources (5 việc)
- [ ] **Phase 3** — Tạo Docker Hub repository (2 việc)
- [ ] **Phase 4** — Cấu hình GitHub Actions (3 việc)
- [ ] **Phase 5** — Cấu hình Azure App Settings (1 việc lớn)
- [ ] **Phase 6** — Deploy lần đầu & kiểm tra (5 việc)
- [ ] **Phase 7** — Việc cần làm khi FE deploy xong (2 việc)

---

## Phase 1 — Thay đổi code

### 1.1 — Chuyển Hangfire storage: Redis → SQL Server

**File:** [SMEFLOWSystem.WebAPI/Extensions/DependencyInjection.cs](../SMEFLOWSystem.WebAPI/Extensions/DependencyInjection.cs)

Xóa toàn bộ block Hangfire cũ và thay bằng:

```csharp
// XÓA cái này:
services.AddHangfire(cfg =>
{
    cfg.SetDataCompatibilityLevel(CompatibilityLevel.Version_170);
    cfg.UseSimpleAssemblyNameTypeSerializer();
    cfg.UseRecommendedSerializerSettings();

    var redisConnectionString = configuration.GetConnectionString("Redis");
    if (string.IsNullOrWhiteSpace(redisConnectionString))
    {
        throw new InvalidOperationException("Missing config: ConnectionStrings:Redis");
    }
    cfg.UseRedisStorage(redisConnectionString);
});

// THAY bằng cái này:
services.AddHangfire(cfg =>
{
    cfg.SetDataCompatibilityLevel(CompatibilityLevel.Version_170);
    cfg.UseSimpleAssemblyNameTypeSerializer();
    cfg.UseRecommendedSerializerSettings();
    cfg.UseSqlServerStorage(configuration.GetConnectionString("DefaultConnection"));
});
```

Sau đó xóa package `Hangfire.Redis.StackExchange` khỏi WebAPI.csproj:
```xml
<!-- XÓA dòng này trong SMEFLOWSystem.WebAPI.csproj -->
<PackageReference Include="Hangfire.Redis.StackExchange" Version="1.12.0" />
```

Thêm package thay thế vào WebAPI.csproj:
```xml
<PackageReference Include="Hangfire.SqlServer" Version="1.8.23" />
```

Chạy lệnh:
```bash
dotnet add SMEFLOWSystem.WebAPI package Hangfire.SqlServer --version 1.8.23
dotnet remove SMEFLOWSystem.WebAPI package Hangfire.Redis.StackExchange
```

---

### 1.2 — Bật Swagger trên Production

**File:** [SMEFLOWSystem.WebAPI/Validator/WebApplicationExtensions.cs](../SMEFLOWSystem.WebAPI/Validator/WebApplicationExtensions.cs)

```csharp
// Tìm đoạn này:
if (app.Environment.IsDevelopment() || app.Environment.IsStaging())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Đổi thành (bật mọi môi trường):
app.UseSwagger();
app.UseSwaggerUI();
```

---

### 1.3 — Bật Hangfire Dashboard với authorization

**File:** [SMEFLOWSystem.WebAPI/Validator/WebApplicationExtensions.cs](../SMEFLOWSystem.WebAPI/Validator/WebApplicationExtensions.cs)

Thêm vào phương thức `UseWebApi`, sau `app.MapControllers()`:

```csharp
// Thêm using ở đầu file nếu chưa có:
// using Hangfire;
// using Hangfire.Dashboard;

app.MapHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[] { new HangfireAuthorizationFilter() },
    DashboardTitle = "SMEFLOW Jobs"
});
```

Tạo class `HangfireAuthorizationFilter` trong thư mục `SMEFLOWSystem.WebAPI/Filters/`:

```csharp
// File: SMEFLOWSystem.WebAPI/Filters/HangfireAuthorizationFilter.cs
using Hangfire.Dashboard;

namespace SMEFLOWSystem.WebAPI.Filters;

public class HangfireAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();

        // Chỉ cho phép user đã đăng nhập và có role SystemAdmin
        return httpContext.User.Identity?.IsAuthenticated == true
            && httpContext.User.IsInRole("SystemAdmin");
    }
}
```

> **Truy cập:** `https://dodosystem.azurewebsites.net/hangfire` (phải đăng nhập với tài khoản SystemAdmin trước)

---

### 1.4 — Tạo file `appsettings.Production.json`

**File mới:** `SMEFLOWSystem.WebAPI/appsettings.Production.json`

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Warning",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "Payment": {
    "Mode": "Sandbox",
    "Gateway": "VNPay"
  }
}
```

> Các secrets (JWT, Email, Cloudinary...) sẽ được set qua **Azure App Settings** ở Phase 5, không ghi vào file này.

---

### 1.5 — Tạo file `.dockerignore`

**File mới:** `.dockerignore` (ở root dự án, cùng cấp với `SMEFLOWSystem.sln`)

```
**/bin/
**/obj/
**/.vs/
.git/
.gitignore
**/*.user
**/*.suo
.env
.env.*
docker-compose*.yml
Claude-Plans/
README.md
```

---

### 1.6 — Cập nhật `appsettings.json` (xóa Redis connection string)

**File:** [SMEFLOWSystem.WebAPI/appsettings.json](../SMEFLOWSystem.WebAPI/appsettings.json)

Xóa dòng Redis khỏi ConnectionStrings:
```json
// XÓA dòng này:
"Redis": "localhost:6379"
```

Sau khi xóa, block ConnectionStrings chỉ còn:
```json
"ConnectionStrings": {
  "DefaultConnection": "Server=localhost,1433;Database=SMEFLOWSystem;User Id=sa;Password=12345;TrustServerCertificate=true;MultipleActiveResultSets=true"
}
```

---

### 1.7 — Build và test local trước khi deploy

```bash
dotnet build SMEFLOWSystem.sln
dotnet run --project SMEFLOWSystem.WebAPI
```

Đảm bảo app chạy được local sau khi thay đổi Hangfire.

---

## Phase 2 — Tạo Azure Resources

> **Yêu cầu:** Đã có tài khoản Azure. Dùng Azure Portal (portal.azure.com).

### 2.1 — Tạo Resource Group

1. Vào Azure Portal → **Resource Groups** → **+ Create**
2. Đặt tên: `rg-smeflow-prod`
3. Region: **Southeast Asia** (Singapore, gần Việt Nam nhất)
4. **Review + Create** → **Create**

---

### 2.2 — Tạo Azure SQL Database

1. Tìm **Azure SQL** → **+ Create** → **SQL Database** (Single database)
2. **Basics tab:**
   - Resource group: `rg-smeflow-prod`
   - Database name: `SMEFLOWSystem`
   - Server: **Create new**
     - Server name: `smeflow-sqlserver` (phải unique toàn cầu, thêm số nếu trùng)
     - Location: Southeast Asia
     - Authentication: **SQL authentication**
     - Admin login: `sqladmin`
     - Password: _(đặt password mạnh, lưu lại)_
3. **Compute + storage:**
   - Click **Configure database**
   - Chọn **Basic** (5 DTU, ~$4.90/month) hoặc **Serverless** (rẻ hơn nếu traffic thấp)
4. **Networking tab:**
   - Connectivity method: **Public endpoint**
   - Allow Azure services and resources: **Yes**
   - Add current client IP: **Yes** (để bạn kết nối từ máy local)
5. **Review + Create** → **Create**

**Sau khi tạo xong, lấy connection string:**
- Vào resource SQL Database → **Connection strings** → tab **ADO.NET**
- Copy chuỗi dạng: `Server=tcp:smeflow-sqlserver.database.windows.net,1433;Database=SMEFLOWSystem;User Id=sqladmin;Password={password};...`
- **Bổ sung thêm** `TrustServerCertificate=true;MultipleActiveResultSets=true;` vào cuối

---

### 2.3 — Tạo Azure Container Instance cho RabbitMQ

1. Tìm **Container Instances** → **+ Create**
2. **Basics tab:**
   - Resource group: `rg-smeflow-prod`
   - Container name: `smeflow-rabbitmq`
   - Region: Southeast Asia
   - Image source: **Docker Hub or other registry**
   - Image: `rabbitmq:3-management`
   - OS type: Linux
3. **Size:** 0.5 vCPU, 0.5 GB RAM (~$5/month)
4. **Networking tab:**
   - DNS name label: `smeflow-rabbitmq` (tạo domain `smeflow-rabbitmq.southeastasia.azurecontainer.io`)
   - Ports: mở `5672` (AMQP) và `15672` (management UI)
   - Protocol: TCP
5. **Environment variables:**
   - `RABBITMQ_DEFAULT_USER` = `smeflow`
   - `RABBITMQ_DEFAULT_PASS` = _(đặt password, lưu lại)_
6. **Review + Create** → **Create**

> **Lưu lại:** hostname của RabbitMQ = `smeflow-rabbitmq.southeastasia.azurecontainer.io`

---

### 2.4 — Tạo App Service Plan

1. Tìm **App Service Plans** → **+ Create**
2. Resource group: `rg-smeflow-prod`
3. Name: `plan-smeflow-prod`
4. OS: **Linux**
5. Region: Southeast Asia
6. Pricing tier: **B1** (Basic, ~$13/month) — đủ để chạy demo
7. **Review + Create** → **Create**

---

### 2.5 — Tạo Azure App Service

1. Tìm **App Services** → **+ Create** → **Web App**
2. **Basics tab:**
   - Resource group: `rg-smeflow-prod`
   - Name: `dodosystem` (tạo URL: `https://dodosystem.azurewebsites.net`)
   - Publish: **Docker Container**
   - OS: **Linux**
   - Region: Southeast Asia
   - App Service Plan: `plan-smeflow-prod`
3. **Docker tab:**
   - Options: **Single Container**
   - Image source: **Docker Hub**
   - Access type: **Public**
   - Image and tag: `<dockerhub-username>/smeflow-webapi:latest` _(điền sau khi tạo Docker Hub ở Phase 3)_
4. **Review + Create** → **Create**

---

## Phase 3 — Cấu hình Docker Hub

### 3.1 — Tạo tài khoản Docker Hub

1. Truy cập [hub.docker.com](https://hub.docker.com)
2. Đăng ký tài khoản miễn phí
3. Username: _(ghi lại, ví dụ: `dodosystem`)_

### 3.2 — Tạo Access Token (dùng cho GitHub Actions)

1. Docker Hub → Account Settings → **Security** → **New Access Token**
2. Tên token: `github-actions`
3. Permission: **Read, Write, Delete**
4. **Generate** → **copy token ngay** (chỉ hiện 1 lần)
5. Lưu lại: `DOCKERHUB_USERNAME` = tên đăng nhập, `DOCKERHUB_TOKEN` = token vừa copy

---

## Phase 4 — Cấu hình GitHub Actions

### 4.1 — Lấy Azure Publish Profile

1. Azure Portal → App Service `dodosystem` → **Overview**
2. Click **Get publish profile** → tải file `.PublishSettings`
3. Mở file đó, copy **toàn bộ nội dung XML**

### 4.2 — Set GitHub Secrets

Vào GitHub repo → **Settings** → **Secrets and variables** → **Actions** → **New repository secret**

Tạo các secrets sau:

| Secret name | Giá trị |
|-------------|---------|
| `DOCKERHUB_USERNAME` | Username Docker Hub |
| `DOCKERHUB_TOKEN` | Access token Docker Hub |
| `AZURE_WEBAPP_PUBLISH_PROFILE` | Nội dung XML file publish profile |
| `JWT_SECRET` | Chuỗi >= 32 ký tự bất kỳ (đặt mới cho production) |
| `DB_CONNECTION_STRING` | Connection string Azure SQL Database (từ bước 2.2) |
| `RABBITMQ_PASSWORD` | Password RabbitMQ đã đặt ở bước 2.3 |
| `EMAIL_SMTP_PASSWORD` | Gmail App Password |
| `CLOUDINARY_CLOUD_NAME` | Cloud name từ Cloudinary dashboard |
| `CLOUDINARY_API_KEY` | API key từ Cloudinary |
| `CLOUDINARY_API_SECRET` | API secret từ Cloudinary |
| `FACEPLUSPLUS_API_KEY` | API key từ FacePlusPlus |
| `FACEPLUSPLUS_API_SECRET` | API secret từ FacePlusPlus |

### 4.3 — Tạo GitHub Actions Workflow

Tạo file: `.github/workflows/deploy.yml`

```yaml
name: Build & Deploy to Azure App Service

on:
  push:
    branches: [ main ]

env:
  IMAGE_NAME: ${{ secrets.DOCKERHUB_USERNAME }}/smeflow-webapi

jobs:
  build-and-deploy:
    runs-on: ubuntu-latest

    steps:
      - name: Checkout code
        uses: actions/checkout@v4

      - name: Log in to Docker Hub
        uses: docker/login-action@v3
        with:
          username: ${{ secrets.DOCKERHUB_USERNAME }}
          password: ${{ secrets.DOCKERHUB_TOKEN }}

      - name: Build and push Docker image
        uses: docker/build-push-action@v5
        with:
          context: .
          file: SMEFLOWSystem.WebAPI/Dockerfile
          push: true
          tags: |
            ${{ env.IMAGE_NAME }}:latest
            ${{ env.IMAGE_NAME }}:${{ github.sha }}

      - name: Deploy to Azure App Service
        uses: azure/webapps-deploy@v3
        with:
          app-name: dodosystem
          publish-profile: ${{ secrets.AZURE_WEBAPP_PUBLISH_PROFILE }}
          images: ${{ env.IMAGE_NAME }}:${{ github.sha }}
```

---

## Phase 5 — Cấu hình Azure App Settings

Đây là bước quan trọng nhất — set tất cả secrets/config cho app trên Azure.

Vào: **Azure Portal → App Service `dodosystem` → Configuration → Application settings**

Click **+ New application setting** cho từng dòng dưới đây:

### Connection Strings

| Name | Value |
|------|-------|
| `ConnectionStrings__DefaultConnection` | _(connection string Azure SQL từ bước 2.2)_ |

### JWT

| Name | Value |
|------|-------|
| `Jwt__Secret` | _(chuỗi >= 32 ký tự, dùng giá trị trong GitHub Secret `JWT_SECRET`)_ |
| `Jwt__Issuer` | `SMEFLOW_Server` |
| `Jwt__Audience` | `SMEFLOW_Client` |
| `Jwt__AccessTokenExpiryMinutes` | `60` |
| `Jwt__RefreshTokenExpiryDays` | `7` |

### Email

| Name | Value |
|------|-------|
| `EmailSettings__SmtpHost` | `smtp.gmail.com` |
| `EmailSettings__SmtpPort` | `587` |
| `EmailSettings__SmtpUsername` | `dodosystem1@gmail.com` |
| `EmailSettings__SmtpPassword` | _(Gmail App Password)_ |
| `EmailSettings__UseSsl` | `true` |
| `EmailSettings__FromName` | `DodoSystem` |
| `EmailSettings__FromEmail` | `dodosystem1@gmail.com` |

### RabbitMQ

| Name | Value |
|------|-------|
| `RabbitMQ__Host` | `smeflow-rabbitmq.southeastasia.azurecontainer.io` |
| `RabbitMQ__Port` | `5672` |
| `RabbitMQ__Username` | `smeflow` |
| `RabbitMQ__Password` | _(password RabbitMQ đã đặt)_ |

### Cloudinary

| Name | Value |
|------|-------|
| `Cloudinary__CloudName` | _(từ Cloudinary dashboard)_ |
| `Cloudinary__ApiKey` | _(từ Cloudinary dashboard)_ |
| `Cloudinary__ApiSecret` | _(từ Cloudinary dashboard)_ |

### FacePlusPlus

| Name | Value |
|------|-------|
| `FacePlusPlus__ApiKey` | _(từ FacePlusPlus console)_ |
| `FacePlusPlus__ApiSecret` | _(từ FacePlusPlus console)_ |
| `FacePlusPlus__BaseUrl` | `https://api-us.faceplusplus.com` |
| `FacePlusPlus__ConfidenceThreshold` | `80.0` |

### VNPay

| Name | Value |
|------|-------|
| `Payment__Mode` | `Sandbox` |
| `Payment__Gateway` | `VNPay` |
| `Payment__FrontendUrl` | `http://localhost:3000` _(cập nhật sau khi FE deploy)_ |
| `Payment__VNPay__TmnCode` | `7BD2ILMB` |
| `Payment__VNPay__HashSecret` | `BCJSBUREQ9UN22CDL8HHYOLXG30X3VI1` |
| `Payment__VNPay__BaseUrl` | `https://sandbox.vnpayment.vn/paymentv2/vpcpay.html` |
| `Payment__VNPay__CallbackUrl` | `/api/payment/callback/vnpay` |

### CORS (tạm thời, cập nhật khi FE deploy)

| Name | Value |
|------|-------|
| `Cors__AllowedOrigins__0` | `http://localhost:3000` |
| `Cors__AllowedOrigins__1` | `http://localhost:5173` |

### General

| Name | Value |
|------|-------|
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `ASPNETCORE_HTTP_PORTS` | `8080` |
| `AttendanceResolution__Enabled` | `true` |
| `AttendanceResolution__Cron` | `*/15 * * * *` |

**Sau khi set xong** → click **Save** ở trên cùng.

---

## Phase 6 — Deploy lần đầu & Kiểm tra

### 6.1 — Trigger deploy lần đầu

```bash
# Commit và push tất cả thay đổi từ Phase 1
git add .
git commit -m "chore: prepare for Azure App Service deployment"
git push origin main
```

→ GitHub Actions sẽ tự chạy. Theo dõi tại tab **Actions** trên GitHub repo.

Build lần đầu mất khoảng **5-8 phút**.

---

### 6.2 — Kiểm tra App Service logs

Nếu app bị lỗi, xem log tại:
- Azure Portal → App Service `dodosystem` → **Log stream** (real-time)
- Hoặc: **Diagnose and solve problems** → **Application Logs**

Lỗi phổ biến cần check:
- `Missing config: ...` → thiếu App Setting, kiểm tra lại Phase 5
- `Cannot connect to SQL Server` → kiểm tra connection string và firewall Azure SQL
- `Cannot connect to RabbitMQ` → kiểm tra Container Instance đang chạy không

---

### 6.3 — Kiểm tra các endpoints

Sau khi app start thành công:

```
✅ Swagger UI:      https://dodosystem.azurewebsites.net/swagger
✅ Health check:    https://dodosystem.azurewebsites.net/api/auth/... (thử 1 endpoint bất kỳ)
✅ Hangfire:        https://dodosystem.azurewebsites.net/hangfire  (login SystemAdmin trước)
✅ SignalR hub:     https://dodosystem.azurewebsites.net/hubs/notifications
```

---

### 6.4 — Kiểm tra Database migration

Lần đầu app chạy, `db.Database.Migrate()` sẽ tự tạo schema + seed Roles + Modules.
Kiểm tra bằng cách:
- Dùng Azure Data Studio hoặc SSMS kết nối tới Azure SQL Database
- Server: `smeflow-sqlserver.database.windows.net`
- Authentication: SQL Login (sqladmin / password đã đặt)
- Xác nhận đã có đủ các bảng và data trong Roles, Modules

**Nếu cần restore backup từ local:**
1. Export từ local: dùng SSMS → Tasks → Generate Scripts (chọn data + schema)
2. Chạy script đó trên Azure SQL Database

---

### 6.5 — Đăng ký VNPay Callback URL

Đăng nhập vào [sandbox.vnpayment.vn](https://sandbox.vnpayment.vn) với tài khoản merchant.
Cập nhật **Return URL** thành:
```
https://dodosystem.azurewebsites.net/api/payment/callback/vnpay
```

---

## Phase 7 — Việc cần làm khi Frontend deploy xong

### 7.1 — Cập nhật CORS

Khi biết URL frontend (ví dụ: `https://dodosystem-fe.vercel.app`), vào:
**Azure Portal → App Service → Configuration → Application settings**

Cập nhật:
```
Cors__AllowedOrigins__0  →  https://dodosystem-fe.vercel.app
Cors__AllowedOrigins__1  →  (xóa hoặc giữ localhost để dev)
```

Click **Save** → App Service tự restart.

### 7.2 — Cập nhật Payment FrontendUrl

```
Payment__FrontendUrl  →  https://dodosystem-fe.vercel.app
```

---

## Tóm tắt chi phí ước tính

| Service | Tier | Chi phí/tháng |
|---------|------|--------------|
| Azure App Service | B1 Linux | ~$13 |
| Azure SQL Database | Basic 5 DTU | ~$5 |
| Azure Container Instance (RabbitMQ) | 0.5 vCPU, 0.5 GB | ~$5 |
| Docker Hub | Free | $0 |
| GitHub Actions | Free (2000 min/month) | $0 |
| **Tổng** | | **~$23/tháng** |

> **Tip:** Azure cung cấp $200 free credit cho tài khoản mới — dùng để test miễn phí ~1 tháng đầu.

---

## Lưu ý quan trọng

1. **Không commit secrets** vào git — tất cả secrets đặt qua Azure App Settings hoặc GitHub Secrets.
2. **`appsettings.json`** giữ nguyên cho local dev, không chứa production secrets.
3. Khi app start lần đầu, **auto-migrate mất 30-60 giây** vì phải tạo toàn bộ schema.
4. **SignalR** hoạt động tốt trên Azure App Service — App Service đã hỗ trợ WebSocket.
5. **Hangfire Dashboard** chỉ vào được sau khi đăng nhập SystemAdmin qua API, lấy JWT token, rồi truy cập `/hangfire`.

---

*Kế hoạch này được tạo dựa trên câu trả lời trong `deploy_questions.md`. Cập nhật lần cuối: 2026-06-03.*
