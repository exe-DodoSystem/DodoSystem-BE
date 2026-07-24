# Triển khai DodoSystem mới hoàn toàn trên AWS Lightsail — từ $7/tháng

> **Kiến trúc:** ASP.NET Core 8 + PostgreSQL 16 + Redis 7 + RabbitMQ 4.3 + Nginx  
> **Hạ tầng khởi đầu:** AWS Lightsail Linux, 1 GB RAM, 2 vCPU, 40 GB SSD, public IPv4  
> **Chi phí compute khởi đầu:** 7 USD/tháng; snapshot/S3 backup và domain có thể phát sinh thêm  
> **Mục tiêu:** dành cho người chưa có tài khoản AWS và chưa có server; làm lần lượt từ đầu đến cuối để có một MVP tải thấp chạy được, có HTTPS, CI/CD, health check, backup, rollback image có kiểm soát và đường nâng lên gói lớn hơn.
>
> **Giới hạn đã chấp nhận:** guide này cố ý giữ gói **7 USD/tháng**. API + PostgreSQL + Redis + RabbitMQ trên 1 GB là rất sát tài nguyên, không có high availability và sẽ có downtime ngắn khi deploy. Chỉ dùng cho demo/MVP tải thấp; phải giữ đúng giới hạn worker, batch, connection pool và RAM trong guide. Khi xuất hiện OOM hoặc swap liên tục thì giảm tải ngay và thực hiện Phần 9.

Thứ tự thực hiện ngắn gọn:

1. Làm Phần 1–2 trên máy Windows local; chưa tốn phí AWS.
2. Chỉ khi local Compose và smoke test đạt mới đăng ký/tạo Lightsail ở Phần 3.
3. Cấu hình VPS, secret và CI/CD theo Phần 4–6.
4. Gắn domain/HTTPS, backup và giám sát theo Phần 7–10.
5. Go-live khi checklist Phần 11 đạt; dùng Phần 9 khi cần nâng gói.

Các lệnh trước Phần 3 chạy bằng PowerShell tại thư mục repository. Các lệnh từ Phần 4 trở đi chạy trong Bash sau khi SSH vào Ubuntu VPS, trừ khi đoạn hướng dẫn ghi rõ chạy ở GitHub/AWS Console.

---

## 0. Quyết định kiến trúc và nguyên tắc bắt buộc

Azure SQL Edge không được dùng trong bản này vì đã hết hỗ trợ từ ngày 30/09/2025. SQL Server Linux chính thức cần tối thiểu khoảng 2 GB RAM nên không phù hợp khi VPS chỉ có 1 GB và còn phải chạy API, Redis, RabbitMQ, Nginx.

Phương án này dùng PostgreSQL. Đây là thay đổi database provider, vì vậy phải hoàn thành toàn bộ **Phần 2** và test local trước khi tạo server.

Guide này không giả định có VPS hay PostgreSQL production cũ. Tên `dodo-prod`, database và volume đều được tạo mới. Các đoạn nói về migration SQL Server chỉ liên quan đến source code hiện tại của repository, không phải yêu cầu truy cập hay sửa một server cũ.

Kiến trúc cuối cùng:

```text
Internet
   │ HTTPS 443
   ▼
Nginx + Let's Encrypt trên host
   │ http://127.0.0.1:8085
   ▼
Docker Compose private network
   ├── webapi      ASP.NET Core 8
   ├── postgres    PostgreSQL 16
   ├── redis       Redis 7, Hangfire storage
   └── rabbitmq    RabbitMQ 4.3
```

Nguyên tắc:

- Chỉ public các cổng 22, 80, 443.
- PostgreSQL, Redis và RabbitMQ không publish port ra host.
- API chỉ bind `127.0.0.1:8085` để Nginx truy cập.
- Không ghi secret thật trong Git, Markdown, Dockerfile hoặc workflow.
- Swap chỉ là lưới an toàn, không phải RAM vận hành.
- Backup nằm cùng VPS không được tính là backup hoàn chỉnh; phải có bản off-site.
- Không build Docker image trên VPS 1 GB. GitHub Actions build image, VPS chỉ pull và restart API.
- Không dùng tag `latest` để deploy production; mỗi lần deploy dùng đúng Git commit SHA để rollback xác định được phiên bản.
- Migration production phải tương thích ngược với image liền trước. Rollback image không tự rollback database.
- Không chạy hai bản `webapi` trỏ vào hai bản sao PostgreSQL khác nhau trong lúc chuyển máy.

---

## 1. Chuẩn bị secret an toàn trước khi đăng ký AWS

Phần này không yêu cầu có server cũ. Nếu các credential dưới đây chưa từng được tạo thì tạo mới khi đến Phần 5; nếu đã từng dùng hoặc từng xuất hiện trong guide/commit cũ thì rotate trước khi deploy:

1. Đổi Gmail app password.
2. Rotate Cloudinary API secret.
3. Rotate Face++ API secret.
4. Rotate SePay API key, webhook secret, VNPay `HashSecret`/`TmnCode` nếu đã từng dùng thật.
5. Tạo JWT secret mới.
6. Tạo mật khẩu database và RabbitMQ mới, không dùng chung với tài khoản khác.
7. Repository hiện từng có giá trị giống credential VNPay trong `SMEFLOWSystem.WebAPI/appsettings.json`. Hãy coi chúng là đã lộ nếu từng commit/push hoặc từng build image và rotate trước khi deploy.
8. Nếu secret từng được push, xóa khỏi Git history bằng `git filter-repo` hoặc BFG; chỉ xóa ở commit mới là chưa đủ. Việc viết lại history ảnh hưởng mọi clone/branch nên phải thống nhất với cả nhóm trước khi làm.

Kiểm tra file bị track:

```powershell
git ls-files | Select-String -Pattern '(^|/)\.env$|appsettings\.Production\.json'
git grep -n -I -E 'Password=12345|HashSecret.*[A-Za-z0-9]{16,}|ApiSecret.*[A-Za-z0-9]{16,}'
```

Lệnh đầu phải không liệt kê `.env` hoặc `appsettings.Production.json`. Lệnh thứ hai phải không tìm thấy secret hoạt động thật; placeholder như `SET-IN-ENV` được phép. Bổ sung vào `.gitignore`:

```gitignore
.env
.env.*
!.env.example
backups/
```

Trong `appsettings.json`:

- Mọi password/key mẫu phải là `SET-IN-ENV`, không dùng giá trị hoạt động thật.
- `DefaultConnection` phải là PostgreSQL local hợp lệ hoặc placeholder; không giữ connection string SQL Server sau khi đã chuyển sang Npgsql.
- `Payment:VNPay:TmnCode` và `Payment:VNPay:HashSecret` phải là placeholder.
- `Invite:OnboardingUrl` chỉ là giá trị local; production sẽ override bằng `Invite__OnboardingUrl` trong `.env`.
- Dockerfile publish `appsettings.json` vào image, vì vậy secret trong file này vẫn bị lộ dù production có environment variable override.

Ví dụ giá trị an toàn để commit:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=dodosystem;Username=dodoapp;Password=SET-IN-USER-SECRETS",
    "Redis": "localhost:6379"
  },
  "Payment": {
    "Mode": "Sandbox",
    "Gateway": "VNPay",
    "VNPay": {
      "TmnCode": "SET-IN-USER-SECRETS",
      "HashSecret": "SET-IN-USER-SECRETS",
      "BaseUrl": "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html",
      "CallbackUrl": "/api/payment/callback/vnpay"
    }
  }
}
```

Đây chỉ là các field liên quan, không thay toàn bộ `appsettings.json` bằng snippet rút gọn. Local secret đặt bằng `dotnet user-secrets` hoặc environment variable; production chỉ lấy từ `/opt/dodo/.env`.

---

## 2. Chuyển code từ SQL Server sang PostgreSQL

### 2.1 Thay EF Core provider

Chạy tại thư mục repository:

```bash
dotnet remove SMEFLOWSystem.Infrastructure package Microsoft.EntityFrameworkCore.SqlServer
dotnet remove SMEFLOWSystem.WebAPI package Microsoft.EntityFrameworkCore.SqlServer
dotnet remove SMEFLOWSystem.Core package Microsoft.EntityFrameworkCore.SqlServer
dotnet add SMEFLOWSystem.Infrastructure package Npgsql.EntityFrameworkCore.PostgreSQL --version 8.0.11
dotnet restore SMEFLOWSystem.sln
```

Không nâng Npgsql lên major 9/10 khi dự án vẫn dùng EF Core 8.

Trong `SMEFLOWSystem.Infrastructure/Extensions/DependencyInjection.cs`, đổi:

```csharp
options.UseSqlServer(configuration.GetConnectionString("DefaultConnection"))
```

thành:

```csharp
options.UseNpgsql(
    configuration.GetConnectionString("DefaultConnection"),
    npgsql => npgsql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(10), null))
```

Trong `SMEFLOWSystem.Infrastructure/Data/SMEFLOWSystemContextFactory.cs`, đổi `UseSqlServer(...)` thành:

```csharp
optionsBuilder.UseNpgsql(
    configuration.GetConnectionString("DefaultConnection")
);
```

### 2.2 Chuyển SQL mặc định và computed columns

Các file trong `SMEFLOWSystem.Infrastructure/Data/Configurations` đang có cú pháp SQL Server. Chỉ sửa configuration source, không sửa migration cũ rồi cố chạy trên PostgreSQL.

Thay toàn bộ biểu thức:

| SQL Server | PostgreSQL |
|---|---|
| `HasDefaultValueSql("(newsequentialid())")` | `HasDefaultValueSql("gen_random_uuid()")` |
| `HasDefaultValueSql("(getdate())")` | `HasDefaultValueSql("CURRENT_TIMESTAMP")` |
| `HasDefaultValueSql("(getutcdate())")` | `HasDefaultValueSql("timezone('utc', now())")` |

Các file hiện cần kiểm tra ít nhất:

- `BillingConfigurations.cs`
- `CoreHRConfigurations.cs`
- `EmployeeBonusDeductionEntryConfiguration.cs`
- `EmployeeSalaryHistoryConfiguration.cs`
- `IdentityConfigurations.cs`
- `SystemConfigurations.cs`

Trong `BillingConfigurations.cs`, đổi computed columns:

```csharp
.HasComputedColumnSql("\"TotalAmount\" - \"DiscountAmount\"", stored: true)
```

và:

```csharp
.HasComputedColumnSql("\"Quantity\" * \"UnitPrice\"", stored: true)
```

Tìm lại toàn repository, bỏ qua thư mục migration cũ:

```powershell
rg -n -i 'newsequentialid|getdate\(|getutcdate\(|\[TotalAmount\]|\[Quantity\]|UseSqlServer' SMEFLOWSystem.Infrastructure -g '!Migrations/**'
```

Kết quả phải rỗng.

`decimal(18,2)` có thể tiếp tục dùng vì PostgreSQL chấp nhận `decimal`/`numeric`. Nếu Npgsql cảnh báo, chuẩn hóa thành `numeric(18,2)` trong một commit riêng.

### 2.3 Entity, migration và dữ liệu: trường hợp nào còn, trường hợp nào mất?

Ba thứ này độc lập với nhau: class Entity trong code, file migration trong Git và bảng/dữ liệu thật trong PostgreSQL.

| Thao tác | Dữ liệu PostgreSQL |
|---|---|
| Xóa một class Entity nhưng chưa tạo/chạy migration | Vẫn còn nguyên trong database |
| `dotnet ef migrations remove` với migration cuối **chưa apply** | Database không đổi; chỉ bỏ file migration và cập nhật model snapshot |
| Migration có `DropTable`/`DropColumn` và migration đó được apply | Bảng/cột cùng dữ liệu bên trong bị xóa |
| Add lại Entity sau khi bảng đã bị drop và tạo migration mới | EF tạo bảng mới rỗng; dữ liệu cũ không tự quay lại |
| Xóa thư mục migration trong source code | Tự nó không đụng tới database đang chạy |
| `docker compose down` | Named volume `postgres_data` và dữ liệu vẫn còn |
| `docker compose down -v`, xóa volume, hoặc tạo volume mới | Dữ liệu trong volume cũ mất/không còn được container mới dùng |

Vì dự án này chuyển từ SQL Server sang một PostgreSQL **mới và trống**, ta tạo một migration PostgreSQL ban đầu mới. Dữ liệu SQL Server local cũ không tự chuyển sang PostgreSQL. Nếu có dữ liệu cần mang theo, phải export/import riêng và không làm theo bước xóa migration bên dưới cho đến khi có kế hoạch chuyển dữ liệu.

Sau khi PostgreSQL production đã có dữ liệu, không lặp lại quy trình “xóa toàn bộ migration và tạo Initial”. Mỗi lần sửa Entity phải:

1. Tạo migration mới với tên mô tả thay đổi.
2. Mở file `Up()` và tìm `DropTable`, `DropColumn`, `AlterColumn` hoặc SQL thủ công.
3. Backup PostgreSQL off-site trước khi apply migration có thay đổi schema.
4. Nếu muốn đổi tên bảng/cột, dùng `RenameTable`/`RenameColumn`; không drop rồi add lại.
5. Test migration và restore trên database staging/test trước production.

Tuyệt đối không dùng `dotnet ef database drop`, `EnsureDeleted`, `docker compose down -v`, `docker volume prune` hoặc `docker system prune --volumes` trên production.

### 2.4 Tạo migration PostgreSQL ban đầu

Phần này chỉ thực hiện một lần, trước lần deploy PostgreSQL đầu tiên. Commit trạng thái hiện tại để Git giữ lịch sử migration SQL Server:

```bash
git add .
git commit -m "checkpoint before PostgreSQL migration"
```

Không cần server cũ, database cũ hay nhánh backup riêng: Git đã giữ các file ở commit trên. Sau đó xóa các file trong `SMEFLOWSystem.Infrastructure/Migrations` trên **máy local**, giữ lại thư mục. Việc này không chạy câu lệnh nào lên database. Cài EF tool nếu máy chưa có:

```bash
dotnet tool install --global dotnet-ef --version 8.0.22
```

Nếu máy đã có `dotnet-ef`, dùng `dotnet tool update --global dotnet-ef --version 8.0.22` thay cho lệnh install.

Nếu máy đang có `dotnet-ef` major cao hơn (ví dụ 9/10), lệnh `update` không cho hạ phiên bản. Khi đó chạy:

```powershell
dotnet tool uninstall --global dotnet-ef
dotnet tool install --global dotnet-ef --version 8.0.22
```

Tạm đổi connection string local trong user secrets hoặc environment thành PostgreSQL local, sau đó tạo migration:

```powershell
dotnet ef migrations add InitialPostgreSql --project SMEFLOWSystem.Infrastructure --startup-project SMEFLOWSystem.WebAPI --output-dir Migrations
```

Kiểm tra migration mới không còn kiểu/cú pháp SQL Server:

```powershell
rg -n -i 'nvarchar|datetime2|newsequentialid|getdate\(|SqlServer:' SMEFLOWSystem.Infrastructure/Migrations
```

Kết quả phải rỗng.

### 2.5 Cấu hình reverse proxy tin cậy và đúng thứ tự middleware

Từ ASP.NET Core 8.0.17, `X-Forwarded-*` từ proxy không nằm trong `KnownProxies`/`KnownNetworks` bị bỏ qua. Với Nginx chạy trên host và API chạy trong Docker, request vào container thường có remote IP là gateway của Docker bridge. Nếu không trust đúng gateway, API có thể hiểu sai scheme HTTPS, client IP hoặc redirect.

Guide cố định Docker subnet `172.30.0.0/24` và gateway `172.30.0.1` ở Compose. Trước khi dùng, kiểm tra subnet này chưa trùng mạng hiện có trên VPS:

```bash
ip route
docker network ls
```

Nếu `172.30.0.0/24` bị trùng, chọn một private subnet chưa dùng và sửa **đồng thời** cả `docker-compose.yml` lẫn IP trong code dưới đây.

Trong `SMEFLOWSystem.WebAPI/Validator/WebApplicationExtensions.cs`, thêm `using System.Net;` và cấu hình proxy chính xác. `UseForwardedHeaders` phải chạy trước `UseHttpsRedirection`:

```csharp
var dockerGateway = IPAddress.Parse("172.30.0.1");
var forwardedHeadersOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor |
                       ForwardedHeaders.XForwardedProto
};
forwardedHeadersOptions.KnownProxies.Add(dockerGateway);
forwardedHeadersOptions.KnownProxies.Add(dockerGateway.MapToIPv6());
app.UseForwardedHeaders(forwardedHeadersOptions);

if (!app.Environment.IsDevelopment() && !app.Environment.IsStaging())
{
    app.UseHttpsRedirection();
}

app.UseCors("AllowFE");
app.UseAuthentication();
app.UseAuthorization();
```

Đảm bảo file có các namespace cần thiết:

```csharp
using Microsoft.AspNetCore.HttpOverrides;
using System.Net;
```

Không dùng `KnownNetworks.Clear()`/`KnownProxies.Clear()` để chấp nhận header từ mọi nguồn. Sau khi deploy HTTPS, Phần 7 sẽ kiểm tra log để xác nhận proxy IP thực tế; nếu Docker trả một IP khác thì thêm đúng IP đó, không mở trust toàn mạng.

### 2.6 Thêm health endpoint

Thêm package tương thích .NET/EF 8:

```bash
dotnet add SMEFLOWSystem.WebAPI package AspNetCore.HealthChecks.NpgSql --version 8.0.2
dotnet add SMEFLOWSystem.WebAPI package AspNetCore.HealthChecks.Redis --version 8.0.2
dotnet add SMEFLOWSystem.WebAPI package AspNetCore.HealthChecks.Rabbitmq --version 8.0.2
```

Trong `AddWebApi`, đăng ký health checks. Dùng chính các config key production:

```csharp
services.AddHealthChecks()
    .AddNpgSql(
        configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Missing PostgreSQL connection string"),
        name: "postgres")
    .AddRedis(
        configuration.GetConnectionString("Redis")
            ?? throw new InvalidOperationException("Missing Redis connection string"),
        name: "redis")
    .AddRabbitMQ(name: "rabbitmq");
