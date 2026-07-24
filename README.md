# DodoSystem Backend

Backend cho nền tảng quản trị doanh nghiệp đa tenant, hỗ trợ quản lý nhân sự, ca làm việc, chấm công, nghỉ phép, bảng lương, module thuê bao và thanh toán.

Hệ thống được xây dựng bằng **ASP.NET Core 8**, tổ chức theo **Clean Architecture** và triển khai trên **AWS Lightsail** bằng Docker Compose.

## Demo

- Swagger: https://dodosystem-api.duckdns.org/swagger/index.html
- Health check: https://dodosystem-api.duckdns.org/health

## Chức năng chính

- Xác thực JWT, refresh token và phân quyền theo vai trò.
- Quản lý tenant, phòng ban, chức vụ, nhân viên và phạm vi quản lý.
- Quản lý ca làm việc, phân ca, ngày nghỉ và onboarding.
- Chấm công theo vị trí và xác minh khuôn mặt.
- Quản lý nghỉ phép, số dư phép, bảng lương, thưởng và khấu trừ.
- Quản lý module thuê bao, đơn hàng và thanh toán.
- Thông báo thời gian thực bằng SignalR.
- Xử lý tác vụ nền bằng Hangfire, Redis và RabbitMQ.
- Sử dụng Outbox Pattern để hỗ trợ phát sự kiện tin cậy.

## Công nghệ

| Nhóm | Công nghệ |
|---|---|
| Backend | ASP.NET Core 8, REST API, Swagger/OpenAPI |
| Kiến trúc | Clean Architecture, Repository, Unit of Work, Outbox Pattern |
| Database | PostgreSQL 16, Entity Framework Core 8 |
| Cache và background jobs | Redis, Hangfire |
| Messaging | RabbitMQ |
| Realtime | SignalR |
| Authentication | JWT Bearer, BCrypt, role-based authorization |
| Testing | xUnit, coverlet |
| Container | Docker, Docker Compose, multi-stage Dockerfile |
| CI/CD | GitHub Actions, GHCR, SSH deployment |
| Infrastructure | AWS Lightsail, Nginx, Let's Encrypt, Amazon S3 |
| Monitoring | Grafana Cloud, Grafana Alloy, Prometheus Remote Write |

## Kiến trúc triển khai

```text
Frontend / Client
       |
       | HTTPS
       v
DuckDNS -> Nginx + Let's Encrypt
                    |
                    v
             ASP.NET Core API
              /      |      \
             v       v       v
        PostgreSQL  Redis  RabbitMQ
             |
             v
      Amazon S3 Backups

Grafana Alloy -> Grafana Cloud
```

Nginx là entry point của backend và chuyển tiếp request đến Web API. PostgreSQL, Redis và RabbitMQ chỉ giao tiếp trong Docker network, không mở trực tiếp ra Internet.

## Cấu trúc repository

```text
SMEFLOWSystem.WebAPI/           API, middleware, JWT, SignalR, health check
SMEFLOWSystem.Application/      Use cases, DTO, validation, mapping, background jobs
SMEFLOWSystem.Core/             Domain entities và cấu hình domain
SMEFLOWSystem.Infrastructure/   EF Core, repository, messaging, external services
SMEFLOWSystem.SharedKernel/     Interface và thành phần dùng chung
SMEFLOWSystem.Tests/            Unit tests
.github/workflows/              CI/CD pipeline
docker-compose.yml              Production services và resource limits
```

Quan hệ phụ thuộc chính:

```text
WebAPI -> Application -> Core -> SharedKernel
   |
   v
Infrastructure
```

## Chạy local

### Yêu cầu

- Docker Desktop
- Docker Compose v2
- Git

### Khởi động

1. Sao chép `.env.example` thành `.env`.
2. Cập nhật các giá trị cấu hình dành cho môi trường local.
3. Khởi động hệ thống:

```bash
docker compose up -d --build
```

Sau khi các service healthy:

- Swagger: http://localhost:8085/swagger
- Health check: http://localhost:8085/health
- SignalR hub: http://localhost:8085/hubs/notifications

## CI/CD

GitHub Actions tự động thực hiện:

1. Restore dependencies.
2. Build solution.
3. Chạy unit tests.
4. Build Docker image.
5. Gắn image tag theo Git commit SHA.
6. Push image lên GitHub Container Registry.
7. Deploy phiên bản mới lên AWS Lightsail qua SSH.
8. Kiểm tra health sau deploy.

Nếu build, test hoặc health check thất bại, phiên bản mới không được giữ làm bản production.

## Production

Hệ thống hiện chạy trên AWS Lightsail với:

- Ubuntu Linux.
- Docker Compose.
- Nginx reverse proxy.
- HTTPS bằng Let's Encrypt.
- UFW và Fail2Ban.
- Resource limit và health check cho từng container.
- Docker log rotation.
- PostgreSQL backup lên Amazon S3.

Đây là kiến trúc dành cho MVP tải thấp, ưu tiên chi phí hợp lý và khả năng vận hành đơn giản.

## Backup

PostgreSQL được backup hằng ngày bằng `pg_dump` và lưu ngoài VPS trên Amazon S3.

Chiến lược phục hồi gồm:

- Database backup trên S3.
- Pre-deploy dump trước mỗi lần phát hành.
- Lightsail snapshot để phục hồi toàn bộ VPS khi cần.

## Monitoring

Grafana Alloy được triển khai bằng Docker Compose và gửi Linux host metrics lên Grafana Cloud qua Prometheus Remote Write.

Các chỉ số đang theo dõi:

- CPU và load average.
- RAM và swap.
- Filesystem và dung lượng disk.
- Network traffic.
- Uptime của VPS.
- Trạng thái health của backend.

Alloy được giới hạn ở **160 MiB RAM** và đã được tối ưu từ khoảng **156 MiB xuống còn 56 MiB** trong quá trình vận hành thử nghiệm.

## Bảo mật

- Chỉ commit `.env.example`; không commit secret production.
- Secret production được lưu trên VPS và GitHub Environment Secrets.
- Backend chỉ expose qua Nginx và HTTPS.
- PostgreSQL, Redis và RabbitMQ không mở port public.
- Firewall chỉ cho phép các cổng cần thiết.
- Docker image không chứa file `.env`.
