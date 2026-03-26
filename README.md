# 🍽️ Restaurant POS — Hệ thống quản lý nhà hàng

Hệ thống Point of Sale (POS) cho nhà hàng, xây dựng bằng **ASP.NET Core 8** và **HTML/CSS/JS** thuần. Hỗ trợ đặt món, thanh toán đa phương thức (tiền mặt, QR VietQR, quẹt thẻ), quản lý thực đơn, phân quyền nhân viên và báo cáo doanh thu.

---

## 📋 Mục lục

- [Tính năng](#tính-năng)
- [Kiến trúc hệ thống](#kiến-trúc-hệ-thống)
- [Cấu trúc thư mục](#cấu-trúc-thư-mục)
- [Yêu cầu môi trường](#yêu-cầu-môi-trường)
- [Hướng dẫn cài đặt](#hướng-dẫn-cài-đặt)
- [Cơ sở dữ liệu](#cơ-sở-dữ-liệu)
- [Backend API](#backend-api)
- [Frontend](#frontend)
- [Phân quyền](#phân-quyền)
- [Tài khoản mặc định](#tài-khoản-mặc-định)
- [Môi trường dev (Mock Data)](#môi-trường-dev-mock-data)

---

## ✨ Tính năng

### Dành cho Thu ngân / Phục vụ
- **Sơ đồ bàn** — xem trạng thái toàn bộ bàn theo khu vực (Trống / Có khách / Đặt trước)
- **Đặt món real-time** — thêm/bớt món, ghi chú đặc biệt ("ít đá", "không hành")
- **Giỏ hàng tự động** — tính tổng tiền tức thì, cập nhật ngay khi thêm/xóa món
- **Thanh toán đa phương thức** — Tiền mặt (tính tiền thừa), QR VietQR (tạo mã tự động), Quẹt thẻ
- **In tạm tính** — in phiếu tạm tính PDF trực tiếp từ trình duyệt, không cần driver

### Dành cho Quản lý / Admin
- **Quản lý thực đơn** — thêm, sửa, xóa mềm món; bật/tắt trạng thái phục vụ tức thì
- **Báo cáo doanh thu** — KPI theo ngày, biểu đồ xu hướng 14 ngày, phân tích theo giờ / danh mục / phương thức thanh toán
- **Top món bán chạy** — xếp hạng theo số lượng và doanh thu
- **Xuất CSV** — tải báo cáo về máy với một nút bấm

### Bảo mật
- Đăng nhập bằng **JWT Token** (10 giờ), tự redirect về login khi hết hạn
- **4 cấp phân quyền**: Admin → Quản lý → Phục vụ → Thu ngân
- Ẩn/hiện UI tự động theo role (không cần viết if/else thủ công)
- Mật khẩu băm bằng **BCrypt** (workFactor 11)

---

## 🏗️ Kiến trúc hệ thống

```
┌─────────────────────────────────────────────────────────┐
│                     FRONTEND (HTML/JS)                  │
│  login.html  │  pos_frontend.html  │  admin_menu.html   │
│                    report.html                          │
│                    auth.js  (JWT guard dùng chung)      │
└──────────────────────┬──────────────────────────────────┘
                       │  HTTP + Bearer Token
                       ▼
┌─────────────────────────────────────────────────────────┐
│              BACKEND  (ASP.NET Core 8 Web API)          │
│  AuthController  │  AreasController  │  OrdersController│
│  ProductsController  │  ReportController               │
│  ─────────────────────────────────────────────────────  │
│  AuthService  │  TableService  │  OrderService          │
│  ProductService  │  ReportService                      │
│  ─────────────────────────────────────────────────────  │
│  AppDbContext  (Entity Framework Core)                  │
└──────────────────────┬──────────────────────────────────┘
                       │  SQL queries
                       ▼
┌─────────────────────────────────────────────────────────┐
│              DATABASE  (SQL Server / SSMS)              │
│  Areas · DiningTables · Categories · Products           │
│  Orders · OrderDetails · PaymentMethods · Users         │
└─────────────────────────────────────────────────────────┘
```

**Luồng request điển hình:**
`Request → Controller → Service → AppDbContext → SQL Server`

---

## 📁 Cấu trúc thư mục

```
restaurant-pos/
│
├── 📂 RestaurantPOS.API/               ← Backend ASP.NET Core
│   ├── Program.cs                      ← Khởi động, DI, JWT, CORS
│   ├── appsettings.json                ← Connection string, JWT secret
│   │
│   ├── 📂 Models/                      ← C# class ↔ bảng DB
│   │   └── Models.cs                   ← Area, DiningTable, Product,
│   │                                     Order, OrderDetail, User...
│   │
│   ├── 📂 Data/
│   │   └── AppDbContext.cs             ← EF Core DbContext, cấu hình quan hệ
│   │
│   ├── 📂 DTOs/                        ← Dữ liệu gửi/nhận qua API
│   │   ├── Dtos.cs                     ← FloorMap, Product, Order, Checkout
│   │   ├── AuthDtos.cs                 ← Login, ChangePassword
│   │   └── ReportDtos.cs               ← Summary, Hourly, Category, TopProduct
│   │
│   ├── 📂 Services/                    ← Logic nghiệp vụ
│   │   ├── Services.cs                 ← TableService, OrderService, ProductService
│   │   ├── AuthService.cs              ← JWT generation, BCrypt verify
│   │   └── ReportService.cs            ← Tổng hợp doanh thu
│   │
│   └── 📂 Controllers/                 ← Nhận HTTP request
│       ├── Controllers.cs              ← Areas, Products, Orders
│       ├── AuthController.cs           ← Login, Me, ChangePassword
│       └── ReportController.cs         ← Daily report
│
├── 📂 Frontend/                        ← Giao diện web
│   ├── login.html                      ← Trang đăng nhập
│   ├── pos_frontend.html               ← POS chính (sơ đồ bàn + đặt món)
│   ├── admin_menu.html                 ← Quản lý thực đơn CRUD
│   ├── report.html                     ← Báo cáo doanh thu + biểu đồ
│   └── auth.js                         ← Module JWT guard dùng chung
│
└── 📂 Database/
    └── RestaurantPOS_Database.sql      ← Script tạo toàn bộ DB + dữ liệu mẫu
```

---

## 💻 Yêu cầu môi trường

| Công cụ | Phiên bản | Ghi chú |
|---|---|---|
| .NET SDK | 8.0+ | [tải tại dotnet.microsoft.com](https://dotnet.microsoft.com/download) |
| SQL Server | 2019+ | Express (miễn phí) là đủ |
| SSMS | 19+ | Để chạy script tạo DB |
| Visual Studio | 2022+ | Hoặc VS Code + C# Extension |
| Trình duyệt | Chrome / Edge / Firefox | Chạy Frontend |

---

## 🚀 Hướng dẫn cài đặt

### Bước 1 — Tạo Database

1. Mở **SSMS**, kết nối vào SQL Server local
2. Nhấn **New Query**, mở file `Database/RestaurantPOS_Database.sql`
3. Nhấn **F5** để chạy — script sẽ tạo database, 8 bảng và dữ liệu mẫu tự động
4. Kiểm tra: database `RestaurantPOS` xuất hiện trong Object Explorer là thành công

### Bước 2 — Cấu hình Backend

```bash
cd RestaurantPOS.API

# Cài NuGet packages
dotnet add package Microsoft.EntityFrameworkCore.SqlServer
dotnet add package Microsoft.EntityFrameworkCore.Design
dotnet add package Swashbuckle.AspNetCore
dotnet add package Microsoft.AspNetCore.Authentication.JwtBearer
dotnet add package BCrypt.Net-Next
```

Mở `appsettings.json` và chỉnh connection string nếu cần:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=RestaurantPOS;Trusted_Connection=True;TrustServerCertificate=True;"
  },
  "Jwt": {
    "Key": "SuperSecretKeyForRestaurantPOS_ChangeThisInProduction_Min32Chars!",
    "Issuer": "RestaurantPOS",
    "Audience": "RestaurantPOS"
  }
}
```

> **Lưu ý:** Nếu SQL Server dùng username/password thay vì Windows Auth:
> `"Server=localhost;Database=RestaurantPOS;User Id=sa;Password=YourPwd;TrustServerCertificate=True;"`

### Bước 3 — Chạy Backend

```bash
dotnet run
```

Mở trình duyệt tại `https://localhost:{PORT}/swagger` để kiểm tra API.
Port thực tế hiển thị trong terminal khi chạy.

### Bước 4 — Chạy Frontend

Đặt tất cả file HTML và `auth.js` cùng một thư mục, sau đó mở bằng một trong hai cách:

**Cách 1 — VS Code Live Server** (khuyên dùng):
1. Cài extension **Live Server** trong VS Code
2. Nhấn chuột phải vào `login.html` → **Open with Live Server**
3. Trình duyệt tự mở tại `http://127.0.0.1:5500/login.html`

**Cách 2 — Mở trực tiếp:**
Double-click `login.html` — hoạt động hoàn toàn với Mock Data khi chưa có API.

### Bước 5 — Kết nối Frontend ↔ Backend

Trong mỗi file HTML, tìm dòng đầu script và đổi port cho khớp:

```javascript
// Trong auth.js
const API_BASE = 'https://localhost:7001/api';  // ← đổi port này

// Trong pos_frontend.html, admin_menu.html, report.html
const API = 'https://localhost:7001/api';       // ← đổi port này
```

---

## 🗄️ Cơ sở dữ liệu

### Sơ đồ quan hệ

```
Areas (1) ──────< DiningTables (nhiều)
                       │
                       │ (1)
                       ▼
PaymentMethods >── Orders (nhiều) ──────< OrderDetails (nhiều)
                                                │
Categories (1) ──< Products (nhiều) >───────────┘
```

### Mô tả các bảng

| Bảng | Mục đích | Cột quan trọng |
|---|---|---|
| `Areas` | Khu vực nhà hàng | `AreaName`, `SortOrder` |
| `DiningTables` | Bàn ăn | `Status` (0=Trống, 1=Có khách, 2=Đặt, 3=Dọn) |
| `Categories` | Danh mục món | `CategoryName`, `SortOrder` |
| `Products` | Thực đơn | `Price`, `IsAvailable`, `IsActive` |
| `Orders` | Hóa đơn | `Status`, `TotalAmount`, `FinalAmount`, `CheckoutAt` |
| `OrderDetails` | Chi tiết món trong HĐ | `UnitPrice` (snapshot giá), `SubTotal` (computed) |
| `PaymentMethods` | Phương thức TT | `MethodName` |
| `Users` | Nhân viên | `PasswordHash` (BCrypt), `Role` |

> **Nguyên tắc snapshot price:** `OrderDetails.UnitPrice` lưu giá tại thời điểm đặt, không tham chiếu `Products.Price`. Đảm bảo hóa đơn cũ không bị ảnh hưởng khi thay đổi bảng giá.

> **Xóa mềm:** `Products.IsActive = false` — dữ liệu lịch sử không bị mất, chỉ ẩn khỏi thực đơn.

### Stored Procedures có sẵn

| Tên SP | Chức năng |
|---|---|
| `sp_GetFloorMap` | Lấy sơ đồ tất cả khu vực + bàn kèm trạng thái |
| `sp_CreateOrder` | Mở bàn mới, kiểm tra không trùng |
| `sp_AddOrderItem` | Thêm món (cộng thêm nếu đã có) |
| `sp_CheckoutOrder` | Thanh toán, tính tiền thừa, giải phóng bàn |

---

## 🔌 Backend API

Base URL: `https://localhost:{PORT}/api`

### Xác thực

Tất cả endpoint (trừ `/auth/login`) yêu cầu header:
```
Authorization: Bearer <jwt_token>
```

### Danh sách endpoint

#### 🔐 Auth
| Method | Endpoint | Quyền | Mô tả |
|---|---|---|---|
| `POST` | `/auth/login` | Công khai | Đăng nhập, trả về JWT |
| `GET` | `/auth/me` | Đã đăng nhập | Thông tin user hiện tại |
| `POST` | `/auth/change-password` | Đã đăng nhập | Đổi mật khẩu |

#### 🗺️ Sơ đồ bàn
| Method | Endpoint | Quyền | Mô tả |
|---|---|---|---|
| `GET` | `/areas` | Thu ngân+ | Toàn bộ khu vực và bàn |
| `POST` | `/areas/tables/{id}/open` | Thu ngân+ | Mở bàn mới |

#### 🍜 Thực đơn
| Method | Endpoint | Quyền | Mô tả |
|---|---|---|---|
| `GET` | `/products` | Thu ngân+ | Toàn bộ thực đơn |
| `GET` | `/products?categoryId=1` | Thu ngân+ | Lọc theo danh mục |
| `GET` | `/products/{id}` | Thu ngân+ | Chi tiết 1 món |
| `POST` | `/products` | Quản lý+ | Thêm món mới |
| `PUT` | `/products/{id}` | Quản lý+ | Cập nhật món |
| `PATCH` | `/products/{id}/toggle` | Quản lý+ | Bật/tắt phục vụ |
| `DELETE` | `/products/{id}` | Admin | Xóa mềm |

#### 📋 Hóa đơn
| Method | Endpoint | Quyền | Mô tả |
|---|---|---|---|
| `GET` | `/orders/{id}` | Thu ngân+ | Chi tiết order |
| `POST` | `/orders/{id}/items` | Thu ngân+ | Thêm món |
| `DELETE` | `/orders/{id}/items/{detailId}` | Thu ngân+ | Xóa món |
| `POST` | `/orders/{id}/checkout` | Thu ngân+ | Thanh toán |

#### 📊 Báo cáo
| Method | Endpoint | Quyền | Mô tả |
|---|---|---|---|
| `GET` | `/report/daily` | Quản lý+ | Báo cáo hôm nay |
| `GET` | `/report/daily?date=2025-03-22` | Quản lý+ | Báo cáo theo ngày |

### Ví dụ request/response

**Đăng nhập:**
```json
POST /api/auth/login
{ "username": "admin", "password": "Admin@123" }

← 200 OK
{
  "token": "eyJhbGci...",
  "fullName": "Quản trị viên",
  "role": 3,
  "roleLabel": "Admin",
  "expiresAt": "2025-03-23T08:00:00Z"
}
```

**Thêm món vào order:**
```json
POST /api/orders/5/items
{ "productId": 3, "quantity": 2, "note": "Ít đá" }
```

**Thanh toán:**
```json
POST /api/orders/5/checkout
{ "paymentMethodId": 1, "discount": 50000, "customerPaid": 300000 }

← 200 OK
{ "success": true, "finalAmount": 250000, "changeAmount": 50000 }
```

---

## 🖥️ Frontend

### Các trang và quyền truy cập

| File | Chức năng | Quyền |
|---|---|---|
| `login.html` | Đăng nhập | Công khai |
| `pos_frontend.html` | POS chính — sơ đồ bàn, đặt món, thanh toán | Tất cả nhân viên |
| `admin_menu.html` | Quản lý thực đơn CRUD | Quản lý, Admin |
| `report.html` | Báo cáo doanh thu, biểu đồ | Quản lý, Admin |

### auth.js — Module bảo vệ route

Tất cả trang bảo mật chỉ cần thêm 2 dòng:

```html
<script src="auth.js"></script>
<script>
  Auth.require();           // Bất kỳ nhân viên nào
  // hoặc:
  Auth.require([2, 3]);     // Chỉ Quản lý (2) và Admin (3)
</script>
```

**API của auth.js:**

```javascript
Auth.getUser()              // → { fullName, role, roleLabel, expiresAt }
Auth.getToken()             // → JWT string
Auth.isLoggedIn()           // → true/false
Auth.logout()               // Xóa session, redirect về login.html
Auth.apiFetch(url, options) // fetch() tự thêm Bearer token + xử lý 401
Auth.applyRoleVisibility()  // Ẩn/hiện element có data-min-role="N"
```

**Ẩn UI theo role** — không cần if/else:
```html
<!-- Chỉ Quản lý trở lên mới thấy nút này -->
<button data-min-role="2" onclick="openConfirm(id)">Xóa</button>

<!-- Gọi sau khi render xong -->
<script>Auth.applyRoleVisibility();</script>
```

### VietQR — Tích hợp thanh toán QR

Đổi thông tin ngân hàng trong `pos_frontend.html`:

```javascript
const bank    = 'MB';          // Mã ngân hàng (MB, VCB, TCB, ACB, TPB...)
const account = '1234567890';  // Số tài khoản
```

URL QR được tạo tự động theo format:
```
https://img.vietqr.io/image/{bank}-{account}-compact2.png?amount={total}&addInfo={note}
```

---

## 🔑 Phân quyền

| Role | Giá trị | Quyền |
|---|---|---|
| Thu ngân | `0` | POS: sơ đồ bàn, đặt món, thanh toán |
| Phục vụ | `1` | POS: xem thực đơn, ghi order (không thanh toán) |
| Quản lý | `2` | Tất cả + quản lý thực đơn + xem báo cáo |
| Admin | `3` | Toàn quyền + xóa dữ liệu |

**Policy trong ASP.NET Core** (`Program.cs`):
```csharp
opt.AddPolicy("AdminOnly",  p => p.RequireClaim("RoleCode", "3"));
opt.AddPolicy("ManagerUp",  p => p.RequireClaim("RoleCode", "2", "3"));
opt.AddPolicy("AllStaff",   p => p.RequireAuthenticatedUser());
```

**Dùng trong Controller:**
```csharp
[Authorize(Policy = "ManagerUp")]   // Chỉ Quản lý và Admin
[HttpDelete("{id}")]
public async Task<IActionResult> Delete(int id) { ... }
```

---

## 👤 Tài khoản mặc định

Được tạo tự động khi khởi động lần đầu (`SeedAdminAsync`):

| Username | Mật khẩu | Role |
|---|---|---|
| `admin` | `Admin@123` | Admin (3) |
| `thungan1` | `Pos@1234` | Thu ngân (0) |

> **⚠️ Quan trọng:** Đổi mật khẩu mặc định trước khi deploy lên production!

---

## 🛠️ Môi trường dev (Mock Data)

Tất cả 4 trang HTML đều có **chế độ Mock Data tích hợp sẵn** — tự động kích hoạt khi API không phản hồi. Điều này cho phép:

- Chạy và test giao diện **mà không cần bật backend**
- Phát triển Frontend độc lập với Backend
- Demo cho khách hàng mà không cần môi trường server

Để biết đang dùng mock hay API thật, mở **DevTools → Console** — khi dùng mock sẽ không thấy lỗi CORS/Network.

---

## 📦 Danh sách file đầy đủ

| File | Loại | Mô tả |
|---|---|---|
| `RestaurantPOS_Database.sql` | SQL | Toàn bộ schema + stored procedures + dữ liệu mẫu |
| `Program.cs` | C# | Entry point, DI, JWT, CORS, Swagger |
| `appsettings.json` | JSON | Connection string, JWT secret |
| `Models/Models.cs` | C# | 7 model class |
| `Data/AppDbContext.cs` | C# | EF Core context, cấu hình quan hệ |
| `DTOs/Dtos.cs` | C# | DTO cho Floor, Products, Orders |
| `DTOs/AuthDtos.cs` | C# | DTO cho Login, ChangePassword |
| `DTOs/ReportDtos.cs` | C# | DTO cho tất cả biểu đồ báo cáo |
| `Services/Services.cs` | C# | TableService, OrderService, ProductService |
| `Services/AuthService.cs` | C# | JWT generation, BCrypt |
| `Services/ReportService.cs` | C# | Tổng hợp doanh thu |
| `Controllers/Controllers.cs` | C# | Areas, Products, Orders controllers |
| `Controllers/AuthController.cs` | C# | Login, Me, ChangePassword |
| `Controllers/ReportController.cs` | C# | Daily report |
| `login.html` | HTML | Trang đăng nhập |
| `auth.js` | JS | JWT guard module dùng chung |
| `pos_frontend.html` | HTML | POS chính |
| `admin_menu.html` | HTML | Quản lý thực đơn |
| `report.html` | HTML | Báo cáo doanh thu + Chart.js |

---

*Hệ thống được xây dựng theo hướng dẫn từng bước: Database → Backend API → POS Frontend → Admin thực đơn → Đăng nhập & phân quyền → Báo cáo doanh thu.*
