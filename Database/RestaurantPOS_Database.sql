-- ============================================================
--  HỆ THỐNG POS NHÀ HÀNG - CẤU TRÚC DATABASE
--  SQL Server (SSMS) | Phiên bản: 1.0
--  Thứ tự tạo bảng phải đúng theo quan hệ khóa ngoại
-- ============================================================

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

USE master;
GO

-- Tạo database mới (bỏ qua nếu đã có)
IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'RestaurantPOS')
BEGIN
    CREATE DATABASE RestaurantPOS;
END
GO

USE RestaurantPOS;
GO

-- ============================================================
--  1. BẢNG PaymentMethods - Phương thức thanh toán
--     Tạo trước vì Orders sẽ tham chiếu đến bảng này
-- ============================================================
CREATE TABLE PaymentMethods (
    PaymentMethodID   INT           IDENTITY(1,1) PRIMARY KEY,
    MethodName        NVARCHAR(50)  NOT NULL,          -- VD: 'Tiền mặt', 'QR Code', 'Quẹt thẻ'
    Description       NVARCHAR(200) NULL,
    IsActive          BIT           NOT NULL DEFAULT 1
);
GO

-- ============================================================
--  2. BẢNG Areas - Khu vực trong nhà hàng
--     VD: Khu A (trong nhà), Khu B (sân vườn), Khu VIP
-- ============================================================
CREATE TABLE Areas (
    AreaID            INT           IDENTITY(1,1) PRIMARY KEY,
    AreaName          NVARCHAR(100) NOT NULL,           -- VD: 'Khu A', 'Khu VIP'
    Description       NVARCHAR(200) NULL,
    SortOrder         INT           NOT NULL DEFAULT 0,  -- Thứ tự hiển thị trên UI
    IsActive          BIT           NOT NULL DEFAULT 1
);
GO

-- ============================================================
--  3. BẢNG Tables - Bàn ăn (không đặt tên là "Tables" 
--     vì trùng keyword của SQL, đặt là "DiningTables")
-- ============================================================
CREATE TABLE DiningTables (
    TableID           INT           IDENTITY(1,1) PRIMARY KEY,
    AreaID            INT           NOT NULL,
    TableName         NVARCHAR(50)  NOT NULL,           -- VD: 'Bàn 01', 'Bàn VIP 1'
    Capacity          INT           NOT NULL DEFAULT 4,  -- Số chỗ ngồi tối đa
    -- 0=Trống | 1=Có khách | 2=Đã đặt trước | 3=Đang dọn
    Status            TINYINT       NOT NULL DEFAULT 0,
    IsActive          BIT           NOT NULL DEFAULT 1,

    CONSTRAINT FK_Tables_Areas FOREIGN KEY (AreaID) REFERENCES Areas(AreaID)
);
GO

-- ============================================================
--  4. BẢNG Categories - Danh mục món ăn
--     VD: Đồ ăn, Đồ uống, Tráng miệng, Khuyến mãi
-- ============================================================
CREATE TABLE Categories (
    CategoryID        INT           IDENTITY(1,1) PRIMARY KEY,
    CategoryName      NVARCHAR(100) NOT NULL,
    IconUrl           NVARCHAR(500) NULL,               -- Đường dẫn icon hiển thị trên menu
    SortOrder         INT           NOT NULL DEFAULT 0,
    IsActive          BIT           NOT NULL DEFAULT 1
);
GO

-- ============================================================
--  5. BẢNG Products - Món ăn / đồ uống
-- ============================================================
CREATE TABLE Products (
    ProductID         INT             IDENTITY(1,1) PRIMARY KEY,
    CategoryID        INT             NOT NULL,
    ProductName       NVARCHAR(200)   NOT NULL,
    Description       NVARCHAR(500)   NULL,
    Price             DECIMAL(18, 0)  NOT NULL,          -- Đơn giá (VND, không có số thập phân)
    ImageUrl          NVARCHAR(500)   NULL,
    IsAvailable       BIT             NOT NULL DEFAULT 1, -- Còn phục vụ hay không
    IsActive          BIT             NOT NULL DEFAULT 1,

    CONSTRAINT FK_Products_Categories FOREIGN KEY (CategoryID) REFERENCES Categories(CategoryID)
);
GO

