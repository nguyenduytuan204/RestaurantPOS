# 🍽️ Restaurant POS — Hệ thống quản lý nhà hàng (Bản Production)

Hệ thống Point of Sale (POS) hiện đại cho nhà hàng, xây dựng bằng **ASP.NET Core 8** và **SQLite/Entity Framework Core**. Hệ thống đã được tối ưu hóa cho môi trường Production (Railway) với khả năng thực hiện thanh toán qua **VNPay** và ứng dụng **Waiter App** trên di động.

---

## 🔗 Các liên kết quan trọng (Production)
- **Trang chủ (POS/Dashboard):** [https://restaurantpos-production-fd3e.up.railway.app/](https://restaurantpos-production-fd3e.up.railway.app/)
- **Ứng dụng Gọi món (Waiter App):** [https://restaurantpos-production-fd3e.up.railway.app/mobile_order.html](https://restaurantpos-production-fd3e.up.railway.app/mobile_order.html)
- **Tài khoản mặc định:** `admin` / `Admin@123`

---

## ✨ Tính năng & Cải tiến nổi bật
- **🕒 GMT+7 Timezone Sync:** Toàn bộ hệ thống (Database, API, Reports) đã được đồng bộ hóa múi giờ Việt Nam (GMT+7) để đảm bảo tính chính xác cho các báo cáo doanh thu theo ngày.
- **📱 Waiter App (Mobile Optimized):** Giao diện gọi món dành riêng cho di động, tự động chuyển tab thực đơn khi chọn bàn, ẩn/hiện thanh công cụ thông minh giúp tối đa không gian màn hình.
- **💳 Cổng thanh toán VNPay:** Tích hợp thanh toán QR và Thẻ qua cổng VNPay với quy trình redirect/callback an toàn.
- **💾 SQLite Database Engine:** Sử dụng SQLite bền bỉ cho môi trường container/cloud, không cần cài đặt SQL Server phức tạp.
- **📊 Robust Dashboard:** Khắc phục triệt để lỗi tính toán doanh thu (SQLite Sum Decimal) và chống crash 500 khi không có dữ liệu.

---

## 📁 Cấu trúc thư mục mới nhất
```
restaurant-pos/
├── 📂 RestaurantPOS.API/
│   ├── 📂 Controllers/
│   │   ├── Controllers.cs      ← Areas, Products, Orders
│   │   ├── VnPayController.cs  ← Xử lý redirect & callback thanh toán
│   │   └── ReportController.cs ← Báo cáo ngày theo GMT+7
│   ├── 📂 Services/
│   │   ├── Services.cs         ← Logic Dashboard, Table, Order
│   │   ├── VnPayService.cs     ← Tạo URL thanh toán VNPay
│   │   └── ReportService.cs    ← Tổng hợp doanh thu chi tiết
│   ├── 📂 Models/
│   │   └── Models.cs           ← Metadata GMT+7 (CreatedAt/CheckoutAt)
│   └── 📂 wwwroot/
│       ├── mobile_order.html   ← Trang nhân viên phục vụ (Mobile)
│       ├── pos_frontend.html   ← POS chính (Staff)
│       ├── dashboard.html      ← Tổng hợp KPI
│       └── report.html         ← Báo cáo doanh thu & Biểu đồ
```

---

## 🏗️ Kiến trúc hệ thống
```
┌─────────────────────────────────────────────────────────┐
│                     FRONTEND (HTML/JS)                  │
│  mobile_order.html  │  pos_frontend.html  │  report.html │
│                    dashboard.html                       │
└──────────────────────┬──────────────────────────────────┘
                       │  HTTP + Bearer Token
                       ▼
┌─────────────────────────────────────────────────────────┐
│              BACKEND  (ASP.NET Core 8 Web API)          │
│  Controllers  │  VnPayController  │  ReportController   │
│  ─────────────────────────────────────────────────────  │
│  TableService │  OrderService     │  VnPayService       │
│  ProductService │ DashboardService │ ReportService      │
│  ─────────────────────────────────────────────────────  │
│  AppDbContext  (EF Core + SQLite)                       │
└──────────────────────┬──────────────────────────────────┘
                       │  SQL Queries (SQLite)
                       ▼
┌─────────────────────────────────────────────────────────┐
│              DATABASE  (restaurant.db)                  │
│  Orders · OrderDetails · Products · Tables · Users      │
└─────────────────────────────────────────────────────────┘
```

---

## 💻 Hướng dẫn chạy Local Dev

Hệ thống cực kỳ đơn giản để chạy trên máy local:

1. **Backend:**
   ```bash
   cd RestaurantPOS.API
   dotnet run
   ```
   *Lần đầu chạy hệ thống sẽ tự tạo file `restaurant.db` và dữ liệu mẫu.*
2. **Frontend:** Mở file `login.html` (hoặc bất kỳ trang nào) bằng trình duyệt. Chế độ **Mock Data** sẽ tự kích hoạt nếu Backend không phản hồi.

---

## 👤 Tài khoản & Phân quyền
- **Phân quyền:** Admin(3) → Manager(2) → Waiter(1) → Cashier(0).
- **Timezone Policy:** `CreatedAt = DateTime.UtcNow.AddHours(7)`.
- **JWT Lifespan:** 10 giờ làm việc.
- **Hashing:** BCrypt (WorkFactor 11).

---
*Dự án đã được tối ưu hóa toàn diện cho việc triển khai trên hạ tầng Railway và thiết bị di động.*
