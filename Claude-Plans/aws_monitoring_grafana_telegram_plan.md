# Kế hoạch bổ sung monitoring cho DodoSystem trên AWS Lightsail

> **Phạm vi:** bổ sung giám sát host, container, health của ứng dụng, tuổi backup S3 và cảnh báo Telegram cho kiến trúc đã triển khai theo `aws_deploy_guide.md`.  
> **Stack monitoring:** Grafana Cloud + Grafana Alloy (`prometheus.exporter.unix` dùng node_exporter tích hợp) + cAdvisor tùy chọn + Telegram.  
> **Môi trường đích:** Ubuntu Lightsail chạy Docker Compose, Nginx trên host và ASP.NET Core/PostgreSQL/Redis/RabbitMQ trong container.  
> **Ngày rà soát tài liệu/phiên bản:** 15/07/2026.  
> **Mục tiêu:** phát hiện sớm VPS quá tải, container chết/OOM, API mất health, disk gần đầy, metric ngừng gửi và backup S3 quá hạn; cảnh báo phải đến Telegram ngay cả khi toàn bộ VPS không còn hoạt động.

Tài liệu này là phần mở rộng của [`aws_deploy_guide.md`](./aws_deploy_guide.md), không thay thế các phần hardening, CI/CD, Nginx, backup hay xử lý sự cố trong guide đó. Không chạy lại migration, không tạo lại database và không thay đổi volume production chỉ để cài monitoring.

Thứ tự thực hiện ngắn gọn:

1. Audit tài nguyên rồi chọn **Profile Lite 1 GB** hoặc **Profile Full từ 2 GB**; không mặc định bật cAdvisor trên máy 1 GB.
2. Tạo Grafana Cloud stack, access policy token chỉ có quyền ghi metric và Telegram bot/chat riêng cho cảnh báo.
3. Tạo `/opt/dodo/monitoring/monitoring.env` chỉ có bốn biến Grafana và chạy Alloy Lite bằng Compose project `dodo-monitoring` riêng; không sửa Application Compose.
4. Sau khi máy có từ 2 GB và đạt resource gate, mới bật cAdvisor privileged nhưng không giữ token; Alloy vẫn non-privileged và remote-write ra Grafana Cloud bằng HTTPS 443.
5. Xuất trạng thái backup thành Prometheus textfile metric.
6. Import/bật dashboard, tạo alert rules và route chúng tới Telegram.
7. Tách workflow/deploy/rollback monitoring khỏi workflow và file Compose phát hành backend.
8. Test warning, critical, resolved, mất telemetry, inhibition chống bão alert và backup quá hạn trước khi nghiệm thu.

---

## 0. Sửa lại cách hiểu kiến trúc

Chuỗi trong yêu cầu mô tả đủ các thành phần nhưng chưa đúng chiều dữ liệu monitoring. Node Exporter/cAdvisor không gửi trực tiếp tới Telegram và Grafana Cloud cũng không tự kết nối vào port exporter trên VPS. Kế hoạch này dùng **Grafana Alloy** làm collector. Ở Profile Lite, Alloy chạy node_exporter ngay trong process bằng `prometheus.exporter.unix`, do đó không cần thêm container Node Exporter riêng.

Kiến trúc đầy đủ gồm bốn luồng độc lập:

```text
Luồng phát hành
GitHub Actions
   │ build/push image SHA + upload operation config
   ▼
AWS Lightsail → Docker Compose

Luồng request
Internet → HTTPS 443 → Nginx host → 127.0.0.1:8085 → ASP.NET Core

Luồng dữ liệu ứng dụng
ASP.NET Core
   ├── PostgreSQL
   ├── Redis
   └── RabbitMQ
PostgreSQL → pg_dump → Amazon S3 backup

Luồng monitoring Profile Lite — Lightsail 1 GB
host /proc,/sys,/filesystem ─┐
backup .prom ────────────────┼─> Alloy + prometheus.exporter.unix
                             └─> remote write HTTPS 443 ─> Grafana Cloud
                                                           ├─> Dashboard
Synthetic Monitoring /health ───────────────────────────────┤
                                                           └─> Alerting ─> Telegram

Phần bổ sung của Profile Full — chỉ bật sau khi VPS từ 2 GB và đạt resource gate
Docker/cgroups ─> cAdvisor privileged, không giữ token ─> Alloy non-privileged ─> Grafana Cloud
```

Vai trò từng thành phần:

| Thành phần | Quan sát được | Không dùng để làm gì |
|---|---|---|
| `prometheus.exporter.unix` trong Alloy | Dùng node_exporter tích hợp để lấy CPU, load, RAM, swap, filesystem, disk I/O, network và textfile metric | Không cần một container Node Exporter riêng; không đọc metric nghiệp vụ trong PostgreSQL/Redis/RabbitMQ |
| cAdvisor standalone, Profile Full | CPU, RAM, network, filesystem, last-seen của từng container; chỉ xuất metric trong monitoring network | Không giữ Grafana token, không bật trên Profile Lite 1 GB và không thay Docker healthcheck |
| Grafana Alloy | Chạy exporter tích hợp, scrape, relabel, buffer ngắn hạn và remote-write | Không phải nơi lưu metric dài hạn |
| Grafana Cloud | Lưu/query metric, dashboard, alert evaluation | Không được phép SSH vào VPS |
| Telegram | Nhận firing/resolved notification | Không phải nguồn dữ liệu hay nơi lưu token của Alloy |

Baseline 1 GB giám sát host, health API từ bên ngoài và backup. Container metrics chỉ thuộc Profile Full. Cả hai profile chưa có query-level PostgreSQL, Redis keyspace hay RabbitMQ queue depth; Phần 14 nêu đường mở rộng khi thực sự cần.

---

## 1. Điều kiện tài nguyên bắt buộc

### 1.1 Không bật nguyên Profile Full trên gói 1 GB

Trong `aws_deploy_guide.md`, profile mục tiêu dùng tổng hard limit container là 816 MB. Tuy nhiên tại thời điểm lập kế hoạch, `docker-compose.yml` thực tế trong repository đang là:

| Service | `mem_limit` hiện tại |
|---|---:|
| PostgreSQL | 256 MB |
| Redis | 96 MB |
| RabbitMQ | 192 MB |
| ASP.NET Core | 384 MB |
| **Tổng** | **928 MB** |

Đây là khác biệt cần reconcile với guide trước lần phát hành tiếp theo. Không tự giảm RAM production chỉ để “nhét” monitoring nếu chưa đo tải và chưa smoke test.

Nếu chạy ba container riêng như plan cũ, monitoring từng dự kiến thêm:

| Service | Hard limit ban đầu | CPU limit ban đầu |
|---|---:|---:|
| Node Exporter | 64 MB | 0.10 |
| cAdvisor | 128 MB | 0.25 |
| Grafana Alloy | 192 MB | 0.25 |
| **Tổng monitoring** | **384 MB** | **0.60** |

Tổng hard limit khi đó là 1.312 MB, chưa tính Ubuntu, Docker daemon, Nginx, kernel cache và `pg_dump`; vì vậy không dùng mô hình ba container trên 1 GB.

Tài liệu sửa lại thành hai profile:

| Profile | Thành phần trên VPS | Khi nào dùng |
|---|---|---|
| **Lite** | Một container Alloy: `prometheus.exporter.unix` + textfile backup + remote write | Triển khai trước trên 1 GB, nhưng chỉ giữ nếu kiểm thử resource gate đạt |
| **Full** | Alloy Lite non-privileged + cAdvisor standalone privileged nhưng không giữ token; sau này mới cân nhắc DB/broker exporter/logs | VPS có ít nhất 2 GB và còn headroom sau kiểm thử |

Không ghi “cứ nâng 2 GB là chắc chắn đủ”. Chỉ bật/giữ Profile Full khi đồng thời đạt:

- VPS có ít nhất 2 GB RAM.
- Sau thời gian tải đại diện vẫn còn khoảng **20–25% memory available**.
- `vmstat 1 5` không cho thấy swap-in/swap-out liên tục.
- Không có OOM ở kernel/container.
- Backup và deploy không làm API health timeout.

`mem_limit` là trần tối đa, không phải RAM được reserve sẵn. Quyết định cuối cùng dựa trên `docker stats --no-stream`, `free -h`, `vmstat 1 5` và dữ liệu Grafana, không chỉ cộng hard limit.

### 1.2 Profile Lite phù hợp nhất khi vẫn giữ 1 GB

Triển khai trước:

- Grafana Cloud và Telegram ở bên ngoài VPS.
- Một container Grafana Alloy, hard limit ban đầu 160 MB.
- `prometheus.exporter.unix` trong Alloy cho host CPU/RAM/swap/filesystem/network.
- Textfile metric cho backup S3.
- Synthetic Monitoring hoặc external uptime gọi `/health`.

Chưa bật:

- cAdvisor/container metrics.
- PostgreSQL/Redis/RabbitMQ exporter.
- Loki/log shipping.
- ASP.NET application metrics có cardinality cao.

Vì hard limit application hiện đã là 928 MB, Profile Lite cũng không được coi là bảo đảm chạy tốt trên 1 GB. Sau rollout canary, nếu memory available dưới 20%, swap liên tục, OOM hoặc health xấu đi thì dừng Alloy và quay về Lightsail native alarm + external monitoring cho đến khi nâng máy.

### 1.3 Baseline trước khi thay đổi

Trên **VPS**, thu số liệu ít nhất một ngày bình thường và lưu kết quả không chứa secret:

```bash
cd /opt/dodo
docker compose ps
docker stats --no-stream
free -h
df -h
vmstat 1 5
docker system df
```

Không triển khai monitoring nếu đang có một trong các dấu hiệu sau:

- Container `Restarting`, `unhealthy` hoặc từng `OOMKilled` chưa rõ nguyên nhân.
- Root filesystem đã dùng trên 80%.
- RAM available gần 0 hoặc swap in/out liên tục.
- Backup S3 hiện tại chưa chạy/restore test chưa đạt.
- `/health` production chưa trả HTTP 200 ổn định.

### 1.4 Dung lượng disk cho Alloy