-- ============================================================
--  6. BẢNG Users - Nhân viên / Admin
-- ============================================================
CREATE TABLE Users (
    UserID            INT           IDENTITY(1,1) PRIMARY KEY,
    Username          NVARCHAR(50)  NOT NULL UNIQUE,
    PasswordHash      NVARCHAR(256) NOT NULL,            -- Lưu hash bcrypt, KHÔNG lưu mật khẩu thô
    FullName          NVARCHAR(100) NOT NULL,
    -- 0=Thu ngân | 1=Phục vụ | 2=Quản lý | 3=Admin
    Role              TINYINT       NOT NULL DEFAULT 0,
    IsActive          BIT           NOT NULL DEFAULT 1,
    CreatedAt         DATETIME2     NOT NULL DEFAULT GETDATE()
);
GO

-- ============================================================
--  7. BẢNG Orders - Hóa đơn chính
-- ============================================================
CREATE TABLE Orders (
    OrderID           INT             IDENTITY(1,1) PRIMARY KEY,
    TableID           INT             NOT NULL,
    UserID            INT             NOT NULL,           -- Nhân viên tạo hóa đơn
    PaymentMethodID   INT             NULL,               -- NULL = chưa thanh toán
    -- 0=Đang phục vụ | 1=Yêu cầu thanh toán | 2=Đã thanh toán | 3=Hủy
    Status            TINYINT         NOT NULL DEFAULT 0,
    TotalAmount       DECIMAL(18, 0)  NOT NULL DEFAULT 0, -- Tổng tiền (tự tính lại từ OrderDetails)
    Discount          DECIMAL(18, 0)  NOT NULL DEFAULT 0, -- Giảm giá (nếu có)
    FinalAmount       DECIMAL(18, 0)  NOT NULL DEFAULT 0, -- Tiền sau giảm giá
    CustomerPaid      DECIMAL(18, 0)  NULL,               -- Số tiền khách đưa (dùng cho Tiền mặt)
    ChangeAmount      DECIMAL(18, 0)  NULL,               -- Tiền thừa trả lại
    Note              NVARCHAR(500)   NULL,               -- Ghi chú đặc biệt
    CreatedAt         DATETIME2       NOT NULL DEFAULT GETDATE(),
    CheckoutAt        DATETIME2       NULL,               -- Thời điểm thanh toán xong

    CONSTRAINT FK_Orders_Tables  FOREIGN KEY (TableID)          REFERENCES DiningTables(TableID),
    CONSTRAINT FK_Orders_Users   FOREIGN KEY (UserID)           REFERENCES Users(UserID),
    CONSTRAINT FK_Orders_Payment FOREIGN KEY (PaymentMethodID)  REFERENCES PaymentMethods(PaymentMethodID)
);
GO

-- ============================================================
--  8. BẢNG OrderDetails - Chi tiết từng món trong hóa đơn
-- ============================================================
CREATE TABLE OrderDetails (
    OrderDetailID     INT             IDENTITY(1,1) PRIMARY KEY,
    OrderID           INT             NOT NULL,
    ProductID         INT             NOT NULL,
    Quantity          INT             NOT NULL DEFAULT 1,
    UnitPrice         DECIMAL(18, 0)  NOT NULL,           -- Giá tại thời điểm đặt (snapshot)
    SubTotal          AS (Quantity * UnitPrice) PERSISTED, -- Tính tự động, lưu vào DB luôn
    Note              NVARCHAR(200)   NULL,               -- VD: 'Không hành', 'Ít đá'

    CONSTRAINT FK_OrderDetails_Orders   FOREIGN KEY (OrderID)   REFERENCES Orders(OrderID),
    CONSTRAINT FK_OrderDetails_Products FOREIGN KEY (ProductID) REFERENCES Products(ProductID)
);
GO