```

Nếu overload RabbitMQ của phiên bản package không tự lấy `IConnection`, truyền `sp.GetRequiredService<IConnection>()` theo API được IDE hiển thị. Không tạo thêm một connection RabbitMQ cho mỗi request.

Trong `UseWebApi`, map endpoint trước `return app`:

```csharp
app.MapHealthChecks("/health");
```

Endpoint `/health` không được đặt sau authorization bắt buộc và không trả secret/configuration.

### 2.7 Quy tắc migration để rollback image không phá database

Ứng dụng hiện gọi `Database.Migrate()` khi khởi động. `deploy.sh` chỉ có thể rollback image; nó **không tự rollback PostgreSQL**. Vì vậy mọi migration bình thường phải tương thích với cả image mới và image liền trước.

Áp dụng quy trình expand/contract:

1. **Expand:** thêm bảng/cột/index mới; cột mới nên nullable hoặc có default an toàn. Không xóa/đổi tên cột mà code cũ còn đọc.
2. Deploy code mới có thể làm việc với cả schema cũ lẫn schema đã expand.
3. Backfill dữ liệu bằng job/script riêng, theo batch nhỏ; theo dõi CPU/RAM/lock trên gói 1 GB.
4. Chỉ ở một release sau, khi chắc chắn không còn image cũ dùng schema cũ, mới **contract**: bỏ cột/index cũ hoặc thêm constraint bắt buộc.
5. Migration có `DropTable`, `DropColumn`, đổi type có thể mất dữ liệu, tạo index lớn hoặc SQL thủ công không được auto-deploy. Phải maintenance, backup off-site và chạy/test riêng.

Trước mỗi pull request có migration:

```powershell
dotnet ef migrations script --idempotent `
  --project SMEFLOWSystem.Infrastructure `
  --startup-project SMEFLOWSystem.WebAPI `
  --output migration-review.sql
rg -n -i 'DROP TABLE|DROP COLUMN|ALTER COLUMN|DELETE FROM|TRUNCATE' migration-review.sql
```

Mở và review toàn bộ `migration-review.sql`; tìm thấy từ khóa nguy hiểm không có nghĩa chắc chắn sai, nhưng bắt buộc phải giải thích và có kế hoạch backup/rollback dữ liệu. Không commit file có connection string hoặc secret.

### 2.8 Docker Compose production hoàn chỉnh

Thay `docker-compose.yml` bằng cấu hình dưới. Image API được CI build và push lên GHCR; local vẫn có thể build bằng override ở phần sau.

```yaml
services:
  postgres:
    image: postgres:16-alpine
    container_name: dodo-postgres
    command: ["postgres", "-c", "shared_buffers=48MB", "-c", "max_connections=25", "-c", "work_mem=1MB", "-c", "maintenance_work_mem=24MB"]
    environment:
      POSTGRES_DB: ${POSTGRES_DB}
      POSTGRES_USER: ${POSTGRES_USER}
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD}
    volumes:
      - postgres_data:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U $${POSTGRES_USER} -d $${POSTGRES_DB}"]
      interval: 10s
      timeout: 5s
      retries: 10
      start_period: 20s
    restart: unless-stopped
    mem_limit: 224m
    shm_size: 64mb
    cpus: 0.60
    pids_limit: 200
    security_opt:
      - no-new-privileges:true

  redis:
    image: redis:7-alpine
    container_name: dodo-redis
    command: ["redis-server", "--appendonly", "yes", "--maxmemory", "32mb", "--maxmemory-policy", "noeviction"]
    volumes:
      - redis_data:/data
    healthcheck:
      test: ["CMD", "redis-cli", "ping"]
      interval: 10s
      timeout: 3s
      retries: 10
    restart: unless-stopped
    mem_limit: 80m
    cpus: 0.20
    pids_limit: 100
    security_opt:
      - no-new-privileges:true

  rabbitmq:
    image: rabbitmq:4.3.2-alpine
    container_name: dodo-rabbitmq
    environment:
      RABBITMQ_DEFAULT_USER: ${RABBITMQ_USER}
      RABBITMQ_DEFAULT_PASS: ${RABBITMQ_PASSWORD}
    volumes:
      - rabbitmq_data:/var/lib/rabbitmq
      - ./rabbitmq.conf:/etc/rabbitmq/conf.d/99-dodo.conf:ro
    healthcheck:
      test: ["CMD", "rabbitmq-diagnostics", "-q", "ping"]
      interval: 15s
      timeout: 5s
      retries: 10
      start_period: 30s
    restart: unless-stopped
    mem_limit: 192m
    cpus: 0.30
    pids_limit: 300
    security_opt:
      - no-new-privileges:true

  webapi:
    image: ghcr.io/${GHCR_OWNER}/dodosystem-api:${IMAGE_TAG:?IMAGE_TAG is required}
    container_name: dodo-webapi
    env_file:
      - .env
    environment:
      ASPNETCORE_ENVIRONMENT: Production
      ASPNETCORE_URLS: http://+:8080
      ConnectionStrings__DefaultConnection: Host=postgres;Port=5432;Database=${POSTGRES_DB};Username=${POSTGRES_USER};Password=${POSTGRES_PASSWORD};Pooling=true;Minimum Pool Size=0;Maximum Pool Size=10;Timeout=15;Command Timeout=30
      ConnectionStrings__Redis: redis:6379,abortConnect=false
      RabbitMQ__Host: rabbitmq
      RabbitMQ__Port: 5672
      RabbitMQ__Username: ${RABBITMQ_USER}
      RabbitMQ__Password: ${RABBITMQ_PASSWORD}
    ports:
      - "127.0.0.1:8085:8080"
    depends_on:
      postgres:
        condition: service_healthy
      redis:
        condition: service_healthy
      rabbitmq:
        condition: service_healthy
    healthcheck:
      test: ["CMD-SHELL", "wget -qO- http://127.0.0.1:8080/health >/dev/null || exit 1"]
      interval: 30s
      timeout: 5s
      retries: 5
      start_period: 60s
    restart: unless-stopped
    mem_limit: 320m
    cpus: 1.00
    pids_limit: 250
    stop_grace_period: 30s
    security_opt:
      - no-new-privileges:true

volumes:
  postgres_data:
  redis_data:
  rabbitmq_data:

networks:
  default:
    name: dodo-private
    ipam:
      config:
        - subnet: 172.30.0.0/24
          gateway: 172.30.0.1
```

Không thêm `ports` cho PostgreSQL, Redis hoặc RabbitMQ.

Các giới hạn trên là profile bắt buộc cho VPS 1 GB: tổng trần container là **816 MB**, chừa khoảng 200 MB danh nghĩa cho Ubuntu, Docker, Nginx và page cache. `mem_limit` là hard limit chứ không phải lượng RAM đặt trước; container vẫn có thể bị OOM khi chạm trần. Swap chỉ đỡ spike ngắn. Không tăng pool, batch, worker hoặc prefetch trước khi đo `docker stats`, `free -h` và `vmstat` ít nhất vài ngày.

Tạo/commit `rabbitmq.conf` cạnh `docker-compose.yml`:

```ini
vm_memory_high_watermark.absolute = 128MiB
disk_free_limit.absolute = 300MB
```

Trong container không dùng watermark tương đối vì RabbitMQ có thể phát hiện cgroup limit khác dự kiến. Watermark là ngưỡng chặn publisher, không phải hard limit; `mem_limit: 192m` vẫn là lớp bảo vệ cuối.