`prometheus.remote_write` có WAL để chịu mất mạng ngắn hạn. Cần chừa tối thiểu 1 GB disk trống cho monitoring/WAL ở quy mô VPS này và alert khi disk còn dưới 15%. WAL không thay cho Grafana Cloud storage; khi mất mạng kéo dài, dữ liệu cũ có thể bị truncate.

---

## 2. Chuẩn bị tài khoản Grafana Cloud và credential

### 2.1 Tạo stack và lấy thông tin Prometheus remote write

Thực hiện trên **GRAFANA CLOUD**:

1. Tạo hoặc mở Grafana Cloud stack dành cho production.
2. Mở **Connections → Add new connection → Hosted Prometheus/Send Metrics** hoặc phần cấu hình Prometheus của stack.
3. Ghi lại ba giá trị:
   - Remote write URL, dạng `https://prometheus-xxx.grafana.net/api/prom/push`.
   - Prometheus username/instance ID.
   - Region/stack name để đối chiếu khi xử lý sự cố.
4. Tạo access policy token riêng, ví dụ `dodo-prod-metrics-writer`, chỉ cấp scope cần để **ghi metric**.
5. Không dùng token có quyền Admin, không dùng token của người dùng cá nhân và không dùng chung token với CI/CD.

Tài liệu chính thức: [Grafana Alloy `prometheus.remote_write`](https://grafana.com/docs/alloy/latest/reference/components/prometheus/prometheus.remote_write/) và [Grafana Cloud integrations](https://grafana.com/docs/grafana-cloud/monitor-infrastructure/integrations/get-started/).

### 2.2 Dùng `monitoring.env` riêng, tuyệt đối không dùng `.env` ứng dụng

`/opt/dodo/.env` chứa database/JWT/SMTP/RabbitMQ/Cloudinary secrets và chỉ thuộc Application Compose. Không dùng `env_file: .env` cho Alloy vì `env_file` sẽ đưa toàn bộ biến trong file vào container.

Bốn biến monitoring chỉ đặt trong `/opt/dodo/monitoring/monitoring.env`:

```dotenv
MONITORING_INSTANCE=dodo-prod
GRAFANA_CLOUD_PROMETHEUS_URL=https://prometheus-xxx.grafana.net/api/prom/push
GRAFANA_CLOUD_PROMETHEUS_USER=YOUR_INSTANCE_ID
GRAFANA_CLOUD_API_TOKEN=YOUR_METRICS_WRITE_TOKEN
```

Trong repository chỉ commit `monitoring/monitoring.env.example`:

```dotenv
MONITORING_INSTANCE=dodo-prod
GRAFANA_CLOUD_PROMETHEUS_URL=https://prometheus-xxx.grafana.net/api/prom/push
GRAFANA_CLOUD_PROMETHEUS_USER=SET-IN-PRODUCTION-ENV
GRAFANA_CLOUD_API_TOKEN=SET-IN-PRODUCTION-ENV
```

Quy tắc:

- `/opt/dodo/monitoring` là `700 ubuntu:ubuntu`; `monitoring.env` là `600 ubuntu:ubuntu`.
- Thêm `monitoring/monitoring.env` vào `.gitignore`; workflow không upload/ghi đè file này.
- Monitoring Compose không có `env_file:`. Nó dùng `--env-file` để interpolation và `environment:` allowlist đúng bốn biến.
- Không đặt token vào `config-*.alloy`, Compose, Markdown, application `.env`, GitHub Actions secret hoặc image.
- GitHub Actions không cần biết Grafana token; nó chỉ upload file cấu hình không chứa secret.
- Khi rotate token, sửa `monitoring.env`, recreate riêng Alloy và revoke token cũ sau khi metric mới đã vào Cloud.

Tạo file và kiểm tra key mà không in value:

```bash
sudo install -d -o ubuntu -g ubuntu -m 0700 /opt/dodo/monitoring
if [ ! -e /opt/dodo/monitoring/monitoring.env ]; then
  install -o ubuntu -g ubuntu -m 0600 /dev/null /opt/dodo/monitoring/monitoring.env
else
  sudo chown ubuntu:ubuntu /opt/dodo/monitoring/monitoring.env
  chmod 0600 /opt/dodo/monitoring/monitoring.env
fi
# Mở bằng editor trên VPS và điền đúng bốn key ở trên.

cd /opt/dodo/monitoring
for key in MONITORING_INSTANCE GRAFANA_CLOUD_PROMETHEUS_URL GRAFANA_CLOUD_PROMETHEUS_USER GRAFANA_CLOUD_API_TOKEN; do
  grep -q "^${key}=" monitoring.env || echo "missing: ${key}"
done
stat -c '%A %U:%G %n' /opt/dodo/monitoring /opt/dodo/monitoring/monitoring.env
```

Kết quả permission phải là `drwx------` cho directory và `-rw-------` cho file. Không chạy `docker compose config` không có `--quiet` vì output đã interpolate có thể in Grafana token.

---

## 3. Tạo Telegram bot và chat nhận cảnh báo

### 3.1 Tạo bot

Thực hiện trong **TELEGRAM**:

1. Chat với bot chính thức `@BotFather`.
2. Chạy `/newbot`, đặt tên và username riêng cho production, ví dụ `Dodo Production Alerts`.
3. Nhận bot token và lưu tạm trong password manager.
4. Không gửi token vào group, issue, commit, ảnh chụp màn hình hoặc VPS.
5. Nếu token từng bị lộ, dùng BotFather revoke token và cập nhật Grafana contact point ngay.

Telegram hướng dẫn tạo bot qua BotFather trong [Bots: An introduction for developers](https://core.telegram.org/bots).

### 3.2 Tạo chat đích và lấy Chat ID

Khuyến nghị tạo một private group `Dodo Production Alerts`, thêm bot vào group và chỉ thêm người trực vận hành.

1. Gửi một tin nhắn bất kỳ trong group sau khi thêm bot.
2. Dùng Bot API `getUpdates` từ máy local để lấy `message.chat.id`; group/supergroup ID thường là số âm.
3. Không lưu response đầy đủ vì nó có thể chứa nội dung chat/tên người dùng.
4. Sau khi lấy ID, xóa token khỏi history terminal nếu shell đã ghi lại. An toàn hơn là dùng Postman/REST client với secret variable hoặc password manager.

Không commit ví dụ có token thật. Bot API mô tả `getUpdates` tại [Telegram Bot API](https://core.telegram.org/bots/api#getupdates).

### 3.3 Tạo contact point trong Grafana Cloud

Thực hiện trên **GRAFANA CLOUD**:

1. Mở **Alerts & IRM → Alerting → Notification configuration → Contact points**.
2. Chọn Grafana Alertmanager và **New contact point**.
3. Tên: `telegram-dodo-production`.
4. Integration: **Telegram**.
5. Nhập Bot API Token và Chat ID.
6. Giữ gửi resolved message để biết hệ thống đã phục hồi.
7. Nhấn **Test → Send test notification**.
8. Chỉ tiếp tục khi group nhận đúng tin test.

Bot token chỉ tồn tại trong protected field của Grafana Cloud contact point, không có lý do đặt nó trên Lightsail. Xem [Grafana contact points](https://grafana.com/docs/grafana/latest/alerting/configure-notifications/manage-contact-points/).

---

## 4. Cấu trúc file cần bổ sung trong repository

Sau khi thực hiện kế hoạch, repository nên có:

```text
DodoSystem-BE/
├── docker-compose.yml                    # Chỉ webapi/PostgreSQL/Redis/RabbitMQ
├── rabbitmq.conf                         # Chỉ application infrastructure
├── monitoring/
│   ├── docker-compose.monitoring.yml     # Project monitoring Profile Lite
│   ├── docker-compose.monitoring-full.yml# Override Full, thêm cAdvisor riêng
│   ├── config-lite.alloy
│   ├── config-full.alloy
│   ├── monitoring.env.example
│   └── README.md
└── .github/workflows/
    ├── ci-cd.yml                         # Application workflow
    └── monitoring.yml                    # Monitoring workflow độc lập
```

Không commit:

```text
.env
monitoring/monitoring.env
monitoring/secrets/
monitoring/alloy-data/
```

Trên VPS cũng giữ ranh giới tương tự:

```text
/opt/dodo/
├── docker-compose.yml                    # Application project
├── .env                                  # Application secrets
├── rabbitmq.conf
└── monitoring/
    ├── docker-compose.monitoring.yml
    ├── docker-compose.monitoring-full.yml
    ├── config-lite.alloy
    ├── config-full.alloy
    ├── monitoring.env                    # Chỉ 4 biến monitoring, mode 600
    └── monitoring-profile                # lite hoặc full, không phải secret
```

Hai Compose project không include/merge file của nhau. `config-full.alloy` là toàn bộ Lite config cộng cAdvisor scrape/relabel đã giới hạn cardinality. Không dùng symlink/file sinh động trên VPS vì CI cần validate chính file sẽ deploy.

---

## 5. Chọn version, profile Compose và quyền

### 5.1 Chính sách pin version

Không mô tả version trong tài liệu là “luôn mới nhất”. Bộ pin đề xuất để bắt đầu vòng kiểm thử hiện tại:

| Thành phần | Version pin để kiểm thử | Ghi chú |
|---|---|---|
| Grafana Alloy | `v1.17.1` | Image thực tế của cả Profile Lite và Full |
| Node Exporter standalone | `v1.12.1` | Chỉ là fallback; Profile Lite dùng node_exporter tích hợp trong Alloy |
| cAdvisor standalone | `v0.60.5` | Chỉ dùng ở Profile Full, container riêng không giữ Grafana token |

Version node_exporter tích hợp phụ thuộc bản build Alloy; không thể giả định nó giống tag standalone trong bảng. Quy trình nâng version luôn là: chọn tag → đọc release notes → validate cả hai config → smoke test host/container labels → đo resource/cardinality → mới triển khai production. Không tự động bump monitoring theo mỗi backend deploy.

Nguồn release cần kiểm tra lại trước triển khai: [prometheus/node_exporter](https://github.com/prometheus/node_exporter/releases), [google/cadvisor](https://github.com/google/cadvisor/releases) và [grafana/alloy](https://github.com/grafana/alloy/releases).

### 5.2 Monitoring Compose Profile Lite — project riêng cho Lightsail 1 GB

Tạo `monitoring/docker-compose.monitoring.yml`. Không sửa/thêm Alloy vào `docker-compose.yml` application:

```yaml
services:
  alloy:
    image: grafana/alloy:v1.17.1
    container_name: dodo-alloy
    command:
      - run
      - --server.http.listen-addr=127.0.0.1:12345
      - --storage.path=/var/lib/alloy/data
      - /etc/alloy/config.alloy
    environment:
      MONITORING_INSTANCE: ${MONITORING_INSTANCE:?required}
      GRAFANA_CLOUD_PROMETHEUS_URL: ${GRAFANA_CLOUD_PROMETHEUS_URL:?required}
      GRAFANA_CLOUD_PROMETHEUS_USER: ${GRAFANA_CLOUD_PROMETHEUS_USER:?required}
      GRAFANA_CLOUD_API_TOKEN: ${GRAFANA_CLOUD_API_TOKEN:?required}
    volumes:
      - ./config-lite.alloy:/etc/alloy/config.alloy:ro
      - alloy_data:/var/lib/alloy/data
      - /:/host/root:ro,rslave
      - /proc:/host/proc:ro
      - /sys:/host/sys:ro
      - /run/udev/data:/run/udev/data:ro
      - /var/lib/node_exporter/textfile_collector:/var/lib/node_exporter/textfile_collector:ro
    restart: unless-stopped
    read_only: true
    tmpfs:
      - /tmp:size=32m,noexec,nosuid,nodev
    mem_limit: 160m
    cpus: 0.20
    pids_limit: 128
    cap_drop:
      - ALL
    security_opt:
      - no-new-privileges:true

volumes:
  alloy_data:
```

Không có `env_file:` và không có `ports:`. `--env-file` chỉ cung cấp giá trị interpolation cho bốn dòng allowlist trong `environment:`; vì vậy container Alloy không nhận application secrets. UI/metrics bind `127.0.0.1` **bên trong Alloy container**: self-scrape vẫn hoạt động nhưng container khác trên network không truy cập được port 12345.

Chạy Lite bằng project riêng:

```bash
MON_ROOT=/opt/dodo/monitoring
unset MONITORING_INSTANCE GRAFANA_CLOUD_PROMETHEUS_URL \
  GRAFANA_CLOUD_PROMETHEUS_USER GRAFANA_CLOUD_API_TOKEN

docker compose -p dodo-monitoring \
  --env-file "$MON_ROOT/monitoring.env" \
  -f "$MON_ROOT/docker-compose.monitoring.yml" \
  config --quiet
docker compose -p dodo-monitoring \
  --env-file "$MON_ROOT/monitoring.env" \
  -f "$MON_ROOT/docker-compose.monitoring.yml" \
  up -d
```

`unset` ngăn biến cùng tên còn sót trong shell ghi đè `monitoring.env`, vì shell có precedence cao hơn `--env-file`. Project name `dodo-monitoring` tạo lifecycle/network/volume riêng với Application Compose. Nếu collector host cần capability đã drop, chỉ thêm đúng capability sau khi đọc log; không chuyển Alloy Lite thành privileged.

Sau khi tạo container, kiểm tra **tên biến** mà không in value. Lệnh phải không có output và trả exit code 0:

```bash
if docker inspect dodo-alloy --format '{{range .Config.Env}}{{println .}}{{end}}' \
  | sed 's/=.*//' \
  | grep -Eq '^(POSTGRES_|JWT|JWT_|SMTP_|RABBITMQ_|CLOUDINARY_)'; then
  echo 'ERROR: Alloy received an application secret variable name' >&2
  exit 1
fi
```

Không chạy `docker inspect ... .Config.Env` ở dạng in nguyên giá trị vì output đó chứa Grafana token. Monitoring project có network và volume riêng; nó không join application network. Profile Lite quan sát host qua read-only mounts, còn Profile Full quan sát container qua cAdvisor nên không cần kết nối trực tiếp tới webapi/PostgreSQL/Redis/RabbitMQ.

### 5.3 Compose override Profile Full — chỉ sau resource gate

Profile Full chuyển cAdvisor sang container riêng. Alloy vẫn non-privileged và là container duy nhất giữ Grafana token. Tạo `monitoring/docker-compose.monitoring-full.yml`:

```yaml
services:
  alloy:
    volumes:
      - ./config-full.alloy:/etc/alloy/config.alloy:ro
    depends_on:
      - cadvisor

  cadvisor:
    image: ghcr.io/google/cadvisor:v0.60.5
    container_name: dodo-cadvisor
    privileged: true
    command:
      - --docker_only=true
      - --disable_root_cgroup_stats=true
      - --store_container_labels=false
      - --whitelisted_container_labels=com.docker.compose.project,com.docker.compose.service
      - --housekeeping_interval=30s
    volumes:
      - /:/rootfs:ro
      - /var/run:/var/run:ro
      - /sys:/sys:ro
      - /var/lib/docker:/var/lib/docker:ro
      - /dev/disk:/dev/disk:ro
    restart: unless-stopped
    read_only: true
    mem_limit: 128m
    cpus: 0.25
    pids_limit: 128
```

Không khai báo `environment`/`env_file` cho cAdvisor, nên nó không nhận Grafana token hay application secrets. cAdvisor có quyền host rộng; chỉ Alloy scrape endpoint `cadvisor:8080` trên network project riêng. Không publish port 8080. Mount `/var/run` và privileged vẫn là quyền gần mức host, nên Full profile là backlog cho đến khi VPS từ 2 GB, team chấp nhận risk và smoke test hoàn tất.

Khởi động Full profile bằng cả hai file:

```bash
MON_ROOT=/opt/dodo/monitoring
unset MONITORING_INSTANCE GRAFANA_CLOUD_PROMETHEUS_URL \
  GRAFANA_CLOUD_PROMETHEUS_USER GRAFANA_CLOUD_API_TOKEN

docker compose -p dodo-monitoring \
  --env-file "$MON_ROOT/monitoring.env" \
  -f "$MON_ROOT/docker-compose.monitoring.yml" \
  -f "$MON_ROOT/docker-compose.monitoring-full.yml" \
  config --quiet
docker compose -p dodo-monitoring \
  --env-file "$MON_ROOT/monitoring.env" \
  -f "$MON_ROOT/docker-compose.monitoring.yml" \
  -f "$MON_ROOT/docker-compose.monitoring-full.yml" \
  up -d
```

### 5.4 Chuẩn bị textfile directory trên VPS

```bash
sudo install -d -o ubuntu -g ubuntu -m 0755 /var/lib/node_exporter/textfile_collector
stat -c '%A %U:%G %n' /var/lib/node_exporter/textfile_collector
```

Thư mục chỉ chứa metric số và label không nhạy cảm. Không ghi bucket credential, database password, access key hoặc dump path có thông tin riêng tư vào `.prom`.

---

## 6. Cấu hình Grafana Alloy và giới hạn cardinality

### 6.1 `monitoring/config-lite.alloy`

Profile Lite chạy node_exporter tích hợp qua `prometheus.exporter.unix`:

```alloy
prometheus.remote_write "grafana_cloud" {
  endpoint {
    url = sys.env("GRAFANA_CLOUD_PROMETHEUS_URL")

    basic_auth {
      username = sys.env("GRAFANA_CLOUD_PROMETHEUS_USER")
      password = sys.env("GRAFANA_CLOUD_API_TOKEN")
    }

    queue_config {
      max_shards           = 2
      capacity             = 2500
      max_samples_per_send = 500
      batch_send_deadline  = "5s"
      retry_on_http_429    = true
    }

    write_relabel_config {
      action = "labeldrop"
      regex  = "container_id|image_id|container_label_com_docker_compose_(config_hash|container_number|image|oneoff|project_config_files|project_working_dir|replace|version)"
    }
  }
}

prometheus.exporter.unix "host" {
  rootfs_path = "/host/root"
  procfs_path = "/host/proc"
  sysfs_path  = "/host/sys"

  set_collectors = [
    "cpu", "diskstats", "filesystem", "loadavg", "meminfo", "netclass",
    "netdev", "stat", "textfile", "time", "uname", "vmstat",
  ]

  textfile {
    directory = "/var/lib/node_exporter/textfile_collector"
  }
}

discovery.relabel "host" {
  targets = prometheus.exporter.unix.host.targets

  rule {
    target_label = "instance"
    replacement  = sys.env("MONITORING_INSTANCE")
  }

  rule {
    target_label = "environment"
    replacement  = "production"
  }
}

prometheus.scrape "host" {
  targets         = discovery.relabel.host.output
  job_name        = "integrations/node_exporter"
  scrape_interval = "30s"
  scrape_timeout  = "10s"
  forward_to      = [prometheus.remote_write.grafana_cloud.receiver]
}

prometheus.scrape "alloy" {
  targets = [{
    "__address__" = "127.0.0.1:12345",
    "instance"    = sys.env("MONITORING_INSTANCE"),
    "environment" = "production",
  }]

  job_name        = "integrations/alloy"
  scrape_interval = "30s"
  scrape_timeout  = "10s"
  forward_to      = [prometheus.remote_write.grafana_cloud.receiver]
}
```

`prometheus.exporter.unix` dùng node_exporter bên trong Alloy và đọc textfile collector trực tiếp. `set_collectors` cố ý giới hạn host metrics; chỉ thêm collector khi có dashboard/alert sử dụng nó.

### 6.2 `monitoring/config-full.alloy`: scrape cAdvisor riêng có ngân sách series

Copy toàn bộ Lite config vào `config-full.alloy`, sau đó thêm scrape/relabel; không chạy `prometheus.exporter.cadvisor` trong Alloy:

```alloy
prometheus.relabel "cadvisor_budget" {
  forward_to = [prometheus.remote_write.grafana_cloud.receiver]

  rule {
    source_labels = ["__name__"]
    action        = "keep"
    regex         = "container_(cpu_usage_seconds_total|memory_working_set_bytes|memory_usage_bytes|spec_memory_limit_bytes|network_receive_bytes_total|network_transmit_bytes_total|fs_usage_bytes|fs_limit_bytes|last_seen|start_time_seconds)"
  }

  rule {
    action = "labeldrop"
    regex  = "id|image|name"
  }
}

prometheus.scrape "cadvisor" {
  targets = [{
    "__address__" = "cadvisor:8080",
    "instance"    = sys.env("MONITORING_INSTANCE"),
    "environment" = "production",
  }]

  job_name        = "integrations/cadvisor"
  scrape_interval = "30s"
  scrape_timeout  = "10s"
  forward_to      = [prometheus.relabel.cadvisor_budget.receiver]
}
```

Standalone cAdvisor đã đặt `--store_container_labels=false` và whitelist đúng hai Compose labels; chúng được chuyển thành `container_label_com_docker_compose_project` và `container_label_com_docker_compose_service`. Alloy tiếp tục dùng bộ `keep` và `labeldrop` như lớp cardinality thứ hai. Muốn thêm metric/label phải có PR giải thích consumer và đo lại series.

### 6.3 Ngân sách Grafana Cloud Free

Tại thời điểm yêu cầu được rà soát, Grafana Cloud Free nêu giới hạn khoảng 10.000 active series và retention metric 14 ngày; quota có thể thay đổi nên phải kiểm tra lại **Cloud Portal → Usage** trước rollout.

Mục tiêu vận hành của DodoSystem:

- Sau Profile Lite: ghi baseline active series.
- Sau Profile Full: giữ tổng **dưới 5.000 active series** để còn chỗ cho PostgreSQL/ASP.NET metrics sau này.
- Warning khi đạt khoảng 3.500 series; dừng mở rộng và review khi gần 4.500–5.000.
- Theo dõi `grafanacloud_instance_active_series` trong Grafana Cloud usage data source.
- Nếu vượt budget, drop metric/label không có consumer; không giảm scrape mù quáng làm hỏng alert.

### 6.4 Validate cả hai profile

Ở **LOCAL** hoặc monitoring CI:

```powershell
$env:GRAFANA_CLOUD_PROMETHEUS_URL='https://example.invalid/api/prom/push'
$env:GRAFANA_CLOUD_PROMETHEUS_USER='placeholder'
$env:GRAFANA_CLOUD_API_TOKEN='placeholder'
$env:MONITORING_INSTANCE='dodo-prod'

foreach ($config in @('config-lite.alloy', 'config-full.alloy')) {
  docker run --rm `
    -e GRAFANA_CLOUD_PROMETHEUS_URL `
    -e GRAFANA_CLOUD_PROMETHEUS_USER `
    -e GRAFANA_CLOUD_API_TOKEN `
    -e MONITORING_INSTANCE `
    -v "${PWD}/monitoring/${config}:/etc/alloy/config.alloy:ro" `
    grafana/alloy:v1.17.1 `
    validate /etc/alloy/config.alloy
}
```

Trên VPS 1 GB, chỉ validate/chạy Monitoring Compose Lite:

```bash
MON_ROOT=/opt/dodo/monitoring
unset MONITORING_INSTANCE GRAFANA_CLOUD_PROMETHEUS_URL \
  GRAFANA_CLOUD_PROMETHEUS_USER GRAFANA_CLOUD_API_TOKEN
docker compose -p dodo-monitoring \
  --env-file "$MON_ROOT/monitoring.env" \
  -f "$MON_ROOT/docker-compose.monitoring.yml" config --quiet
docker compose -p dodo-monitoring \
  --env-file "$MON_ROOT/monitoring.env" \
  -f "$MON_ROOT/docker-compose.monitoring.yml" up -d
docker compose -p dodo-monitoring \
  --env-file "$MON_ROOT/monitoring.env" \
  -f "$MON_ROOT/docker-compose.monitoring.yml" ps
docker compose -p dodo-monitoring \
  --env-file "$MON_ROOT/monitoring.env" \
  -f "$MON_ROOT/docker-compose.monitoring.yml" logs --since=5m --tail=200 alloy
docker stats --no-stream alloy
free -h
vmstat 1 5
```

Log Alloy không được chứa token. HTTP `401` thường là sai username/token; `404` thường là sai remote-write URL; `429` yêu cầu kiểm tra quota/cardinality. Không public 12345 để debug.

---

## 7. Đưa trạng thái Amazon S3 backup thành metric

### 7.1 Metric cần có

Sau khi `pg_dump` đã upload và `head-object` thành công, backup script ghi atomically:

```text
dodo_backup_last_success_timestamp_seconds 1784077200
dodo_backup_last_size_bytes 12345678
```

Chỉ cập nhật timestamp sau khi S3 xác nhận object tồn tại. Nếu dump/upload lỗi, script phải exit non-zero và giữ timestamp cũ để alert “backup quá hạn” tự firing.

### 7.2 Sửa `backup-postgres.sh`

Trong script hiện tại, ngay sau `aws s3api head-object ...` thành công và trước khi xóa file local, thêm logic tương đương:

```bash
METRIC_DIR=/var/lib/node_exporter/textfile_collector
METRIC_TMP="${METRIC_DIR}/dodo_backup.prom.$$"
METRIC_FILE="${METRIC_DIR}/dodo_backup.prom"
BACKUP_SIZE_BYTES="$(stat -c '%s' "$FILE")"
BACKUP_SUCCESS_UNIX="$(date -u +%s)"

{
  printf '# HELP dodo_backup_last_success_timestamp_seconds Unix timestamp of the last PostgreSQL backup verified in S3.\n'
  printf '# TYPE dodo_backup_last_success_timestamp_seconds gauge\n'
  printf 'dodo_backup_last_success_timestamp_seconds %s\n' "$BACKUP_SUCCESS_UNIX"
  printf '# HELP dodo_backup_last_size_bytes Size of the last PostgreSQL backup verified in S3.\n'
  printf '# TYPE dodo_backup_last_size_bytes gauge\n'
  printf 'dodo_backup_last_size_bytes %s\n' "$BACKUP_SIZE_BYTES"
} > "$METRIC_TMP"

chmod 0644 "$METRIC_TMP"
mv -f "$METRIC_TMP" "$METRIC_FILE"
```

Dùng file tạm + `mv` để `prometheus.exporter.unix` không đọc file đang ghi dở. Không thêm label tên bucket vì không cần thiết và làm lộ định danh hạ tầng.

### 7.3 Khởi tạo metric trước lần backup kế tiếp

Không ghi timestamp giả. Chạy backup thật bằng tay:

```bash
/opt/dodo/backup-postgres.sh
test -s /var/lib/node_exporter/textfile_collector/dodo_backup.prom
grep -E '^dodo_backup_' /var/lib/node_exporter/textfile_collector/dodo_backup.prom
```

Xác nhận Alloy được mount đúng thư mục và không có textfile scrape error:

```bash
cd /opt/dodo
docker inspect dodo-alloy --format '{{range .Mounts}}{{println .Source "->" .Destination}}{{end}}' \
  | grep /var/lib/node_exporter/textfile_collector
docker compose -p dodo-monitoring \
  --env-file /opt/dodo/monitoring/monitoring.env \
  -f /opt/dodo/monitoring/docker-compose.monitoring.yml \
  logs --since=10m --tail=200 alloy | grep -iE 'textfile|error' || true
```

Sau đó dùng query ở Phần 8 để xác nhận metric đã tới Cloud. Không public Alloy/exporter port chỉ để kiểm tra.

---

## 8. Xác nhận metric đã tới Grafana Cloud

Trong **Grafana Cloud → Explore → Metrics**, chạy từng query:

```promql
up{instance="dodo-prod"}
```

```promql
node_uname_info{instance="dodo-prod"}
```

```promql
container_last_seen{instance="dodo-prod"}
```

```promql
dodo_backup_last_success_timestamp_seconds{instance="dodo-prod"}
```

Điều kiện đạt:

- Profile Lite có hai series `up` cho jobs `integrations/node_exporter` và `integrations/alloy`, đều bằng 1.
- Profile Full có thêm `up{job="integrations/cadvisor"} == 1`.
- `node_uname_info` phản ánh đúng host.
- Chỉ ở Profile Full: cAdvisor thấy `webapi`, `postgres`, `redis`, `rabbitmq`, `alloy` và chính `cadvisor` qua Compose service label; không có container Node Exporter standalone.
- Backup timestamp tương ứng lần upload S3 vừa kiểm tra.
- Không xuất hiện hai `instance` khác nhau cho cùng VPS do typo.
- Active series sau Full profile vẫn dưới mục tiêu 5.000.

Nếu metric trùng đôi, kiểm tra có hai Alloy instance/agent cũ cùng scrape không. Profile Lite chỉ có một Alloy process cho cả host exporter và remote write.

---

## 9. Dashboard tối thiểu

### 9.1 Dashboard `Dodo - Host Overview`

Ưu tiên cài **Linux Server integration** trong Grafana Cloud để nhận dashboard/mixin chính thức, sau đó clone dashboard cần tùy chỉnh. Các panel bắt buộc:

| Panel | PromQL gợi ý | Mục đích |
|---|---|---|
| Host CPU % | `100 * (1 - avg(rate(node_cpu_seconds_total{instance="$instance",mode="idle"}[5m])))` | Phát hiện CPU cao kéo dài |
| RAM used % | `100 * (1 - node_memory_MemAvailable_bytes / node_memory_MemTotal_bytes)` | Đo áp lực RAM thực tế |
| Swap used | `node_memory_SwapTotal_bytes - node_memory_SwapFree_bytes` | Xem VPS có phụ thuộc swap |
| Disk `/` used % | `100 * (1 - node_filesystem_avail_bytes / node_filesystem_size_bytes)` với filter root filesystem | Chặn disk đầy |
| Disk I/O | `rate(node_disk_read_bytes_total[5m])`, `rate(node_disk_written_bytes_total[5m])` | Phát hiện backup/query gây I/O |
| Network | rate receive/transmit theo interface | Quan sát traffic/bất thường |
| Uptime | `time() - node_boot_time_seconds` | Nhận biết reboot ngoài kế hoạch |

### 9.2 Dashboard `Dodo - Containers` — chỉ Profile Full

Các panel:

- CPU theo Compose service.
- Working set RAM và tỷ lệ so với `container_spec_memory_limit_bytes`.
- Network RX/TX theo service.
- Filesystem usage/rate theo container.
- Restart/OOM cần đối chiếu Docker event/log; cAdvisor không đảm bảo giữ lịch sử restart count cho mọi runtime.
- `container_last_seen` để phát hiện service biến mất.

Chuẩn hóa group label bằng `container_label_com_docker_compose_service`. Do plan đã drop `id`, `image`, `name`, dashboard không được phụ thuộc các label đó. Trước khi viết alert, mở series thật trong Explore và xác nhận allowlist hoạt động.

### 9.3 Dashboard `Dodo - Availability & Backup`

Panel bắt buộc:

- `up` của host/alloy; thêm cAdvisor khi dùng Full profile.
- Tuổi backup: `(time() - dodo_backup_last_success_timestamp_seconds) / 3600`.
- Kích thước backup gần nhất.
- Alloy remote-write failed/retried/pending samples.
- API public `/health` từ Synthetic Monitoring hoặc external uptime monitor.
- Lightsail CPU/burst alarm link và S3 bucket link ở dashboard annotation/text panel, không nhúng AWS credential.
- Active series hiện tại và tỷ lệ so với mục tiêu 5.000.

### 9.4 Variable và label

Dashboard dùng variables:

- `$environment`, mặc định `production`.
- `$instance`, mặc định `dodo-prod`.
- `$service`, lấy từ Compose service label.

Mọi alert rule production phải filter ít nhất `environment="production"` và `instance="dodo-prod"` để tránh alert staging trộn vào Telegram production.

---

## 10. Bộ alert rules production

### 10.1 Nguyên tắc chung

- Warning dùng `for` dài hơn và không đánh thức người trực nếu chỉ có spike ngắn.
- Critical phải actionable, có dashboard/runbook URL và gửi resolved message.
- Tất cả rule có labels: `environment`, `instance`, `severity`, `service` khi phù hợp.
- Chỉ heartbeat/availability gốc dùng `absent_over_time` hoặc **No data = Alerting** để phát hiện toàn bộ VPS chết.
- Dependent rule như container/backup không tự chuyển mọi No Data thành Alerting; dùng rule missing có điều kiện và inhibition để tránh bão cảnh báo.
- Evaluation interval ban đầu 1 phút; không scrape/evaluate 5 giây trên VPS nhỏ.

### 10.2 Host rules

#### `DodoHostCpuHigh` — warning

```promql
100 * (1 - avg by (instance) (
  rate(node_cpu_seconds_total{environment="production",instance="dodo-prod",mode="idle"}[5m])
)) > 85
```

- For: 10 phút.
- Runbook: xem container CPU, process/job và Lightsail burst capacity.

#### `DodoHostMemoryLow` — warning/critical

```promql
100 * node_memory_MemAvailable_bytes{environment="production",instance="dodo-prod"}
  / node_memory_MemTotal_bytes{environment="production",instance="dodo-prod"} < 15
```

- Warning: dưới 15% trong 10 phút.
- Critical: dưới 8% trong 5 phút hoặc có swap-in/out liên tục.

#### `DodoHostDiskLow` — warning/critical

Tạo query từ series root filesystem thực tế:

```promql
100 * node_filesystem_avail_bytes{environment="production",instance="dodo-prod",mountpoint="/",fstype!~"tmpfs|overlay"}
  / node_filesystem_size_bytes{environment="production",instance="dodo-prod",mountpoint="/",fstype!~"tmpfs|overlay"} < 15
```

- Warning: còn dưới 15% trong 15 phút.
- Critical: còn dưới 8% trong 5 phút.
- Không tự chạy `docker system prune --volumes` từ alert webhook.

#### `DodoHostRebooted`

Alert khi uptime dưới 10 phút nhưng metric đã có ít nhất hai evaluation. Notification phải nói đây có thể là reboot có kế hoạch, không tự kết luận incident.

### 10.3 Container rules

Toàn bộ mục này chỉ áp dụng cho Profile Full. Không tạo `DodoContainerMissing` trên Profile Lite vì chưa có cAdvisor series.

#### `DodoContainerMemoryNearLimit`

```promql
100 * container_memory_working_set_bytes{
  environment="production",
  instance="dodo-prod",
  container_label_com_docker_compose_service!=""
}
/
container_spec_memory_limit_bytes{
  environment="production",
  instance="dodo-prod",
  container_label_com_docker_compose_service!=""
} > 85
```

- For: 10 phút.
- Loại series có limit bằng 0 nếu runtime xuất chúng.
- Group notification theo `container_label_com_docker_compose_service`.

#### `DodoContainerMissing`

Tạo một rule cho từng service quan trọng bằng `absent_over_time`, ví dụ webapi:

```promql
absent_over_time(container_last_seen{
  environment="production",
  instance="dodo-prod",
  container_label_com_docker_compose_service="webapi"
}[3m])
```

Lặp cho `postgres`, `redis`, `rabbitmq`; `webapi` và `postgres` là critical, Redis/RabbitMQ severity tùy ảnh hưởng nghiệp vụ thực tế. Xác nhận Compose label thật trước khi save rule.

### 10.4 Collector/telemetry rules

#### `DodoTelemetryMissing` — critical

```promql
absent_over_time(up{
  environment="production",
  instance="dodo-prod",
  job="integrations/node_exporter"
}[5m])
```

Rule này phải được Grafana Cloud đánh giá, không phải Alloy local. Alert rule gắn tĩnh labels `environment="production"` và `instance="dodo-prod"` để inhibition vẫn match dù PromQL `absent_over_time` không giữ label nguồn. Nó tiếp tục firing khi Lightsail/Alloy chết vì Cloud vẫn chạy evaluation.

Tạo thêm `DodoHostMetricsTargetDown` với `up{job="integrations/node_exporter"} == 0`. Profile Full có thêm `DodoCadvisorTargetDown`; không tạo rule cAdvisor trên Lite.

#### `DodoAlloyRemoteWriteFailing`

Trong Explore, tìm metric self-monitoring có prefix `prometheus_remote_storage_` của Alloy version đang chạy. Alert khi failed samples tăng trong 10 phút hoặc pending samples tăng liên tục. Tên metric có thể thay đổi theo upstream; không tạo rule từ tên đoán mà chưa thấy series thực.

### 10.5 Backup rule — critical

```promql
time() - dodo_backup_last_success_timestamp_seconds{
  environment="production",
  instance="dodo-prod"
} > 26 * 3600
```

- For: 10 phút.
- No data của rule overdue: **Normal** hoặc **Keep Last State**, vì missing metric được tách thành rule bên dưới và telemetry outage đã có `DodoTelemetryMissing`.
- Annotation phải hướng dẫn kiểm tra cron, backup log, `aws sts get-caller-identity` và object S3.
- Không tự tạo backup mới từ Grafana webhook; người vận hành phải xác định disk/IAM/PostgreSQL trước.

Thêm warning nếu kích thước dump giảm trên 50% so với median gần đây, nhưng chỉ sau khi đã có ít nhất 7 backup; database nhỏ hoặc cleanup hợp lệ có thể làm giảm size.

Tạo `DodoBackupMetricMissing` chỉ khi host telemetry vẫn hoạt động:

```promql
(up{environment="production",instance="dodo-prod",job="integrations/node_exporter"} == 1)
unless on (environment, instance)
dodo_backup_last_success_timestamp_seconds{environment="production",instance="dodo-prod"}
```

Như vậy cả VPS/Alloy chết không sinh thêm backup-missing notification. Khi telemetry trở lại mà metric vẫn thiếu, rule này mới firing.

### 10.6 API availability

Node Exporter/cAdvisor không kiểm tra HTTPS/Nginx. Chọn một monitor **nằm ngoài VPS**:

- Grafana Cloud Synthetic Monitoring probe gọi `https://api.example.com/health`, hoặc
- External uptime monitor độc lập đã nêu ở guide cũ.

Grafana Cloud Free hiện công bố 100.000 API test executions/tháng. Công thức tham chiếu cho một HTTP check hoàn thành dưới một phút là:

```text
executions/tháng = probes × tests × 1 phút duration × (43.200 / frequency phút)
```

| Cấu hình một `/health` test | Ước tính/tháng | Quyết định |
|---|---:|---|
| 1 probe, mỗi 1 phút | 43.200 | **Khuyến nghị mặc định** |
| 2 probes, mỗi 1 phút | 86.400 | Dưới quota nhưng headroom thấp |
| 2 probes, mỗi 2 phút | 43.200 | **Khuyến nghị nếu cần hai vị trí** |
| 3 probes, mỗi 1 phút | 129.600 | Vượt Free, không dùng |

Chọn một trong hai baseline in đậm; timeout 10 giây, kỳ vọng HTTP 200 và TLS hợp lệ. Alert sau 2–3 lần thất bại liên tiếp. Nếu thêm target/probe/check khác, tính lại bằng calculator trong Synthetic Monitoring UI; duration được làm tròn theo quy tắc billing và quota/pricing có thể thay đổi.

Synthetic Monitoring là lớp phát hiện DNS/Nginx/TLS/API và cả trường hợp Lightsail chết hoàn toàn. Xem [Grafana Synthetic Monitoring usage](https://grafana.com/docs/grafana-cloud/cost-management-and-billing/manage-invoices/understand-your-invoice/synthetic-monitoring-invoice/) và [Grafana pricing](https://grafana.com/pricing/).

### 10.7 Label contract phục vụ inhibition

Các rule liên quan cùng VPS phải gắn labels tĩnh giống nhau:

```text
environment=production
instance=dodo-prod
```

Thêm `service` cho container rule và `severity` cho routing. Không dựa vào label do `absent_over_time` tự sinh vì kết quả absent có thể không giữ `instance`/`environment`.

---

## 11. Notification policy và nội dung Telegram

### 11.1 Routing

Tạo policy:

```text
environment = production
  ├── severity = critical → telegram-dodo-production
  └── severity = warning  → telegram-dodo-production
```

Cấu hình gợi ý:

- Group by: `alertname`, `instance`, `service`.
- Group wait: 30 giây.
- Group interval: 5 phút.
- Repeat interval: 4 giờ cho warning, 1 giờ cho critical nếu vẫn chưa xử lý.
- Không mute resolved notification.

Tách maintenance mute timing thay vì xóa rule. Trước maintenance có kế hoạch, mute đúng instance/thời gian và giữ external availability alert nếu vẫn cần xác nhận downtime.

### 11.2 Inhibition chống bão cảnh báo

Khi toàn bộ VPS hoặc Alloy mất, chỉ cần giữ hai notification gốc:

- `DodoApiPublicDown`: người dùng không truy cập được API.
- `DodoTelemetryMissing`: telemetry từ VPS đã mất.

Trong lúc `DodoTelemetryMissing` đang firing, suppress notification của:

- `DodoContainerMissing` cho webapi/PostgreSQL/Redis/RabbitMQ.
- `DodoHostMetricsTargetDown` và `DodoCadvisorTargetDown`.
- `DodoBackupMetricMissing`.

Không suppress `DodoBackupOverdue` khi timestamp thật sự quá 26 giờ và telemetry vẫn hoạt động. Không suppress public API alert.

Inhibition rule logic:

```json
{
  "name": "dodo-telemetry-missing-inhibits-dependent-alerts",
  "source_matchers": [
    { "label": "alertname", "type": "=", "value": "DodoTelemetryMissing" }
  ],
  "target_matchers": [
    {
      "label": "alertname",
      "type": "=~",
      "value": "DodoContainerMissing|DodoHostMetricsTargetDown|DodoCadvisorTargetDown|DodoBackupMetricMissing"
    }
  ],
  "equal": ["environment", "instance"]
}
```

Grafana inhibition chỉ suppress **notification**, alert instance vẫn được evaluate và hiển thị để điều tra. Ở Grafana 13+, inhibition rule được quản lý qua Grafana App Platform API và API hiện còn beta; chọn đúng Alertmanager đang nhận các Grafana-managed rules. Nếu tenant/version chưa hỗ trợ, chưa coi phần chống bão alert là hoàn tất: dùng biểu thức phụ thuộc telemetry như backup rule, đặt No Data của dependent rule thành Normal/Keep Last State, và group notification trong khi chuẩn bị phương án Alertmanager tương thích.

Test bắt buộc:

1. Cho `DodoTelemetryMissing` firing trong môi trường kiểm thử.
2. Xác nhận dependent alerts vẫn xuất hiện trong Grafana nhưng không gửi Telegram riêng.
3. Xác nhận `DodoApiPublicDown` vẫn gửi.
4. Khôi phục telemetry; nếu backup timestamp thực sự quá hạn, `DodoBackupOverdue` phải gửi độc lập.

Tài liệu: [Grafana inhibition rules](https://grafana.com/docs/grafana/latest/alerting/configure-notifications/inhibition-rules/).

### 11.3 Notification template

Tin nhắn phải có:

```text
[FIRING|RESOLVED] [severity] alertname
Environment: production
Instance: dodo-prod
Service: <service nếu có>
Summary: <triệu chứng định lượng>
Started: <UTC timestamp>
Dashboard: <Grafana URL>
Runbook: <link tới mục xử lý sự cố>
```

Không đưa vào Telegram:

- Connection string, token, access key, header Authorization.
- Full application log có dữ liệu người dùng.
- Nội dung request/payment/webhook.
- Tên object S3 nếu naming chứa thông tin nhạy cảm.

---

## 12. Tách Application workflow và Monitoring workflow

### 12.1 Nguyên tắc failure domain

Không buộc mọi backend release upload/validate/restart monitoring. Hai luồng độc lập:

```text
Application workflow
├── Build/test backend
├── Push image SHA
├── Backup predeploy như guide cũ
└── Deploy/rollback riêng webapi image

Monitoring workflow
├── Chạy chỉ khi monitoring/** hoặc chính monitoring workflow thay đổi
├── Validate config-lite và config-full
├── Upload/deploy riêng monitoring files
└── Kiểm tra Alloy + metric tới Grafana Cloud
```

Kết quả bắt buộc:

- Backend image/API khỏe không rollback chỉ vì Alloy config lỗi.
- Monitoring change không recreate/restart webapi, PostgreSQL, Redis hoặc RabbitMQ.
- Dashboard/alert chỉnh trên Grafana Cloud không deploy backend.
- Hai workflow có concurrency group khác nhau; không dùng chung rollback directory.

### 12.2 Sửa Application workflow

`.github/workflows/ci-cd.yml` tiếp tục quản lý application theo `aws_deploy_guide.md`: build/test/push image, upload application `docker-compose.yml`/`rabbitmq.conf` khi cần và gọi application `deploy.sh`. Nó không đọc, upload, validate hoặc rollback bất kỳ file nào dưới `monitoring/`:

```text
monitoring/docker-compose.monitoring.yml
monitoring/docker-compose.monitoring-full.yml
monitoring/config-lite.alloy
monitoring/config-full.alloy
monitoring/monitoring.env
```

Giới hạn trigger application bằng paths của source/backend, solution, Dockerfile, application Compose/RabbitMQ và chính workflow; thay đổi chỉ `monitoring/**` không được build/deploy API. Application project vẫn dùng `/opt/dodo/.env`; nó không dùng `monitoring.env`.

### 12.3 Tạo `.github/workflows/monitoring.yml`

Khung trigger:

```yaml
name: Validate and deploy monitoring

on:
  pull_request:
    branches: [main]
    paths:
      - "monitoring/**"
      - ".github/workflows/monitoring.yml"
  push:
    branches: [main]
    paths:
      - "monitoring/**"
      - ".github/workflows/monitoring.yml"
  workflow_dispatch:

permissions:
  contents: read

concurrency:
  group: dodo-production-monitoring
  cancel-in-progress: false
```

Job validate phải chạy cho PR và push:

```yaml
- uses: actions/checkout@v4

- name: Validate Alloy profiles
  shell: bash
  run: |
    for config in config-lite.alloy config-full.alloy; do
      docker run --rm \
        -e GRAFANA_CLOUD_PROMETHEUS_URL=https://example.invalid/api/prom/push \
        -e GRAFANA_CLOUD_PROMETHEUS_USER=placeholder \
        -e GRAFANA_CLOUD_API_TOKEN=placeholder \
        -e MONITORING_INSTANCE=dodo-prod \
        -v "$PWD/monitoring/${config}:/etc/alloy/config.alloy:ro" \
        grafana/alloy:v1.17.1 \
        validate /etc/alloy/config.alloy
    done

- name: Validate Compose profiles
  shell: bash
  run: |
    docker compose -p dodo-monitoring \
      --env-file monitoring/monitoring.env.example \
      -f monitoring/docker-compose.monitoring.yml \
      config --quiet
    docker compose -p dodo-monitoring \
      --env-file monitoring/monitoring.env.example \
      -f monitoring/docker-compose.monitoring.yml \
      -f monitoring/docker-compose.monitoring-full.yml \
      config --quiet

    test "$(docker compose -p dodo-monitoring \
      --env-file monitoring/monitoring.env.example \
      -f monitoring/docker-compose.monitoring.yml \
      config --services | sort | tr '\n' ' ')" = "alloy "

    test "$(docker compose -p dodo-monitoring \
      --env-file monitoring/monitoring.env.example \
      -f monitoring/docker-compose.monitoring.yml \
      -f monitoring/docker-compose.monitoring-full.yml \
      config --services | sort | tr '\n' ' ')" = "alloy cadvisor "
```

`monitoring.env.example` chỉ có placeholder không hoạt động. Không dùng production writer token trong validation. Hai assertion service là guard chống việc vô tình include application Compose vào monitoring project.

### 12.4 Deploy monitoring riêng

Lần cài đầu tiên thực hiện thủ công trong maintenance window. Sau khi Lite profile ổn định mới bật deploy job tự động.

Monitoring deploy job trên push `main`:

1. Dùng SSH strict host checking như guide cũ.
2. Tạo `/opt/dodo/incoming-monitoring/<GIT_SHA>`.
3. Upload **explicitly** bốn file `monitoring/docker-compose.monitoring.yml`, `monitoring/docker-compose.monitoring-full.yml`, `monitoring/config-lite.alloy`, `monitoring/config-full.alloy`.
4. Gọi `/opt/dodo/deploy-monitoring.sh <RELEASE_DIR>`.
5. Script đọc `/opt/dodo/monitoring/monitoring-profile`, chỉ chấp nhận `lite` hoặc `full`; file này không phải secret.
6. Script luôn dùng `-p dodo-monitoring`, `--env-file /opt/dodo/monitoring/monitoring.env` và Compose file dưới monitoring directory.
7. Lite chạy chỉ Alloy; Full chạy Alloy + cAdvisor trong **monitoring project**. Không câu lệnh nào tham chiếu application Compose.

Workflow không dùng wildcard upload toàn thư mục và không upload `monitoring.env.example` thành `monitoring.env`; production secret file phải được giữ nguyên trên VPS.

Khởi tạo profile lần đầu:

```bash
printf '%s\n' lite | sudo tee /opt/dodo/monitoring/monitoring-profile >/dev/null
sudo chown ubuntu:ubuntu /opt/dodo/monitoring/monitoring-profile
sudo chmod 0644 /opt/dodo/monitoring/monitoring-profile
```

Chỉ đổi thành `full` trong maintenance window sau khi VPS từ 2 GB và đạt resource gate.

### 12.5 Monitoring rollback và Cloud verification

`deploy-monitoring.sh` chỉ backup/restore:

```text
/opt/dodo/monitoring/docker-compose.monitoring.yml
/opt/dodo/monitoring/docker-compose.monitoring-full.yml
/opt/dodo/monitoring/config-lite.alloy
/opt/dodo/monitoring/config-full.alloy
```

Không backup/restore `monitoring.env` hoặc application files. Nếu monitoring release mới lỗi, restore bốn file trên và recreate monitoring project theo profile cũ. Không đổi `IMAGE_TAG`, không restart webapi/dependency, không xóa monitoring `alloy_data`, không restore database.

Sau local health gate, xác nhận metric tới Cloud bằng một trong hai cách:

- Khuyến nghị: workflow dùng một **read-only query token riêng** để poll `up{instance="dodo-prod",job="integrations/alloy"}`; không dùng metrics writer token của VPS.
- Nếu chưa tự động hóa query: monitoring job chờ Alloy ổn định, rồi người vận hành/Grafana heartbeat xác nhận series mới trong tối đa 5 phút trước khi đóng change.

Token query read-only nếu dùng phải là GitHub Environment secret riêng, không in response/header. Monitoring deploy thất bại chỉ rollback monitoring; application đang khỏe tiếp tục phục vụ.

---

## 13. Rollout an toàn

### Giai đoạn A — Cloud/Telegram, chưa đổi VPS

- [ ] Grafana Cloud stack hoạt động.
- [ ] Token chỉ có quyền metrics write.
- [ ] Telegram bot ở private group.
- [ ] Contact point test nhận firing và test message.
- [ ] Notification policy chưa route rule production giả.

### Giai đoạn B — Cài Profile Lite thủ công trên 1 GB

- [ ] Baseline CPU/RAM/disk/swap trước thay đổi đã lưu.
- [ ] Chỉ một container Alloy; chưa có cAdvisor/DB exporter/Loki.
- [ ] Alloy chạy `config-lite.alloy` và không privileged.
- [ ] `sudo ss -lntp` không thấy public port 12345/9100/8080.
- [ ] Hai jobs host/alloy có `up=1`; backup metric đã tới Cloud.
- [ ] Không có HTTP 401/403/429 lặp lại.

### Giai đoạn C — Resource gate Profile Lite

- [ ] Theo dõi ít nhất 24 giờ có tải đại diện, gồm một lần backup và nếu có thể một backend deploy.
- [ ] Memory available vẫn khoảng 20–25% hoặc cao hơn trong trạng thái bình thường.
- [ ] Không swap liên tục, không OOM, không health timeout.
- [ ] Alloy không thường xuyên chạm 70% hard limit.
- [ ] Nếu fail gate, đã dừng Alloy và giữ external uptime/native alarm cho đến khi nâng VPS.

### Giai đoạn D — Profile Full tùy chọn sau khi nâng máy

- [ ] VPS có ít nhất 2 GB RAM.
- [ ] Risk của cAdvisor privileged đã được chấp nhận; Alloy vẫn non-privileged và là nơi duy nhất giữ Grafana token.
- [ ] `config-full.alloy` và Compose override đã validate.
- [ ] Chỉ hai Compose labels được allowlist; `id`, `image`, `name` đã drop.
- [ ] Container dashboard/alert có dữ liệu đúng.
- [ ] Tổng active series dưới 5.000.
- [ ] Sau ít nhất 24 giờ vẫn đạt 20–25% memory available, không swap liên tục/OOM.

### Giai đoạn E — Dashboard/alerts/inhibition

- [ ] Dashboard host/container/availability có dữ liệu.
- [ ] Rule warning/critical có `for`, label và runbook.
- [ ] No-data behavior đã test.
- [ ] Telegram nhận firing, grouped notification và resolved.
- [ ] Khi telemetry mất, dependent alerts bị inhibit nhưng API public down vẫn gửi.

### Giai đoạn F — CI/CD tách rời

- [ ] Application workflow không upload/restart monitoring.
- [ ] Monitoring workflow có path filter và validate cả Lite/Full.
- [ ] Monitoring deploy/rollback chỉ recreate monitoring project: Alloy ở Lite, Alloy + cAdvisor ở Full.
- [ ] First install thủ công đã ổn định trước khi bật auto-deploy monitoring.
- [ ] Token không xuất hiện trong Actions logs/artifact.

---

## 14. Phạm vi mở rộng sau baseline

Chỉ thêm khi baseline ổn định và có nhu cầu cụ thể:

### 14.1 PostgreSQL

Dùng exporter/integration để theo dõi connections, locks, transactions, cache hit, database size và slow query indicators. Tạo database monitoring user quyền read-only tối thiểu; không dùng `POSTGRES_USER` production trong collector.

### 14.2 Redis

Theo dõi memory used/maxmemory, evictions, rejected connections, connected clients và persistence errors. Không public Redis exporter và không log Redis password.

### 14.3 RabbitMQ

Bật `rabbitmq_prometheus` hoặc integration được hỗ trợ để theo dõi queue depth, unacked messages, consumer count, publisher block, memory/disk alarm. Việc bật management UI public không phải điều kiện để có metric và không được mở port 15672 ra Internet.

### 14.4 ASP.NET Core application metrics

Thêm OpenTelemetry/Prometheus metrics cho request rate, error rate, latency, background jobs và business SLI. Không gắn label chứa user ID, email, tenant ID, URL có ID hoặc payment reference vì gây cardinality cao và rò dữ liệu.

### 14.5 Logs

Alloy có thể gửi Docker/Nginx logs tới Grafana Loki, nhưng đây là phase riêng vì tăng bandwidth, quota và rủi ro PII/secret. Trước khi bật phải có redaction, retention, multiline parsing và cardinality budget.

---

## 15. Test bắt buộc trước nghiệm thu

Thực hiện trong maintenance window hoặc staging. Không phá production có người dùng chỉ để test.

### 15.1 Test resource Profile Lite

```bash
cd /opt/dodo
docker stats --no-stream alloy
free -h
vmstat 1 5
dmesg -T | grep -iE 'out of memory|killed process' || true
```

Kỳ vọng:

- Host/alloy `up=1`, backup metric có dữ liệu.
- Memory available bình thường còn khoảng 20–25% trở lên.
- Không swap-in/out liên tục, không OOM và API health không xấu đi.
- Nếu fail, stop Alloy và không bật cAdvisor để “test thêm”.

### 15.2 Test telemetry mất hoàn toàn

Dừng Alloy trong cửa sổ ngắn đã lên kế hoạch:

```bash
MON_ROOT=/opt/dodo/monitoring
docker compose -p dodo-monitoring \
  --env-file "$MON_ROOT/monitoring.env" \
  -f "$MON_ROOT/docker-compose.monitoring.yml" \
  stop alloy
```

Kỳ vọng:

- `DodoTelemetryMissing` được Grafana Cloud firing sau 5 phút.
- Synthetic monitor vẫn độc lập và `DodoApiPublicDown` vẫn gửi nếu API thực sự down.
- Container/target/backup-missing dependent alerts không gửi Telegram riêng do inhibition.
- Các dependent alert instance vẫn có thể xuất hiện trong Grafana để điều tra.

Sau đó start Alloy bằng đúng project/file của profile đang dùng và xác nhận resolved. Với Full, thêm `-f "$MON_ROOT/docker-compose.monitoring-full.yml"` vào cùng lệnh. Nếu không có telemetry alert hoặc inhibition không hoạt động, hệ thống chưa đạt.

### 15.3 Test container missing — chỉ Profile Full

Chỉ dùng service có thể dừng an toàn ở staging. Không dừng PostgreSQL production tùy tiện. Xác nhận query `container_last_seen`/Compose label trước rồi kiểm tra alert.

### 15.4 Test backup alert

Không sửa timestamp production thành giá trị giả rồi quên. Tạo rule/metric test riêng hoặc dùng Grafana preview expression để mô phỏng quá 26 giờ. Test thêm hai trường hợp:

- Telemetry up nhưng backup metric thiếu: chỉ `DodoBackupMetricMissing` gửi.
- Telemetry mất: backup-missing bị suppress; khi telemetry trở lại và timestamp thật quá hạn, `DodoBackupOverdue` gửi độc lập.

Sau đó chạy backup thật, xác nhận metric timestamp mới và resolved.

### 15.5 Test API/Nginx từ bên ngoài

Trong staging hoặc maintenance, block endpoint/probe test có kiểm soát. External probe phải firing; monitoring cùng VPS không được tính là bằng chứng cho outage detection.

### 15.6 Test Telegram security

- User ngoài group không đọc được alert.
- Bot không là admin nếu không cần.
- Grafana token chỉ có trong `/opt/dodo/monitoring/monitoring.env`; không có trong application `.env`, Git, Grafana dashboard JSON hay Actions logs.
- Kiểm tra tên environment của `dodo-alloy` như Phần 5.2; không có `POSTGRES_*`, JWT, SMTP, RabbitMQ hoặc Cloudinary variable.
- Rotate thử token trong staging và xác nhận contact point hoạt động lại.

### 15.7 Test cardinality — chỉ Profile Full

Sau khi bật cAdvisor, kiểm tra Usage dashboard và query `grafanacloud_instance_active_series`. Xác nhận:

- Chỉ có hai Compose label allowlist.
- Không còn container ID dài, image SHA hoặc deployment-varying labels trong series gửi lên.
- Tổng active series dưới 5.000.
- Tắt Full profile/rollback nếu series tăng ngoài dự kiến; không chờ đến khi chạm quota 10.000.

---

## 16. Runbook xử lý alert

### 16.1 Host CPU cao

```bash
cd /opt/dodo
docker stats --no-stream
uptime
ps -eo pid,ppid,cmd,%mem,%cpu --sort=-%cpu | head -n 20
```

Đối chiếu deploy/job/backup timestamp. Không restart tất cả container chỉ vì một spike. Nếu CPU/burst kéo dài và tải hợp lệ, scale plan theo guide.

### 16.2 RAM thấp/container gần limit

```bash
free -h
vmstat 1 5
docker stats --no-stream
dmesg -T | grep -iE 'out of memory|killed process'
```

Nếu OOM, thu log/exit state trước restart. Không tăng hàng loạt `mem_limit` vượt RAM vật lý.

### 16.3 Disk thấp

```bash
df -h
docker system df
sudo du -xhd1 /var/lib/docker 2>/dev/null | sort -h
du -hd2 /opt/dodo 2>/dev/null | sort -h | tail -n 30
```

Không xóa PostgreSQL volume, không dùng `docker system prune --volumes`. Dọn theo Phần 13.5 của guide cũ.

### 16.4 Container missing

```bash
cd /opt/dodo
docker compose ps
docker compose logs --since=30m --tail=300 SERVICE_NAME
docker inspect CONTAINER_NAME --format \
  'status={{.State.Status}} exit={{.State.ExitCode}} oom={{.State.OOMKilled}} error={{.State.Error}}'
```

Xử lý theo service runbook trong guide; không recreate volume.

### 16.5 Telemetry missing/remote write lỗi

```bash
MON_ROOT=/opt/dodo/monitoring
cat "$MON_ROOT/monitoring-profile"
docker compose -p dodo-monitoring \
  --env-file "$MON_ROOT/monitoring.env" \
  -f "$MON_ROOT/docker-compose.monitoring.yml" \
  ps alloy
docker compose -p dodo-monitoring \
  --env-file "$MON_ROOT/monitoring.env" \
  -f "$MON_ROOT/docker-compose.monitoring.yml" \
  logs --since=30m --tail=300 alloy
```

Các lệnh trên đủ cho Alloy ở cả hai profile; khi thao tác cAdvisor của Full, thêm file override. Chỉ restart Alloy bằng đúng monitoring project sau khi đã lấy log. Nếu 401/403, rotate/kiểm tra token và username; nếu 429, kiểm tra active series/label và giảm cardinality trước khi tăng queue/RAM; nếu DNS/TLS, kiểm tra outbound 443 và system time. Không restart application service để chữa lỗi remote write.

### 16.6 Backup quá hạn

```bash
crontab -l
tail -n 100 /opt/dodo/backups/backup.log
aws sts get-caller-identity
aws s3 ls 's3://ACTUAL_BUCKET/postgres/' | tail
```

Sửa nguyên nhân rồi chạy `/opt/dodo/backup-postgres.sh` bằng tay. Chỉ resolved khi S3 `head-object` thành công và metric timestamp cập nhật.

### 16.7 Telegram không nhận alert

1. Xem alert instance có thật sự `Firing` không.
2. Xem rule label có match notification policy không.
3. Test contact point.
4. Xác nhận bot còn trong group và Chat ID đúng.
5. Rotate token nếu nghi ngờ bị revoke/lộ.

Không paste bot token vào ticket/chat để nhờ debug.

---

## 17. Bảo mật và vận hành định kỳ

### Hằng ngày trong tuần đầu

- Xem Grafana dashboard host/container.
- Kiểm tra alert history/no-data.
- Kiểm tra Alloy 401/429/pending samples.
- Xác nhận backup age dưới 26 giờ.
- So sánh monitoring overhead với baseline.

### Hằng tuần

- Review firing/noisy alerts và điều chỉnh `for`, không tắt rule chỉ vì phiền.
- Kiểm tra active series/quota.
- Kiểm tra release/security note của Alloy và exporter version được bundle; nếu dùng standalone fallback mới kiểm tra tag Node Exporter/cAdvisor riêng. Không auto-upgrade.
- Test external `/health` và contact point.

### Hằng tháng

- Test restore PostgreSQL như guide cũ.
- Review người trong Telegram group/Grafana organization.
- Review access policy token và last-used nếu nền tảng cung cấp.
- Chụp snapshot/dashboard evidence không chứa secret.
- Rà chi phí Grafana/AWS/S3 và retention.

### Mỗi quý

- Rotate Grafana Cloud metrics token và Telegram token theo policy nhóm.
- Test `DodoTelemetryMissing` end-to-end.
- Review alert threshold theo tải thực tế.
- Review quyền mount; Profile Lite và Alloy Full không privileged, chỉ cAdvisor Full privileged và không nhận secret/token. Đối chiếu image digest/version pin.

---

## 18. Chi phí và cardinality

Không ghi một mức giá cố định vào kế hoạch vì quota/pricing Grafana Cloud và Lightsail có thể thay đổi. Trước triển khai:

1. Xác nhận quota hiện tại trong Grafana Cloud Portal; mốc tham chiếu lúc sửa plan là 10.000 active series và 14 ngày retention cho Free.
2. Ước lượng số container × số metric × số label combinations.
3. Giữ scrape interval 30 giây ở baseline.
4. Không gắn label thay đổi liên tục như container ID đầy đủ, request ID, user/tenant/payment ID vào application metric.
5. Đặt budget nội bộ dưới 5.000 active series. Nếu gần budget, drop collector/metric/label không dùng dựa trên dashboard/alert thực tế; không giảm mù quáng làm hỏng rule.
6. AWS Budget tiếp tục bao phủ Lightsail/S3; Grafana billing/quota phải có owner kiểm tra riêng.

Monitoring không được làm production OOM để tiết kiệm chi phí. Nếu chưa nâng 2 GB, dùng Lite profile; nếu Lite cũng fail resource gate thì dừng Alloy và chỉ giữ native alarm + external monitoring cho đến khi có đủ tài nguyên.

---

## 19. Checklist nghiệm thu cuối cùng

### Tài nguyên và mạng

- [ ] Profile Lite trên 1 GB đã qua resource gate; hoặc Full profile chỉ bật trên máy từ 2 GB và vẫn còn 20–25% memory available.
- [ ] Đã reconcile tổng `mem_limit` thực tế với guide.
- [ ] Không public 9100, 8080, 12345.
- [ ] Lite chỉ có một Alloy container, không privileged; Full cAdvisor đã có risk acceptance riêng.
- [ ] Application và monitoring là hai Compose project riêng; monitoring lifecycle không tham chiếu application Compose/network.
- [ ] Alloy HTTP UI bind `127.0.0.1:12345` bên trong container; không container khác truy cập được.
- [ ] Version được ghi là pin đã kiểm thử, không mô tả là tự động mới nhất.
- [ ] Alloy WAL volume không ảnh hưởng PostgreSQL volume.

### Secret

- [ ] Grafana token chỉ có metrics write.
- [ ] Grafana token chỉ nằm trong `/opt/dodo/monitoring/monitoring.env`, permission 600; application `.env` không chứa biến Grafana.
- [ ] Alloy chỉ nhận đúng bốn biến monitoring; không nhận database/JWT/SMTP/RabbitMQ/Cloudinary variables.
- [ ] cAdvisor Full không có `environment`/`env_file` và không giữ Grafana token.
- [ ] Telegram token chỉ nằm trong Grafana protected contact point/password manager.
- [ ] Git history, Actions log, dashboard JSON và Markdown không có token thật.

### Metric và dashboard

- [ ] Lite có host/alloy `up=1`; Full mới yêu cầu thêm cAdvisor `up=1`.
- [ ] Host CPU/RAM/swap/disk/network có dữ liệu.
- [ ] Nếu dùng Full: bốn container ứng dụng xuất hiện đúng Compose service label, label động/ID/image SHA đã bị drop.
- [ ] Nếu dùng Full: active series dưới 5.000.
- [ ] Backup timestamp chỉ cập nhật sau S3 verify.
- [ ] Dashboard host/availability có variable production đúng; container dashboard chỉ bắt buộc với Full.

### Alert và Telegram

- [ ] CPU, RAM, disk, telemetry missing, backup overdue và API health có rule; container rules chỉ với Full.
- [ ] Warning/critical có `for`, label, annotation và runbook.
- [ ] No-data behavior của availability đã test.
- [ ] Telegram nhận test, firing, group và resolved notification.
- [ ] External probe phát hiện được khi toàn bộ VPS chết.
- [ ] Telemetry missing inhibit container/target/backup-missing notification nhưng không inhibit API public down.

### CI/CD và rollback

- [ ] Monitoring CI validate cả Lite/Full bằng đúng pinned Alloy image.
- [ ] Application workflow không upload/restart/rollback monitoring.
- [ ] Monitoring workflow dùng path filter, không upload application Compose và chỉ recreate monitoring project.
- [ ] `deploy-monitoring.sh` backup/restore riêng monitoring files; infrastructure/RabbitMQ có luồng riêng.
- [ ] Rollback monitoring không xóa volume business và không restore database.
- [ ] First install thủ công và một monitoring deploy staging/maintenance end-to-end đã thành công.

Chỉ đánh dấu kế hoạch hoàn tất khi có bằng chứng không chứa secret: profile đang chạy, resource gate, dashboard, target `up` tương ứng profile, active-series count nếu Full, timestamp backup, monitoring Actions run, Telegram firing/resolved/inhibition test và kiểm tra port public.

---

## 20. Tài liệu chính thức cần đối chiếu khi triển khai

- [Grafana Cloud Linux Server integration](https://grafana.com/docs/grafana-cloud/monitor-infrastructure/integrations/integration-reference/integration-linux-node/)
- [Grafana Cloud Docker integration](https://grafana.com/docs/grafana-cloud/monitor-infrastructure/integrations/integration-reference/integration-docker/)
- [Alloy `prometheus.exporter.unix`](https://grafana.com/docs/alloy/latest/reference/components/prometheus/prometheus.exporter.unix/)
- [Monitor Docker containers with Grafana Alloy](https://grafana.com/docs/alloy/latest/monitor/monitor-docker-containers/)
- [Alloy `prometheus.remote_write`](https://grafana.com/docs/alloy/latest/reference/components/prometheus/prometheus.remote_write/)
- [Alloy HTTP server và bind address](https://grafana.com/docs/grafana-cloud/send-data/alloy/reference/http/)
- [Alloy security và access permissions](https://grafana.com/docs/alloy/latest/access_permissions/)
- [Grafana Cloud metrics usage](https://grafana.com/docs/grafana-cloud/cost-management-and-billing/understand-usage-cost/metrics/)
- [Grafana Synthetic Monitoring usage calculation](https://grafana.com/docs/grafana-cloud/cost-management-and-billing/manage-invoices/understand-your-invoice/synthetic-monitoring-invoice/)
- [Grafana Cloud pricing/quota](https://grafana.com/pricing/)
- [Grafana Alerting contact points](https://grafana.com/docs/grafana/latest/alerting/configure-notifications/manage-contact-points/)
- [Grafana inhibition rules](https://grafana.com/docs/grafana/latest/alerting/configure-notifications/inhibition-rules/)
- [Prometheus Node Exporter](https://github.com/prometheus/node_exporter)
- [cAdvisor running guide](https://github.com/google/cadvisor/blob/master/docs/running.md)
- [Docker Compose: set container environment variables](https://docs.docker.com/compose/how-tos/environment-variables/set-environment-variables/)
- [Docker Compose: `--env-file` interpolation và precedence](https://docs.docker.com/compose/how-tos/environment-variables/variable-interpolation/)
- [Telegram Bot API](https://core.telegram.org/bots/api)

Khi UI hoặc cú pháp thay đổi, ưu tiên tài liệu chính thức tương ứng với version đã pin, cập nhật kế hoạch trong một pull request và test staging trước production.