-- ============================================================
--  DỮ LIỆU MẪU (Sample Data) - Để test giao diện
-- ============================================================

-- Phương thức thanh toán
INSERT INTO PaymentMethods (MethodName, Description) VALUES
('Tiền mặt',  'Khách trả tiền mặt trực tiếp'),
('QR Code',   'Chuyển khoản qua mã VietQR'),
('Quẹt thẻ', 'Thanh toán bằng thẻ ATM / Visa / Mastercard');
GO

-- Khu vực
INSERT INTO Areas (AreaName, SortOrder) VALUES
('Khu A - Trong nhà', 1),
('Khu B - Sân vườn',  2),
('Phòng VIP',         3);
GO

-- Bàn ăn
INSERT INTO DiningTables (AreaID, TableName, Capacity) VALUES
(1, 'Bàn 01', 4), (1, 'Bàn 02', 4), (1, 'Bàn 03', 6),
(1, 'Bàn 04', 2), (1, 'Bàn 05', 4),
(2, 'Bàn B1', 6), (2, 'Bàn B2', 6), (2, 'Bàn B3', 8),
(3, 'VIP 01', 10),(3, 'VIP 02', 12);
GO

-- Danh mục món
INSERT INTO Categories (CategoryName, SortOrder) VALUES
('Đồ ăn',       1),
('Đồ uống',     2),
('Tráng miệng', 3);
GO

-- Món ăn mẫu
INSERT INTO Products (CategoryID, ProductName, Price) VALUES
(1, 'Cơm sườn nướng',        65000),
(1, 'Bún bò Huế',            55000),
(1, 'Phở bò tái chín',       60000),
(1, 'Gà nướng muối ớt',     120000),
(1, 'Lẩu thái hải sản',     350000),
(2, 'Trà đá',                 5000),
(2, 'Pepsi / 7Up',           20000),
(2, 'Nước ngọt lon',         25000),
(2, 'Cà phê đen đá',        25000),
(2, 'Trà sữa trân châu',    45000),
(3, 'Chè thái',              35000),
(3, 'Kem dừa',               30000);
GO

-- Tài khoản admin mặc định (password: Admin@123 đã hash bcrypt)
INSERT INTO Users (Username, PasswordHash, FullName, Role) VALUES
('admin', '$2a$11$placeholder_hash_change_this', N'Quản trị viên', 3),
('cashier1', '$2a$11$placeholder_hash_change_this', N'Thu ngân 1', 0);
GO

-- ============================================================
--  INDEX - Tăng tốc truy vấn thường dùng
-- ============================================================

-- Lọc bàn theo khu vực (màn hình sơ đồ bàn)
CREATE INDEX IX_DiningTables_AreaID ON DiningTables(AreaID);

-- Lọc món theo danh mục (màn hình thực đơn)
CREATE INDEX IX_Products_CategoryID ON Products(CategoryID);

-- Lấy order của 1 bàn đang phục vụ
CREATE INDEX IX_Orders_TableID_Status ON Orders(TableID, Status);

-- Lấy tất cả món trong 1 hóa đơn
CREATE INDEX IX_OrderDetails_OrderID ON OrderDetails(OrderID);
GO

-- ============================================================
--  STORED PROCEDURE thường dùng
-- ============================================================

-- SP: Lấy sơ đồ bàn (tất cả khu vực + bàn kèm trạng thái)
CREATE OR ALTER PROCEDURE sp_GetFloorMap
AS
BEGIN
    SELECT
        a.AreaID,
        a.AreaName,
        t.TableID,
        t.TableName,
        t.Capacity,
        t.Status,
        -- Lấy luôn OrderID đang mở (nếu có) để frontend dùng
        o.OrderID
    FROM Areas a
    INNER JOIN DiningTables t ON a.AreaID = t.AreaID
    LEFT JOIN Orders o ON t.TableID = o.TableID AND o.Status IN (0, 1)
    WHERE a.IsActive = 1 AND t.IsActive = 1
    ORDER BY a.SortOrder, t.TableName;