Guide pin RabbitMQ 4.3.2 vì đây là bản 4.3 mới nhất còn community support tại ngày kiểm tra; dòng 3.13 đã hết community support. Dự án hiện dùng `RabbitMQ.Client` 6.8.1 với AMQP 0-9-1, cần smoke test publish/consume ở Phần 2.10 trước khi deploy. Theo dõi [RabbitMQ release information](https://www.rabbitmq.com/release-information); chỉ đổi tag broker sau khi đã đọc release notes và test để lần pull không tự đổi phiên bản ngoài ý muốn.

Guide này giữ profile 1 GB/$7. Phần 9 chỉ là phương án thoát khi có dấu hiệu thiếu RAM; không tự tăng các limit khi VPS vẫn là 1 GB.

Giảm Hangfire worker cho đúng gói $7. Trong `SMEFLOWSystem.WebAPI/Extensions/DependencyInjection.cs`, thay `services.AddHangfireServer();` bằng:

```csharp
services.AddHangfireServer(options =>
{
    options.WorkerCount = configuration.GetValue<int?>("Hangfire:WorkerCount") ?? 2;
});
```

Hai worker phù hợp điểm khởi đầu cho 2 vCPU/1 GB. Không tăng khi chưa đo RAM và thời gian hoàn tất job.

Lưu ý: healthcheck API trên dùng `wget`. Image ASP.NET runtime cần có binary này. Thêm vào final stage của Dockerfile:

```dockerfile
RUN apt-get update \
    && apt-get install -y --no-install-recommends wget \
    && rm -rf /var/lib/apt/lists/*
```

Nếu runtime base image không dùng Debian/apt, thay healthcheck bằng một executable có sẵn trong image.

### 2.9 Tạo `.env.example`

Commit file `.env.example` sau, chỉ có placeholder:

```dotenv
GHCR_OWNER=your-github-owner-lowercase
IMAGE_TAG=bootstrap-not-deployed

POSTGRES_DB=dodosystem
POSTGRES_USER=dodoapp
POSTGRES_PASSWORD=CHANGE_ME_RANDOM_32_CHARS

RABBITMQ_USER=dodoapp
RABBITMQ_PASSWORD=CHANGE_ME_RANDOM_32_CHARS

Jwt__Issuer=SMEFLOW_Server
Jwt__Audience=SMEFLOW_Client
Jwt__Secret=CHANGE_ME_RANDOM_64_CHARS
Jwt__AccessTokenExpiryMinutes=60
Jwt__RefreshTokenExpiryDays=7

EmailSettings__SmtpHost=smtp.gmail.com
EmailSettings__SmtpPort=587
EmailSettings__SmtpUsername=CHANGE_ME
EmailSettings__SmtpPassword=CHANGE_ME
EmailSettings__UseSsl=true
EmailSettings__FromName=DodoSystem
EmailSettings__FromEmail=CHANGE_ME

Cloudinary__CloudName=CHANGE_ME
Cloudinary__ApiKey=CHANGE_ME
Cloudinary__ApiSecret=CHANGE_ME

FacePlusPlus__BaseUrl=https://api-us.faceplusplus.com
FacePlusPlus__ApiKey=CHANGE_ME
FacePlusPlus__ApiSecret=CHANGE_ME
FacePlusPlus__ConfidenceThreshold=80

Payment__Mode=Sandbox
Payment__Gateway=VNPay
Payment__FrontendUrl=https://YOUR_FRONTEND_DOMAIN
Payment__VNPay__TmnCode=CHANGE_ME_SANDBOX
Payment__VNPay__HashSecret=CHANGE_ME_SANDBOX
Payment__VNPay__BaseUrl=https://sandbox.vnpayment.vn/paymentv2/vpcpay.html
Payment__VNPay__CallbackUrl=/api/payment/callback/vnpay
Payment__SePay__ApiKey=SET_WHEN_SWITCHING_TO_SEPAY
Payment__SePay__WebhookSecret=SET_WHEN_SWITCHING_TO_SEPAY
Payment__SePay__BankAccountNumber=SET_WHEN_SWITCHING_TO_SEPAY
Payment__SePay__BankAccountName=SET_WHEN_SWITCHING_TO_SEPAY
Payment__SePay__BankCode=SET_WHEN_SWITCHING_TO_SEPAY
Payment__SePay__PaymentContentPrefix=DODO

Cors__AllowedOrigins__0=https://YOUR_FRONTEND_DOMAIN
Invite__OnboardingUrl=https://YOUR_FRONTEND_DOMAIN/onboard
AllowedHosts=YOUR_API_DOMAIN;localhost;127.0.0.1

Hangfire__WorkerCount=2

AttendanceResolution__Enabled=true
AttendanceResolution__Cron=*/15 * * * *
AttendanceResolution__BatchSize=100
AttendanceResolution__DedupWindowMinutes=2
AttendanceResolution__MaxBatchesPerRun=3

RabbitMQ__VirtualHost=/
RabbitMQ__RequestedHeartbeat=30
RabbitMQ__AutomaticRecoveryEnabled=true
RabbitMQ__NetworkRecoveryIntervalSeconds=5
RabbitMQ__PrefetchCount=10
RabbitMQ__Exchange=smeflow.exchange
RabbitMQ__ExchangeType=topic
RabbitMQ__Durable=true
RabbitMQ__Queues__PaymentSucceeded=payment.succeeded.queue
RabbitMQ__Queues__SendEmail=email.queue
RabbitMQ__Queues__Payroll=payroll.queue
RabbitMQ__Queues__Attendance=attendance.queue
RabbitMQ__RoutingKeys__PaymentSucceeded=payment.succeeded
RabbitMQ__RoutingKeys__SendEmail=email.send
RabbitMQ__RoutingKeys__Payroll=payroll.process
RabbitMQ__RoutingKeys__Attendance=attendance.event
```

Tên email ở đây khớp chính xác với `ValidateConfiguration`: `SmtpHost`, `SmtpUsername`, `FromName`, `FromEmail`. Mặc định payment là VNPay sandbox; không chuyển `Payment__Mode=Production` hoặc `Payment__Gateway=SePay` trước khi hoàn tất HTTPS, credential thật và smoke test webhook ở Phần 7.

### 2.10 Test local bắt buộc

Trước khi chạy Docker, restore/build và kiểm tra dependency. Không bỏ qua cảnh báo `NU1901`–`NU1904`; repository hiện cần xử lý các advisory của AutoMapper/MailKit trước go-live:

```powershell
dotnet restore SMEFLOWSystem.sln
dotnet build SMEFLOWSystem.sln -c Release --no-restore
dotnet list SMEFLOWSystem.sln package --vulnerable --include-transitive
```

Solution phải có ít nhất một test project thật. `dotnet test` có thể trả exit code 0 khi không có test project, nên kiểm tra trước:

```powershell
$testProjects = @(Get-ChildItem -Recurse -Filter '*Tests.csproj')
if ($testProjects.Count -eq 0) { throw 'Chưa có test project; CI xanh lúc này không chứng minh ứng dụng đã được test.' }
dotnet test SMEFLOWSystem.sln -c Release --no-build --verbosity normal
```

Test tối thiểu nên bao phủ login/refresh token, tenant filter, tạo billing order, callback/webhook idempotency và một repository chạy trên PostgreSQL thật. Không dùng InMemory provider để thay cho toàn bộ integration test PostgreSQL.

Tạo `.dockerignore` để secret và file thừa không bị gửi vào Docker build context:

```dockerignore
**/bin
**/obj
.git
.github
.env
.env.*
!.env.example
backups
Claude-Plans
TestResults
```

Tạo `docker-compose.override.yml` chỉ dùng local và không deploy file này lên server nếu nó thay production image:

```yaml
services:
  webapi:
    build:
      context: .
      dockerfile: SMEFLOWSystem.WebAPI/Dockerfile
    image: dodosystem-api:local
```

Tạo `.env` từ `.env.example`, điền credential test, rồi chạy:

```powershell
Copy-Item .env.example .env
# Mở .env, điền credential sandbox/local. Không dùng credential production trên máy dev.
```

Khi test local, đặt `Payment__FrontendUrl=http://localhost:3000`, `Cors__AllowedOrigins__0=http://localhost:3000`, `Invite__OnboardingUrl=http://localhost:3000/onboard` và `AllowedHosts=localhost;127.0.0.1`. Khi tạo `.env` trên VPS ở Phần 5, thay lại bằng domain HTTPS thật.

```bash
docker compose up -d --build
docker compose ps
docker compose logs --tail=200 webapi
curl.exe http://localhost:8085/health
```

Không tiếp tục nếu:

- Bất kỳ container nào `unhealthy` hoặc restart loop.
- Migration lỗi.
- `/health` không trả `Healthy` và HTTP 200.
- RabbitMQ báo `PLAIN login refused`.

Smoke test tối thiểu:

1. Register/login và refresh token.
2. Tạo tenant, employee, shift.
3. Kiểm tra Hangfire recurring jobs được đăng ký.
4. Publish và consume một RabbitMQ event.
5. Mở kết nối SignalR.
6. Tạo payment sandbox trước khi bật production.
7. Restart toàn bộ Compose và xác nhận dữ liệu vẫn còn.
8. `docker stats --no-stream` không có container chạm sát `mem_limit` khi idle.
9. `docker compose logs webapi` không có cảnh báo unknown proxy, migration retry vô hạn hoặc secret bị log ra.

---

## 3. Đăng ký AWS và tạo Lightsail $7 từ đầu

Thông số và giá trong phần này được kiểm tra ngày **12/07/2026** theo [bảng Lightsail bundles chính thức](https://docs.aws.amazon.com/lightsail/latest/userguide/amazon-lightsail-bundles.html). AWS có thể đổi giá hoặc quota; trước khi bấm **Create instance**, kiểm tra lại giá hiển thị trong console.

### 3.1 Tạo và bảo vệ tài khoản AWS

1. Vào [trang đăng ký AWS](https://portal.aws.amazon.com/billing/signup), nhập email chưa dùng cho AWS, tên tài khoản, số điện thoại và thẻ thanh toán. AWS có thể xác minh danh tính/thẻ; hoàn tất đến khi đăng nhập được AWS Console.
2. Đăng nhập root đúng một lần để bật MFA: menu tài khoản → **Security credentials** → **Multi-factor authentication**.
3. Tạo người dùng quản trị để làm việc hằng ngày bằng IAM/Identity Center; không tạo access key cho root và không dùng root để deploy.
4. Vào **Billing and Cost Management → Budgets → Create budget → Cost budget**. Đặt ngân sách 10 USD/tháng lúc đang dùng gói 7 USD, cảnh báo email ở 80%, 100% và forecast 100%. Budget chỉ cảnh báo, **không tự chặn chi tiêu**.
5. Mở Lightsail tại <https://lightsail.aws.amazon.com/>. Nếu AWS yêu cầu bật Region hoặc hoàn tất xác minh tài khoản, làm xong rồi mới tiếp tục.

Không dựa vào quảng cáo free trial để tính ngân sách: eligibility tùy tài khoản và thời điểm. Hóa đơn vẫn cần được theo dõi trong Billing.

### 3.2 Tạo instance mới

Trong Lightsail chọn **Create instance**:

- Region: Singapore `ap-southeast-1`.
- Availability Zone: để mặc định.
- Platform: Linux/Unix.
- Blueprint: **OS Only → Ubuntu 24.04 LTS**; nếu image/tool chưa tương thích thì dùng 22.04 LTS.
- Networking type: **Dual-stack/public IPv4** để dùng Static IPv4 và DNS A record theo guide này.
- Plan: **Micro-1GB Linux with public IPv4 — 7 USD/tháng**: 2 vCPU, 1 GB RAM, 40 GB SSD, 2 TB transfer.
- Name: `dodo-prod`.

Không chọn WordPress/LAMP/Node blueprint vì dự án dùng Docker Compose và cần một Ubuntu sạch. Đây là server đầu tiên; không restore snapshot và không chọn “create from snapshot”.

Ngay sau khi instance ở trạng thái Running:

1. Vào **Networking → Create static IP**, đặt tên `dodo-prod-ip` và attach vào `dodo-prod`.
2. Ghi lại Static IP. Static IP đang attach không tính phí; IP bỏ rời quá 1 giờ bị tính phí theo [Lightsail billing FAQ](https://docs.aws.amazon.com/lightsail/latest/userguide/amazon-lightsail-frequently-asked-questions-faq-billing-and-account-management.html).
3. Tải private key của Region hoặc tạo/upload SSH key riêng. Cất private key ngoài Git và không gửi cho người khác.

Lightsail firewall chỉ mở:

- TCP 22: giới hạn source IP cá nhân nếu có IP ổn định.
- TCP 80: mọi nơi.
- TCP 443: mọi nơi.

Không mở 5432, 6379, 5672, 15672 hoặc 8085.

Tạo DNS A record, ví dụ:

```text
api.example.com -> LIGHTSAIL_STATIC_IP
```

Chờ DNS resolve trước khi chạy Certbot:

```bash
nslookup api.example.com
```

Nếu chưa mua domain, vẫn có thể hoàn thành Docker và test bằng `http://STATIC_IP`, nhưng chưa thể cấp chứng chỉ Let's Encrypt chuẩn. Không bật payment production/webhook cho đến khi có domain HTTPS thật.

---

## 4. Hardening và cài phần mềm trên VPS

SSH vào VPS bằng key của Lightsail, sau đó:

```bash
sudo apt update
sudo apt upgrade -y
sudo apt install -y ca-certificates curl nginx certbot python3-certbot-nginx \
  fail2ban unattended-upgrades awscli
sudo install -m 0755 -d /etc/apt/keyrings
sudo curl -fsSL https://download.docker.com/linux/ubuntu/gpg \
  -o /etc/apt/keyrings/docker.asc
sudo chmod a+r /etc/apt/keyrings/docker.asc
. /etc/os-release
echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.asc] https://download.docker.com/linux/ubuntu ${UBUNTU_CODENAME:-$VERSION_CODENAME} stable" \
  | sudo tee /etc/apt/sources.list.d/docker.list >/dev/null
sudo apt update
sudo apt install -y docker-ce docker-ce-cli containerd.io \
  docker-buildx-plugin docker-compose-plugin
sudo systemctl enable --now docker
sudo usermod -aG docker ubuntu
```

Logout/login lại để group Docker có hiệu lực, rồi kiểm tra:

```bash
docker version
docker compose version
```

### 4.1 Tạo swap 2 GB

```bash
sudo fallocate -l 2G /swapfile
sudo chmod 600 /swapfile
sudo mkswap /swapfile
sudo swapon /swapfile
echo '/swapfile none swap sw 0 0' | sudo tee -a /etc/fstab
echo 'vm.swappiness=10' | sudo tee /etc/sysctl.d/99-dodo.conf
sudo sysctl --system
free -h
```

Không tăng swap để cố chữa việc container thường xuyên thiếu RAM. Nếu `si/so` liên tục cao, phải giảm workload hoặc nâng plan.

### 4.2 UFW, SSH và Fail2Ban

Phải allow SSH trước khi enable UFW:

```bash
sudo apt update
sudo apt install -y fail2ban unattended-upgrades
sudo ufw default deny incoming
sudo ufw default allow outgoing
sudo ufw allow OpenSSH
sudo ufw allow 80/tcp
sudo ufw allow 443/tcp
sudo ufw enable
sudo ufw status verbose
sudo systemctl enable --now fail2ban
sudo systemctl enable --now unattended-upgrades
```

Nếu `sudo ufw enable` hỏi `Command may disrupt existing ssh connections. Proceed with operation (y|n)?`, nhập `y`.

Mở một terminal thứ hai trên máy cá nhân và đăng nhập thử bằng SSH key trước khi tắt password login. Giữ terminal SSH hiện tại đang mở, không logout:

```bash
ssh -i /path/to/LightsailDefaultKey-ap-southeast-1.pem ubuntu@STATIC_IP
```

Thay `/path/to/LightsailDefaultKey-ap-southeast-1.pem` bằng đường dẫn file key thật và thay `STATIC_IP` bằng Static IP của Lightsail. Nếu terminal thứ hai đăng nhập được, quay lại terminal đầu tiên và tạo file cấu hình SSH:

```bash
sudo mkdir -p /etc/ssh/sshd_config.d
sudo tee /etc/ssh/sshd_config.d/99-dodo.conf >/dev/null <<'EOF'
PasswordAuthentication no
PermitRootLogin no
PubkeyAuthentication yes
EOF
```

Kiểm tra cấu hình SSH không lỗi cú pháp, reload SSH, rồi kiểm tra service:

```bash
sudo sshd -t
sudo systemctl reload ssh
sudo systemctl status ssh --no-pager
sudo systemctl status fail2ban --no-pager
```

Sau khi reload, mở thêm một terminal mới và SSH lại lần nữa. Nếu login bằng key vẫn vào được thì cấu hình ổn; nếu không vào được, terminal cũ vẫn còn mở để sửa lại file.

Docker có thể tạo iptables rule riêng không đi qua cách hiểu UFW thông thường. Vì vậy lớp bảo vệ chính ở guide là không publish DB/Redis/RabbitMQ và chỉ publish API vào `127.0.0.1`. Sau deploy luôn chạy `sudo ss -lntp`; nếu thấy `0.0.0.0:5432`, `6379`, `5672`, `15672` hoặc `8085` thì dừng go-live và sửa Compose.

### 4.3 Giới hạn Docker log

Tạo hoặc ghi đè `/etc/docker/daemon.json` bằng cấu hình giới hạn log:

```bash
sudo mkdir -p /etc/docker
sudo tee /etc/docker/daemon.json >/dev/null <<'EOF'
{
  "log-driver": "json-file",
  "log-opts": {
    "max-size": "10m",
    "max-file": "3"
  }
}
EOF
```

Kiểm tra file vừa tạo:

```bash
sudo cat /etc/docker/daemon.json
```

Restart Docker để áp dụng cấu hình. Lệnh này có thể làm container đang chạy bị gián đoạn ngắn, nên làm trước khi deploy production thật:

```bash
sudo systemctl restart docker
sudo systemctl status docker --no-pager
docker info --format '{{.LoggingDriver}}'
```

---

## 5. Chuẩn bị thư mục production, file vận hành và `.env`

Production VPS **không cần clone toàn bộ source code** để build. Quy trình đúng cho VPS 1 GB là:

1. GitHub Actions checkout source trên GitHub runner.
2. GitHub Actions build Docker image và push lên GHCR.
3. VPS chỉ giữ các file vận hành trong `/opt/dodo`: `docker-compose.yml`, `rabbitmq.conf`, `.env`, `deploy.sh`, `backup-postgres.sh`.
4. Mỗi lần push lên `main`, GitHub Actions SSH vào VPS và chạy `/opt/dodo/deploy.sh IMAGE_TAG`.

Không build image trực tiếp trên VPS 1 GB vì dễ thiếu RAM.

Trong Phần 5 phải dùng **hai cửa sổ terminal khác nhau**. Nhìn prompt trước mỗi lệnh để không chạy nhầm:

| Cửa sổ | Chạy ở đâu | Prompt thường thấy | Dùng để làm gì |
|---|---|---|---|
| A — Local | PowerShell trên máy Windows, tại thư mục repository | `PS D:\...\DodoSystem-BE>` | Kiểm tra và copy file bằng `scp` |
| B — VPS | Bash sau khi SSH vào Lightsail | `ubuntu@ip-...:~$` | Tạo `/opt/dodo`, `.env`, `deploy.sh`, chạy Docker |

Khi guide ghi **Windows PowerShell**, chỉ chạy ở cửa sổ A. Khi ghi **VPS**, chỉ chạy ở cửa sổ B. Không copy dấu prompt `PS ...>` hoặc `ubuntu@...$`; chỉ copy phần lệnh phía sau.

Ba file `docker-compose.yml`, `rabbitmq.conf`, `.env.example` **đã nằm trong repository local** sau khi hoàn thành Phần 2; ta copy chúng lên VPS. Hai file `.env` và `deploy.sh` **chưa có trên VPS**; ta sẽ tạo ở Phần 5.3 và 5.4. `backup-postgres.sh` được tạo sau ở Phần 8, chưa cần trong bước này.

### 5.1 Tạo thư mục `/opt/dodo` trên VPS

Nếu cửa sổ B chưa SSH vào VPS, mở PowerShell local, thay đường dẫn key/IP rồi đăng nhập:

```powershell
$KEY="$env:USERPROFILE\Downloads\LightsailDefaultKey-ap-southeast-1.pem"
$VPS_IP="STATIC_IP"
ssh -i $KEY "ubuntu@$VPS_IP"
```

Khi đăng nhập thành công, prompt đổi từ `PS ...>` thành dạng `ubuntu@ip-...:~$`. Từ đây các lệnh trong mục 5.1 chạy **trên VPS**. Xác nhận đúng user:

```bash
whoami
```

Kết quả phải là `ubuntu`. Sau đó chạy từng lệnh, có thể copy cả block:

```bash
sudo mkdir -p /opt/dodo/backups
sudo chown -R ubuntu:ubuntu /opt/dodo
chmod 700 /opt/dodo
cd /opt/dodo
pwd
stat -c '%A %U:%G %n' /opt/dodo /opt/dodo/backups
```

Ý nghĩa:

- `sudo mkdir -p /opt/dodo/backups`: tạo thư mục ứng dụng và thư mục chứa dump; `-p` cho phép chạy lại mà không lỗi nếu đã tồn tại.
- `sudo chown -R ubuntu:ubuntu /opt/dodo`: thư mục dưới `/opt` mặc định do `root` sở hữu. Lệnh này chuyển owner/group của `/opt/dodo` thành user `ubuntu` để `scp` và GitHub Actions có thể ghi file mà không dùng root. `-R` áp dụng cả thư mục con.
- `chmod 700 /opt/dodo`: chỉ user `ubuntu` được đọc/ghi/truy cập thư mục chứa `.env`.
- `cd` chuyển vào thư mục; `pwd` phải in `/opt/dodo`.
- `stat` phải cho thấy owner `ubuntu:ubuntu`; `/opt/dodo` có permission bắt đầu bằng `drwx------`.

Nếu `whoami` không phải `ubuntu`, thay `ubuntu:ubuntu` bằng đúng user SSH ở toàn bộ guide hoặc đăng nhập lại bằng user `ubuntu`; không chạy tiếp một cách lẫn lộn giữa nhiều owner.

### 5.2 Copy file vận hành từ máy Windows lên VPS

Giữ cửa sổ B đang SSH mở. Mở **một cửa sổ PowerShell mới trên Windows** làm cửa sổ A. Chuyển vào đúng repository; với cấu trúc hiện tại:

```powershell
Set-Location 'D:\Project\ProjectMonHoc\EXE101\DodoSystem-BE'
Get-Location
```

`Get-Location` phải kết thúc bằng `DodoSystem-BE`. Kiểm tra ba file nguồn thực sự tồn tại trước khi copy:

```powershell
$files = @('docker-compose.yml', 'rabbitmq.conf', '.env.example')
$files | ForEach-Object { "$_ = $(Test-Path -LiteralPath $_)" }
```

Cả ba dòng phải là `True`. Nếu có `False`, quay lại Phần 2 tạo/sửa file đó; không tạo file rỗng để tiếp tục.

Đặt biến key và IP trong **chính cửa sổ A này**:

```powershell
$KEY="$env:USERPROFILE\Downloads\LightsailDefaultKey-ap-southeast-1.pem"
$VPS_IP="STATIC_IP"
```

Sửa chuỗi `STATIC_IP` thành IP thật, ví dụ thao tác trong terminal là `$VPS_IP="203.0.113.10"`; `203.0.113.10` chỉ là IP tài liệu, không dùng nguyên ví dụ đó. Không đặt biến là `$HOST` vì `$Host` là biến hệ thống chỉ đọc của PowerShell. Kiểm tra key tồn tại và thử SSH không tương tác:

```powershell
Test-Path -LiteralPath $KEY
ssh -i $KEY "ubuntu@$VPS_IP" "echo ssh-ok"
```

`Test-Path` phải là `True`, lệnh SSH phải in `ssh-ok`. Lần đầu kết nối có thể hỏi xác nhận host fingerprint; chỉ nhập `yes` sau khi IP đúng và fingerprint đã được đối chiếu theo Phần 6.1.

Vẫn ở cửa sổ A và thư mục repository, copy ba file:

```powershell
scp -i $KEY docker-compose.yml rabbitmq.conf .env.example "ubuntu@${VPS_IP}:/opt/dodo/"
```

Đọc lệnh từ trái sang phải:

- `scp -i $KEY`: dùng private key Lightsail để copy qua SSH.
- `docker-compose.yml rabbitmq.conf .env.example`: ba file nguồn trên Windows.
- `ubuntu@${VPS_IP}:/opt/dodo/`: thư mục đích trên VPS.

Kết quả đúng là PowerShell hiển thị tiến độ `100%` cho từng file và quay lại prompt, không có `Permission denied`. Nếu báo `No such file`, kiểm tra lại `Get-Location`/`Test-Path`. Nếu báo permission ở `/opt/dodo`, quay lại cửa sổ B và chạy lại `sudo chown -R ubuntu:ubuntu /opt/dodo`. Nếu báo private key permission, sửa quyền file `.pem` theo phần SSH rồi thử lại; không dùng password SSH thay thế.

Quay lại **cửa sổ B trên VPS**, kiểm tra file đã lên và đặt permission đọc cho file cấu hình:

```bash
cd /opt/dodo
chmod 644 docker-compose.yml rabbitmq.conf .env.example
ls -lah docker-compose.yml rabbitmq.conf .env.example
```

Kết quả tối thiểu phải có:

```text
docker-compose.yml
rabbitmq.conf
.env.example
```

Mỗi file phải có dung lượng lớn hơn 0. Kiểm tra thêm:

```bash
test -s docker-compose.yml && echo compose-ok
test -s rabbitmq.conf && echo rabbitmq-ok
test -s .env.example && echo env-example-ok
```

Phải thấy đủ ba dòng `*-ok`. `scp` chỉ copy snapshot hiện tại; từ Phần 6 trở đi CI sẽ tự upload `docker-compose.yml` và `rabbitmq.conf` của commit mới. CI không bao giờ ghi đè `.env` production.

### 5.3 Tạo `/opt/dodo/.env` production

Mục này chạy hoàn toàn trong **cửa sổ B trên VPS**. `.env.example` là mẫu được phép commit; `.env` là bản thật chứa secret và không được copy ngược về Git.

Trước hết tạo `.env` nếu nó chưa tồn tại. Block dưới cố ý không ghi đè khi bạn chạy lại guide:

```bash
cd /opt/dodo
if [ -f .env ]; then
  echo '.env đã tồn tại — không ghi đè'
else
  cp .env.example .env
  echo 'đã tạo .env từ .env.example'
fi
chmod 600 .env
stat -c '%A %U:%G %n' .env
```

Kết quả permission phải là `-rw-------` và owner `ubuntu:ubuntu`. Nếu owner là `root`, sửa bằng:

```bash
sudo chown ubuntu:ubuntu /opt/dodo/.env
chmod 600 /opt/dodo/.env
```

Tạo ba secret nội bộ ngay trên VPS. Mỗi lệnh in ra một dòng; copy phần sau dấu `=` vào đúng dòng trong `.env`. Không dùng lại cùng một giá trị cho nhiều mục:

```bash
printf 'POSTGRES_PASSWORD='; openssl rand -hex 24
printf 'RABBITMQ_PASSWORD='; openssl rand -hex 24
printf 'Jwt__Secret='; openssl rand -hex 64
```

Ví dụ output có dạng `POSTGRES_PASSWORD=8f...`; đây chỉ hiển thị ở terminal, không tự sửa file. Sau khi đã giữ các giá trị để paste, mở file:

```bash
nano /opt/dodo/.env
```

Trong Nano:

1. Dùng phím mũi tên để di chuyển; `Ctrl+W` để tìm `CHANGE_ME` hoặc tên key.
2. Xóa phần placeholder bên phải dấu `=` và paste giá trị thật. Không thêm khoảng trắng quanh dấu `=`.
3. Lưu bằng `Ctrl+O`, nhấn `Enter` xác nhận tên `/opt/dodo/.env`.
4. Thoát bằng `Ctrl+X`.

Không chạy `sudo nano`: file phải tiếp tục thuộc user `ubuntu`. Nếu paste nhầm, mở lại bằng `nano /opt/dodo/.env`.

Trong `.env`, sửa ít nhất các dòng sau:

```env
GHCR_OWNER=your-github-owner-lowercase
IMAGE_TAG=bootstrap-not-deployed
POSTGRES_PASSWORD=CHANGE_ME_RANDOM_32_CHARS
RABBITMQ_PASSWORD=CHANGE_ME_RANDOM_32_CHARS
Jwt__Secret=CHANGE_ME_RANDOM_64_CHARS
Payment__Mode=Sandbox
Payment__Gateway=VNPay
Payment__FrontendUrl=https://YOUR_FRONTEND_DOMAIN
Payment__VNPay__TmnCode=CHANGE_ME_SANDBOX
Payment__VNPay__HashSecret=CHANGE_ME_SANDBOX
Payment__VNPay__BaseUrl=https://sandbox.vnpayment.vn/paymentv2/vpcpay.html
Payment__VNPay__CallbackUrl=/api/payment/callback/vnpay
Cors__AllowedOrigins__0=https://YOUR_FRONTEND_DOMAIN
Invite__OnboardingUrl=https://YOUR_FRONTEND_DOMAIN/onboard
AllowedHosts=YOUR_API_DOMAIN;localhost;127.0.0.1
```

Nguồn của từng nhóm giá trị:

| Key/nhóm key | Lấy ở đâu hoặc điền thế nào |
|---|---|
| `GHCR_OWNER` | Phần owner trong URL GitHub. Repo `https://github.com/LongTH/DodoSystem-BE` dùng `longth`; phải viết thường. |
| `IMAGE_TAG` | Giữ nguyên `bootstrap-not-deployed`; CI thay bằng commit SHA khi deploy đầu tiên. |
| `POSTGRES_PASSWORD`, `RABBITMQ_PASSWORD`, `Jwt__Secret` | Dùng ba output `openssl` vừa tạo. |
| `EmailSettings__*` | Gmail address và Gmail App Password. App Password thường hiển thị theo nhóm; paste liền, bỏ khoảng trắng. Không dùng mật khẩu Gmail đăng nhập thông thường. |
| `Cloudinary__*` | Cloudinary Console → API Keys; dùng cloud name, API key, API secret của tài khoản dự án. |
| `FacePlusPlus__*` | Face++ Console → API Key; giữ `BaseUrl` đúng region của tài khoản. |
| `Payment__VNPay__TmnCode`, `HashSecret` | Credential **sandbox** do VNPay cấp. Không copy giá trị từng nằm trong `appsettings.json`; phải rotate nếu đã lộ. |
| `Payment__FrontendUrl` | Origin frontend thật, ví dụ `https://app.example.com`, không có dấu `/` cuối. |
| `Cors__AllowedOrigins__0` | Giống origin frontend; không điền `*` vì ứng dụng cho phép credentials. |
| `Invite__OnboardingUrl` | URL onboarding đầy đủ, ví dụ `https://app.example.com/onboard`. |
| `AllowedHosts` | Domain API không gồm `https://`, ví dụ `api.example.com;localhost;127.0.0.1`. Giữ localhost/127.0.0.1 cho health check nội bộ. |
| `Payment__SePay__*` | Khi đang dùng VNPay sandbox, được giữ `SET_WHEN_SWITCHING_TO_SEPAY`. Điền credential thật trước khi đổi gateway sang SePay. |

Ví dụ nếu frontend là `https://app.example.com` và API là `https://api.example.com`:

```env
Payment__FrontendUrl=https://app.example.com
Cors__AllowedOrigins__0=https://app.example.com
Invite__OnboardingUrl=https://app.example.com/onboard
AllowedHosts=api.example.com;localhost;127.0.0.1
```

Không copy nguyên domain ví dụ; thay bằng domain của bạn.

`GHCR_OWNER` phải là username hoặc organization GitHub viết thường, ví dụ nếu repo là `LongTH/DodoSystem-BE` thì dùng:

```env
GHCR_OWNER=longth
```

Sau khi lưu Nano, kiểm tra placeholder mà không in toàn bộ secret:

```bash
grep -nE 'CHANGE_ME|YOUR_|your-github-owner' /opt/dodo/.env || echo 'không còn placeholder bắt buộc'
grep -E '^(GHCR_OWNER|IMAGE_TAG|Payment__Mode|Payment__Gateway|AllowedHosts)=' /opt/dodo/.env
stat -c '%a %U:%G %n' /opt/dodo/.env
```

Kết quả đúng:

- Lệnh đầu in `không còn placeholder bắt buộc`.
- Lệnh thứ hai chỉ in các giá trị không phải secret để bạn kiểm tra mode/gateway/domain.
- `stat` bắt đầu bằng `600 ubuntu:ubuntu`.

Nếu lệnh đầu còn dòng, mở lại Nano và sửa. `IMAGE_TAG=bootstrap-not-deployed` được giữ trước deploy đầu tiên. Các dòng `SET_WHEN_SWITCHING_TO_SEPAY` được phép giữ khi gateway đang là VNPay sandbox.

Cuối cùng kiểm tra Compose đọc được `.env` nhưng không in config đã interpolate:

```bash
cd /opt/dodo
docker compose config --quiet && echo 'compose-env-ok'
```

Phải thấy `compose-env-ok`. Nếu báo `variable is not set`, mở `.env` và thêm key bị thiếu. Không dùng `docker compose config` không có `--quiet` trong log/chat vì output đầy đủ có thể chứa secret. Không dùng `cat .env`, không chụp màn hình và không gửi file này cho người khác.

### 5.4 Tạo `/opt/dodo/deploy.sh`

Mục này vẫn chạy trong **cửa sổ B trên VPS**. Bạn không phải tự gõ từng dòng script và cũng không cần tạo file trước. Lệnh `tee deploy.sh <<'EOF'` sẽ tạo/ghi file; mọi dòng giữa hai mốc được đưa vào file cho đến khi Bash gặp dòng `EOF` đứng riêng.

Cách làm:

1. Chạy `cd /opt/dodo && pwd`; kết quả phải là `/opt/dodo`.
2. Copy **toàn bộ code block dài bên dưới**, bắt đầu từ `cd /opt/dodo` và kết thúc bằng `chmod 700 /opt/dodo/deploy.sh`.
3. Paste một lần vào terminal VPS rồi chờ prompt `ubuntu@...$` xuất hiện lại.
4. Dòng đóng `EOF` phải nằm đầu dòng, không có khoảng trắng trước/sau. Nếu terminal hiện prompt phụ `>` mãi, nhấn `Ctrl+C` và paste lại toàn bộ block; lần chạy lại sẽ ghi đè file chưa hoàn chỉnh.

Nên dùng SSH từ Windows Terminal thay vì ô browser SSH nếu trình duyệt có vấn đề khi paste block dài.

```bash
cd /opt/dodo
tee deploy.sh >/dev/null <<'EOF'
#!/usr/bin/env bash
set -Eeuo pipefail
umask 077

ROOT=/opt/dodo
cd "$ROOT"

NEW_TAG="${1:?Usage: ./deploy.sh 40_CHAR_GIT_SHA [RELEASE_DIR]}"
RELEASE_DIR="${2:-}"

if [[ ! "$NEW_TAG" =~ ^[0-9a-f]{40}$ ]]; then
  echo "IMAGE_TAG phải là Git commit SHA đủ 40 ký tự." >&2
  exit 2
fi

test -f .env
grep -q '^IMAGE_TAG=' .env

OLD_TAG="$(grep '^IMAGE_TAG=' .env | cut -d= -f2- || true)"
HAS_OLD=false
if [[ "$OLD_TAG" =~ ^[0-9a-f]{40}$ && "$OLD_TAG" != "$NEW_TAG" ]]; then
  HAS_OLD=true
fi

CONFIG_BACKUP="$(mktemp -d "$ROOT/.rollback-config.XXXXXX")"
CONFIG_CHANGED=false
PREDEPLOY_BACKUP=""

set_tag() {
  sed -i "s/^IMAGE_TAG=.*/IMAGE_TAG=$1/" .env
}

rollback() {
  local exit_code=$?
  trap - ERR
  set +e
  echo "Deploy thất bại. Bắt đầu rollback cấu hình/image..." >&2

  if [[ "$CONFIG_CHANGED" == true ]]; then
    cp "$CONFIG_BACKUP/docker-compose.yml" "$ROOT/docker-compose.yml"
    cp "$CONFIG_BACKUP/rabbitmq.conf" "$ROOT/rabbitmq.conf"
    chmod 644 "$ROOT/docker-compose.yml" "$ROOT/rabbitmq.conf"
  fi

  if [[ "$HAS_OLD" == true ]]; then
    set_tag "$OLD_TAG"
    docker compose config --quiet
    docker compose up -d postgres redis rabbitmq
    docker compose pull webapi
    docker compose up -d --no-deps webapi

    for _ in $(seq 1 24); do
      if curl --fail --silent http://127.0.0.1:8085/health >/dev/null; then
        echo "Đã rollback về image $OLD_TAG" >&2
        break
      fi
      sleep 5
    done
  else
    echo "Đây là deploy đầu tiên, chưa có image cũ hợp lệ để rollback." >&2
    docker compose stop webapi >/dev/null 2>&1 || true
    set_tag "bootstrap-not-deployed"
  fi

  if [[ -n "$PREDEPLOY_BACKUP" ]]; then
    echo "Giữ backup trước deploy tại: $PREDEPLOY_BACKUP" >&2
  fi
  echo "Không tự restore database. Xem log và Phần 13 trước khi restore thủ công." >&2
  exit "$exit_code"
}
trap rollback ERR

if [[ -n "$RELEASE_DIR" ]]; then
  case "$RELEASE_DIR" in
    /opt/dodo/incoming/*) ;;
    *) echo "RELEASE_DIR không hợp lệ." >&2; exit 2 ;;
  esac

  test -f "$RELEASE_DIR/docker-compose.yml"
  test -f "$RELEASE_DIR/rabbitmq.conf"
  cp "$ROOT/docker-compose.yml" "$CONFIG_BACKUP/docker-compose.yml"
  cp "$ROOT/rabbitmq.conf" "$CONFIG_BACKUP/rabbitmq.conf"
  install -m 644 "$RELEASE_DIR/docker-compose.yml" "$ROOT/docker-compose.yml"
  install -m 644 "$RELEASE_DIR/rabbitmq.conf" "$ROOT/rabbitmq.conf"
  CONFIG_CHANGED=true
fi

docker compose config --quiet
docker compose up -d postgres redis rabbitmq

for service in postgres redis rabbitmq; do
  healthy=false
  for _ in $(seq 1 30); do
    container_id="$(docker compose ps -q "$service")"
    status="$(docker inspect --format '{{if .State.Health}}{{.State.Health.Status}}{{else}}{{.State.Status}}{{end}}' "$container_id")"
    if [[ "$status" == "healthy" || "$status" == "running" ]]; then
      healthy=true
      break
    fi
    sleep 2
  done
  [[ "$healthy" == true ]] || { echo "$service chưa healthy" >&2; false; }
done

STAMP="$(date -u +%Y-%m-%dT%H-%M-%SZ)"
PREDEPLOY_BACKUP="$ROOT/backups/predeploy-${STAMP}.dump"
docker compose exec -T postgres sh -c \
  'pg_dump -U "$POSTGRES_USER" -d "$POSTGRES_DB" -Fc' > "$PREDEPLOY_BACKUP"
test -s "$PREDEPLOY_BACKUP"

set_tag "$NEW_TAG"
docker compose config --quiet
docker compose pull webapi
docker compose up -d --no-deps webapi

for attempt in $(seq 1 24); do
  if curl --fail --silent http://127.0.0.1:8085/health >/dev/null; then
    trap - ERR
    find "$ROOT/backups" -type f -name 'predeploy-*.dump' -mtime +7 -delete
    rm -rf "$CONFIG_BACKUP"
    if [[ -n "$RELEASE_DIR" ]]; then
      rm -rf "$RELEASE_DIR"
    fi
    echo "Deploy $NEW_TAG thành công"
    echo "Backup trước deploy: $PREDEPLOY_BACKUP"
    exit 0
  fi
  sleep 5
done

echo "Health check timeout" >&2
false
EOF

chmod 700 /opt/dodo/deploy.sh
```

Sau khi prompt trở lại, kiểm tra file đã được tạo đúng:

```bash
cd /opt/dodo
ls -lh deploy.sh
head -n 1 deploy.sh
bash -n deploy.sh && echo 'deploy-script-syntax-ok'
test -x deploy.sh && echo 'deploy-script-executable-ok'
```

Kết quả đúng:

- `ls` hiển thị permission dạng `-rwx------` và owner `ubuntu`.
- `head` in `#!/usr/bin/env bash`.
- Có hai dòng `deploy-script-*-ok`.

Nếu `bash -n` in lỗi kèm số dòng, file thường bị thiếu khi paste. Không sửa mò từng đoạn: chạy lại nguyên code block tạo script, rồi kiểm tra lại.

Script này thực hiện lần lượt: kiểm tra SHA → nhận Compose/RabbitMQ config mới từ CI → validate config → start PostgreSQL/Redis/RabbitMQ → dump PostgreSQL trước deploy → pull image SHA → start API → chờ `/health` → rollback image/config khi lỗi. Script không tự restore database vì thao tác đó có thể xóa write mới.

Chưa chạy `./deploy.sh` ở bước này vì GHCR có thể chưa có image SHA. Lần deploy đầu tiên sẽ do workflow Phần 6 gọi sau khi build/push image thành công.

Xác thực Compose lần cuối mà không in config đã interpolate vào log công khai:

```bash
cd /opt/dodo
docker compose config --quiet
```

Nếu lệnh này lỗi kiểu thiếu biến môi trường, sửa `/opt/dodo/.env` rồi chạy lại. Backup `predeploy-*.dump` chỉ nằm cùng VPS nên không thay thế S3 backup. Script cố ý không tự restore database khi rollback: tự động restore có thể ghi đè dữ liệu mới và chỉ an toàn sau khi con người xác định migration nào đã chạy.

### 5.5 Login GHCR một lần trên VPS

GHCR là nơi GitHub Actions lưu Docker image. GitHub Actions có token để **push**, nhưng VPS cần quyền **pull** nếu package private.

Trên trình duyệt đăng nhập GitHub bằng tài khoản có quyền đọc package:

1. Ảnh đại diện góc phải → **Settings**.
2. **Developer settings** → **Personal access tokens** → **Tokens (classic)**.
3. **Generate new token (classic)**.
4. Note: `dodo-prod-ghcr-read`; chọn expiration phù hợp, không chọn `No expiration` nếu không cần.
5. Chỉ tick `read:packages`. Nếu package thuộc organization có SSO, authorize token cho organization.
6. Generate và copy token một lần vào password manager; không dán vào Markdown/Git.

Quay lại **cửa sổ B trên VPS**. Trong lệnh dưới chỉ thay `YOUR_GITHUB_USER` bằng username GitHub; không thay `$GHCR_PAT` bằng token:

```bash
read -rsp 'GHCR read:packages PAT: ' GHCR_PAT
echo
printf '%s' "$GHCR_PAT" | docker login ghcr.io -u YOUR_GITHUB_USER --password-stdin
unset GHCR_PAT
chmod 700 ~/.docker
chmod 600 ~/.docker/config.json
```

Sau lệnh `read`, terminal không hiện ký tự khi bạn paste token; đó là bình thường. Nhấn `Enter`, Docker phải in `Login Succeeded`. Kiểm tra file credential tồn tại mà không mở nội dung:

```bash
test -s ~/.docker/config.json && echo 'ghcr-login-config-ok'
```

Không đặt PAT trực tiếp trong command vì shell history sẽ lưu lại. Không lưu PAT trong `.env`, không thêm vào GitHub Actions secret và không commit vào Git. Workflow dùng `GITHUB_TOKEN` riêng; PAT này chỉ nằm trong Docker credential store trên VPS.

Nếu package được đặt public, VPS có thể pull không cần login. Khi đó có thể bỏ qua tạo PAT; sau khi Phần 6 build image đầu tiên, thử `docker pull ghcr.io/GHCR_OWNER/dodosystem-api:FULL_SHA`. Nếu báo `unauthorized`, quay lại làm mục 5.5.

Khi token hết hạn hoặc bị revoke, deploy sẽ lỗi ở `docker compose pull webapi`; tạo token read-only mới và login lại theo đúng các bước trên.

### 5.6 Checklist kết thúc Phần 5

Trong cửa sổ B trên VPS chạy:

```bash
cd /opt/dodo
printf '%s\n' '--- files ---'
ls -lah docker-compose.yml rabbitmq.conf .env deploy.sh
printf '%s\n' '--- permissions ---'
stat -c '%a %U:%G %n' /opt/dodo /opt/dodo/.env /opt/dodo/deploy.sh
printf '%s\n' '--- syntax ---'
docker compose config --quiet && echo compose-ok
bash -n deploy.sh && echo deploy-script-ok
printf '%s\n' '--- placeholders ---'
grep -nE 'CHANGE_ME|YOUR_|your-github-owner' .env || echo no-required-placeholders
```

Chỉ sang Phần 6 khi:

- Có đủ bốn file được liệt kê, dung lượng không bằng 0.
- `/opt/dodo` là `700`, `.env` là `600`, `deploy.sh` là `700`, owner đều là `ubuntu:ubuntu`.
- Có `compose-ok`, `deploy-script-ok`, `no-required-placeholders`.
- `IMAGE_TAG` vẫn là `bootstrap-not-deployed`; đây không phải lỗi.
- Nếu GHCR private, đã thấy `Login Succeeded` và `ghcr-login-config-ok`.

Không chạy `docker compose up` với tag bootstrap. Phần 6 sẽ build image thật, upload config và gọi deploy script bằng SHA chính xác.

---

## 6. GitHub Actions CI/CD: push `main` là tự deploy

Phần này có thao tác ở **ba nơi khác nhau**:

| Ký hiệu | Thao tác ở đâu? | Dùng để làm gì? |
|---|---|---|
| **LOCAL** | PowerShell trên máy Windows, đang đứng tại thư mục dự án | Tạo key, sửa workflow, commit và push code |
| **GITHUB** | Trình duyệt, trong repository GitHub | Tạo environment/secrets và xem kết quả workflow |
| **VPS** | Cửa sổ SSH `ubuntu@...` | Chỉ dùng để kiểm tra deploy; workflow sẽ tự SSH vào đây |

Không chạy lẫn lệnh giữa ba nơi. Khối có nhãn `powershell` chạy ở **LOCAL**; khối `bash` có câu “trên VPS” mới chạy trong cửa sổ SSH.

### 6.0 Kiểm tra file workflow trên máy local

**Thực hiện ở LOCAL — PowerShell**, không thực hiện trên VPS:

```powershell
Set-Location "D:\Project\ProjectMonHoc\EXE101\DodoSystem-BE"
Test-Path ".github\workflows\ci-cd.yml"
```

Kết quả phải là `True`. File workflow đã nằm sẵn trong repository tại:

```text
.github/workflows/ci-cd.yml
```

Trong VS Code, mở **Explorer → `.github` → `workflows` → `ci-cd.yml`**. Nếu đang dựng repository mới và file chưa tồn tại, tạo lần lượt hai thư mục `.github`, `workflows`, sau đó tạo file `ci-cd.yml` bên trong. Copy **nguyên khối YAML bên dưới**, từ dòng `name:` đến hết dòng cuối, vào file rồi lưu bằng `Ctrl+S`. Không copy ba dấu backtick vào file.

Workflow production phải build/test cho pull request; khi push lên `main`, nó build Docker image, push image lên GHCR rồi SSH vào VPS để chạy deploy.

Workflow dưới cố ý fail nếu solution không có test project hoặc NuGet báo `NU1901`–`NU1904`. Repository hiện chưa có test project và đang có advisory cần xử lý, nên hoàn thành Phần 2.10 trước khi kỳ vọng pipeline xanh; không xóa gate để deploy nhanh.

```yaml
name: Build and deploy production

on:
  push:
    branches: [main]
  pull_request:
    branches: [main]
  workflow_dispatch:

permissions:
  contents: read

concurrency:
  group: ${{ github.workflow }}-${{ github.event.pull_request.number || github.ref }}
  cancel-in-progress: ${{ github.event_name == 'pull_request' }}

jobs:
  build-test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: 8.0.x

      - name: Restore
        run: dotnet restore SMEFLOWSystem.sln -p:WarningsAsErrors=NU1901%3BNU1902%3BNU1903%3BNU1904

      - name: Build
        run: dotnet build SMEFLOWSystem.sln -c Release --no-restore

      - name: Test
        shell: bash
        run: |
          mapfile -t TEST_PROJECTS < <(find . -name '*Tests.csproj' -o -name '*.Tests.csproj')
          if [ "${#TEST_PROJECTS[@]}" -eq 0 ]; then
            echo "Không tìm thấy test project. CI không được phép xanh giả." >&2
            exit 1
          fi
          dotnet test SMEFLOWSystem.sln -c Release --no-build --verbosity normal

  build-push-image:
    needs: build-test
    if: github.event_name != 'pull_request'
    runs-on: ubuntu-latest
    permissions:
      contents: read
      packages: write
    outputs:
      image_tag: ${{ steps.meta.outputs.image_tag }}
      image_name: ${{ steps.meta.outputs.image_name }}
      owner_lower: ${{ steps.meta.outputs.owner_lower }}
    steps:
      - uses: actions/checkout@v4

      - name: Set image metadata
        id: meta
        shell: bash
        run: |
          OWNER_LOWER="$(echo "${GITHUB_REPOSITORY_OWNER}" | tr '[:upper:]' '[:lower:]')"
          echo "owner_lower=${OWNER_LOWER}" >> "$GITHUB_OUTPUT"
          echo "image_tag=${GITHUB_SHA}" >> "$GITHUB_OUTPUT"
          echo "image_name=ghcr.io/${OWNER_LOWER}/dodosystem-api" >> "$GITHUB_OUTPUT"

      - name: Login to GHCR
        uses: docker/login-action@v3
        with:
          registry: ghcr.io
          username: ${{ github.actor }}
          password: ${{ secrets.GITHUB_TOKEN }}

      - name: Build and push image
        uses: docker/build-push-action@v6
        with:
          context: .
          file: SMEFLOWSystem.WebAPI/Dockerfile
          push: true
          tags: ${{ steps.meta.outputs.image_name }}:${{ steps.meta.outputs.image_tag }}
          cache-from: type=gha
          cache-to: type=gha,mode=max

  deploy:
    needs: build-push-image
    if: github.ref == 'refs/heads/main'
    runs-on: ubuntu-latest
    environment: production
    permissions:
      contents: read
    steps:
      - uses: actions/checkout@v4

      - name: Prepare verified SSH connection
        shell: bash
        env:
          PROD_SSH_KEY: ${{ secrets.PROD_SSH_KEY }}
          PROD_KNOWN_HOSTS: ${{ secrets.PROD_KNOWN_HOSTS }}
        run: |
          install -m 700 -d "$HOME/.ssh"
          printf '%s\n' "$PROD_SSH_KEY" > "$HOME/.ssh/id_ed25519"
          printf '%s\n' "$PROD_KNOWN_HOSTS" > "$HOME/.ssh/known_hosts"
          chmod 600 "$HOME/.ssh/id_ed25519" "$HOME/.ssh/known_hosts"

      - name: Upload operation files and deploy exact SHA
        shell: bash
        env:
          PROD_HOST: ${{ secrets.PROD_HOST }}
          PROD_USER: ${{ secrets.PROD_USER }}
          IMAGE_TAG: ${{ needs.build-push-image.outputs.image_tag }}
        run: |
          SSH=(ssh -i "$HOME/.ssh/id_ed25519" -o BatchMode=yes -o StrictHostKeyChecking=yes)
          SCP=(scp -i "$HOME/.ssh/id_ed25519" -o BatchMode=yes -o StrictHostKeyChecking=yes)
          RELEASE_DIR="/opt/dodo/incoming/${IMAGE_TAG}"
          "${SSH[@]}" "${PROD_USER}@${PROD_HOST}" "mkdir -p '${RELEASE_DIR}'"
          "${SCP[@]}" docker-compose.yml rabbitmq.conf \
            "${PROD_USER}@${PROD_HOST}:${RELEASE_DIR}/"
          "${SSH[@]}" "${PROD_USER}@${PROD_HOST}" \
            "/opt/dodo/deploy.sh '${IMAGE_TAG}' '${RELEASE_DIR}'"
```

Sau khi lưu, kiểm tra nhanh ở **LOCAL — PowerShell**:

```powershell
Get-Content ".github\workflows\ci-cd.yml" -TotalCount 5
git diff -- ".github/workflows/ci-cd.yml"
```

Dòng đầu phải là `name: Build and deploy production`. Lệnh `git diff` chỉ để xem thay đổi, chưa push và không làm mất file.

Workflow trên luôn upload `docker-compose.yml` và `rabbitmq.conf` của cùng commit trước khi deploy, nên thay đổi resource limit/config không còn bị mắc kẹt trên VPS. Production chỉ push/deploy tag SHA; không tạo hoặc dùng `latest`.

Workflow **không bao giờ upload/ghi đè `.env`**. Khi code thêm config bắt buộc mới, cập nhật thủ công `/opt/dodo/.env`, giữ permission `600` và chạy `docker compose config --quiet` trước khi merge release. Không đưa secret vào workflow để tiện đồng bộ.

### 6.1 Tạo deploy SSH key cho GitHub Actions

Không dùng private key Lightsail cá nhân làm secret CI/CD. Ta tạo một key riêng, chỉ dành cho GitHub Actions.

#### Bước 6.1.1 — Tạo key ở LOCAL

Mở **PowerShell trên máy Windows** và chạy:

```powershell
ssh-keygen -t ed25519 -C "github-actions-dodo-prod" -f "$env:USERPROFILE\.ssh\dodo_prod_actions" -N ""
```

Lệnh tạo hai file:

- `dodo_prod_actions`: private key, phải giữ bí mật và sẽ đưa vào GitHub secret.
- `dodo_prod_actions.pub`: public key, được phép chép lên VPS.

Kiểm tra cả hai file đã tồn tại:

```powershell
Test-Path "$env:USERPROFILE\.ssh\dodo_prod_actions"
Test-Path "$env:USERPROFILE\.ssh\dodo_prod_actions.pub"
```

Cả hai dòng phải in `True`. Nếu `ssh-keygen` hỏi có ghi đè file cũ không và key đó đang được dùng, chọn `n` rồi kiểm tra key cũ thay vì tạo lại tùy tiện.

#### Bước 6.1.2 — Thêm public key vào VPS

Vẫn ở **LOCAL — PowerShell**. Dùng key Lightsail hiện tại để đăng nhập một lần và nối public key mới vào `authorized_keys`:

```powershell
$KEY="$env:USERPROFILE\Downloads\LightsailDefaultKey-ap-southeast-1.pem"
$VPS_IP="STATIC_IP"
type "$env:USERPROFILE\.ssh\dodo_prod_actions.pub" | ssh -i $KEY ubuntu@$VPS_IP "mkdir -p ~/.ssh && cat >> ~/.ssh/authorized_keys && chmod 700 ~/.ssh && chmod 600 ~/.ssh/authorized_keys"
```

Thay `STATIC_IP` bằng Static IP thật. Lệnh không có output khi thành công. Nó chỉ gửi file `.pub`; private key không rời khỏi máy local.

#### Bước 6.1.3 — Test key mới

Vẫn ở **LOCAL — PowerShell**:

```powershell
ssh -i "$env:USERPROFILE\.ssh\dodo_prod_actions" ubuntu@$VPS_IP "test -x /opt/dodo/deploy.sh && echo deploy-key-ok"
```

Nếu in ra `deploy-key-ok` là GitHub Actions cũng có thể SSH vào VPS bằng key này.

Nếu gặp `Permission denied (publickey)`, đăng nhập lại bằng key Lightsail rồi chạy trên **VPS**:

```bash
tail -n 3 ~/.ssh/authorized_keys
stat -c '%A %U:%G %n' ~/.ssh ~/.ssh/authorized_keys
```

Thư mục phải là `drwx------`, file phải là `-rw-------` và owner là `ubuntu:ubuntu`.

#### Bước 6.1.4 — Xác minh host key của VPS

Mục này ngăn workflow SSH nhầm máy. Trong cửa sổ **VPS** đang tin cậy, chạy:

```bash
sudo ssh-keygen -lf /etc/ssh/ssh_host_ed25519_key.pub -E sha256
```

Giữ nguyên cửa sổ này để so sánh chuỗi bắt đầu bằng `SHA256:`.

Mở **LOCAL — PowerShell**, chạy:

```powershell
$scanned = ssh-keyscan -t ed25519 $VPS_IP 2>$null
$scanned | ssh-keygen -lf - -E sha256
```

Hai fingerprint SHA256 phải giống hệt nhau. Nếu khác, dừng lại và kiểm tra IP/host; không bấm bỏ qua cảnh báo. Sau khi khớp, lấy dòng known-hosts:

```powershell
$knownHosts = ssh-keyscan -H -t ed25519 $VPS_IP 2>$null
$knownHosts
```

Output là một dòng dài bắt đầu bằng `|1|...` và kết thúc bằng loại key/nội dung key. Không tự sửa dòng này. Giữ biến `$knownHosts` trong cửa sổ PowerShell để dùng ở bước 6.2.

### 6.2 Tạo GitHub environment và secrets

Thực hiện ở **GITHUB — trình duyệt**:

1. Mở đúng repository chứa DodoSystem.
2. Chọn **Settings**. Nếu không thấy Settings, tài khoản hiện tại chưa có quyền quản trị repository.
3. Ở menu trái chọn **Environments**.
4. Bấm **New environment**.
5. Nhập đúng tên bên dưới rồi bấm **Configure environment**:

```text
production
```

6. Trong trang environment `production`, tìm **Environment secrets**.
7. Bấm **Add environment secret** và tạo lần lượt bốn secret sau:

- `PROD_HOST`: static IP/domain.
- `PROD_USER`: `ubuntu`.
- `PROD_SSH_KEY`: nội dung private key deploy riêng.
- `PROD_KNOWN_HOSTS`: toàn bộ dòng `$knownHosts` đã được đối chiếu fingerprint ở bước trên.

Tên secret phải đúng cả dấu gạch dưới và chữ hoa. Không thêm dấu nháy `"` vào giá trị.

Giá trị dùng khi chạy `ssh-keyscan` phải giống chính xác `PROD_HOST` (cùng IP hoặc cùng hostname); known-hosts của IP không tự khớp khi workflow kết nối bằng domain khác.

Lấy nội dung private key bằng PowerShell:

```powershell
Get-Content "$env:USERPROFILE\.ssh\dodo_prod_actions" -Raw
```

Copy toàn bộ nội dung bắt đầu bằng `-----BEGIN OPENSSH PRIVATE KEY-----` và kết thúc bằng `-----END OPENSSH PRIVATE KEY-----` vào secret `PROD_SSH_KEY`.

Để lấy `PROD_KNOWN_HOSTS`, quay lại đúng cửa sổ **LOCAL — PowerShell** đã chạy bước 6.1.4:

```powershell
$knownHosts
```

Copy nguyên một dòng output vào secret, không copy chữ `$knownHosts`.

Sau khi tạo xong, GitHub chỉ còn hiển thị tên secret, không hiển thị lại giá trị. Đây là hành vi bình thường. Danh sách phải có đủ:

```text
PROD_HOST
PROD_USER
PROD_SSH_KEY
PROD_KNOWN_HOSTS
```

Khi tạo instance mới từ snapshot ở Phần 9, host key của máy mới có thể đổi. Phải xác minh fingerprint lại và cập nhật `PROD_KNOWN_HOSTS` trước lần deploy kế tiếp; không dùng `StrictHostKeyChecking=no`.

### 6.3 Push code lên `main` để deploy tự động

#### Bước 6.3.1 — Kiểm tra trước khi commit

Thực hiện ở **LOCAL — PowerShell**, từ thư mục dự án:

```powershell
Set-Location "D:\Project\ProjectMonHoc\EXE101\DodoSystem-BE"
git status --short
git branch --show-current
```

Đọc danh sách file trước khi `git add`. Không dùng `git add .` nếu thư mục còn secret, file `.env`, key `.pem` hoặc thay đổi không muốn đưa lên GitHub. Tuyệt đối không commit `dodo_prod_actions`, key Lightsail hay `.env`.

Workflow hiện cố ý chặn deploy nếu chưa có test project hoặc còn cảnh báo bảo mật NuGet. Vì vậy phải hoàn tất Phần 2.10 trước. Kiểm tra local:

```powershell
dotnet restore SMEFLOWSystem.sln -p:WarningsAsErrors="NU1901;NU1902;NU1903;NU1904"
dotnet build SMEFLOWSystem.sln -c Release --no-restore
dotnet test SMEFLOWSystem.sln -c Release --no-build
```

Chỉ tiếp tục khi ba lệnh thành công và solution có test project thật.

#### Bước 6.3.2 — Commit đúng file

Ví dụ, nếu đây đúng là toàn bộ file bạn chủ động thay đổi:

```bash
git add docker-compose.yml rabbitmq.conf .github/workflows/ci-cd.yml Claude-Plans/aws_deploy_guide.md
git commit -m "configure production CI/CD"
git push origin main
```

Nếu `git status --short` có file khác, không copy mù dòng `git add` trên. Chỉ thêm các file đã kiểm tra. Nếu đang ở branch khác `main`, push branch đó, mở pull request rồi merge vào `main`; production chỉ tự deploy từ `main`.

Nếu đang làm trên branch khác `main`, tạo pull request và merge vào `main`, hoặc chuyển sang `main` rồi merge thay đổi trước khi push. Workflow production chỉ deploy từ `main`.

#### Bước 6.3.3 — Theo dõi workflow

Ở **GITHUB — trình duyệt**:

1. Mở tab **Actions**.
2. Chọn workflow **Build and deploy production**.
3. Mở run mới nhất đúng commit vừa push.
4. Chờ từng job chuyển sang dấu tick xanh.

Kết quả đúng là:

- `build-test` thành công, có test project thật và không còn advisory NuGet bị nâng thành lỗi.
- `build-push-image` push image lên GHCR.
- `deploy` upload đúng Compose/RabbitMQ config của commit rồi chạy `/opt/dodo/deploy.sh <commit-sha> <release-dir>`.

Từ lần sau, mỗi lần code mới được push lên `main`, GitHub Actions tự deploy. Không cần SSH vào VPS để pull code hay gõ deploy tay, trừ khi cần xử lý sự cố.

Nếu job đỏ, bấm vào đúng job và mở step đỏ đầu tiên. Không chạy lại liên tục trước khi đọc lỗi:

- Đỏ ở `Restore`, `Build` hoặc `Test`: sửa code/test/dependency ở local rồi push commit mới.
- Đỏ ở `Login to GHCR` hoặc `Build and push image`: kiểm tra quyền `packages: write` và Dockerfile.
- Đỏ ở `Prepare verified SSH connection`: secret private key hoặc known-hosts sai định dạng.
- Đỏ ở `Upload operation files and deploy exact SHA`: kiểm tra `PROD_HOST`, `PROD_USER`, firewall port 22 và `/opt/dodo/deploy.sh`.

Sau khi workflow xanh, kiểm tra ở **VPS**:

```bash
cd /opt/dodo
docker compose ps
grep '^IMAGE_TAG=' .env
curl --fail --show-error http://127.0.0.1:8085/health
```

`docker compose ps` phải cho thấy các service đang `Up`/`healthy`; `curl` phải trả thành công. Dòng `IMAGE_TAG` có thể xem vì không phải secret và phải là SHA 40 ký tự của commit vừa deploy.

### 6.4 Deploy thủ công lần đầu hoặc khi cần

Đường chính cho lần deploy đầu tiên vẫn là **push/merge vào `main` và chờ workflow** ở 6.3. Chỉ dùng deploy thủ công khi image SHA đã tồn tại trên GHCR nhưng job deploy bị gián đoạn, hoặc khi cần rollback có kiểm soát.

#### Bước 6.4.1 — Lấy SHA chính xác

Ở **GITHUB**, mở **Actions → run đã build thành công**. SHA hiển thị cạnh tên commit; bấm vào commit và copy đủ 40 ký tự. Không dùng `latest`, SHA rút gọn 7 ký tự hay tên branch.

#### Bước 6.4.2 — Chạy trên VPS

Chỉ khi `docker-compose.yml` và `rabbitmq.conf` trên VPS đúng với commit đó, SSH vào **VPS** và thay `FULL_40_CHARACTER_GIT_SHA` bằng SHA thật:

```bash
cd /opt/dodo
./deploy.sh FULL_40_CHARACTER_GIT_SHA
docker compose ps
docker compose logs --tail=200 webapi
curl -i http://127.0.0.1:8085/health
```

Ví dụ hình thức lệnh đúng là `./deploy.sh 012345...` với đúng 40 ký tự; không giữ nguyên chữ `FULL_40_CHARACTER_GIT_SHA`. Script tự pull image, backup database trước deploy, cập nhật `IMAGE_TAG`, khởi động và health-check. Nếu health-check thất bại, script cố rollback image/config cũ; nó không tự đảo ngược migration database.

Nếu commit có thay đổi `docker-compose.yml` hoặc `rabbitmq.conf`, ưu tiên bấm **Run workflow** trên đúng branch/commit để workflow upload đồng bộ; không chỉ chạy script với image mới và giữ config cũ.

Không dùng `docker compose down -v`; tùy chọn `-v` xóa dữ liệu PostgreSQL/Redis/RabbitMQ.

---

## 7. Nginx, HTTPS và SignalR

Phần này thực hiện sau khi API đã chạy và lệnh sau trên **VPS** trả thành công:

```bash
curl --fail --show-error http://127.0.0.1:8085/health
```

Ta sẽ cho domain trỏ về Static IP, tạo Nginx reverse proxy, sau đó xin chứng chỉ HTTPS. Ví dụ trong guide dùng `api.example.com`; ở mọi lệnh bạn phải thay bằng domain API thật, ví dụ `api.tenmiencuaban.com`.

### 7.1 Trỏ DNS về Lightsail Static IP

Thực hiện ở trang quản lý DNS của nơi mua domain:

1. Mở phần **DNS Management / DNS Records**.
2. Tạo record loại `A`.
3. Nếu muốn domain `api.example.com`, phần **Name/Host** nhập `api`.
4. Phần **Value/Points to** nhập Static IP của Lightsail.
5. TTL có thể chọn `300` giây hoặc `Auto`.
6. Lưu record. Nếu đã có record `A`/`AAAA` cũ cho cùng host, phải kiểm tra và xóa/sửa record xung đột.

Từ **LOCAL — PowerShell**, thay domain rồi kiểm tra:

```powershell
$DOMAIN="api.example.com"
Resolve-DnsName $DOMAIN -Type A
```

Địa chỉ ở cột `IPAddress` phải đúng Static IP. DNS có thể cần vài phút đến vài giờ để cập nhật. Không chạy Certbot khi domain còn trả IP khác vì xác minh chứng chỉ sẽ thất bại.

### 7.2 Tạo file cấu hình Nginx

Thực hiện trên **VPS**. Đầu tiên đặt domain thật vào biến và kiểm tra lại output; không giữ nguyên `api.example.com`:

```bash
DOMAIN="api.example.com"
echo "$DOMAIN"
```

Tiếp theo copy **nguyên khối lệnh**, từ dòng `sudo tee` đến dòng `EOF`, rồi paste vào VPS. Dòng `EOF` cuối phải đứng một mình, không có khoảng trắng. Khối này tạo file `/etc/nginx/sites-available/dodo-api`; không cần mở Nano:

```bash
sudo tee /etc/nginx/sites-available/dodo-api >/dev/null <<'EOF'
map $http_upgrade $connection_upgrade {
    default upgrade;
    ''      close;
}

limit_req_zone $binary_remote_addr zone=api_limit:10m rate=20r/s;

server {
    listen 80;
    listen [::]:80;
    server_name api.example.com;

    server_tokens off;
    client_max_body_size 15m;

    location /hubs/ {
        proxy_pass http://127.0.0.1:8085;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_set_header X-Forwarded-Host $host;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection $connection_upgrade;
        proxy_read_timeout 3600s;
        proxy_send_timeout 3600s;
    }

    location / {
        limit_req zone=api_limit burst=40 nodelay;

        proxy_pass http://127.0.0.1:8085;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_set_header X-Forwarded-Host $host;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection $connection_upgrade;
        proxy_read_timeout 120s;
        proxy_send_timeout 120s;
    }

    location = /health {
        proxy_pass http://127.0.0.1:8085/health;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        access_log off;
    }
}
EOF
```

File vừa tạo vẫn chứa placeholder `api.example.com`. Thay placeholder bằng giá trị `$DOMAIN`, rồi kiểm tra domain đã xuất hiện đúng:

```bash
sudo sed -i "s/api\.example\.com/${DOMAIN}/g" /etc/nginx/sites-available/dodo-api
grep -n 'server_name' /etc/nginx/sites-available/dodo-api
```

Output phải giống `server_name api.tenmiencuaban.com;`. Nếu vẫn là `api.example.com`, dừng lại và sửa `$DOMAIN` trước khi xin chứng chỉ.

### 7.3 Bật site và kiểm tra HTTP

Vẫn trên **VPS**, chạy từng lệnh:

```bash
sudo ln -sfn /etc/nginx/sites-available/dodo-api /etc/nginx/sites-enabled/dodo-api
sudo rm -f /etc/nginx/sites-enabled/default
sudo nginx -t
sudo systemctl reload nginx
sudo systemctl --no-pager --full status nginx
curl -i -H "Host: $DOMAIN" http://127.0.0.1/health
```

`nginx -t` phải in `syntax is ok` và `test is successful`. Lệnh `curl` phải trả HTTP `200`. Nếu Nginx lỗi, không chạy Certbot; xem:

```bash
sudo nginx -t
sudo journalctl -u nginx --since '10 minutes ago' --no-pager
```

Không xóa file cấu hình đang hoạt động để chữa nhanh. Sửa đúng dòng được `nginx -t` báo rồi test lại.

### 7.4 Xin chứng chỉ HTTPS bằng Let's Encrypt

Trên **VPS**, dùng biến `$DOMAIN` đã đặt ở 7.2:

```bash
sudo certbot --nginx -d "$DOMAIN" --redirect
```

Ở lần chạy đầu, Certbot sẽ hỏi:

1. Email nhận cảnh báo hết hạn: nhập email thật.
2. Đồng ý điều khoản: nhập `Y`.
3. Chia sẻ email với EFF: chọn `Y` hoặc `N`, không ảnh hưởng chứng chỉ.

Certbot sẽ chỉnh cấu hình Nginx để HTTP tự redirect sang HTTPS. Sau khi báo thành công, chạy:

```bash
sudo certbot renew --dry-run
curl -I "http://${DOMAIN}/health"
curl -i "https://${DOMAIN}/health"
cd /opt/dodo
docker compose logs --since=10m webapi | grep -iE 'unknown proxy|forwarded' || true
```

Kết quả đúng:

- Lệnh HTTP trả `301` hoặc `308` và có header `Location: https://...`.
- Lệnh HTTPS trả `200`.
- `certbot renew --dry-run` báo mô phỏng gia hạn thành công.
- Log không có cảnh báo proxy không được trust.

Nếu Certbot báo challenge/timeout, kiểm tra lại DNS ở 7.1 và firewall Lightsail/UFW phải mở port 80, 443. Nếu có `Unknown proxy`, xem remote IP trong log, đối chiếu Docker gateway bằng:

```bash
docker network inspect dodo-private
```

Sau đó sửa `KnownProxies` đúng gateway trong code và deploy lại. Không clear toàn bộ danh sách proxy để chữa nhanh vì client có thể giả mạo forwarded headers.

Timeout dài chỉ áp dụng cho `/hubs/`; API thường vẫn là 120 giây. Test SignalR bằng frontend qua domain HTTPS, giữ kết nối ít nhất vài phút và xác nhận không reconnect loop.

### 7.5 Cập nhật URL production trong `.env`

Sau khi HTTPS hoạt động, mở trên **VPS**:

```bash
cd /opt/dodo
nano .env
```

Kiểm tra các biến `AllowedHosts`, CORS, onboarding URL và callback/webhook URL dùng đúng domain production. Trong Nano: `Ctrl+W` để tìm, `Ctrl+O` rồi Enter để lưu, `Ctrl+X` để thoát. Không thêm `https://` vào `AllowedHosts`; biến này chỉ nhận hostname. Sau khi sửa:

```bash
chmod 600 .env
docker compose config --quiet && echo compose-env-ok
docker compose up -d --no-deps --force-recreate webapi
curl --fail --show-error "https://${DOMAIN}/health"
```

Nếu frontend ở domain khác, CORS phải chứa chính xác origin đầy đủ như `https://app.example.com`, không dùng `*` khi gửi cookie/credential.

---

## 8. Backup PostgreSQL off-site và restore

Backup chỉ được xem là hoàn thành khi đã đáp ứng đủ ba việc:

1. File dump được tạo từ PostgreSQL.
2. File được upload ra S3, ngoài VPS.
3. Đã restore thử thành công vào một database test.

Các bước 8.1 thực hiện ở **AWS Console trên trình duyệt**; các lệnh từ 8.2 trở đi thực hiện trên **VPS**. Không đặt AWS access key vào repository hoặc `.env` của ứng dụng.

### 8.1 Tạo S3 bucket riêng cho backup

#### Bước 8.1.1 — Tạo bucket

Ở **AWS Console**:

1. Dùng ô tìm kiếm phía trên, tìm và mở **S3**.
2. Bấm **Create bucket**.
3. **AWS Region** chọn `Asia Pacific (Singapore) ap-southeast-1`.
4. **Bucket name** nhập một tên duy nhất toàn cầu, ví dụ `dodo-prod-backups-123456789012`. Không dùng nguyên chữ `UNIQUE`.
5. **Object Ownership** giữ `ACLs disabled (recommended)`.
6. **Block Public Access settings** giữ chọn **Block all public access** và toàn bộ bốn ô con.
7. **Bucket Versioning** chọn `Enable`.
8. **Default encryption** giữ `Server-side encryption with Amazon S3 managed keys (SSE-S3)` để không phát sinh quản lý KMS key riêng.
9. Bấm **Create bucket**.

Ghi lại chính xác tên bucket; bên dưới gọi là `BUCKET_THẬT`. Tên này phân biệt với URI `s3://...`: khi policy yêu cầu ARN thì dùng `arn:aws:s3:::BUCKET_THẬT`, khi AWS CLI copy file thì dùng `s3://BUCKET_THẬT/...`.

Sau khi tạo, mở bucket → tab **Permissions** và xác nhận **Block public access** là `On`. Bucket backup không được public. AWS cũng khuyến nghị giữ Block Public Access và ACL bị tắt cho trường hợp này.

#### Bước 8.1.2 — Tạo lifecycle 30 ngày

Trong bucket vừa tạo:

1. Chọn tab **Management**.
2. Ở **Lifecycle rules**, bấm **Create lifecycle rule**.
3. Đặt tên `expire-postgres-backups-30-days`.
4. Chọn giới hạn scope bằng prefix và nhập `postgres/`. Nếu Console yêu cầu xác nhận scope, xác nhận rule áp dụng cho prefix đó.
5. Chọn **Expire current versions of objects**, nhập `30` ngày.
6. Chọn **Permanently delete noncurrent versions of objects**, nhập `30` ngày kể từ khi object thành noncurrent. Nếu có ô số version mới hơn cần giữ, có thể để trống/giữ mặc định nếu Console cho phép.
7. Chọn **Delete expired object delete markers or incomplete multipart uploads** và đặt abort incomplete multipart upload sau `7` ngày.
8. Kiểm tra summary rồi bấm **Create rule**.

Vì đã bật Versioning, chỉ expire current version là chưa đủ: các noncurrent version có thể tiếp tục tích lũy chi phí. Rule phải xử lý cả current và noncurrent như trên.

#### Bước 8.1.3 — Tạo IAM policy chỉ cho prefix backup

Ở **AWS Console**, tìm **IAM** → **Policies** → **Create policy** → tab **JSON**. Xóa JSON mẫu rồi paste policy sau. Trước khi bấm tạo, thay cả ba chỗ `dodo-prod-backups-UNIQUE` bằng đúng tên bucket thật:

```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Sid": "ListOnlyPostgresPrefix",
      "Effect": "Allow",
      "Action": "s3:ListBucket",
      "Resource": "arn:aws:s3:::dodo-prod-backups-UNIQUE",
      "Condition": {
        "StringLike": { "s3:prefix": ["postgres", "postgres/*"] }
      }
    },
    {
      "Sid": "ReadWriteBackupObjects",
      "Effect": "Allow",
      "Action": ["s3:PutObject", "s3:GetObject"],
      "Resource": "arn:aws:s3:::dodo-prod-backups-UNIQUE/postgres/*"
    }
  ]
}
```

Kiểm tra bằng `Ctrl+F`: file policy không còn chữ `UNIQUE`. Bấm **Next**, đặt tên policy `DodoBackupS3PrefixPolicy`, rồi **Create policy**.

Policy không cấp `DeleteObject`; lifecycle của bucket mới là thành phần xóa file cũ. Vì vậy credential của VPS nếu bị lộ không thể gọi API để xóa trực tiếp backup hiện có. Tài khoản quản trị có quyền sửa lifecycle/policy phải bật MFA.

#### Bước 8.1.4 — Tạo IAM user và access key

Trong **IAM**:

1. Mở **Users** → **Create user**.
2. User name nhập `dodo-backup-writer`.
3. Không cấp quyền đăng nhập AWS Console cho user này.
4. Ở bước permissions, chọn **Attach policies directly**, tìm và chọn `DodoBackupS3PrefixPolicy`.
5. Hoàn tất tạo user.
6. Mở user `dodo-backup-writer` → tab **Security credentials**.
7. Trong **Access keys**, bấm **Create access key**.
8. Chọn use case **Command Line Interface (CLI)**, xác nhận khuyến nghị rồi tạo.
9. Copy cả **Access key ID** và **Secret access key** vào password manager ngay. Secret chỉ hiển thị một lần; nếu làm mất, xóa key đó và tạo key mới.

Không tạo access key của root user.

#### Bước 8.1.5 — Cấu hình AWS CLI trên VPS

SSH vào **VPS** bằng user `ubuntu`, chạy:

```bash
aws configure
```

Nhập lần lượt theo prompt:

```text
AWS Access Key ID: <Access key ID của dodo-backup-writer>
AWS Secret Access Key: <Secret access key vừa tạo>
Default region name: ap-southeast-1
Default output format: json
```

Khi paste secret, terminal có thể vẫn hiển thị ký tự; bảo đảm không share màn hình/record terminal lúc này. Sau đó khóa permission và test identity:

```bash
chmod 700 ~/.aws
chmod 600 ~/.aws/credentials ~/.aws/config
aws sts get-caller-identity
```

Output phải có ARN chứa `dodo-backup-writer`. Nếu `aws: command not found`, quay lại Phần 4 và cài AWS CLI trước. Nếu `InvalidClientTokenId`/`SignatureDoesNotMatch`, chạy lại `aws configure` và nhập đúng key; không paste key vào lệnh shell vì sẽ lưu vào history.

Đặt tên bucket thật thành biến để test quyền. Biến chỉ sống trong phiên SSH hiện tại:

```bash
BUCKET_NAME="dodo-prod-backups-UNIQUE"
echo "$BUCKET_NAME"
aws s3 ls "s3://${BUCKET_NAME}/postgres/"
```

Thay `dodo-prod-backups-UNIQUE` trước khi chạy. Bucket mới chưa có file nên lệnh `ls` có thể không in gì nhưng phải thoát không lỗi. `AccessDenied` thường có nghĩa tên bucket sai hoặc policy chưa attach đúng.

Không ghi access key vào `.env`, guide, shell history hoặc GitHub secret vì GitHub Actions không cần upload backup.

### 8.2 Script backup

#### Bước 8.2.1 — Tạo file script

Trên **VPS**, chạy `cd /opt/dodo`. Copy **nguyên khối** từ `tee` đến `EOF`. Dòng `EOF` cuối đứng một mình:

```bash
cd /opt/dodo
tee /opt/dodo/backup-postgres.sh >/dev/null <<'EOF'
#!/usr/bin/env bash
set -Eeuo pipefail
umask 077

cd /opt/dodo

STAMP="$(date -u +%Y-%m-%dT%H-%M-%SZ)"
FILE="/opt/dodo/backups/dodosystem-${STAMP}.dump"
BUCKET="s3://dodo-prod-backups-UNIQUE/postgres/"

mkdir -p /opt/dodo/backups

docker compose exec -T postgres sh -c \
  'pg_dump -U "$POSTGRES_USER" -d "$POSTGRES_DB" -Fc' > "$FILE"

test -s "$FILE"
aws s3 cp "$FILE" "$BUCKET" --only-show-errors
aws s3api head-object \
  --bucket dodo-prod-backups-UNIQUE \
  --key "postgres/$(basename "$FILE")" >/dev/null
rm -f "$FILE"

find /opt/dodo/backups -type f -name 'dodosystem-*.dump' -mtime +2 -delete
echo "Backup ${STAMP} completed"
EOF
```

Nếu terminal hiện dấu `>` và chờ mãi, nghĩa là chưa nhận được dòng `EOF`; nhập đúng `EOF` rồi Enter. Có thể bấm `Ctrl+C` để hủy và làm lại.

Thay placeholder bucket trong script. Dùng chính tên bucket đã đặt ở `$BUCKET_NAME` bước 8.1.5:

```bash
BUCKET_NAME="dodo-prod-backups-UNIQUE"
test -n "${BUCKET_NAME:-}" || echo 'Chưa đặt BUCKET_NAME'
sed -i "s/dodo-prod-backups-UNIQUE/${BUCKET_NAME}/g" /opt/dodo/backup-postgres.sh
grep -nE 'BUCKET=|--bucket' /opt/dodo/backup-postgres.sh
grep -n 'UNIQUE' /opt/dodo/backup-postgres.sh || echo 'bucket-placeholder-ok'
```

Thay `dodo-prod-backups-UNIQUE` ở dòng đầu bằng bucket thật. Phải đặt lại biến nếu bạn đã đóng/mở lại SSH vì biến shell không được lưu qua phiên mới. Hai vị trí bucket trong script phải cùng một tên thật và output cuối phải có `bucket-placeholder-ok`.

#### Bước 8.2.2 — Cấp quyền và kiểm tra cú pháp

Vẫn trên **VPS**:

```bash
chmod 700 /opt/dodo/backup-postgres.sh
mkdir -p /opt/dodo/backups
chmod 700 /opt/dodo/backups
bash -n /opt/dodo/backup-postgres.sh && echo backup-script-syntax-ok
stat -c '%A %U:%G %n' /opt/dodo/backup-postgres.sh /opt/dodo/backups
```

Phải thấy `backup-script-syntax-ok`; script và thư mục thuộc `ubuntu:ubuntu`, permission lần lượt `-rwx------` và `drwx------`.

#### Bước 8.2.3 — Chạy backup đầu tiên bằng tay

Trên **VPS**:

```bash
/opt/dodo/backup-postgres.sh
aws s3 ls "s3://${BUCKET_NAME}/postgres/"
```

Script phải in `Backup ... completed`; lệnh `aws s3 ls` phải thấy file dạng `dodosystem-2026-...Z.dump` có kích thước lớn hơn 0. Script chỉ xóa file local sau khi upload và `head-object` trên S3 thành công.

Nếu script lỗi, đọc đúng dòng lỗi:

- `service "postgres" is not running`: chạy `cd /opt/dodo && docker compose ps`, sửa database trước.
- `AccessDenied`: kiểm tra bucket trong script và IAM policy.
- `No space left on device`: kiểm tra `df -h`; không xóa volume database.
- Dump có kích thước 0: script sẽ dừng ở `test -s`; xem log PostgreSQL.

#### Bước 8.2.4 — Lập lịch backup mỗi ngày

Cron của VPS dùng timezone hệ thống. Kiểm tra trước:

```bash
timedatectl | grep 'Time zone'
```

Guide giả định VPS dùng UTC. `02:00` giờ Việt Nam (UTC+7) tương ứng `19:00 UTC` ngày hôm trước. Mở cron của user `ubuntu`:

```bash
crontab -e
```

Nếu được hỏi chọn editor, nhập số tương ứng với `/bin/nano` rồi Enter. Xuống cuối file, thêm đúng một dòng:

```cron
0 19 * * * /opt/dodo/backup-postgres.sh >> /opt/dodo/backups/backup.log 2>&1
```

Trong Nano, `Ctrl+O`, Enter để lưu; `Ctrl+X` để thoát. Kiểm tra cron đã lưu:

```bash
crontab -l
```

Không thêm cùng một dòng nhiều lần. Nếu server không dùng UTC, đổi giờ cron tương ứng hoặc cấu hình `CRON_TZ=Asia/Ho_Chi_Minh` nếu cron trên hệ điều hành hỗ trợ; cách ít nhầm nhất là giữ server UTC và dòng `0 19` như trên.

#### Bước 8.2.5 — Giới hạn dung lượng backup log

Copy nguyên khối sau trên **VPS** để tạo `/etc/logrotate.d/dodo-backup`:

```bash
sudo tee /etc/logrotate.d/dodo-backup >/dev/null <<'EOF'
/opt/dodo/backups/backup.log {
    weekly
    rotate 8
    compress
    missingok
    notifempty
    copytruncate
}
EOF
sudo logrotate -d /etc/logrotate.d/dodo-backup
```

Lệnh `-d` chỉ mô phỏng/debug, không rotate thật. Output không được có lỗi syntax.

Cuối cùng, ở **AWS Console → S3 → bucket → Objects → `postgres/`**, mở object mới và xác nhận:

- Kích thước lớn hơn 0.
- Server-side encryption đang bật.
- Object không public.
- Thời gian upload khớp lần vừa chạy.

### 8.3 Restore thử bắt buộc

Không restore thử đè vào production database `dodosystem`. Ta tải một dump về VPS và restore vào database riêng tên `dodosystem_restore_test`.

#### Bước 8.3.1 — Chọn đúng file backup

Trên **VPS**:

```bash
BUCKET_NAME="dodo-prod-backups-UNIQUE"
aws s3 ls "s3://${BUCKET_NAME}/postgres/"
```

Thay placeholder bằng bucket thật. Copy đúng tên file ở cột cuối, ví dụ `dodosystem-2026-07-13T19-00-01Z.dump`, rồi gán vào biến:

```bash
BACKUP_FILE="dodosystem-YYYY-MM-DDTHH-MM-SSZ.dump"
echo "$BACKUP_FILE"
```

Không giữ nguyên chữ `YYYY...`. Tải file về:

```bash
aws s3 cp "s3://${BUCKET_NAME}/postgres/${BACKUP_FILE}" /opt/dodo/backups/restore-test.dump
test -s /opt/dodo/backups/restore-test.dump && echo restore-file-ok
```

Phải thấy `restore-file-ok`.

#### Bước 8.3.2 — Tạo database test

Kiểm tra database test chưa tồn tại:

```bash
cd /opt/dodo
docker compose exec -T postgres sh -c \
  'psql -U "$POSTGRES_USER" -d postgres -tAc "SELECT 1 FROM pg_database WHERE datname='"'"'dodosystem_restore_test'"'"';"'
```

Nếu lệnh in `1`, database test cũ còn tồn tại. Chỉ xóa đúng database test, không xóa production:

```bash
docker compose exec -T postgres sh -c \
  'dropdb -U "$POSTGRES_USER" --if-exists dodosystem_restore_test'
```

Tạo database test mới:

```bash
docker compose exec -T postgres sh -c \
  'createdb -U "$POSTGRES_USER" dodosystem_restore_test'
```

#### Bước 8.3.3 — Restore và kiểm tra

Vẫn trên **VPS**, copy nguyên khối:

```bash
docker compose exec -T postgres sh -c \
  'pg_restore -U "$POSTGRES_USER" -d dodosystem_restore_test --clean --if-exists' \
  < /opt/dodo/backups/restore-test.dump
docker compose exec -T postgres sh -c \
  'psql -U "$POSTGRES_USER" -d dodosystem_restore_test -c "\\dt"'
```

Lệnh `\dt` phải liệt kê các bảng ứng dụng, không phải `Did not find any relations`. Có thể kiểm tra thêm migration history:

```bash
docker compose exec -T postgres sh -c \
  'psql -U "$POSTGRES_USER" -d dodosystem_restore_test -c "SELECT * FROM \"__EFMigrationsHistory\" ORDER BY \"MigrationId\";"'
```

Chỉ khi restore thành công mới đánh dấu checklist backup đạt.

#### Bước 8.3.4 — Dọn database/file test

Sau khi xác nhận bảng và dữ liệu, chạy trên **VPS**:

```bash
docker compose exec -T postgres sh -c \
  'dropdb -U "$POSTGRES_USER" dodosystem_restore_test'
rm -f /opt/dodo/backups/restore-test.dump
```

Lệnh chỉ xóa database có hậu tố `_restore_test` và file download local; object trên S3 vẫn còn.

#### Bước 8.3.5 — Bật Lightsail automatic snapshot

Ở **AWS Console → Lightsail → Instances → chọn instance → Snapshots**:

1. Bật **Automatic snapshots**.
2. Chọn thời gian ít tải; nhớ thời gian hiển thị theo timezone Console mô tả.
3. Sau ngày đầu, quay lại tab Snapshots và xác nhận có automatic snapshot.

Snapshot giúp phục hồi cả VPS nhưng không thay thế logical PostgreSQL backup trên S3. AWS giữ 7 automatic snapshot gần nhất; automatic snapshot gắn với resource sẽ bị xóa khi xóa resource, trừ khi giữ/copy một bản thành manual snapshot trước. Theo [Lightsail snapshot FAQ](https://docs.aws.amazon.com/lightsail/latest/userguide/amazon-lightsail-faq-snapshots.html), snapshot tính phí theo dung lượng thực dùng và các snapshot kế tiếp được tối ưu theo phần dữ liệu thay đổi.

PostgreSQL là nguồn dữ liệu nghiệp vụ chính và có outbox; Redis/Hangfire và RabbitMQ vẫn nằm trong Lightsail snapshot chứ chưa có off-site logical backup riêng. Với MVP $7, chấp nhận RPO tối đa theo snapshot cho queued/background state, đồng thời phải có job reconciliation/idempotency để tái tạo công việc từ PostgreSQL. Nếu mất một job/message là không chấp nhận được thì kiến trúc single-VPS này chưa đủ.

---

## 9. Phương án thoát hiểm khi gói $7 không còn đủ

Có thể mở rộng, nhưng Lightsail không resize RAM/SSD trực tiếp trên instance hiện tại. Quy trình chính thức là tạo snapshot rồi tạo **instance mới** có plan bằng hoặc lớn hơn. AWS không hỗ trợ tạo instance nhỏ hơn từ snapshot; xem [hướng dẫn upsize chính thức](https://docs.aws.amazon.com/lightsail/latest/userguide/how-to-create-larger-instance-from-snapshot-using-console.html).

Không thực hiện phần này ngay khi cài mới. Chỉ dùng khi số liệu ở Phần 10 cho thấy gói 1 GB thường xuyên OOM/swap và việc tối ưu ứng dụng không đủ. Trong phần này:

- **Máy cũ**: instance $7 đang phục vụ production và đang giữ Static IP.
- **Máy mới**: instance tạo từ snapshot với gói lớn hơn.
- **Maintenance**: API tạm ngừng nhận request/write; cần báo trước cho người dùng.
- **Static IP**: địa chỉ được chuyển từ máy cũ sang máy mới; không tạo DNS record mới nếu vẫn dùng cùng Static IP.

Ảnh hưởng của gói 7 USD so với 12 USD:

| | $7 | $12 |
|---|---:|---:|
| vCPU | 2 | 2 |
| RAM | 1 GB | 2 GB |
| SSD | 40 GB | 60 GB |
| Transfer allowance | 2 TB | 3 TB |
| Phù hợp | demo/MVP tải thấp | production nhỏ an toàn hơn |

Guide vẫn triển khai ban đầu bằng gói $7. Phần này chỉ thực hiện khi số liệu ở Phần 10 chứng minh 1 GB không đủ. Khác biệt lớn nhất là RAM, không phải CPU. Snapshot chứa system disk và Docker named volumes nhưng snapshot nóng của PostgreSQL chỉ mang tính crash-consistent; muốn không phân kỳ dữ liệu phải dừng toàn bộ writer trước snapshot cuối.

Quy trình dưới đây ưu tiên không phân kỳ dữ liệu và chấp nhận downtime.

### 9.1 Chuẩn bị trước maintenance

1. Chọn giờ ít người dùng và báo trước thời gian ngừng dịch vụ.
2. Tạm ngừng webhook/payment ở gateway nếu gateway có nút disable/pause. Ghi lại để bật lại sau.
3. Mở hai cửa sổ riêng và đặt tiêu đề/ghi chú rõ **OLD VPS** và **NEW VPS**; chưa có NEW VPS ở bước này.
4. Ở **OLD VPS**, đặt bucket thật và chạy backup cuối:

```bash
cd /opt/dodo
BUCKET_NAME="dodo-prod-backups-UNIQUE"
./backup-postgres.sh
aws s3 ls "s3://${BUCKET_NAME}/postgres/" | tail
```

Không tiếp tục nếu backup không in `completed` hoặc object mới không xuất hiện trên S3.

### 9.2 Dừng sạch máy cũ và tạo snapshot

Trên **OLD VPS**, chạy:

```bash
cd /opt/dodo
docker update --restart=no dodo-webapi dodo-postgres dodo-redis dodo-rabbitmq
docker compose stop webapi
docker compose stop rabbitmq redis postgres
docker ps -a --filter name=dodo
```

`docker ps -a` phải cho thấy toàn bộ container Dodo là `Exited`, không còn `Up`. Lệnh `docker update --restart=no` rất quan trọng: instance tạo từ snapshot không được tự chạy stack trong lúc Static IP vẫn ở máy cũ.

Từ thời điểm này hệ thống đang maintenance. Không chạy `docker compose up` trên OLD VPS trong lúc chờ snapshot.

Ở **AWS Console**:

1. Mở **Lightsail → Instances → chọn máy cũ → Snapshots**.
2. Chọn tạo **manual snapshot**.
3. Đặt tên `dodo-quiesced-before-upsize-YYYYMMDD-HHMM`, thay ngày/giờ thật.
4. Bấm tạo và chờ trạng thái hoàn tất. Không dùng snapshot còn `Pending`.

### 9.3 Tạo máy mới từ snapshot

Trong danh sách snapshot:

1. Mở menu của manual snapshot vừa hoàn tất.
2. Chọn **Create new instance**.
3. Chọn đúng Region Singapore.
4. Chọn plan lớn hơn, ví dụ gói 2 GB; không thể chọn nhỏ hơn dung lượng snapshot hỗ trợ.
5. Đặt tên dễ nhận biết như `dodo-prod-2gb`.
6. Tạo instance nhưng **chưa detach/attach Static IP production**.

Lightsail sẽ cấp một public IP tạm cho NEW VPS. Dùng browser SSH hoặc tải/áp dụng đúng SSH key để đăng nhập public IP tạm đó.

### 9.4 Kiểm tra và khởi động máy mới bằng IP tạm

Trên **NEW VPS**, trước khi start:

```bash
hostname
cd /opt/dodo
docker ps -a --filter name=dodo
docker compose config --quiet && echo compose-config-ok
```

Xác nhận hostname/IP đây là máy mới; container vẫn phải `Exited`. Nếu container tự chạy, dừng ngay và kiểm tra restart policy trước khi tiếp tục.

Start dependency trước, chờ trạng thái healthy, rồi mới start API:

```bash
docker compose up -d postgres redis rabbitmq
docker compose ps
docker compose up -d --no-deps webapi
for i in $(seq 1 30); do
  if curl --fail --show-error http://127.0.0.1:8085/health; then
    echo 'new-vps-health-ok'
    break
  fi
  sleep 5
done
docker compose ps
docker compose logs --tail=200 webapi
```

Phải thấy `new-vps-health-ok`, các dependency healthy và webapi Up. Nếu không thấy dòng đó sau khoảng 150 giây, không chuyển Static IP; đọc log và sửa trên NEW VPS trước.

Kiểm tra migration và dung lượng:

```bash
docker compose exec -T postgres sh -c \
  'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -c "SELECT * FROM \"__EFMigrationsHistory\" ORDER BY \"MigrationId\";"'
df -h
free -h
```

Chỉ test thao tác không tạo side effect lớn khi NEW VPS còn dùng IP tạm. Không bật payment/webhook lúc này.

### 9.5 Chuyển Static IP sang máy mới

Ở **AWS Console → Lightsail → Networking → Static IPs**:

1. Mở Static IP production, ví dụ `dodo-prod-ip`.
2. Detach khỏi OLD VPS.
3. Attach ngay Static IP đó vào NEW VPS.
4. Không sửa DNS A record vì domain vẫn trỏ cùng Static IP.

Từ **LOCAL — PowerShell**, xóa host-key cache cũ chỉ sau khi chắc chắn IP đã chuyển:

```powershell
$VPS_IP="STATIC_IP"
ssh-keygen -R $VPS_IP
```

SSH vào NEW VPS và thực hiện lại bước đối chiếu fingerprint 6.1.4. Sau khi fingerprint khớp, cập nhật environment secret `PROD_KNOWN_HOSTS` trên GitHub. Host key có thể khác dù IP giữ nguyên; không dùng `StrictHostKeyChecking=no`.

### 9.6 Smoke test và bật lại production

Từ **LOCAL — PowerShell** hoặc máy có Internet:

```powershell
$DOMAIN="api.example.com"
curl.exe -i "https://$DOMAIN/health"
```

Sau đó test bằng frontend: login, refresh token, SignalR, một job nền an toàn và webhook/payment sandbox. Nếu tất cả đạt, trên **NEW VPS** khôi phục restart policy theo Compose:

```bash
cd /opt/dodo
docker compose up -d
docker inspect dodo-webapi --format '{{.HostConfig.RestartPolicy.Name}}'
docker inspect dodo-postgres --format '{{.HostConfig.RestartPolicy.Name}}'
```

Output restart policy phải khớp cấu hình Compose (ví dụ `unless-stopped`). Bật lại webhook/payment đã pause ở 9.1 và quan sát log.

Giữ OLD VPS ở trạng thái container stopped trong một khoảng quan sát ngắn, ví dụ 24 giờ. Lightsail vẫn tính phí cả hai instance trong thời gian này. Sau khi NEW VPS ổn định và có backup S3 mới, xóa OLD VPS để ngừng tính compute; giữ một manual snapshot cần thiết và xóa snapshot thủ công thừa để tránh phí.

### 9.7 Khi nào được rollback về máy cũ?

Rollback đơn giản chỉ an toàn khi NEW VPS **chưa nhận bất kỳ write/payment/job nào**:

1. Detach Static IP khỏi NEW VPS.
2. Attach lại OLD VPS.
3. SSH vào OLD VPS và chạy `cd /opt/dodo && docker compose up -d`.
4. Kiểm tra health và cập nhật lại `PROD_KNOWN_HOSTS` nếu cần.

Nếu NEW VPS đã nhận dữ liệu mới, không được attach ngược rồi start OLD VPS vì hai database đã phân kỳ. Khi đó phải bật maintenance, backup NEW VPS, xác định nguồn dữ liệu chuẩn và lập kế hoạch restore/đồng bộ riêng. Không có một lệnh tự động an toàn cho trường hợp này.

---

## 10. Giám sát và vận hành

### 10.1 Kiểm tra nhanh mỗi ngày trong tuần đầu

Các lệnh dưới chỉ đọc trạng thái, không restart/xóa dữ liệu. SSH vào **VPS**, chạy:

```bash
cd /opt/dodo
docker compose ps
docker stats --no-stream
docker system df
free -h
df -h
vmstat 1 5
sudo journalctl -u docker --since '1 hour ago'
curl --fail https://api.example.com/health
```

Trước dòng `curl`, thay `api.example.com` bằng domain thật. Đọc kết quả như sau:

| Lệnh | Kết quả bình thường | Dấu hiệu cần xử lý |
|---|---|---|
| `docker compose ps` | webapi Up; postgres/redis/rabbitmq Up/healthy | `Exited`, `Restarting`, `unhealthy` |
| `docker stats --no-stream` | Tổng RAM không chạm hard limit; CPU trở về thấp sau request | Một container dùng >85% limit hoặc CPU cao liên tục |
| `docker system df` | Image/build cache còn trong khả năng disk | Reclaimable/image tăng nhiều GB |
| `free -h` | `available` thường trên 100 MB khi idle | Available gần 0, swap dùng/tăng liên tục |
| `df -h` | Phân vùng `/` dưới 80% | Trên 80% cần tìm nguồn tăng; trên 90% là khẩn cấp |
| `vmstat 1 5` | Cột `si`/`so` chủ yếu bằng 0 | Swap-in/swap-out liên tục |
| `journalctl` | Không có Docker daemon/OOM lỗi lặp lại | Daemon restart, I/O error, OOM |
| `curl` | Exit code 0, health thành công | Timeout, DNS/TLS/HTTP 5xx |

Để xem exit code của health check:

```bash
curl --silent --show-error --output /dev/null \
  --write-out 'HTTP %{http_code}\n' \
  https://api.example.com/health
```

Kết quả phải là `HTTP 200`.

### 10.2 Quyết định khi nào phải nâng lên 2 GB

Dấu hiệu gói 1 GB không còn đủ và nên nâng lên 2 GB:

- Container bị `OOMKilled`.
- Swap in/out xảy ra liên tục.
- PostgreSQL query latency tăng mạnh.
- CPU thường xuyên chạm baseline/burst cạn.
- Deploy health check timeout dù service không lỗi logic.
- `available` RAM thường xuyên dưới 100 MB khi idle hoặc webapi/RabbitMQ chạm hơn 85% hard limit.
- Disk trên 80% do image, PostgreSQL volume hoặc log.

Xem OOM:

```bash
dmesg -T | grep -iE 'out of memory|killed process'
docker inspect dodo-webapi --format '{{.State.OOMKilled}}'
```

Lệnh `docker inspect` in `true` nghĩa là webapi từng bị kernel/container runtime kill vì thiếu RAM ở lần chạy hiện tại/gần nhất. Sau một restart, vẫn phải đối chiếu `dmesg` và timestamp log.

Ghi số liệu ít nhất sáng/tối trong vài ngày đầu vào một bảng đơn giản:

```text
Thời gian | available RAM | swap used | disk % | webapi RAM | rabbit RAM | health | ghi chú
```

Nếu chỉ có một spike ngắn lúc deploy và health nhanh chóng bình thường, giữ gói $7 và theo dõi. Nếu có một trong các tình huống sau lặp lại từ hai lần trở lên trong ngày, lên lịch thực hiện Phần 9:

- OOMKilled.
- `si`/`so` trong `vmstat` liên tục khác 0 khi có tải bình thường.
- Health timeout hoặc latency người dùng tăng cùng lúc RAM chạm giới hạn.
- Available RAM dưới 100 MB ngay cả khi idle.

Trong lúc chưa nâng, giảm/bỏ job không cấp thiết và tạm giảm tải; không tăng memory limit vượt tổng RAM 1 GB vì sẽ làm cả VPS OOM.

### 10.3 Dọn Docker image mỗi tuần

Trên **VPS**, trước tiên xem dung lượng:

```bash
cd /opt/dodo
docker image ls --digests
docker system df
```

Xác nhận container production đang chạy và GHCR còn image SHA rollback. Sau đó mới chạy:

```bash
docker image prune -af --filter 'until=720h'
docker system df
```

Lệnh chỉ xóa image local không được container nào dùng và cũ hơn 30 ngày. Không thêm `--volumes`; không chạy `docker system prune --volumes`. Named volume chứa PostgreSQL là dữ liệu production.

Luôn giữ SHA đang chạy và ít nhất một SHA tốt gần nhất trong GHCR để có thể pull rollback. Trước khi xóa package version trên GitHub, so sánh với:

```bash
grep '^IMAGE_TAG=' /opt/dodo/.env
docker inspect dodo-webapi --format '{{.Config.Image}}'
```

### 10.4 Tạo cảnh báo bên ngoài VPS

Ở AWS/GitHub/dịch vụ monitoring, thiết lập:

- AWS billing alarm/budget.
- Lightsail CPU và burst-capacity alarm.
- External uptime monitor gọi `/health` mỗi 1–5 phút.
- Cảnh báo khi backup S3 không có object mới trong 26 giờ.

Các bước tối thiểu cho AWS Budget:

1. AWS Console → **Billing and Cost Management → Budgets → Create budget**.
2. Chọn **Cost budget** và chu kỳ monthly.
3. Nhập mức ngân sách phù hợp, ví dụ `10 USD` nếu muốn phát hiện sớm chi phí vượt compute $7.
4. Tạo cảnh báo email ở 80% actual và 100% forecasted/actual.

Budget cảnh báo chứ không tự tắt VPS. Email tài khoản AWS phải được kiểm tra thường xuyên.

Trong Lightsail, mở instance → **Metrics** → tạo alarm cho CPU/burst capacity theo mức Console hỗ trợ. Uptime monitor phải chạy ngoài VPS; nếu cron trên cùng VPS gọi health thì nó không thể báo khi cả máy chết.

Mỗi ngày có thể kiểm tra tuổi backup thủ công trên **VPS**:

```bash
BUCKET_NAME="dodo-prod-backups-UNIQUE"
aws s3 ls "s3://${BUCKET_NAME}/postgres/" | tail -n 3
tail -n 50 /opt/dodo/backups/backup.log
```

Thay bucket thật. Phải có object mới trong vòng 26 giờ và log gần nhất chứa `completed`.

### 10.5 Cập nhật .NET và package

Dự án đang dùng .NET 8, hết support ngày 10/11/2026. Trước ngày đó phải có issue/milestone nâng lên .NET 10 LTS, cập nhật EF/Npgsql/health-check packages theo cùng major, chạy migration test và smoke test đầy đủ. Hằng tháng chạy:

```powershell
dotnet list SMEFLOWSystem.sln package --outdated
dotnet list SMEFLOWSystem.sln package --vulnerable --include-transitive
```

Hai lệnh này chạy ở **LOCAL — PowerShell trong thư mục repository**, không chạy trên VPS. Chúng chỉ liệt kê; muốn nâng package phải tạo branch riêng, sửa project, chạy test/migration test, để CI build image rồi deploy bằng SHA. Không sửa source hoặc nâng package trực tiếp trên VPS.

---

## 11. Checklist go-live

Checklist này không phải khối lệnh để copy. Làm từ trên xuống; chỉ đổi `[ ]` thành `[x]` khi đã kiểm tra thực tế. Nếu một mục không áp dụng, ghi lý do bên cạnh thay vì tự đánh dấu đạt. Chưa go-live nếu còn mục liên quan secret, database, HTTPS, CI hoặc backup chưa đạt.

### 11.1 Secret và cấu hình

- [ ] Toàn bộ secret cần thiết đã được tạo mới hoặc rotate nếu từng sử dụng.
- [ ] `appsettings.json`, Git history và Docker image không còn credential VNPay/SMTP/Cloudinary/Face++ hoạt động thật.
- [ ] `.env` không bị Git track và permission trên VPS là `600`.

Kiểm tra permission trên **VPS** bằng `stat -c '%A %U:%G %n' /opt/dodo/.env`; kết quả phải là `-rw------- ubuntu:ubuntu`. Không dùng `cat .env` để chụp màn hình minh chứng.

### 11.2 Code, database và test

- [ ] Không còn Azure SQL Edge/SQL Server provider trong code production.
- [ ] EF configuration không còn SQL Server-specific SQL.
- [ ] Migration PostgreSQL mới chạy thành công trên database trống.
- [ ] Migration mới đã review script idempotent và tương thích ngược với image liền trước; thay đổi destructive có maintenance plan riêng.
- [ ] Đã hiểu `DropTable`/`DropColumn` làm mất dữ liệu và không dùng `down -v` trên production.
- [ ] Local Compose chạy và mọi container healthy.
- [ ] Solution có test project thật; CI không còn advisory `NU1901`–`NU1904`.

Các mục này xác minh ở branch/repository và CI, không sửa trực tiếp trên VPS.

### 11.3 Tài nguyên và bảo mật mạng

- [ ] RabbitMQ dùng user riêng, không dùng `guest/guest`.
- [ ] RabbitMQ dùng watermark tuyệt đối 128 MiB; Hangfire worker = 2, attendance batch = 100/3 và prefetch = 10.
- [ ] Chỉ 22, 80, 443 mở ngoài Internet.
- [ ] API bind `127.0.0.1:8085`.
- [ ] Docker subnet không trùng mạng khác; `KnownProxies` đúng gateway và log không có unknown proxy.
- [ ] `https://api.example.com/health` trả HTTP 200.

Thay domain mẫu trước khi test. Port kiểm tra ở cả Lightsail Networking, UFW và `sudo ss -lntp`; PostgreSQL 5432, Redis 6379, RabbitMQ 5672/15672 không được listen trên public interface.

### 11.4 Chức năng ứng dụng và thanh toán

- [ ] Login, refresh token, SignalR, Hangfire và RabbitMQ smoke test đạt.
- [ ] Payment chạy sandbox end-to-end trước khi đổi Production.
- [ ] SePay/VNPay webhook dùng HTTPS domain thật và xác minh chữ ký.
- [ ] `Invite__OnboardingUrl`, CORS và `AllowedHosts` dùng đúng domain production.

Không test thanh toán production bằng cách tự sửa database. Lưu timestamp/request ID của giao dịch sandbox để có thể đối chiếu log nhưng không đưa secret/signature đầy đủ vào tài liệu công khai.

### 11.5 CI/CD và rollback

- [ ] CI build/test/push image thành công.
- [ ] `PROD_KNOWN_HOSTS` đã đối chiếu fingerprint; CI dùng strict host checking.
- [ ] Deploy một SHA 40 ký tự thành công và Compose/RabbitMQ config của cùng commit đã được upload.
- [ ] Cố tình deploy SHA image không tồn tại và xác nhận rollback image/config hoạt động.
- [ ] Đã hiểu rollback image không rollback database; không test destructive migration trên production.
- [ ] Có file `predeploy-*.dump` trước deploy và không dựa vào nó thay S3 backup.

Không cần cố tình làm hỏng production đang có người dùng để test rollback. Thực hiện bài test image không tồn tại trước go-live khi chưa có traffic, theo dõi script quay lại image/config cũ và health vẫn 200.

### 11.6 Backup, snapshot và chi phí

- [ ] Backup đã có trên S3.
- [ ] Restore thử vào database test thành công.
- [ ] Lightsail snapshot và billing alarm đã bật.

Khi toàn bộ checkbox đạt, chụp/lưu bằng chứng không chứa secret: URL health, GitHub Actions run, tên/SHA image, timestamp backup/restore test và ảnh alarm/budget.

---

## 12. Chi phí dự kiến

| Hạng mục | Chi phí |
|---|---:|
| Lightsail 1 GB, public IPv4 | 7 USD/tháng |
| Static IP khi gắn vào instance | 0 USD |
| Let's Encrypt | 0 USD |
| GHCR/GitHub Actions | theo quota tài khoản/repository |
| S3 database backup | phụ thuộc dung lượng/request, thường nhỏ với MVP |
| Lightsail snapshots | 0,05 USD/GB-tháng dung lượng snapshot thực dùng tại thời điểm kiểm tra |
| Domain | tùy nhà cung cấp |

`7 USD/tháng` chỉ là compute nền, không phải trần hóa đơn. Static IP đang attach và Let's Encrypt là 0 USD; snapshot, S3, domain, data transfer vượt quota và thời gian chạy song song khi nâng plan có thể phát sinh phí. Cách tiết kiệm đúng là dùng gói 7 USD, lifecycle S3 30 ngày, xóa snapshot thủ công thừa, không dùng Load Balancer/managed database lúc MVP và bật Budget; không được tiết kiệm bằng cách bỏ backup.

Để giữ gần mức $7 nhất:

1. Chỉ duy trì một instance $7 trong vận hành bình thường.
2. Luôn attach Static IP vào instance đang dùng; IP rời instance có thể phát sinh phí.
3. Giữ lifecycle S3 30 ngày và kiểm tra noncurrent version cũng được xóa.
4. Chỉ giữ manual snapshot có mục đích; automatic snapshot vẫn phát sinh storage cost.
5. Khi nâng máy, thời gian chạy song song máy cũ/mới càng lâu thì phần chi phí theo giờ càng tăng.
6. Không tạo Load Balancer, managed database hoặc NAT Gateway cho kiến trúc MVP này.

Mở AWS Billing/Budget ít nhất mỗi tuần trong tháng đầu. Bảng trên là dự toán, hóa đơn AWS Console mới là số thực tế.

Lightsail tính theo giờ đến mức tối đa theo tháng và instance dừng vẫn phát sinh phí; muốn dừng tính compute phải **xóa instance**. Xem [Lightsail billing FAQ](https://docs.aws.amazon.com/lightsail/latest/userguide/amazon-lightsail-frequently-asked-questions-faq-billing-and-account-management.html).

---

## 13. Lệnh xử lý sự cố thường dùng

Phần này thực hiện trên **VPS** trừ khi có ghi rõ LOCAL/GITHUB. Khi có lỗi, không chạy tất cả lệnh cùng lúc. Làm theo thứ tự: xác định triệu chứng → thu thập log → sửa nguyên nhân → health-check. Trước tiên:

```bash
cd /opt/dodo
pwd
docker compose ps
curl -i http://127.0.0.1:8085/health
```

Nếu `pwd` không phải `/opt/dodo`, dừng lại và `cd /opt/dodo` trước khi dùng Docker Compose.

### 13.1 API trả 502/503 hoặc health lỗi

Chạy:

```bash
docker compose ps webapi
docker compose logs --since=15m --tail=300 webapi
docker inspect dodo-webapi --format \
  'status={{.State.Status}} exit={{.State.ExitCode}} oom={{.State.OOMKilled}} error={{.State.Error}}'
curl -i http://127.0.0.1:8085/health
sudo nginx -t
sudo tail -n 100 /var/log/nginx/error.log
```

Đọc kết quả:

- `webapi` là `Exited`: đọc exception đầu tiên trong log; không chỉ đọc dòng lỗi cuối.
- `oom=true`: xem Phần 10 và chuẩn bị nâng gói ở Phần 9; restart chỉ là tạm thời.
- Health local `200` nhưng domain lỗi: tập trung vào Nginx, DNS, TLS/firewall.
- Health local cũng lỗi: tập trung vào webapi hoặc dependency.

Chỉ khi log cho thấy lỗi tạm thời và không có migration đang chạy, restart riêng API:

```bash
docker compose restart webapi
for i in $(seq 1 20); do
  curl --fail http://127.0.0.1:8085/health && break
  sleep 3
done
docker compose logs --since=5m --tail=200 webapi
```

Restart webapi không restart database. Nếu API tiếp tục crash, không lặp restart vô hạn; sửa nguyên nhân hoặc rollback image ở 13.6.

### 13.2 PostgreSQL lỗi hoặc API báo không kết nối database

```bash
docker compose ps postgres
docker compose logs --since=30m --tail=300 postgres
docker compose exec -T postgres pg_isready
docker compose exec -T postgres sh -c \
  'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -c "SELECT now(), current_database();"'
docker compose exec -T postgres sh -c \
  'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -c "SELECT * FROM \"__EFMigrationsHistory\" ORDER BY \"MigrationId\";"'
```

`pg_isready` phải báo accepting connections. Nếu disk đầy, không xóa volume; xem 13.5. Nếu password/config sai sau khi sửa `.env`, kiểm tra `docker compose config` không lộ secret ra màn hình chia sẻ và recreate đúng service có kiểm soát.

Không chạy `pg_restore --clean` vào database production đang hoạt động để chữa lỗi kết nối.

### 13.3 RabbitMQ hoặc background job lỗi

```bash
docker compose ps rabbitmq redis
docker compose logs --since=30m --tail=300 rabbitmq
docker compose exec -T rabbitmq rabbitmq-diagnostics -q ping
docker compose exec -T rabbitmq rabbitmq-diagnostics memory_breakdown
docker compose logs --since=30m --tail=300 webapi | grep -iE 'rabbit|hangfire|redis|background|exception' || true
```

`ping` phải thành công. Nếu RabbitMQ chạm memory watermark, không tăng limit tùy tiện trên gói 1 GB; giảm tốc độ producer/background worker và xem Phần 10. Queue/job quan trọng phải có reconciliation/idempotency dựa vào PostgreSQL.

### 13.4 HTTPS, domain hoặc Certbot lỗi

Trên **LOCAL — PowerShell**:

```powershell
$DOMAIN="api.example.com"
Resolve-DnsName $DOMAIN -Type A
curl.exe -I "http://$DOMAIN/health"
curl.exe -i "https://$DOMAIN/health"
```

Trên **VPS**:

```bash
DOMAIN="api.example.com"
sudo nginx -t
sudo systemctl --no-pager status nginx
sudo certbot certificates
curl -i -H "Host: $DOMAIN" http://127.0.0.1/health
sudo tail -n 100 /var/log/nginx/error.log
```

Thay domain mẫu ở cả hai nơi. Nếu DNS sai IP, sửa record và chờ propagation; không xin chứng chỉ lặp lại. Nếu `nginx -t` lỗi, sửa đúng file/dòng được báo rồi mới reload. Nếu chứng chỉ gần hết hạn, chạy `sudo certbot renew --dry-run` để tìm lỗi trước.

### 13.5 Disk đầy

Trước tiên chỉ quan sát:

```bash
df -h
docker system df
sudo du -xhd1 /var/lib/docker 2>/dev/null | sort -h
du -hd2 /opt/dodo 2>/dev/null | sort -h | tail -n 30
```

Các cách dọn an toàn tương đối:

```bash
docker image prune -af --filter 'until=720h'
sudo journalctl --vacuum-time=14d
sudo logrotate -f /etc/logrotate.d/dodo-backup
df -h
```

Chỉ image không được container dùng và journal/log cũ bị dọn. Không xóa thủ công file dưới `/var/lib/docker/volumes`, không chạy prune volume và không xóa file PostgreSQL.

### 13.6 Deploy lỗi hoặc cần rollback image

Trước tiên xem SHA/config đang chạy:

```bash
cd /opt/dodo
grep '^IMAGE_TAG=' .env
docker inspect dodo-webapi --format '{{.Config.Image}}'
ls -1t /opt/dodo/backups/predeploy-*.dump 2>/dev/null | head
docker compose logs --since=30m --tail=300 webapi
```

Ở **GITHUB → Actions/Packages**, chọn một SHA 40 ký tự đã build thành công và được biết là tốt. Xác nhận migration của release mới tương thích ngược với SHA đó. Sau đó trên **VPS**, thay placeholder bằng SHA thật:

```bash
cd /opt/dodo
./deploy.sh KNOWN_GOOD_40_CHARACTER_SHA
docker compose ps
curl --fail --show-error http://127.0.0.1:8085/health
```

Không giữ nguyên chữ `KNOWN_GOOD_40_CHARACTER_SHA`, không dùng SHA 7 ký tự và không dùng `latest`.

Rollback image/config **không rollback database**. Nếu lỗi bắt đầu sau migration, trước khi rollback phải xem `__EFMigrationsHistory`, nội dung migration và file `predeploy-*.dump`. Không tự chạy restore chỉ vì image rollback không lên.

### 13.7 Backup cron không chạy

```bash
crontab -l
tail -n 100 /opt/dodo/backups/backup.log
stat -c '%A %U:%G %n' /opt/dodo/backup-postgres.sh /opt/dodo/backups
aws sts get-caller-identity
BUCKET_NAME="dodo-prod-backups-UNIQUE"
aws s3 ls "s3://${BUCKET_NAME}/postgres/" | tail
```

Thay bucket thật. Script phải executable, cron phải thuộc user `ubuntu`, AWS identity phải là `dodo-backup-writer`. Sau khi sửa, chạy bằng tay `/opt/dodo/backup-postgres.sh`; không đợi đến giờ cron tiếp theo mới biết còn lỗi.

### 13.8 SSH hoặc GitHub Actions không vào được VPS

Từ **LOCAL — PowerShell**:

```powershell
Test-NetConnection STATIC_IP -Port 22
ssh -vv -i "$env:USERPROFILE\.ssh\dodo_prod_actions" ubuntu@STATIC_IP "echo ssh-ok"
```

Nếu port 22 không mở, kiểm tra Lightsail Networking và UFW bằng browser-based SSH. Nếu báo host identification changed sau khi đổi instance, xác minh fingerprint theo 6.1.4 rồi mới cập nhật known-hosts; không tắt strict checking.

Nếu local SSH được nhưng Actions lỗi, kiểm tra lại tên environment chính xác là `production` và đủ bốn secret `PROD_HOST`, `PROD_USER`, `PROD_SSH_KEY`, `PROD_KNOWN_HOSTS`.

### 13.9 Các lệnh bị cấm khi chưa có kế hoạch phục hồi

Không chạy các lệnh sau trên production nếu chưa có backup đã kiểm tra và chưa hiểu tác động:

```text
docker compose down -v
docker volume prune
docker system prune --volumes
rm -rf /var/lib/docker/volumes/...
DROP DATABASE
pg_restore --clean vào database production đang có người dùng
```

`down -v`, `volume prune` và `system prune --volumes` có thể xóa dữ liệu PostgreSQL/Redis/RabbitMQ. Restore production là maintenance operation riêng: dừng webapi/writer, tạo thêm backup hiện trạng, xác nhận đúng file/đúng database, ghi rõ RPO và chỉ thực hiện khi đã chấp nhận mất mọi write sau thời điểm dump.
