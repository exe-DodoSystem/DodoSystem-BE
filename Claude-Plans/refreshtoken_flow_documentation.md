# Refresh Token Flow Documentation

> Tài liệu này mô tả chi tiết cách hệ thống SMEFLOW xử lý JWT Access Token và Refresh Token,
> dành cho team Frontend (React/Next.js) và Mobile (React Native / Flutter) tích hợp.

---

## Mục lục

1. [Tổng quan](#1-tổng-quan)
2. [Thời hạn token](#2-thời-hạn-token)
3. [API Endpoints](#3-api-endpoints)
4. [Luồng chi tiết](#4-luồng-chi-tiết)
5. [Cách lưu trữ token](#5-cách-lưu-trữ-token)
6. [Hướng dẫn tích hợp Frontend (React/Next.js)](#6-hướng-dẫn-tích-hợp-frontend-reactnextjs)
7. [Hướng dẫn tích hợp Mobile (React Native)](#7-hướng-dẫn-tích-hợp-mobile-react-native)
8. [Xử lý lỗi](#8-xử-lý-lỗi)
9. [Lưu ý bảo mật](#9-lưu-ý-bảo-mật)

---

## 1. Tổng quan

Hệ thống dùng **JWT Bearer Token** kết hợp **Refresh Token Rotation**:

- **Access Token (JWT)**: Gửi kèm mỗi request dưới dạng `Authorization: Bearer <token>`.
- **Refresh Token**: Chuỗi ngẫu nhiên 64-byte (Base64 ~88 ký tự), lưu hash SHA-256 trong DB. Dùng để lấy cặp token mới khi access token hết hạn.
- **Token Rotation**: Mỗi lần gọi refresh, refresh token cũ bị revoke và trả về refresh token mới. Client **phải** cập nhật và lưu refresh token mới này.
- **Không dùng Cookie**: Tất cả token truyền qua HTTP body và `Authorization` header, không dùng httpOnly cookie.

```
Client                          Server
  |                               |
  |-- POST /api/auth/login ------->|
  |<-- { token, refreshToken } ---|
  |                               |
  |-- API call (Bearer token) ---->|
  |<-- 200 OK -------------------|
  |                               |
  | [Access token sắp hết hạn]    |
  |-- POST /refresh-tokens/refresh->|
  |<-- { accessToken, refreshToken }|
  |                               |
  |-- POST /refresh-tokens/logout ->|  (khi user đăng xuất)
  |<-- { message: "Logout thành công" }|
```

---

## 2. Thời hạn token

| Token         | Thời hạn  | Ghi chú                                          |
|---------------|-----------|--------------------------------------------------|
| Access Token  | **24 giờ**  | Hardcode trong `AuthHelper.cs`                 |
| Refresh Token | **30 ngày** | Default; có thể cấu hình `Jwt:RefreshTokenDays` |

> **Khuyến nghị cho client:** Gọi refresh khi còn khoảng 5 phút trước khi access token hết hạn
> (decode JWT lấy field `exp` để kiểm tra).

---

## 3. API Endpoints

### Base URL
```
https://<your-domain>/api
```

---

### 3.1 Đăng nhập

```
POST /api/auth/login
Content-Type: application/json
```

**Request body:**
```json
{
  "email": "user@example.com",
  "password": "password123"
}
```

**Response 200 OK:**
```json
{
  "fullName": "Nguyễn Văn A",
  "phone": "0912345678",
  "isActive": true,
  "isDeleted": false,
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "refreshToken": "KzN2L4p8Q5m9r3s7t1u2...(~88 ký tự)",
  "tenantName": "Tên công ty"
}
```

> Lưu ý: field `token` chính là **access token (JWT)**.

**Response lỗi:**
| HTTP | body | Ý nghĩa |
|------|------|---------|
| 400  | `{ "Error": "Tài khoản hoặc mật khẩu không chính xác" }` | Sai email/password |
| 400  | `{ "Error": "Tài khoản của bạn đã bị khóa." }` | Tài khoản bị khóa |
| 400  | `{ "Error": "Hết hạn tất cả module, thanh toán để tiếp tục" }` | Hết subscription |
| 400  | `{ "Error": "Tài khoản công ty chưa sẵn sàng để đăng nhập." }` | Tenant chưa kích hoạt |

---

### 3.2 Refresh Token

```
POST /api/refresh-tokens/refresh
Content-Type: application/json
```

**Request body:**
```json
{
  "refreshToken": "KzN2L4p8Q5m9r3s7t1u2..."
}
```

**Response 200 OK:**
```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...(mới)",
  "refreshToken": "W9x1a3c5e7g9i1k3m5o7...(mới, phải lưu lại)"
}
```

> **QUAN TRỌNG**: Refresh token cũ bị revoke ngay sau khi gọi thành công.
> Client phải lưu lại **refreshToken mới** để lần sau dùng tiếp.

**Response lỗi:**
| HTTP | body | Ý nghĩa | Hành động client |
|------|------|---------|-----------------|
| 400  | `{ "error": "RefreshToken là bắt buộc" }` | Không gửi token | Kiểm tra code |
| 401  | `{ "error": "RefreshToken không hợp lệ" }` | Token không tồn tại trong DB | Redirect về màn login |
| 401  | `{ "error": "RefreshToken đã bị thu hồi" }` | Token đã bị revoke | Redirect về màn login |
| 401  | `{ "error": "RefreshToken đã hết hạn" }` | Quá 30 ngày | Redirect về màn login |
| 401  | `{ "error": "Không tìm thấy user" }` | User bị xóa | Redirect về màn login |

---

### 3.3 Đăng xuất

```
POST /api/refresh-tokens/logout
Authorization: Bearer <access_token>
```

**Response 200 OK:**
```json
{ "message": "Logout thành công" }
```

**Side effect**: Tất cả refresh token của user bị revoke trong DB. Client xóa token khỏi local storage.

---

### 3.4 Gọi API có bảo vệ (mọi API khác)

```
GET /api/hr/employees
Authorization: Bearer <access_token>
```

Nếu thiếu hoặc sai token → `401 Unauthorized`.

---

## 4. Luồng chi tiết

### 4.1 Luồng đăng nhập

```
1. User nhập email + password
2. POST /api/auth/login
3. Server trả về { token, refreshToken, fullName, tenantName, ... }
4. Client lưu:
   - access token  → bộ nhớ tạm (memory / state) hoặc storage
   - refresh token → secure persistent storage
   - fullName, tenantName → state/context của app
5. Decode JWT lấy thời gian hết hạn (field "exp")
```

### 4.2 Luồng gọi API thông thường

```
1. Đọc access token từ storage/memory
2. Gắn vào header: Authorization: Bearer <token>
3. Gửi request
4. Nếu server trả 401 → chạy luồng refresh (4.3)
5. Nếu server trả 403 → module chưa đăng ký, hiển thị thông báo
```

### 4.3 Luồng tự động refresh token

```
Trigger: access token hết hạn (kiểm tra "exp") HOẶC nhận 401 từ API

1. POST /api/refresh-tokens/refresh  { refreshToken: "<stored>" }
2. Nếu thành công (200):
   a. Lưu accessToken mới vào memory
   b. Lưu refreshToken mới vào secure storage (overwrites cũ)
   c. Retry request gốc với access token mới
3. Nếu lỗi (401):
   a. Xóa toàn bộ token khỏi storage
   b. Redirect user về màn hình đăng nhập
```

### 4.4 Luồng đăng xuất

```
1. POST /api/refresh-tokens/logout (Bearer access_token)
2. Xóa access token khỏi memory
3. Xóa refresh token khỏi secure storage
4. Xóa user info (fullName, tenantName, ...) khỏi state
5. Redirect về màn hình đăng nhập
```

---

## 5. Cách lưu trữ token

### Web Frontend

| Token | Khuyến nghị | Lý do |
|-------|-------------|-------|
| Access Token | **Memory (biến JS / React state / Zustand)** | Ngắn hạn (24h), không cần persist qua tab |
| Refresh Token | **`localStorage`** hoặc `sessionStorage` | Cần persist qua reload; không có httpOnly cookie |

> Nếu cần persist qua tab/reload cho access token, có thể dùng `sessionStorage` thay vì memory.

### Mobile (React Native / Flutter)

| Token | Khuyến nghị |
|-------|-------------|
| Access Token | **SecureStore** (Expo) / **Keychain** (iOS) / **Keystore** (Android) |
| Refresh Token | **SecureStore** (Expo) / **Keychain** (iOS) / **Keystore** (Android) |

> Trên mobile, **không dùng AsyncStorage** để lưu token vì không được mã hóa.

---

## 6. Hướng dẫn tích hợp Frontend (React/Next.js)

### Bước 1: Tạo axios instance với interceptor

```typescript
// src/lib/axiosInstance.ts
import axios from 'axios';

const BASE_URL = process.env.NEXT_PUBLIC_API_URL; // hoặc import.meta.env.VITE_API_URL

// Lưu access token trong memory (không dùng localStorage)
let accessToken: string | null = null;

export const setAccessToken = (token: string | null) => {
  accessToken = token;
};

export const getAccessToken = () => accessToken;

const axiosInstance = axios.create({
  baseURL: BASE_URL,
  headers: { 'Content-Type': 'application/json' },
});

// Request interceptor: gắn access token vào mọi request
axiosInstance.interceptors.request.use((config) => {
  if (accessToken) {
    config.headers.Authorization = `Bearer ${accessToken}`;
  }
  return config;
});

// Flag tránh gọi refresh nhiều lần đồng thời
let isRefreshing = false;
let failedQueue: Array<{
  resolve: (token: string) => void;
  reject: (err: unknown) => void;
}> = [];

const processQueue = (error: unknown, token: string | null) => {
  failedQueue.forEach(({ resolve, reject }) => {
    if (error) reject(error);
    else resolve(token!);
  });
  failedQueue = [];
};

// Response interceptor: xử lý 401 → tự động refresh
axiosInstance.interceptors.response.use(
  (response) => response,
  async (error) => {
    const originalRequest = error.config;

    if (error.response?.status === 401 && !originalRequest._retry) {
      if (isRefreshing) {
        // Đang refresh rồi, queue request lại
        return new Promise((resolve, reject) => {
          failedQueue.push({ resolve, reject });
        }).then((token) => {
          originalRequest.headers.Authorization = `Bearer ${token}`;
          return axiosInstance(originalRequest);
        });
      }

      originalRequest._retry = true;
      isRefreshing = true;

      const storedRefreshToken = localStorage.getItem('refreshToken');
      if (!storedRefreshToken) {
        processQueue(error, null);
        isRefreshing = false;
        window.location.href = '/login';
        return Promise.reject(error);
      }

      try {
        const { data } = await axios.post(`${BASE_URL}/api/refresh-tokens/refresh`, {
          refreshToken: storedRefreshToken,
        });

        const newAccessToken: string = data.accessToken;
        const newRefreshToken: string = data.refreshToken;

        setAccessToken(newAccessToken);
        localStorage.setItem('refreshToken', newRefreshToken);

        processQueue(null, newAccessToken);
        originalRequest.headers.Authorization = `Bearer ${newAccessToken}`;
        return axiosInstance(originalRequest);
      } catch (refreshError) {
        processQueue(refreshError, null);
        setAccessToken(null);
        localStorage.removeItem('refreshToken');
        window.location.href = '/login';
        return Promise.reject(refreshError);
      } finally {
        isRefreshing = false;
      }
    }

    return Promise.reject(error);
  }
);

export default axiosInstance;
```

### Bước 2: Xử lý đăng nhập

```typescript
// src/services/authService.ts
import axiosInstance, { setAccessToken } from '@/lib/axiosInstance';

interface LoginResponse {
  fullName: string;
  phone: string;
  isActive: boolean;
  token: string;          // access token
  refreshToken: string;
  tenantName: string;
}

export async function login(email: string, password: string) {
  const { data } = await axiosInstance.post<LoginResponse>('/api/auth/login', {
    email,
    password,
  });

  // Lưu access token vào memory
  setAccessToken(data.token);

  // Lưu refresh token vào localStorage
  localStorage.setItem('refreshToken', data.refreshToken);

  return data;
}
```

### Bước 3: Xử lý đăng xuất

```typescript
// src/services/authService.ts (tiếp)
import axiosInstance, { setAccessToken } from '@/lib/axiosInstance';

export async function logout() {
  try {
    await axiosInstance.post('/api/refresh-tokens/logout');
  } catch {
    // Vẫn clear local dù server lỗi
  } finally {
    setAccessToken(null);
    localStorage.removeItem('refreshToken');
    window.location.href = '/login';
  }
}
```

### Bước 4: Khôi phục session khi reload trang

```typescript
// src/app/layout.tsx hoặc _app.tsx
// Khi load app, refresh ngay để lấy access token mới từ refresh token đã lưu

import { setAccessToken } from '@/lib/axiosInstance';
import axios from 'axios';

export async function restoreSession(): Promise<boolean> {
  const storedRefreshToken = localStorage.getItem('refreshToken');
  if (!storedRefreshToken) return false;

  try {
    const { data } = await axios.post(
      `${process.env.NEXT_PUBLIC_API_URL}/api/refresh-tokens/refresh`,
      { refreshToken: storedRefreshToken }
    );

    setAccessToken(data.accessToken);
    localStorage.setItem('refreshToken', data.refreshToken);
    return true;
  } catch {
    localStorage.removeItem('refreshToken');
    return false;
  }
}
```

---

## 7. Hướng dẫn tích hợp Mobile (React Native)

### Cài đặt SecureStore (Expo)

```bash
npx expo install expo-secure-store
```

### Bước 1: Token storage helper

```typescript
// src/utils/tokenStorage.ts
import * as SecureStore from 'expo-secure-store';

const ACCESS_TOKEN_KEY = 'smeflow_access_token';
const REFRESH_TOKEN_KEY = 'smeflow_refresh_token';

export const tokenStorage = {
  async saveTokens(accessToken: string, refreshToken: string) {
    await SecureStore.setItemAsync(ACCESS_TOKEN_KEY, accessToken);
    await SecureStore.setItemAsync(REFRESH_TOKEN_KEY, refreshToken);
  },

  async getAccessToken(): Promise<string | null> {
    return SecureStore.getItemAsync(ACCESS_TOKEN_KEY);
  },

  async getRefreshToken(): Promise<string | null> {
    return SecureStore.getItemAsync(REFRESH_TOKEN_KEY);
  },

  async clearTokens() {
    await SecureStore.deleteItemAsync(ACCESS_TOKEN_KEY);
    await SecureStore.deleteItemAsync(REFRESH_TOKEN_KEY);
  },
};
```

### Bước 2: Axios instance cho mobile

```typescript
// src/lib/apiClient.ts
import axios from 'axios';
import { tokenStorage } from '@/utils/tokenStorage';
import { router } from 'expo-router'; // hoặc navigation ref của bạn

const BASE_URL = process.env.EXPO_PUBLIC_API_URL;

const apiClient = axios.create({
  baseURL: BASE_URL,
  headers: { 'Content-Type': 'application/json' },
});

apiClient.interceptors.request.use(async (config) => {
  const token = await tokenStorage.getAccessToken();
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

let isRefreshing = false;
let failedQueue: Array<{
  resolve: (token: string) => void;
  reject: (err: unknown) => void;
}> = [];

const processQueue = (error: unknown, token: string | null) => {
  failedQueue.forEach(({ resolve, reject }) => {
    if (error) reject(error);
    else resolve(token!);
  });
  failedQueue = [];
};

apiClient.interceptors.response.use(
  (response) => response,
  async (error) => {
    const originalRequest = error.config;

    if (error.response?.status === 401 && !originalRequest._retry) {
      if (isRefreshing) {
        return new Promise((resolve, reject) => {
          failedQueue.push({ resolve, reject });
        }).then((token) => {
          originalRequest.headers.Authorization = `Bearer ${token}`;
          return apiClient(originalRequest);
        });
      }

      originalRequest._retry = true;
      isRefreshing = true;

      const storedRefreshToken = await tokenStorage.getRefreshToken();
      if (!storedRefreshToken) {
        processQueue(error, null);
        isRefreshing = false;
        await tokenStorage.clearTokens();
        router.replace('/login');
        return Promise.reject(error);
      }

      try {
        const { data } = await axios.post(`${BASE_URL}/api/refresh-tokens/refresh`, {
          refreshToken: storedRefreshToken,
        });

        await tokenStorage.saveTokens(data.accessToken, data.refreshToken);

        processQueue(null, data.accessToken);
        originalRequest.headers.Authorization = `Bearer ${data.accessToken}`;
        return apiClient(originalRequest);
      } catch (refreshError) {
        processQueue(refreshError, null);
        await tokenStorage.clearTokens();
        router.replace('/login');
        return Promise.reject(refreshError);
      } finally {
        isRefreshing = false;
      }
    }

    return Promise.reject(error);
  }
);

export default apiClient;
```

### Bước 3: Đăng nhập & đăng xuất (mobile)

```typescript
// src/services/auth.ts
import axios from 'axios';
import apiClient from '@/lib/apiClient';
import { tokenStorage } from '@/utils/tokenStorage';

export async function loginMobile(email: string, password: string) {
  const { data } = await axios.post(`${process.env.EXPO_PUBLIC_API_URL}/api/auth/login`, {
    email,
    password,
  });

  await tokenStorage.saveTokens(data.token, data.refreshToken);
  return data;
}

export async function logoutMobile() {
  try {
    await apiClient.post('/api/refresh-tokens/logout');
  } finally {
    await tokenStorage.clearTokens();
  }
}
```

### Bước 4: Khôi phục session khi mở app

```typescript
// src/app/_layout.tsx (Expo Router)
import { useEffect, useState } from 'react';
import axios from 'axios';
import { tokenStorage } from '@/utils/tokenStorage';

export default function RootLayout() {
  const [isReady, setIsReady] = useState(false);
  const [isAuthenticated, setIsAuthenticated] = useState(false);

  useEffect(() => {
    async function bootstrap() {
      const refreshToken = await tokenStorage.getRefreshToken();
      if (!refreshToken) {
        setIsReady(true);
        return;
      }

      try {
        const { data } = await axios.post(
          `${process.env.EXPO_PUBLIC_API_URL}/api/refresh-tokens/refresh`,
          { refreshToken }
        );

        await tokenStorage.saveTokens(data.accessToken, data.refreshToken);
        setIsAuthenticated(true);
      } catch {
        await tokenStorage.clearTokens();
      } finally {
        setIsReady(true);
      }
    }

    bootstrap();
  }, []);

  if (!isReady) return null; // Splash screen

  // Redirect dựa vào isAuthenticated
}
```

---

## 8. Xử lý lỗi

### Bảng lỗi và hành động đề xuất

| Endpoint | Status | Error body | Hành động |
|----------|--------|-----------|-----------|
| `/auth/login` | 400 | `"Tài khoản hoặc mật khẩu không chính xác"` | Hiển thị lỗi, giữ màn login |
| `/auth/login` | 400 | `"Tài khoản của bạn đã bị khóa."` | Hiển thị thông báo khóa tài khoản |
| `/auth/login` | 400 | `"Hết hạn tất cả module, thanh toán để tiếp tục"` | Redirect đến trang thanh toán/liên hệ |
| `/refresh-tokens/refresh` | 401 | bất kỳ | Xóa token, redirect về login |
| Mọi API | 401 | JWT lỗi | Chạy luồng refresh, retry 1 lần; nếu vẫn lỗi → login |
| Mọi API | 403 | `"Bạn chưa đăng ký module X"` | Hiển thị màn hình nâng cấp gói |

### Phân biệt 401 từ API thường và từ refresh endpoint

```typescript
// Phân biệt bằng URL để tránh vòng lặp vô tận
if (
  error.response?.status === 401 &&
  !originalRequest._retry &&
  !originalRequest.url?.includes('/refresh-tokens/refresh') &&
  !originalRequest.url?.includes('/auth/login')
) {
  // Chạy luồng refresh
}
```

---

## 9. Lưu ý bảo mật

| Điểm | Chi tiết |
|------|----------|
| **Token rotation** | Refresh token cũ bị revoke ngay sau mỗi lần gọi refresh thành công. Nếu thấy `401 RefreshToken đã bị thu hồi` mà không phải do client gọi, hãy nghi ngờ token bị đánh cắp — redirect về login ngay. |
| **Không commit token** | Không log hoặc gửi access/refresh token lên bất kỳ logging service nào (Sentry, console.log production). |
| **HTTPS bắt buộc** | Toàn bộ request phải qua HTTPS trong môi trường production. |
| **Multi-tenant** | Mỗi refresh token gắn với một TenantId. Token từ tenant A không dùng được cho tenant B. |
| **Logout toàn bộ thiết bị** | `POST /api/refresh-tokens/logout` revoke TẤT CẢ refresh token của user (không chỉ thiết bị hiện tại). Nếu cần logout chỉ một thiết bị, cần thảo luận thêm với backend. |
| **Access token 24 giờ** | Thời hạn khá dài; nếu token bị lộ, attacker có tới 24 giờ. Cân nhắc đề xuất backend giảm xuống 15-60 phút và dùng refresh token thường xuyên hơn. |
| **Không lưu access token trong localStorage** (web) | localStorage dễ bị XSS đọc. Dùng memory thay thế cho access token. Refresh token trong localStorage chấp nhận được vì cần persist, nhưng app phải chống XSS nghiêm ngặt. |

---

## Thông tin kỹ thuật tham khảo

**JWT Claims** (payload của access token sau khi decode):
```json
{
  "nameid": "user-guid",
  "email": "user@example.com",
  "unique_name": "Nguyễn Văn A",
  "tenantId": "tenant-guid",
  "role": ["TenantAdmin"],
  "nbf": 1719360000,
  "exp": 1719446400,
  "iss": "SMEFLOW_Server",
  "aud": "SMEFLOW_Client"
}
```

**Thư viện decode JWT (client-side)**:
```bash
npm install jwt-decode
```
```typescript
import { jwtDecode } from 'jwt-decode';

function isTokenExpired(token: string): boolean {
  const { exp } = jwtDecode<{ exp: number }>(token);
  return Date.now() >= exp * 1000;
}
```

**Files backend liên quan:**

| File | Mục đích |
|------|----------|
| `SMEFLOWSystem.WebAPI/Controllers/AuthController.cs` | Login, register, change password |
| `SMEFLOWSystem.WebAPI/Controllers/RefreshTokenController.cs` | Refresh, logout, list tokens |
| `SMEFLOWSystem.Application/Services/RefreshTokenService.cs` | Logic rotation & validation |
| `SMEFLOWSystem.Application/Helpers/AuthHelper.cs` | JWT generation (24h hardcoded) |
| `SMEFLOWSystem.Core/Entities/RefreshToken.cs` | DB entity |