END
GO

-- SP: Tạo order mới khi khách ngồi vào bàn
CREATE OR ALTER PROCEDURE sp_CreateOrder
    @TableID INT,
    @UserID  INT
AS
BEGIN
    -- Kiểm tra bàn đã có order chưa
    IF EXISTS (SELECT 1 FROM Orders WHERE TableID = @TableID AND Status IN (0, 1))
    BEGIN
        RAISERROR('Bàn này đang có khách. Không thể tạo order mới.', 16, 1);
        RETURN;
    END

    -- Tạo order
    INSERT INTO Orders (TableID, UserID, Status) VALUES (@TableID, @UserID, 0);

    -- Cập nhật trạng thái bàn → Có khách
    UPDATE DiningTables SET Status = 1 WHERE TableID = @TableID;

    -- Trả về OrderID vừa tạo
    SELECT SCOPE_IDENTITY() AS NewOrderID;
END
GO

-- SP: Thêm món vào order (hoặc cộng thêm nếu đã có)
CREATE OR ALTER PROCEDURE sp_AddOrderItem
    @OrderID   INT,
    @ProductID INT,
    @Quantity  INT = 1,
    @Note      NVARCHAR(200) = NULL
AS
BEGIN
    DECLARE @Price DECIMAL(18,0);
    SELECT @Price = Price FROM Products WHERE ProductID = @ProductID;

    -- Nếu món đã có trong order thì cộng thêm số lượng
    IF EXISTS (SELECT 1 FROM OrderDetails WHERE OrderID = @OrderID AND ProductID = @ProductID)
    BEGIN
        UPDATE OrderDetails
        SET Quantity = Quantity + @Quantity
        WHERE OrderID = @OrderID AND ProductID = @ProductID;
    END
    ELSE
    BEGIN
        INSERT INTO OrderDetails (OrderID, ProductID, Quantity, UnitPrice, Note)
        VALUES (@OrderID, @ProductID, @Quantity, @Price, @Note);
    END

    -- Cập nhật lại TotalAmount và FinalAmount trên Orders
    UPDATE Orders
    SET TotalAmount  = (SELECT SUM(SubTotal) FROM OrderDetails WHERE OrderID = @OrderID),
        FinalAmount  = TotalAmount - Discount
    WHERE OrderID = @OrderID;
END
GO

-- SP: Thanh toán hóa đơn
CREATE OR ALTER PROCEDURE sp_CheckoutOrder
    @OrderID         INT,
    @PaymentMethodID INT,
    @CustomerPaid    DECIMAL(18,0) = NULL,
    @Discount        DECIMAL(18,0) = 0
AS
BEGIN
    DECLARE @TableID    INT;
    DECLARE @Final      DECIMAL(18,0);

    SELECT @TableID = TableID, @Final = TotalAmount - @Discount
    FROM Orders WHERE OrderID = @OrderID;

    -- Cập nhật hóa đơn
    UPDATE Orders SET
        Status          = 2,              -- Đã thanh toán
        PaymentMethodID = @PaymentMethodID,
        Discount        = @Discount,
        FinalAmount     = @Final,
        CustomerPaid    = @CustomerPaid,
        ChangeAmount    = CASE
                            WHEN @CustomerPaid IS NOT NULL
                            THEN @CustomerPaid - @Final
                            ELSE NULL
                          END,
        CheckoutAt      = GETDATE()
    WHERE OrderID = @OrderID;

    -- Giải phóng bàn → Trống
    UPDATE DiningTables SET Status = 0 WHERE TableID = @TableID;

    SELECT @Final AS FinalAmount,
           CASE WHEN @CustomerPaid IS NOT NULL
                THEN @CustomerPaid - @Final ELSE 0 END AS ChangeAmount;
END
GO

PRINT 'Database RestaurantPOS đã được tạo thành công!';
GO
