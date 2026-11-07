/* =====================================================================
   PHONESTOREDB - CSDL TOÀN DIỆN QUẢN LÝ CỬA HÀNG ĐIỆN THOẠI & CÔNG NGHỆ
   Dự án: BTL_PTTKYC (PhoneStore Management System)
   Phiên bản: 2.0 (Hợp nhất toàn bộ Schema, Views, SPs, Triggers & Seed Data 24hStore)
   Cấu trúc file:
     PHẦN 1. Khởi tạo CSDL và 20 Bảng (đã chuẩn hóa, ràng buộc toàn vẹn)
     PHẦN 2. 8 Views (truy vấn danh mục, báo cáo, chi tiết đơn, bảo mật)
     PHẦN 3. 6 Functions (tính doanh thu, tồn kho, chiết khấu, hạng thẻ)
     PHẦN 4. 7 Triggers (tự động cập nhật kho, tính tổng tiền, thăng hạng thẻ)
     PHẦN 5. 13 Stored Procedures (nghiệp vụ bán lẻ, kho hàng, bảo hành, hủy đơn)
     PHẦN 6. Dữ liệu mẫu hoàn chỉnh (iPhone 24hStore, Android, Tablet, Watch, Phụ kiện, Tài khoản)
     PHẦN 7. Kịch bản kiểm thử (Test scripts)
   Yêu cầu: Microsoft SQL Server 2016+
   ===================================================================== */

-- Xóa DB cũ (nếu có) để chạy lại script an toàn và đồng bộ
IF DB_ID('PhoneStoreDB') IS NOT NULL
BEGIN
    ALTER DATABASE PhoneStoreDB SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE PhoneStoreDB;
END
GO

CREATE DATABASE PhoneStoreDB;
GO
USE PhoneStoreDB;
GO
SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO

/* =====================================================================
   PHẦN 1. TẠO BẢNG DỮ LIỆU
   ===================================================================== */

-- 1. Hãng sản xuất
CREATE TABLE Brand (
    brand_id    INT IDENTITY(1,1) NOT NULL,
    brand_name  NVARCHAR(100)     NOT NULL,
    CONSTRAINT PK_Brand PRIMARY KEY (brand_id),
    CONSTRAINT UQ_Brand_Name UNIQUE (brand_name)
);

-- 2. Danh mục sản phẩm (Smartphone, Tablet, Accessory, Smartwatch)
CREATE TABLE Category (
    category_id    INT IDENTITY(1,1) NOT NULL,
    category_name  NVARCHAR(100)     NOT NULL,
    CONSTRAINT PK_Category PRIMARY KEY (category_id),
    CONSTRAINT UQ_Category_Name UNIQUE (category_name)
);

-- 3. Hạng thành viên (Xác định theo tổng chi tiêu tối thiểu)
CREATE TABLE MembershipRank (
    rank_id    INT IDENTITY(1,1) NOT NULL,
    rank_name  NVARCHAR(50)      NOT NULL,
    min_spent  DECIMAL(18,2)     NOT NULL,
    CONSTRAINT PK_MembershipRank PRIMARY KEY (rank_id),
    CONSTRAINT UQ_MembershipRank_Name UNIQUE (rank_name),
    CONSTRAINT CK_MembershipRank_Min CHECK (min_spent >= 0)
);

-- 4. Sản phẩm (Kèm hình ảnh, mô tả, tồn kho)
CREATE TABLE Product (
    product_id     INT IDENTITY(1,1) NOT NULL,
    brand_id       INT               NOT NULL,
    category_id    INT               NOT NULL,
    product_name   NVARCHAR(150)     NOT NULL,
    quantity       INT               NOT NULL CONSTRAINT DF_Product_Qty DEFAULT 0,
    import_price   DECIMAL(18,2)     NOT NULL CONSTRAINT DF_Product_Import DEFAULT 0,
    selling_price  DECIMAL(18,2)     NOT NULL CONSTRAINT DF_Product_Sell DEFAULT 0,
    is_active      BIT               NOT NULL CONSTRAINT DF_Product_Active DEFAULT 1,
    image_url      NVARCHAR(500)     NULL,
    description    NVARCHAR(MAX)     NULL,
    created_at     DATETIME          NOT NULL CONSTRAINT DF_Product_Created DEFAULT GETDATE(),
    CONSTRAINT PK_Product PRIMARY KEY (product_id),
    CONSTRAINT FK_Product_Brand    FOREIGN KEY (brand_id)    REFERENCES Brand(brand_id),
    CONSTRAINT FK_Product_Category FOREIGN KEY (category_id) REFERENCES Category(category_id),
    CONSTRAINT UQ_Product_BrandName UNIQUE (brand_id, product_name),
    CONSTRAINT CK_Product_Qty   CHECK (quantity >= 0),
    CONSTRAINT CK_Product_Price CHECK (import_price >= 0 AND selling_price >= 0)
);

-- 5. Nhà cung cấp
CREATE TABLE Supplier (
    supplier_id    INT IDENTITY(1,1) NOT NULL,
    supplier_name  NVARCHAR(150)     NOT NULL,
    phone          VARCHAR(15)       NULL,
    address        NVARCHAR(255)     NULL,
    CONSTRAINT PK_Supplier PRIMARY KEY (supplier_id)
);

-- 6. Khách hàng
CREATE TABLE Customer (
    customer_id    INT IDENTITY(1,1) NOT NULL,
    customer_name  NVARCHAR(100)     NOT NULL,
    phone          VARCHAR(15)       NULL,
    address        NVARCHAR(255)     NULL,
    rank_id        INT               NOT NULL CONSTRAINT DF_Customer_Rank DEFAULT 1,
    total_spent    DECIMAL(18,2)     NOT NULL CONSTRAINT DF_Customer_Spent DEFAULT 0,
    created_at     DATETIME          NOT NULL CONSTRAINT DF_Customer_Created DEFAULT GETDATE(),
    CONSTRAINT PK_Customer PRIMARY KEY (customer_id),
    CONSTRAINT UQ_Customer_Phone UNIQUE (phone),
    CONSTRAINT FK_Customer_Rank FOREIGN KEY (rank_id) REFERENCES MembershipRank(rank_id)
);

-- 7. Nhân viên
CREATE TABLE Employee (
    employee_id    INT IDENTITY(1,1) NOT NULL,
    employee_name  NVARCHAR(100)     NOT NULL,
    phone          VARCHAR(15)       NULL,
    position       NVARCHAR(50)      NULL,
    CONSTRAINT PK_Employee PRIMARY KEY (employee_id),
    CONSTRAINT CK_Employee_Position CHECK (position IN (N'Manager', N'Sales', N'Warehouse Staff', N'Technician', N'Kỹ thuật viên', N'Technical Staff'))
);

-- 8. Tài khoản đăng nhập hệ thống
CREATE TABLE Account (
    account_id     INT IDENTITY(1,1) NOT NULL,
    employee_id    INT               NOT NULL,
    username       VARCHAR(50)       NOT NULL,
    password_hash  VARCHAR(255)      NOT NULL,
    role           VARCHAR(20)       NOT NULL,
    is_active      BIT               NOT NULL CONSTRAINT DF_Account_Active DEFAULT 1,
    CONSTRAINT PK_Account PRIMARY KEY (account_id),
    CONSTRAINT UQ_Account_Employee UNIQUE (employee_id),
    CONSTRAINT UQ_Account_Username UNIQUE (username),
    CONSTRAINT FK_Account_Employee FOREIGN KEY (employee_id) REFERENCES Employee(employee_id),
    CONSTRAINT CK_Account_Role CHECK (role IN ('Admin', 'Sales', 'Warehouse', 'Technician'))
);

-- 9. Kho hàng vật lý
CREATE TABLE Warehouse (
    warehouse_id    INT IDENTITY(1,1) NOT NULL,
    warehouse_name  NVARCHAR(150)     NOT NULL,
    address         NVARCHAR(255)     NULL,
    capacity        INT               NULL,
    manager_name    NVARCHAR(100)     NULL,
    CONSTRAINT PK_Warehouse PRIMARY KEY (warehouse_id),
    CONSTRAINT CK_Warehouse_Capacity CHECK (capacity IS NULL OR capacity > 0)
);

-- 10. Serial / IMEI định danh thiết bị
CREATE TABLE Product_Serial (
    serial_id      INT IDENTITY(1,1) NOT NULL,
    product_id     INT               NOT NULL,
    imei_number    VARCHAR(50)       NULL,
    serial_number  VARCHAR(100)      NULL,
    status         VARCHAR(50)       NOT NULL CONSTRAINT DF_Serial_Status DEFAULT 'In Stock',
    warehouse_id   INT               NOT NULL,
    CONSTRAINT PK_Product_Serial PRIMARY KEY (serial_id),
    CONSTRAINT UQ_Serial_IMEI UNIQUE (imei_number),
    CONSTRAINT UQ_Serial_Number UNIQUE (serial_number),
    CONSTRAINT FK_ProductSerial_Product   FOREIGN KEY (product_id)   REFERENCES Product(product_id),
    CONSTRAINT FK_ProductSerial_Warehouse FOREIGN KEY (warehouse_id) REFERENCES Warehouse(warehouse_id),
    CONSTRAINT CK_Serial_Status CHECK (status IN ('In Stock', 'Sold', 'Defective', 'Returned'))
);

-- 11. Phiếu nhập kho (Header)
CREATE TABLE Import_Receipt (
    import_id     INT IDENTITY(1,1) NOT NULL,
    supplier_id   INT               NOT NULL,
    employee_id   INT               NOT NULL,
    import_date   DATE              NOT NULL CONSTRAINT DF_Import_Date DEFAULT GETDATE(),
    total_amount  DECIMAL(18,2)     NOT NULL CONSTRAINT DF_Import_Total DEFAULT 0,
    CONSTRAINT PK_Import_Receipt PRIMARY KEY (import_id),
    CONSTRAINT FK_ImportReceipt_Supplier FOREIGN KEY (supplier_id) REFERENCES Supplier(supplier_id),
    CONSTRAINT FK_ImportReceipt_Employee FOREIGN KEY (employee_id) REFERENCES Employee(employee_id)
);

-- 12. Chi tiết phiếu nhập kho
CREATE TABLE Import_Detail (
    import_detail_id  INT IDENTITY(1,1) NOT NULL,
    import_id         INT               NOT NULL,
    product_id        INT               NOT NULL,
    quantity          INT               NOT NULL,
    import_price      DECIMAL(18,2)     NOT NULL,
    line_total        AS (quantity * import_price) PERSISTED,
    CONSTRAINT PK_Import_Detail PRIMARY KEY (import_detail_id),
    CONSTRAINT FK_ImportDetail_ImportReceipt FOREIGN KEY (import_id)  REFERENCES Import_Receipt(import_id),
    CONSTRAINT FK_ImportDetail_Product       FOREIGN KEY (product_id) REFERENCES Product(product_id),
    CONSTRAINT CK_ImportDetail_Qty   CHECK (quantity > 0),
    CONSTRAINT CK_ImportDetail_Price CHECK (import_price >= 0)
);

-- 13. Khuyến mãi & Mã giảm giá
CREATE TABLE Promotion (
    promotion_id      INT IDENTITY(1,1) NOT NULL,
    promotion_name    NVARCHAR(150)     NOT NULL,
    discount_percent  DECIMAL(5,2)      NOT NULL,
    start_date        DATE              NOT NULL,
    end_date          DATE              NOT NULL,
    description       NVARCHAR(500)     NULL,
    CONSTRAINT PK_Promotion PRIMARY KEY (promotion_id),
    CONSTRAINT CK_Promotion_Percent CHECK (discount_percent > 0 AND discount_percent <= 100),
    CONSTRAINT CK_Promotion_Dates   CHECK (end_date >= start_date)
);

-- 14. Hóa đơn bán hàng & Đơn đặt hàng (Header)
CREATE TABLE Invoice (
    invoice_id              INT IDENTITY(1,1) NOT NULL,
    customer_id             INT               NOT NULL,
    employee_id             INT               NULL,
    promotion_id            INT               NULL,
    invoice_date            DATE              NOT NULL CONSTRAINT DF_Invoice_Date DEFAULT GETDATE(),
    sub_total               DECIMAL(18,2)     NOT NULL CONSTRAINT DF_Invoice_Sub DEFAULT 0,
    discount_amount         DECIMAL(18,2)     NOT NULL CONSTRAINT DF_Invoice_Disc DEFAULT 0,
    total_amount            DECIMAL(18,2)     NOT NULL CONSTRAINT DF_Invoice_Total DEFAULT 0,
    status                  VARCHAR(30)       NOT NULL CONSTRAINT DF_Invoice_Status DEFAULT 'Pending',
    shipping_address        NVARCHAR(255)     NULL,
    order_notes             NVARCHAR(500)     NULL,
    payment_method_selected NVARCHAR(50)      NULL,
    cancel_reason           NVARCHAR(500)     NULL,
    cancel_requested_by     NVARCHAR(100)     NULL,
    cancel_requested_at     DATETIME          NULL,
    cancel_approved_by      NVARCHAR(100)     NULL,
    cancel_approved_at      DATETIME          NULL,
    CONSTRAINT PK_Invoice PRIMARY KEY (invoice_id),
    CONSTRAINT FK_Invoice_Customer  FOREIGN KEY (customer_id)  REFERENCES Customer(customer_id),
    CONSTRAINT FK_Invoice_Employee  FOREIGN KEY (employee_id)  REFERENCES Employee(employee_id),
    CONSTRAINT FK_Invoice_Promotion FOREIGN KEY (promotion_id) REFERENCES Promotion(promotion_id),
    CONSTRAINT CK_Invoice_Status CHECK (status IN ('Pending', 'Paid', 'Shipping', 'Delivered', 'Cancelled', 'CancelRequested'))
);

-- 15. Chi tiết hóa đơn bán hàng
CREATE TABLE Invoice_Detail (
    invoice_detail_id  INT IDENTITY(1,1) NOT NULL,
    invoice_id         INT               NOT NULL,
    product_id         INT               NOT NULL,
    quantity           INT               NOT NULL,
    unit_price         DECIMAL(18,2)     NOT NULL,
    line_total         AS (quantity * unit_price) PERSISTED,
    CONSTRAINT PK_Invoice_Detail PRIMARY KEY (invoice_detail_id),
    CONSTRAINT FK_InvoiceDetail_Invoice FOREIGN KEY (invoice_id) REFERENCES Invoice(invoice_id),
    CONSTRAINT FK_InvoiceDetail_Product FOREIGN KEY (product_id) REFERENCES Product(product_id),
    CONSTRAINT CK_InvoiceDetail_Qty   CHECK (quantity > 0),
    CONSTRAINT CK_InvoiceDetail_Price CHECK (unit_price >= 0)
);

-- 16. Thanh toán
CREATE TABLE Payment (
    payment_id      INT IDENTITY(1,1) NOT NULL,
    invoice_id      INT               NOT NULL,
    payment_method  VARCHAR(50)       NOT NULL,
    payment_date    DATE              NOT NULL CONSTRAINT DF_Payment_Date DEFAULT GETDATE(),
    amount          DECIMAL(18,2)     NOT NULL,
    CONSTRAINT PK_Payment PRIMARY KEY (payment_id),
    CONSTRAINT FK_Payment_Invoice FOREIGN KEY (invoice_id) REFERENCES Invoice(invoice_id),
    CONSTRAINT CK_Payment_Method CHECK (payment_method IN ('Cash', 'Bank Transfer', 'Momo', 'Credit Card')),
    CONSTRAINT CK_Payment_Amount CHECK (amount > 0)
);

-- 17. Bảo hành sản phẩm
CREATE TABLE Warranty (
    warranty_id        INT IDENTITY(1,1) NOT NULL,
    invoice_detail_id  INT               NOT NULL,
    serial_id          INT               NULL,
    warranty_start     DATE              NOT NULL,
    warranty_end       DATE              NOT NULL,
    warranty_status    VARCHAR(50)       NOT NULL CONSTRAINT DF_Warranty_Status DEFAULT 'Active',
    warranty_note      NVARCHAR(500)     NULL,
    CONSTRAINT PK_Warranty PRIMARY KEY (warranty_id),
    CONSTRAINT FK_Warranty_InvoiceDetail FOREIGN KEY (invoice_detail_id) REFERENCES Invoice_Detail(invoice_detail_id),
    CONSTRAINT FK_Warranty_Serial        FOREIGN KEY (serial_id)         REFERENCES Product_Serial(serial_id),
    CONSTRAINT CK_Warranty_Dates  CHECK (warranty_end >= warranty_start),
    CONSTRAINT CK_Warranty_Status CHECK (warranty_status IN ('Active', 'Expired', 'Claimed'))
);

-- 18. Lịch sử biến động tồn kho
CREATE TABLE Stock_Movement (
    movement_id      INT IDENTITY(1,1) NOT NULL,
    product_id       INT               NOT NULL,
    movement_type    VARCHAR(20)       NOT NULL,
    quantity_change  INT               NOT NULL,
    reference_id     INT               NULL,
    movement_date    DATETIME          NOT NULL CONSTRAINT DF_Movement_Date DEFAULT GETDATE(),
    note             NVARCHAR(200)     NULL,
    CONSTRAINT PK_Stock_Movement PRIMARY KEY (movement_id),
    CONSTRAINT FK_StockMovement_Product FOREIGN KEY (product_id) REFERENCES Product(product_id),
    CONSTRAINT CK_Movement_Type CHECK (movement_type IN ('IMPORT', 'SALE', 'CANCEL_SALE'))
);

-- 19. Đánh giá sản phẩm
CREATE TABLE Review (
    review_id            INT IDENTITY(1,1) NOT NULL,
    product_id           INT               NOT NULL,
    customer_id          INT               NULL,
    invoice_id           INT               NULL,
    customer_name        NVARCHAR(100)     NOT NULL,
    phone                VARCHAR(15)       NULL,
    rating               INT               NOT NULL CONSTRAINT DF_Review_Rating DEFAULT 5,
    comment              NVARCHAR(1000)    NOT NULL,
    reply                NVARCHAR(1000)    NULL,
    is_verified_purchase BIT               NOT NULL CONSTRAINT DF_Review_Verified DEFAULT 1,
    created_at           DATETIME          NOT NULL CONSTRAINT DF_Review_Created DEFAULT GETDATE(),
    CONSTRAINT PK_Review PRIMARY KEY (review_id),
    CONSTRAINT FK_Review_Product FOREIGN KEY (product_id) REFERENCES Product(product_id) ON DELETE CASCADE,
    CONSTRAINT FK_Review_Customer FOREIGN KEY (customer_id) REFERENCES Customer(customer_id),
    CONSTRAINT FK_Review_Invoice FOREIGN KEY (invoice_id) REFERENCES Invoice(invoice_id),
    CONSTRAINT CK_Review_Rating CHECK (rating >= 1 AND rating <= 5)
);

-- 20. Phiếu tiếp nhận & xử lý bảo hành (Warranty Ticket)
CREATE TABLE Warranty_Ticket (
    ticket_id            INT IDENTITY(1,1) NOT NULL,
    ticket_code          VARCHAR(50)       NOT NULL,
    warranty_id          INT               NULL,
    serial_id            INT               NULL,
    customer_id          INT               NULL,
    customer_name        NVARCHAR(100)     NOT NULL,
    customer_phone       VARCHAR(15)       NOT NULL,
    technician_id        INT               NULL,
    receive_date         DATETIME          NOT NULL CONSTRAINT DF_WarrantyTicket_ReceiveDate DEFAULT GETDATE(),
    expected_return_date DATE              NULL,
    completed_date       DATETIME          NULL,
    customer_issue       NVARCHAR(1000)    NOT NULL,
    device_condition     NVARCHAR(500)     NULL,
    diagnosis            NVARCHAR(1000)    NULL,
    repair_action        NVARCHAR(1000)    NULL,
    repair_cost          DECIMAL(18,2)     NOT NULL CONSTRAINT DF_WarrantyTicket_Cost DEFAULT 0,
    status               VARCHAR(30)       NOT NULL CONSTRAINT DF_WarrantyTicket_Status DEFAULT 'Received',
    notes                NVARCHAR(500)     NULL,
    created_at           DATETIME          NOT NULL CONSTRAINT DF_WarrantyTicket_CreatedAt DEFAULT GETDATE(),
    CONSTRAINT PK_Warranty_Ticket PRIMARY KEY (ticket_id),
    CONSTRAINT UQ_Warranty_Ticket_Code UNIQUE (ticket_code),
    CONSTRAINT FK_WarrantyTicket_Warranty   FOREIGN KEY (warranty_id)   REFERENCES Warranty(warranty_id),
    CONSTRAINT FK_WarrantyTicket_Serial     FOREIGN KEY (serial_id)     REFERENCES Product_Serial(serial_id),
    CONSTRAINT FK_WarrantyTicket_Customer   FOREIGN KEY (customer_id)   REFERENCES Customer(customer_id),
    CONSTRAINT FK_WarrantyTicket_Technician FOREIGN KEY (technician_id) REFERENCES Employee(employee_id),
    CONSTRAINT CK_WarrantyTicket_Status     CHECK (status IN ('Received', 'Diagnosing', 'Processing', 'WaitingParts', 'Completed', 'Delivered', 'Cancelled')),
    CONSTRAINT CK_WarrantyTicket_Cost       CHECK (repair_cost >= 0),
    CONSTRAINT CK_WarrantyTicket_Dates      CHECK (completed_date IS NULL OR completed_date >= receive_date)
);

-- 21. Chi tiết linh kiện thay thế / sửa chữa bảo hành
CREATE TABLE Warranty_Ticket_Component (
    component_id     INT IDENTITY(1,1) NOT NULL,
    ticket_id        INT               NOT NULL,
    component_name   NVARCHAR(150)     NOT NULL,
    quantity         INT               NOT NULL CONSTRAINT DF_WTC_Qty DEFAULT 1,
    price            DECIMAL(18,2)     NOT NULL CONSTRAINT DF_WTC_Price DEFAULT 0,
    note             NVARCHAR(255)     NULL,
    CONSTRAINT PK_Warranty_Ticket_Component PRIMARY KEY (component_id),
    CONSTRAINT FK_WTC_Ticket FOREIGN KEY (ticket_id) REFERENCES Warranty_Ticket(ticket_id) ON DELETE CASCADE,
    CONSTRAINT CK_WTC_Qty   CHECK (quantity > 0),
    CONSTRAINT CK_WTC_Price CHECK (price >= 0)
);

-- Chỉ mục (Indexes) tối ưu hóa hiệu năng truy vấn
CREATE INDEX IX_Product_Brand           ON Product(brand_id);
CREATE INDEX IX_Product_Category        ON Product(category_id);
CREATE INDEX IX_Invoice_Customer        ON Invoice(customer_id);
CREATE INDEX IX_Invoice_Date            ON Invoice(invoice_date);
CREATE INDEX IX_InvoiceDetail_Invoice   ON Invoice_Detail(invoice_id);
CREATE INDEX IX_InvoiceDetail_Product   ON Invoice_Detail(product_id);
CREATE INDEX IX_ImportDetail_Import     ON Import_Detail(import_id);
CREATE INDEX IX_Payment_Invoice         ON Payment(invoice_id);
CREATE INDEX IX_StockMovement_Product   ON Stock_Movement(product_id);
CREATE INDEX IX_Review_Product          ON Review(product_id);
CREATE INDEX IX_WarrantyTicket_Code       ON Warranty_Ticket(ticket_code);
CREATE INDEX IX_WarrantyTicket_Status     ON Warranty_Ticket(status);
CREATE INDEX IX_WarrantyTicket_Technician ON Warranty_Ticket(technician_id);
CREATE INDEX IX_WarrantyTicket_Customer   ON Warranty_Ticket(customer_id);
CREATE INDEX IX_WarrantyTicket_Serial     ON Warranty_Ticket(serial_id);
GO

/* =====================================================================
   PHẦN 2. VIEW
   ===================================================================== */

-- View 1: Danh mục sản phẩm kinh doanh (Kèm hình ảnh, mô tả, tồn kho)
CREATE VIEW vw_ProductCatalog
AS
SELECT
    p.product_id,
    p.product_name,
    b.brand_id,
    b.brand_name,
    c.category_id,
    c.category_name,
    p.selling_price,
    p.import_price,
    p.quantity,
    p.image_url,
    p.description,
    p.is_active,
    CASE
        WHEN p.quantity = 0 THEN 'Out of stock'
        WHEN p.quantity <= 5 THEN 'Low stock'
        ELSE 'In stock'
    END AS stock_status
FROM Product p
JOIN Brand b    ON p.brand_id = b.brand_id
JOIN Category c ON p.category_id = c.category_id
WHERE p.is_active = 1;
GO

-- View 2: Chi tiết hóa đơn đầy đủ
CREATE VIEW vw_InvoiceFull
AS
SELECT
    i.invoice_id,
    i.invoice_date,
    i.status,
    i.shipping_address,
    i.order_notes,
    i.payment_method_selected,
    i.cancel_reason,
    i.cancel_requested_by,
    i.cancel_requested_at,
    i.cancel_approved_by,
    c.customer_id,
    c.customer_name,
    c.phone          AS customer_phone,
    e.employee_name,
    pr.promotion_name,
    p.product_id,
    p.product_name,
    p.image_url,
    d.quantity,
    d.unit_price,
    d.line_total,
    i.sub_total,
    i.discount_amount,
    i.total_amount
FROM Invoice i
JOIN Customer c        ON i.customer_id = c.customer_id
LEFT JOIN Employee e   ON i.employee_id = e.employee_id
JOIN Invoice_Detail d  ON d.invoice_id = i.invoice_id
JOIN Product p         ON d.product_id = p.product_id
LEFT JOIN Promotion pr ON i.promotion_id = pr.promotion_id;
GO

-- View 3: Thống kê doanh thu theo tháng (Hóa đơn đã Paid)
CREATE VIEW vw_MonthlyRevenue
AS
SELECT
    YEAR(invoice_date)   AS revenue_year,
    MONTH(invoice_date)  AS revenue_month,
    COUNT(*)             AS total_invoices,
    SUM(sub_total)       AS gross_amount,
    SUM(discount_amount) AS total_discount,
    SUM(total_amount)    AS revenue
FROM Invoice
WHERE status = 'Paid'
GROUP BY YEAR(invoice_date), MONTH(invoice_date);
GO

-- View 4: Top sản phẩm bán chạy nhất
CREATE VIEW vw_BestSellingProducts
AS
SELECT
    p.product_id,
    p.product_name,
    b.brand_name,
    c.category_name,
    p.image_url,
    SUM(d.quantity)   AS total_sold_quantity,
    SUM(d.line_total) AS total_revenue
FROM Invoice_Detail d
JOIN Invoice i ON i.invoice_id = d.invoice_id AND i.status = 'Paid'
JOIN Product p ON p.product_id = d.product_id
JOIN Brand b   ON b.brand_id = p.brand_id
JOIN Category c ON c.category_id = p.category_id
GROUP BY p.product_id, p.product_name, b.brand_name, c.category_name, p.image_url;
GO

-- View 5: Cảnh báo sản phẩm sắp hết hàng (tồn <= 5)
CREATE VIEW vw_LowStockProducts
AS
SELECT
    p.product_id,
    p.product_name,
    b.brand_name,
    c.category_name,
    p.quantity,
    p.import_price,
    p.selling_price
FROM Product p
JOIN Brand b    ON p.brand_id = b.brand_id
JOIN Category c ON p.category_id = c.category_id
WHERE p.is_active = 1 AND p.quantity <= 5;
GO

-- View 6: Tổng quan khách hàng & Hạng thành viên
CREATE VIEW vw_CustomerSummary
AS
SELECT
    c.customer_id,
    c.customer_name,
    c.phone,
    c.address,
    r.rank_name,
    c.total_spent,
    COUNT(i.invoice_id) AS total_invoices
FROM Customer c
JOIN MembershipRank r ON c.rank_id = r.rank_id
LEFT JOIN Invoice i   ON i.customer_id = c.customer_id AND i.status = 'Paid'
GROUP BY c.customer_id, c.customer_name, c.phone, c.address, r.rank_name, c.total_spent;
GO

-- View 7: Theo dõi trạng thái bảo hành
CREATE VIEW vw_WarrantyStatus
AS
SELECT
    w.warranty_id,
    p.product_name,
    ps.imei_number,
    ps.serial_number,
    c.customer_name,
    c.phone AS customer_phone,
    w.warranty_start,
    w.warranty_end,
    w.warranty_status,
    w.warranty_note
FROM Warranty w
JOIN Invoice_Detail d ON w.invoice_detail_id = d.invoice_detail_id
JOIN Product p        ON d.product_id = p.product_id
JOIN Invoice i        ON d.invoice_id = i.invoice_id
JOIN Customer c       ON i.customer_id = c.customer_id
LEFT JOIN Product_Serial ps ON w.serial_id = ps.serial_id;
GO

-- View 8: An toàn tài khoản (Không lộ hash mật khẩu)
CREATE VIEW vw_AccountSecurity
AS
SELECT 
    a.account_id,
    a.username,
    e.employee_name,
    a.role,
    N'•••••••• [Bảo mật - Không thể xem mật khẩu]' AS password_display,
    N'Đã băm an toàn bằng thuật toán BCrypt chuẩn quốc tế' AS security_status,
    a.is_active
FROM Account a
JOIN Employee e ON e.employee_id = a.employee_id;
GO

-- View 9: Tra cứu chi tiết phiếu bảo hành tổng hợp (vw_WarrantyTicketDetail)
CREATE VIEW vw_WarrantyTicketDetail
AS
SELECT
    t.ticket_id,
    t.ticket_code,
    t.warranty_id,
    t.serial_id,
    t.customer_id,
    t.customer_name,
    t.customer_phone,
    t.technician_id,
    e.employee_name AS technician_name,
    e.phone AS technician_phone,
    t.receive_date,
    t.expected_return_date,
    t.completed_date,
    t.customer_issue,
    t.device_condition,
    t.diagnosis,
    t.repair_action,
    t.repair_cost,
    t.status,
    t.notes,
    t.created_at,
    ps.imei_number,
    ps.serial_number,
    p.product_id,
    p.product_name,
    b.brand_name,
    w.warranty_start,
    w.warranty_end,
    w.warranty_status AS original_warranty_status,
    CASE 
        WHEN w.warranty_end IS NULL THEN N'Không có thẻ BH'
        WHEN w.warranty_end < CAST(GETDATE() AS DATE) THEN N'Hết hạn BH'
        ELSE N'Còn hạn BH'
    END AS warranty_coverage_text,
    CASE 
        WHEN w.warranty_end >= CAST(GETDATE() AS DATE) THEN DATEDIFF(DAY, CAST(GETDATE() AS DATE), w.warranty_end)
        ELSE 0
    END AS warranty_days_remaining
FROM Warranty_Ticket t
LEFT JOIN Employee e        ON t.technician_id = e.employee_id
LEFT JOIN Product_Serial ps ON t.serial_id = ps.serial_id
LEFT JOIN Product p         ON ps.product_id = p.product_id
LEFT JOIN Brand b           ON p.brand_id = b.brand_id
LEFT JOIN Warranty w        ON t.warranty_id = w.warranty_id;
GO

/* =====================================================================
   PHẦN 3. FUNCTION
   ===================================================================== */

-- Function 1: Tiền tạm tính trước thuế & chiết khấu của hóa đơn
CREATE FUNCTION fn_GetInvoiceSubTotal (@invoice_id INT)
RETURNS DECIMAL(18,2)
AS
BEGIN
    DECLARE @sub DECIMAL(18,2);
    SELECT @sub = ISNULL(SUM(line_total), 0)
    FROM Invoice_Detail
    WHERE invoice_id = @invoice_id;
    RETURN @sub;
END;
GO

-- Function 2: Tồn kho hiện tại của một sản phẩm
CREATE FUNCTION fn_GetProductStock (@product_id INT)
RETURNS INT
AS
BEGIN
    DECLARE @qty INT;
    SELECT @qty = quantity FROM Product WHERE product_id = @product_id;
    RETURN ISNULL(@qty, 0);
END;
GO

-- Function 3: Tỷ lệ % giảm giá của mã khuyến mãi tại một thời điểm
CREATE FUNCTION fn_GetPromotionPercent (@promotion_id INT, @on_date DATE)
RETURNS DECIMAL(5,2)
AS
BEGIN
    DECLARE @percent DECIMAL(5,2);
    SELECT @percent = discount_percent
    FROM Promotion
    WHERE promotion_id = @promotion_id
      AND @on_date BETWEEN start_date AND end_date;
    RETURN ISNULL(@percent, 0);
END;
GO

-- Function 4: Tên hạng thành viên của khách hàng
CREATE FUNCTION fn_GetCustomerRank (@customer_id INT)
RETURNS NVARCHAR(50)
AS
BEGIN
    DECLARE @rank NVARCHAR(50);
    SELECT @rank = r.rank_name
    FROM Customer c
    JOIN MembershipRank r ON c.rank_id = r.rank_id
    WHERE c.customer_id = @customer_id;
    RETURN @rank;
END;
GO

-- Function 5: Doanh thu thực tế trong một khoảng thời gian
CREATE FUNCTION fn_GetRevenueByDateRange (@from_date DATE, @to_date DATE)
RETURNS DECIMAL(18,2)
AS
BEGIN
    DECLARE @revenue DECIMAL(18,2);
    SELECT @revenue = SUM(total_amount)
    FROM Invoice
    WHERE status = 'Paid'
      AND invoice_date BETWEEN @from_date AND @to_date;
    RETURN ISNULL(@revenue, 0);
END;
GO

-- Function 6: Lấy danh sách hóa đơn theo khách hàng
CREATE FUNCTION fn_GetInvoicesByCustomer (@customer_id INT)
RETURNS TABLE
AS
RETURN
(
    SELECT invoice_id, invoice_date, sub_total, discount_amount, total_amount, status
    FROM Invoice
    WHERE customer_id = @customer_id
);
GO

-- Function 7: Tra cứu bảo hành theo số IMEI / Serial
CREATE FUNCTION fn_CheckWarrantyByImei (@imei VARCHAR(50))
RETURNS TABLE
AS
RETURN (
    SELECT 
        w.warranty_id,
        p.product_id,
        p.product_name,
        b.brand_name,
        ps.serial_id,
        ps.imei_number,
        ps.serial_number,
        c.customer_id,
        c.customer_name,
        c.phone AS customer_phone,
        i.invoice_id,
        i.invoice_date,
        w.warranty_start,
        w.warranty_end,
        w.warranty_status,
        CASE 
            WHEN w.warranty_end < CAST(GETDATE() AS DATE) THEN N'Hết hạn bảo hành'
            ELSE N'Còn trong hạn bảo hành'
        END AS warranty_eligibility,
        CASE 
            WHEN w.warranty_end >= CAST(GETDATE() AS DATE) 
            THEN DATEDIFF(DAY, CAST(GETDATE() AS DATE), w.warranty_end)
            ELSE 0
        END AS days_left
    FROM Product_Serial ps
    JOIN Product p          ON ps.product_id = p.product_id
    JOIN Brand b            ON p.brand_id = b.brand_id
    LEFT JOIN Warranty w    ON w.serial_id = ps.serial_id
    LEFT JOIN Invoice_Detail id ON w.invoice_detail_id = id.invoice_detail_id
    LEFT JOIN Invoice i     ON id.invoice_id = i.invoice_id
    LEFT JOIN Customer c    ON i.customer_id = c.customer_id
    WHERE ps.imei_number = @imei OR ps.serial_number = @imei
);
GO

-- Function 8: Thống kê hiệu suất làm việc của Kỹ thuật viên
CREATE FUNCTION fn_GetTechnicianPerformance (@employee_id INT)
RETURNS TABLE
AS
RETURN (
    SELECT 
        e.employee_id,
        e.employee_name,
        COUNT(t.ticket_id) AS total_received,
        SUM(CASE WHEN t.status IN ('Received', 'Diagnosing', 'Processing', 'WaitingParts') THEN 1 ELSE 0 END) AS in_progress_count,
        SUM(CASE WHEN t.status IN ('Completed', 'Delivered') THEN 1 ELSE 0 END) AS completed_count,
        SUM(CASE WHEN t.status = 'Cancelled' THEN 1 ELSE 0 END) AS cancelled_count,
        ISNULL(SUM(CASE WHEN t.status = 'Delivered' THEN t.repair_cost ELSE 0 END), 0) AS total_repair_revenue
    FROM Employee e
    LEFT JOIN Warranty_Ticket t ON e.employee_id = t.technician_id
    WHERE e.employee_id = @employee_id
    GROUP BY e.employee_id, e.employee_name
);
GO

/* =====================================================================
   PHẦN 4. TRIGGER
   ===================================================================== */

-- Trigger 1: Khi nhập kho -> Tăng tồn kho, ghi lịch sử, cập nhật tổng phiếu
CREATE TRIGGER trg_ImportDetail_AfterInsert
ON Import_Detail
AFTER INSERT
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE p
    SET p.quantity = p.quantity + x.total_qty
    FROM Product p
    JOIN (SELECT product_id, SUM(quantity) AS total_qty
          FROM inserted
          GROUP BY product_id) x ON p.product_id = x.product_id;

    INSERT INTO Stock_Movement (product_id, movement_type, quantity_change, reference_id, note)
    SELECT product_id, 'IMPORT', quantity, import_id, N'Import receipt'
    FROM inserted;

    UPDATE r
    SET r.total_amount = (SELECT SUM(d.line_total) FROM Import_Detail d WHERE d.import_id = r.import_id)
    FROM Import_Receipt r
    WHERE r.import_id IN (SELECT import_id FROM inserted);
END;
GO

-- Trigger 2: Kiểm tra tồn kho và tính hợp lệ trước khi ghi dòng hóa đơn
CREATE TRIGGER trg_InvoiceDetail_CheckBeforeInsert
ON Invoice_Detail
INSTEAD OF INSERT
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (SELECT 1 FROM inserted i
               JOIN Invoice inv ON inv.invoice_id = i.invoice_id
               WHERE inv.status <> 'Pending')
        THROW 50001, 'Items can only be added to a Pending invoice.', 1;

    IF EXISTS (SELECT 1 FROM inserted i
               JOIN Product p ON p.product_id = i.product_id
               WHERE p.is_active = 0)
        THROW 50002, 'This product is no longer sold.', 1;

    IF EXISTS (SELECT 1
               FROM (SELECT product_id, SUM(quantity) AS need
                     FROM inserted
                     GROUP BY product_id) n
               JOIN Product p ON p.product_id = n.product_id
               WHERE n.need > p.quantity)
        THROW 50003, 'Not enough product quantity in stock.', 1;

    INSERT INTO Invoice_Detail (invoice_id, product_id, quantity, unit_price)
    SELECT invoice_id, product_id, quantity, unit_price
    FROM inserted;
END;
GO

-- Trigger 3: Sau khi thêm dòng hóa đơn -> Giảm tồn kho, ghi nhật ký, tính tổng tiền
CREATE TRIGGER trg_InvoiceDetail_AfterInsert
ON Invoice_Detail
AFTER INSERT
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE p
    SET p.quantity = p.quantity - x.total_qty
    FROM Product p
    JOIN (SELECT product_id, SUM(quantity) AS total_qty
          FROM inserted
          GROUP BY product_id) x ON p.product_id = x.product_id;

    INSERT INTO Stock_Movement (product_id, movement_type, quantity_change, reference_id, note)
    SELECT product_id, 'SALE', -quantity, invoice_id, N'Sold on invoice'
    FROM inserted;

    UPDATE inv
    SET inv.sub_total       = s.sub,
        inv.discount_amount = dsc.disc,
        inv.total_amount    = s.sub - dsc.disc
    FROM Invoice inv
    LEFT JOIN Promotion pr ON pr.promotion_id = inv.promotion_id
    CROSS APPLY (SELECT ISNULL(SUM(d.line_total), 0) AS sub
                 FROM Invoice_Detail d
                 WHERE d.invoice_id = inv.invoice_id) s
    CROSS APPLY (SELECT ROUND(s.sub * ISNULL(pr.discount_percent, 0) / 100, 0) AS disc) dsc
    WHERE inv.invoice_id IN (SELECT invoice_id FROM inserted);
END;
GO

-- Trigger 4: Cập nhật tổng chi tiêu và tự động nâng hạng thẻ thành viên
CREATE TRIGGER trg_Invoice_UpdateCustomerRank
ON Invoice
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE c
    SET c.total_spent = s.spent,
        c.rank_id     = r.rank_id
    FROM Customer c
    JOIN (SELECT customer_id FROM inserted
          UNION
          SELECT customer_id FROM deleted) a ON a.customer_id = c.customer_id
    CROSS APPLY (SELECT ISNULL(SUM(i.total_amount), 0) AS spent
                 FROM Invoice i
                 WHERE i.customer_id = c.customer_id AND i.status = 'Paid') s
    CROSS APPLY (SELECT TOP 1 mr.rank_id
                 FROM MembershipRank mr
                 WHERE mr.min_spent <= s.spent
                 ORDER BY mr.min_spent DESC) r;
END;
GO

-- Trigger 5: Hủy hóa đơn -> Tự động hoàn tồn kho
CREATE TRIGGER trg_Invoice_AfterUpdateStatus
ON Invoice
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT UPDATE(status) RETURN;

    IF EXISTS (SELECT 1 FROM inserted i
               JOIN deleted d ON d.invoice_id = i.invoice_id
               WHERE d.status = 'Cancelled' AND i.status <> 'Cancelled')
    BEGIN
        ROLLBACK TRANSACTION;
        THROW 50004, 'A cancelled invoice cannot be reopened.', 1;
    END;

    UPDATE p
    SET p.quantity = p.quantity + x.qty
    FROM Product p
    JOIN (SELECT d.product_id, SUM(d.quantity) AS qty
          FROM Invoice_Detail d
          JOIN inserted i  ON i.invoice_id = d.invoice_id
          JOIN deleted old ON old.invoice_id = i.invoice_id
          WHERE i.status = 'Cancelled' AND old.status <> 'Cancelled'
          GROUP BY d.product_id) x ON p.product_id = x.product_id;

    INSERT INTO Stock_Movement (product_id, movement_type, quantity_change, reference_id, note)
    SELECT d.product_id, 'CANCEL_SALE', d.quantity, d.invoice_id, N'Invoice cancelled'
    FROM Invoice_Detail d
    JOIN inserted i  ON i.invoice_id = d.invoice_id
    JOIN deleted old ON old.invoice_id = i.invoice_id
    WHERE i.status = 'Cancelled' AND old.status <> 'Cancelled';
END;
GO

-- Trigger 6: Thanh toán thành công -> Tự động cập nhật trạng thái Paid
CREATE TRIGGER trg_Payment_AfterInsert
ON Payment
AFTER INSERT
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (SELECT 1 FROM inserted p
               JOIN Invoice inv ON inv.invoice_id = p.invoice_id
               WHERE inv.status = 'Cancelled')
    BEGIN
        ROLLBACK TRANSACTION;
        THROW 50005, 'Cannot add a payment to a cancelled invoice.', 1;
    END;

    UPDATE inv
    SET inv.status = 'Paid'
    FROM Invoice inv
    WHERE inv.invoice_id IN (SELECT invoice_id FROM inserted)
      AND inv.status = 'Pending'
      AND inv.total_amount > 0
      AND (SELECT SUM(p.amount) FROM Payment p WHERE p.invoice_id = inv.invoice_id) >= inv.total_amount;
END;
GO

-- Trigger 7: Cập nhật trạng thái bảo hành thiết bị
CREATE TRIGGER trg_Warranty_SetStatus
ON Warranty
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE w
    SET w.warranty_status =
        CASE WHEN w.warranty_end < CAST(GETDATE() AS DATE) THEN 'Expired' ELSE 'Active' END
    FROM Warranty w
    JOIN inserted i ON i.warranty_id = w.warranty_id
    WHERE w.warranty_status <> 'Claimed';
END;
GO

-- Trigger 8: Tự động cập nhật Serial thành Defective và thẻ BH thành Claimed khi lập phiếu bảo hành
CREATE TRIGGER trg_Warranty_Ticket_AfterInsert
ON Warranty_Ticket
AFTER INSERT
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE ps
    SET ps.status = 'Defective'
    FROM Product_Serial ps
    JOIN inserted i ON i.serial_id = ps.serial_id
    WHERE i.serial_id IS NOT NULL;

    UPDATE w
    SET w.warranty_status = 'Claimed'
    FROM Warranty w
    JOIN inserted i ON i.warranty_id = w.warranty_id
    WHERE i.warranty_id IS NOT NULL;
END;
GO

-- Trigger 9: Tự động ghi nhận hoàn tất và khôi phục trạng thái Serial khi bàn giao trả máy
CREATE TRIGGER trg_Warranty_Ticket_AfterUpdate
ON Warranty_Ticket
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE t
    SET t.completed_date = GETDATE()
    FROM Warranty_Ticket t
    JOIN inserted i ON i.ticket_id = t.ticket_id
    WHERE i.status IN ('Completed', 'Delivered') AND t.completed_date IS NULL;

    UPDATE ps
    SET ps.status = 'Sold'
    FROM Product_Serial ps
    JOIN inserted i ON i.serial_id = ps.serial_id
    WHERE i.status = 'Delivered' AND i.serial_id IS NOT NULL;
END;
GO

/* =====================================================================
   PHẦN 5. STORED PROCEDURES
   ===================================================================== */

-- SP 1: Tìm kiếm sản phẩm
CREATE PROCEDURE usp_Product_Search
    @keyword        NVARCHAR(100) = NULL,
    @brand_id       INT = NULL,
    @category_id    INT = NULL,
    @only_in_stock  BIT = 0
AS
BEGIN
    SET NOCOUNT ON;

    SELECT *
    FROM vw_ProductCatalog
    WHERE (@keyword IS NULL OR product_name LIKE N'%' + @keyword + N'%')
      AND (@brand_id IS NULL OR brand_id = @brand_id)
      AND (@category_id IS NULL OR category_id = @category_id)
      AND (@only_in_stock = 0 OR quantity > 0)
    ORDER BY product_name;
END;
GO

-- SP 2: Thêm sản phẩm mới
CREATE PROCEDURE usp_Product_Insert
    @brand_id       INT,
    @category_id    INT,
    @product_name   NVARCHAR(150),
    @import_price   DECIMAL(18,2),
    @selling_price  DECIMAL(18,2),
    @image_url      NVARCHAR(500) = NULL,
    @description    NVARCHAR(MAX) = NULL,
    @new_product_id INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO Product (brand_id, category_id, product_name, import_price, selling_price, image_url, description)
    VALUES (@brand_id, @category_id, @product_name, @import_price, @selling_price, @image_url, @description);

    SET @new_product_id = SCOPE_IDENTITY();
END;
GO

-- SP 3: Cập nhật sản phẩm
CREATE PROCEDURE usp_Product_Update
    @product_id     INT,
    @brand_id       INT,
    @category_id    INT,
    @product_name   NVARCHAR(150),
    @import_price   DECIMAL(18,2),
    @selling_price  DECIMAL(18,2),
    @is_active      BIT = 1,
    @image_url      NVARCHAR(500) = NULL,
    @description    NVARCHAR(MAX) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM Product WHERE product_id = @product_id)
        THROW 50030, 'Product not found.', 1;

    UPDATE Product
    SET brand_id      = @brand_id,
        category_id   = @category_id,
        product_name  = @product_name,
        import_price  = @import_price,
        selling_price = @selling_price,
        is_active     = @is_active,
        image_url     = ISNULL(@image_url, image_url),
        description   = ISNULL(@description, description)
    WHERE product_id = @product_id;
END;
GO

-- SP 4: Xóa hoặc ngưng kinh doanh sản phẩm
CREATE PROCEDURE usp_Product_Delete
    @product_id INT
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM Product WHERE product_id = @product_id)
        THROW 50030, 'Product not found.', 1;

    IF EXISTS (SELECT 1 FROM Invoice_Detail  WHERE product_id = @product_id)
    OR EXISTS (SELECT 1 FROM Import_Detail   WHERE product_id = @product_id)
    OR EXISTS (SELECT 1 FROM Product_Serial  WHERE product_id = @product_id)
    OR EXISTS (SELECT 1 FROM Stock_Movement  WHERE product_id = @product_id)
    BEGIN
        UPDATE Product SET is_active = 0 WHERE product_id = @product_id;
        SELECT 'Deactivated' AS result;
    END
    ELSE
    BEGIN
        DELETE FROM Product WHERE product_id = @product_id;
        SELECT 'Deleted' AS result;
    END
END;
GO

-- SP 5: Thêm khách hàng mới
CREATE PROCEDURE usp_Customer_Insert
    @customer_name NVARCHAR(100),
    @phone         VARCHAR(15),
    @address       NVARCHAR(255) = NULL,
    @new_customer_id INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (SELECT 1 FROM Customer WHERE phone = @phone)
        THROW 50040, 'Phone number already exists.', 1;

    INSERT INTO Customer (customer_name, phone, address)
    VALUES (@customer_name, @phone, @address);

    SET @new_customer_id = SCOPE_IDENTITY();
END;
GO

-- SP 6: Cập nhật thông tin khách hàng
CREATE PROCEDURE usp_Customer_Update
    @customer_id   INT,
    @customer_name NVARCHAR(100),
    @phone         VARCHAR(15),
    @address       NVARCHAR(255) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM Customer WHERE customer_id = @customer_id)
        THROW 50041, 'Customer not found.', 1;

    IF EXISTS (SELECT 1 FROM Customer WHERE phone = @phone AND customer_id <> @customer_id)
        THROW 50040, 'Phone number already exists.', 1;

    UPDATE Customer
    SET customer_name = @customer_name, phone = @phone, address = @address
    WHERE customer_id = @customer_id;
END;
GO

-- SP 7: Tạo hóa đơn hoàn chỉnh (kèm chi tiết & thanh toán)
CREATE PROCEDURE usp_CreateInvoice
    @customer_id     INT,
    @employee_id     INT,
    @items_json      NVARCHAR(MAX),
    @promotion_id    INT = NULL,
    @payment_method  VARCHAR(50) = NULL,
    @new_invoice_id  INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        IF ISJSON(@items_json) = 0
            THROW 50010, 'items_json is not valid JSON.', 1;

        DECLARE @items TABLE (product_id INT NOT NULL, quantity INT NOT NULL);

        INSERT INTO @items (product_id, quantity)
        SELECT product_id, quantity
        FROM OPENJSON(@items_json)
             WITH (product_id INT '$.product_id', quantity INT '$.quantity');

        IF NOT EXISTS (SELECT 1 FROM @items)
            THROW 50011, 'Invoice must contain at least one item.', 1;

        IF EXISTS (SELECT 1 FROM @items WHERE quantity <= 0)
            THROW 50011, 'Quantity must be greater than 0.', 1;

        IF NOT EXISTS (SELECT 1 FROM Customer WHERE customer_id = @customer_id)
            THROW 50012, 'Customer not found.', 1;

        IF EXISTS (SELECT 1 FROM @items it
                   LEFT JOIN Product p ON p.product_id = it.product_id
                   WHERE p.product_id IS NULL)
            THROW 50014, 'One or more products do not exist.', 1;

        IF @promotion_id IS NOT NULL
           AND dbo.fn_GetPromotionPercent(@promotion_id, CAST(GETDATE() AS DATE)) = 0
            THROW 50013, 'Promotion is invalid or expired.', 1;

        BEGIN TRANSACTION;

        INSERT INTO Invoice (customer_id, employee_id, promotion_id)
        VALUES (@customer_id, @employee_id, @promotion_id);

        SET @new_invoice_id = SCOPE_IDENTITY();

        INSERT INTO Invoice_Detail (invoice_id, product_id, quantity, unit_price)
        SELECT @new_invoice_id, it.product_id, it.quantity, p.selling_price
        FROM @items it
        JOIN Product p ON p.product_id = it.product_id;

        IF @payment_method IS NOT NULL
        BEGIN
            DECLARE @total DECIMAL(18,2);
            SELECT @total = total_amount FROM Invoice WHERE invoice_id = @new_invoice_id;

            INSERT INTO Payment (invoice_id, payment_method, amount)
            VALUES (@new_invoice_id, @payment_method, @total);
        END

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        SET @new_invoice_id = NULL;
        THROW;
    END CATCH
END;
GO

-- SP 8: Hủy hóa đơn (Có lý do & người duyệt)
CREATE PROCEDURE usp_CancelInvoice
    @invoice_id   INT,
    @reason       NVARCHAR(500) = NULL,
    @approved_by  NVARCHAR(100) = 'Admin'
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        IF NOT EXISTS (SELECT 1 FROM Invoice WHERE invoice_id = @invoice_id)
            THROW 50020, N'Hóa đơn không tồn tại.', 1;

        IF EXISTS (SELECT 1 FROM Invoice WHERE invoice_id = @invoice_id AND status = 'Cancelled')
            THROW 50021, N'Hóa đơn này đã được hủy trước đó.', 1;

        BEGIN TRANSACTION;
        UPDATE Invoice 
        SET status = 'Cancelled',
            cancel_reason = ISNULL(@reason, cancel_reason),
            cancel_approved_by = @approved_by,
            cancel_approved_at = GETDATE()
        WHERE invoice_id = @invoice_id;
        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END;
GO

-- SP 9: Ghi nhận thanh toán
CREATE PROCEDURE usp_AddPayment
    @invoice_id      INT,
    @payment_method  VARCHAR(50),
    @amount          DECIMAL(18,2)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        IF NOT EXISTS (SELECT 1 FROM Invoice WHERE invoice_id = @invoice_id)
            THROW 50020, 'Invoice not found.', 1;

        IF EXISTS (SELECT 1 FROM Invoice WHERE invoice_id = @invoice_id AND status = 'Cancelled')
            THROW 50005, 'Cannot add a payment to a cancelled invoice.', 1;

        IF @amount <= 0
            THROW 50022, 'Payment amount must be greater than 0.', 1;

        BEGIN TRANSACTION;
        INSERT INTO Payment (invoice_id, payment_method, amount)
        VALUES (@invoice_id, @payment_method, @amount);
        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END;
GO

-- SP 10: Lấy chi tiết hóa đơn
CREATE PROCEDURE usp_GetInvoiceDetail
    @invoice_id INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        d.invoice_detail_id,
        d.product_id,
        p.product_name,
        p.image_url,
        d.quantity,
        d.unit_price,
        d.line_total
    FROM Invoice_Detail d
    JOIN Product p ON p.product_id = d.product_id
    WHERE d.invoice_id = @invoice_id;

    SELECT
        i.invoice_id,
        i.invoice_date,
        i.status,
        i.sub_total,
        i.discount_amount,
        i.total_amount,
        i.shipping_address,
        i.order_notes,
        i.payment_method_selected,
        i.cancel_reason,
        c.customer_id,
        c.customer_name,
        c.phone AS customer_phone,
        e.employee_name,
        pr.promotion_name
    FROM Invoice i
    JOIN Customer c        ON i.customer_id = c.customer_id
    LEFT JOIN Employee e   ON i.employee_id = e.employee_id
    LEFT JOIN Promotion pr ON i.promotion_id = pr.promotion_id
    WHERE i.invoice_id = @invoice_id;
END;
GO

-- SP 11: Tạo phiếu nhập kho
CREATE PROCEDURE usp_CreateImportReceipt
    @supplier_id    INT,
    @employee_id    INT,
    @items_json     NVARCHAR(MAX),
    @new_import_id  INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        IF ISJSON(@items_json) = 0
            THROW 50050, 'items_json is not valid JSON.', 1;

        DECLARE @items TABLE (product_id INT NOT NULL, quantity INT NOT NULL, import_price DECIMAL(18,2) NOT NULL);

        INSERT INTO @items (product_id, quantity, import_price)
        SELECT product_id, quantity, import_price
        FROM OPENJSON(@items_json)
             WITH (product_id INT '$.product_id', quantity INT '$.quantity', import_price DECIMAL(18,2) '$.import_price');

        IF NOT EXISTS (SELECT 1 FROM @items)
            THROW 50051, 'Import receipt must contain at least one item.', 1;

        BEGIN TRANSACTION;

        INSERT INTO Import_Receipt (supplier_id, employee_id) VALUES (@supplier_id, @employee_id);
        SET @new_import_id = SCOPE_IDENTITY();

        INSERT INTO Import_Detail (import_id, product_id, quantity, import_price)
        SELECT @new_import_id, product_id, quantity, import_price FROM @items;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        SET @new_import_id = NULL;
        THROW;
    END CATCH
END;
GO

-- SP 12: Báo cáo doanh thu theo năm
CREATE PROCEDURE usp_GetRevenueReport
    @year INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT revenue_month, total_invoices, gross_amount, total_discount, revenue
    FROM vw_MonthlyRevenue
    WHERE revenue_year = @year
    ORDER BY revenue_month;
END;
GO

-- SP 13: Đăng ký bảo hành cho sản phẩm
CREATE PROCEDURE usp_RegisterWarranty
    @invoice_detail_id INT,
    @serial_id         INT = NULL,
    @months            INT = 12,
    @note              NVARCHAR(500) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @start DATE;

    SELECT @start = i.invoice_date
    FROM Invoice_Detail d
    JOIN Invoice i ON i.invoice_id = d.invoice_id
    WHERE d.invoice_detail_id = @invoice_detail_id AND i.status = 'Paid';

    IF @start IS NULL
        THROW 50060, 'Invoice line not found or invoice is not paid.', 1;

    INSERT INTO Warranty (invoice_detail_id, serial_id, warranty_start, warranty_end, warranty_note)
    VALUES (@invoice_detail_id, @serial_id, @start, DATEADD(MONTH, @months, @start), @note);
END;
GO

-- SP 14: Lập phiếu tiếp nhận bảo hành máy
CREATE PROCEDURE usp_CreateWarrantyTicket
    @serial_number_or_imei VARCHAR(50) = NULL,
    @customer_name         NVARCHAR(100),
    @customer_phone        VARCHAR(15),
    @technician_id         INT = NULL,
    @customer_issue        NVARCHAR(1000),
    @device_condition      NVARCHAR(500) = NULL,
    @expected_days         INT = 3,
    @notes                 NVARCHAR(500) = NULL,
    @new_ticket_code       VARCHAR(50) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRY
        BEGIN TRANSACTION;

        DECLARE @found_serial_id INT = NULL;
        DECLARE @found_warranty_id INT = NULL;
        DECLARE @found_customer_id INT = NULL;

        IF @serial_number_or_imei IS NOT NULL AND LEN(TRIM(@serial_number_or_imei)) > 0
        BEGIN
            SELECT TOP 1 
                @found_serial_id = ps.serial_id,
                @found_warranty_id = w.warranty_id,
                @found_customer_id = i.customer_id
            FROM Product_Serial ps
            LEFT JOIN Warranty w ON w.serial_id = ps.serial_id
            LEFT JOIN Invoice_Detail id ON w.invoice_detail_id = id.invoice_detail_id
            LEFT JOIN Invoice i ON id.invoice_id = i.invoice_id
            WHERE ps.imei_number = @serial_number_or_imei OR ps.serial_number = @serial_number_or_imei;
        END

        IF @found_customer_id IS NULL AND @customer_phone IS NOT NULL
        BEGIN
            SELECT TOP 1 @found_customer_id = customer_id FROM Customer WHERE phone = @customer_phone;
        END

        DECLARE @today_str VARCHAR(8) = CONVERT(VARCHAR(8), GETDATE(), 112);
        DECLARE @next_num INT;

        SELECT @next_num = ISNULL(MAX(CAST(RIGHT(ticket_code, 4) AS INT)), 0) + 1
        FROM Warranty_Ticket
        WHERE ticket_code LIKE 'BH-' + @today_str + '-%';

        SET @new_ticket_code = 'BH-' + @today_str + '-' + RIGHT('0000' + CAST(@next_num AS VARCHAR(4)), 4);

        INSERT INTO Warranty_Ticket (
            ticket_code, warranty_id, serial_id, customer_id, customer_name, customer_phone,
            technician_id, receive_date, expected_return_date, customer_issue,
            device_condition, status, notes
        )
        VALUES (
            @new_ticket_code, @found_warranty_id, @found_serial_id, @found_customer_id, @customer_name, @customer_phone,
            @technician_id, GETDATE(), DATEADD(DAY, @expected_days, CAST(GETDATE() AS DATE)), @customer_issue,
            @device_condition, 'Received', @notes
        );

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END;
GO

-- SP 15: Kỹ thuật viên cập nhật chẩn đoán, biện pháp xử lý, chi phí
CREATE PROCEDURE usp_UpdateWarrantyTicket
    @ticket_id        INT,
    @technician_id    INT = NULL,
    @diagnosis        NVARCHAR(1000) = NULL,
    @repair_action    NVARCHAR(1000) = NULL,
    @repair_cost      DECIMAL(18,2) = 0,
    @status           VARCHAR(30),
    @notes            NVARCHAR(500) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE Warranty_Ticket
    SET 
        technician_id = COALESCE(@technician_id, technician_id),
        diagnosis = COALESCE(@diagnosis, diagnosis),
        repair_action = COALESCE(@repair_action, repair_action),
        repair_cost = @repair_cost,
        status = @status,
        notes = COALESCE(@notes, notes)
    WHERE ticket_id = @ticket_id;
END;
GO

-- SP 16: Bàn giao máy cho khách hàng
CREATE PROCEDURE usp_DeliverWarrantyTicket
    @ticket_id INT,
    @notes     NVARCHAR(500) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE Warranty_Ticket
    SET 
        status = 'Delivered',
        completed_date = GETDATE(),
        notes = CASE WHEN @notes IS NOT NULL THEN ISNULL(notes + ' | ', '') + @notes ELSE notes END
    WHERE ticket_id = @ticket_id;
END;
GO

/* =====================================================================
   PHẦN 6. DỮ LIỆU MẪU (SEED DATA TOÀN DIỆN 24HSTORE & CÔNG NGHỆ)
   ===================================================================== */

-- Hãng sản xuất
SET IDENTITY_INSERT Brand ON;
INSERT INTO Brand (brand_id, brand_name) VALUES 
(1, N'Apple'), 
(2, N'Samsung'), 
(3, N'Xiaomi'), 
(4, N'Oppo'), 
(5, N'Vivo'), 
(6, N'Baseus'), 
(7, N'Havit');
SET IDENTITY_INSERT Brand OFF;

-- Danh mục sản phẩm
SET IDENTITY_INSERT Category ON;
INSERT INTO Category (category_id, category_name) VALUES 
(1, N'Smartphone'), 
(2, N'Tablet'), 
(3, N'Accessory'), 
(4, N'Smartwatch');
SET IDENTITY_INSERT Category OFF;

-- Hạng thành viên
SET IDENTITY_INSERT MembershipRank ON;
INSERT INTO MembershipRank (rank_id, rank_name, min_spent) VALUES
(1, 'Standard', 0),
(2, 'Silver',   30000000),
(3, 'Gold',     80000000),
(4, 'Platinum', 150000000);
SET IDENTITY_INSERT MembershipRank OFF;

-- Nhà cung cấp
INSERT INTO Supplier (supplier_name, phone, address) VALUES
(N'Công Ty Phân Phối Synnex FPT',   '0901111111', N'TP. Hồ Chí Minh'),
(N'Nhà Phân Phối Viễn Sơn',         '0902222222', N'Hà Nội'),
(N'Digiworld Distribution',         '0902222223', N'Đà Nẵng'),
(N'Petrosetco Distribution (PSD)',  '0903333333', N'Cần Thơ');

-- Khách hàng ban đầu
INSERT INTO Customer (customer_name, phone, address, rank_id, total_spent) VALUES
(N'Nguyễn Văn An',  '0911111111', N'Ninh Kiều, Cần Thơ', 1, 0),
(N'Trần Thị Bích',  '0922222222', N'Hải Châu, Đà Nẵng',  1, 0),
(N'Lê Hoàng Nam',   '0933333333', N'Quận 1, TP. HCM',    1, 0),
(N'Phạm Gia Bảo',   '0944444444', N'Cầu Giấy, Hà Nội',   1, 0),
(N'Võ Minh Thư',    '0955555555', N'TP. Vũng Tàu',       1, 0);

-- Nhân viên cửa hàng
INSERT INTO Employee (employee_name, phone, position) VALUES
(N'Lê Văn Admin',    '0966666666', N'Manager'),
(N'Phạm Văn Sales',  '0977777777', N'Sales'),
(N'Trần Minh Kho',   '0988888888', N'Warehouse Staff');

-- Tài khoản hệ thống (Mật khẩu mặc định: admin123, sales123, kho123)
-- DbInitializer sẽ tự băm BCrypt chuẩn khi ứng dụng khởi chạy
INSERT INTO Account (employee_id, username, password_hash, role) VALUES
(1, 'admin', 'REPLACE_WITH_BCRYPT_HASH', 'Admin'),
(2, 'sales', 'REPLACE_WITH_BCRYPT_HASH', 'Sales'),
(3, 'kho',   'REPLACE_WITH_BCRYPT_HASH', 'Warehouse');

-- Kho hàng
INSERT INTO Warehouse (warehouse_name, address, capacity, manager_name) VALUES
(N'Kho Tổng Hồ Chí Minh',  N'249 Trần Quang Khải, Tân Định, Q.1, TP.HCM', 1000, N'Trần Minh Kho'),
(N'Kho Chi Nhánh Hà Nội',  N'120 Thái Hà, Đống Đa, Hà Nội',               500,  N'Lê Văn Admin');

-- Chương trình khuyến mãi
INSERT INTO Promotion (promotion_name, discount_percent, start_date, end_date, description) VALUES
(N'Đại Tiệc Mùa Hè',   10, '2026-06-01', '2026-06-30', N'Giảm giá 10% toàn bộ smartphone'),
(N'Apple Festival',    15, '2026-07-01', '2026-07-15', N'Ưu đãi 15% cho hệ sinh thái Apple'),
(N'Khuyến Mãi Mùa Thu', 5, '2026-10-01', '2026-10-31', N'Tri ân khách hàng tháng 10');

-- =========================================================================
-- Danh mục sản phẩm công nghệ (Storefront phong cách 24hStore.vn)
-- =========================================================================
INSERT INTO Product (brand_id, category_id, product_name, import_price, selling_price, quantity, is_active, image_url, description) VALUES
(1, 1, N'iPhone Duo 2TB | Chính hãng Việt Nam', 90000000, 103990000, 5, 1, 'https://24hstore.vn/images/products/2026/09/10/large/iphone-duo-2tb-troi-dem_1789000472.jpg', N'Siêu phẩm iPhone Duo màn hình kép gập tương lai độc quyền tại KittyPhone. Thiết kế bản lề titan không nếp gấp, dung lượng đỉnh cao 2TB, trang bị chip xử lý Apple Silicon thế hệ mới tối ưu hóa trí tuệ nhân tạo Apple Intelligence toàn diện.

--- THÔNG SỐ KỸ THUẬT ---
• Màn hình chính: Ultra Retina XDR OLED kép 7.8 inch, 120Hz ProMotion, 3000 nits, HDR10+
• Màn hình phụ: Super Retina OLED 6.3 inch, Ceramic Shield thế hệ mới chống trầy xước
• Vi xử lý (CPU): Apple M4 Pro / A20 Fusion thế hệ mới, Neural Engine 16 nhân
• RAM & Bộ nhớ: 16GB RAM Unified, Bộ nhớ trong 2TB NVMe siêu tốc độ
• Camera sau: Cụm 3 camera 48MP Fusion OIS + 48MP Siêu rộng + 48MP Tiềm vọng Zoom quang 10x
• Camera trước: Camera kép 12MP TrueDepth dưới màn hình
• Pin & Sạc: Thời lượng xem video lên đến 36 giờ, Sạc siêu tốc 45W, MagSafe 25W
• Thiết kế & Khung viền: Titan siêu nhẹ chuẩn hàng không vũ trụ, kháng nước bụi IP68
• Hệ điều hành: iOS 18 tích hợp trọn vẹn Apple Intelligence tiếng Việt'),
(1, 1, N'iPhone Duo 1TB | Chính hãng Việt Nam', 75000000, 84490000, 6, 1, 'https://24hstore.vn/images/products/2026/09/10/large/iphone-duo-1tb-trang_1789000266.jpg', N'iPhone Duo 1TB màn hình mở rộng công nghệ cao, bản lề titan không nếp gấp, camera tiềm vọng thế hệ đột phá, hỗ trợ làm việc đa nhiệm 2 màn hình cùng lúc mượt mà.

--- THÔNG SỐ KỸ THUẬT ---
• Màn hình chính: Ultra Retina XDR OLED kép 7.8 inch, 120Hz ProMotion, 3000 nits
• Màn hình phụ: Super Retina OLED 6.3 inch Ceramic Shield
• Vi xử lý (CPU): Apple Silicon A20 Fusion thế hệ mới
• RAM & Bộ nhớ: 16GB RAM, 1TB ROM NVMe
• Camera sau: Cụm 3 camera 48MP Fusion + 48MP Góc siêu rộng + 48MP Tiềm vọng 10x
• Pin & Sạc: Xem video 35 giờ, sạc nhanh 45W Type-C
• Hệ điều hành: iOS 18 tích hợp Apple Intelligence'),
(1, 1, N'iPhone Duo 512GB | Chính hãng Việt Nam', 63000000, 71490000, 8, 1, 'https://24hstore.vn/images/products/2026/09/10/large/iphone-duo-512gb-troi-dem_1788999777.jpg', N'iPhone Duo 512GB màu Trời Đêm bí ẩn, khung viền siêu cứng cáp, sạc nhanh MagSafe thế hệ mới, bảo hành 12 tháng chính hãng VN/A tại KittyPhone.

--- THÔNG SỐ KỸ THUẬT ---
• Màn hình: Màn hình gập kép 7.8 inch 120Hz ProMotion, màn hình ngoài 6.3 inch
• Vi xử lý (CPU): Apple A20 Fusion, GPU 6 nhân
• RAM & Bộ nhớ: 16GB RAM, 512GB ROM
• Camera: 48MP Fusion + 48MP Ultra Wide + 48MP Tele 10x
• Pin: Thời lượng dùng liên tục cả ngày, sạc nhanh 45W'),
(1, 1, N'iPhone Duo 256GB | Chính hãng Việt Nam', 56000000, 64990000, 10, 1, 'https://24hstore.vn/images/products/2026/09/10/large/iphone-duo-trang_1788999205.jpg', N'iPhone Duo 256GB chính hãng Việt Nam. Thiết kế gập mở sang trọng màu Trắng Tinh Khôi, màn hình OLED ProMotion 120Hz mượt mà, cấu hình đỉnh cao.

--- THÔNG SỐ KỸ THUẬT ---
• Màn hình: Kép gập OLED 7.8 inch 120Hz, màn ngoài 6.3 inch
• Vi xử lý: Apple A20 Fusion
• RAM & Bộ nhớ: 16GB RAM, 256GB ROM
• Camera: 48MP + 48MP + 48MP
• Hệ điều hành: iOS 18'),
(1, 1, N'iPhone 18 Pro Max 2TB | Chính hãng Việt Nam', 72000000, 80990000, 7, 1, 'https://24hstore.vn/images/products/2026/09/10/large/iphone-18-pro-max-2tb-den_1788994688.jpg', N'Đỉnh cao công nghệ Apple với iPhone 18 Pro Max 2TB. Vi xử lý A20 Pro tiến trình 2nm tối tân, camera zoom tiềm vọng 10x quang học, thời lượng pin lên đến 35 giờ liên tục.

--- THÔNG SỐ KỸ THUẬT ---
• Màn hình: Super Retina XDR OLED 6.9 inch, 2868 x 1320 pixels, ProMotion 120Hz, 3000 nits
• Vi xử lý (CPU): Apple A20 Pro 2nm siêu mạnh mẽ, GPU 6 nhân Ray Tracing
• RAM & Bộ nhớ: 12GB RAM, Bộ nhớ trong 2TB NVMe siêu tốc
• Camera sau: Cụm 3 camera 48MP Fusion OIS + 48MP Siêu rộng + 48MP Tiềm vọng 10x quang
• Camera trước: 12MP TrueDepth f/1.9, Autofocus, Cinematic Mode 4K
• Pin & Sạc: Xem video 35 giờ, sạc nhanh 50% trong 25 phút, MagSafe 25W
• Nút điều khiển: Nút Camera Control cảm biến lực + Nút Action Button đa chức năng
• Kết nối: 5G đa băng tần, Wi-Fi 7, Bluetooth 5.4, USB-C 10Gbps
• Hệ điều hành: iOS 18 tích hợp Apple Intelligence'),
(1, 1, N'iPhone 18 Pro Max 1TB | Chính hãng Việt Nam', 54000000, 61490000, 8, 1, 'https://24hstore.vn/images/products/2026/09/10/large/iphone-18-pro-max-1tb-bac_1788994673.jpg', N'iPhone 18 Pro Max 1TB màu Bạc Ánh Kim, khung titan thế hệ mới nhất, màn hình siêu sáng 3000 nits hiển thị rõ nét dưới trời nắng gắt.

--- THÔNG SỐ KỸ THUẬT ---
• Màn hình: Super Retina XDR OLED 6.9 inch 120Hz ProMotion
• Vi xử lý: Apple A20 Pro 2nm
• RAM & Bộ nhớ: 12GB RAM, 1TB ROM
• Camera: 48MP + 48MP + 48MP Zoom 10x
• Pin: Xem video 35 giờ, sạc Type-C USB 3'),
(1, 1, N'iPhone 18 Pro Max 512GB | Chính hãng Việt Nam', 42000000, 48490000, 12, 1, 'https://24hstore.vn/images/products/2026/09/10/large/iphone-18-pro-max-512gb-bang_1788994658.jpg', N'iPhone 18 Pro Max 512GB màu Băng Tuyết sang trọng. Trợ giá thu cũ đổi mới lên đến 2.5 triệu đồng, hỗ trợ trả góp 0% tại KittyPhone.

--- THÔNG SỐ KỸ THUẬT ---
• Màn hình: 6.9 inch OLED ProMotion 120Hz
• Vi xử lý: Apple A20 Pro 2nm
• RAM & Bộ nhớ: 12GB RAM, 512GB ROM
• Camera: 48MP Fusion OIS + 48MP Ultra Wide + 48MP Tele 10x
• Pin: 35 giờ sử dụng, sạc nhanh MagSafe 25W'),
(1, 1, N'iPhone 18 Pro Max 256GB | Chính hãng Việt Nam', 36000000, 41990000, 15, 1, 'https://24hstore.vn/images/products/2026/09/10/large/iphone-18-pro-max-do_1788994643.jpg', N'iPhone 18 Pro Max 256GB màu Đỏ Ruby quyền lực, camera điện toán cao cấp, nút Camera Control cảm ứng trượt siêu nhạy.

--- THÔNG SỐ KỸ THUẬT ---
• Màn hình: 6.9 inch Super Retina XDR OLED 120Hz
• Vi xử lý: Apple A20 Pro 2nm
• RAM & Bộ nhớ: 12GB RAM, 256GB ROM
• Camera: 48MP + 48MP + 48MP Zoom 10x quang
• Hệ điều hành: iOS 18'),
(1, 1, N'iPhone 18 Pro 256GB | Chính hãng Việt Nam', 33000000, 38490000, 14, 1, 'https://24hstore.vn/images/products/2026/09/10/large/iphone-18-pro-256gb-do_1788994377.jpg', N'iPhone 18 Pro 256GB nhỏ gọn vừa tay, sức mạnh Pro vô đối với chip A20 Pro, màn hình ProMotion 120Hz viền siêu mỏng.

--- THÔNG SỐ KỸ THUẬT ---
• Màn hình: Super Retina XDR OLED 6.3 inch, 120Hz ProMotion, 3000 nits
• Vi xử lý (CPU): Apple A20 Pro 2nm, GPU 6 nhân
• RAM & Bộ nhớ: 12GB RAM, 256GB ROM
• Camera sau: 48MP Fusion + 48MP Ultra Wide + 48MP Telephoto 5x
• Pin & Sạc: Xem video 29 giờ, sạc nhanh USB-C, sạc không dây MagSafe
• Hệ điều hành: iOS 18'),
(1, 1, N'iPhone 17 Pro Max 256GB | Chính hãng Việt Nam', 28000000, 33990000, 16, 1, 'https://24hstore.vn/images/products/2025/09/10/large/iphone-17-pro-max-cam_1757461075.jpg', N'iPhone 17 Pro Max 256GB phiên bản màu Cam Sa Mạc ấn tượng. Viền màn hình mỏng kỷ lục, chip A19 Pro tiết kiệm điện năng vượt trội.

--- THÔNG SỐ KỸ THUẬT ---
• Màn hình: Super Retina XDR OLED 6.9 inch, ProMotion 120Hz, Dynamic Island
• Vi xử lý (CPU): Apple A19 Pro 3nm
• RAM & Bộ nhớ: 8GB RAM, 256GB ROM
• Camera sau: 48MP chính + 48MP góc siêu rộng + 12MP Telephoto 5x
• Pin & Sạc: Xem video 33 giờ, sạc nhanh Type-C'),
(1, 1, N'iPhone 17 256GB | Chính hãng Việt Nam', 20500000, 24990000, 18, 1, 'https://24hstore.vn/images/products/2025/09/10/large/iphone-17-xanh-la_1757461999.jpg', N'iPhone 17 256GB màu Xanh Lá Pastel thời thượng, trang bị màn hình 120Hz ProMotion lần đầu xuất hiện trên bản tiêu chuẩn.

--- THÔNG SỐ KỸ THUẬT ---
• Màn hình: Super Retina XDR OLED 6.1 inch, ProMotion 120Hz
• Vi xử lý (CPU): Apple A19 Bionic 6 nhân
• RAM & Bộ nhớ: 8GB RAM, 256GB ROM
• Camera sau: Kép 48MP Fusion + 48MP Ultra Wide
• Pin & Sạc: Xem video 26 giờ, sạc nhanh Type-C'),
(1, 1, N'iPhone Air 1TB | Chính hãng Việt Nam', 26000000, 31990000, 8, 1, 'https://24hstore.vn/images/products/2025/09/10/large/iphone-air-den_1757462555.jpg', N'iPhone Air siêu phẩm mỏng nhẹ chỉ 5.5mm, chất liệu titan siêu cứng, định nghĩa lại trải nghiệm cầm nắm thiết bị di động.

--- THÔNG SỐ KỸ THUẬT ---
• Màn hình: Super Retina XDR OLED 6.6 inch viền siêu mỏng, 120Hz
• Vi xử lý (CPU): Apple A19 Bionic
• RAM & Bộ nhớ: 8GB RAM, 1TB ROM
• Độ mỏng: Chỉ 5.5mm, trọng lượng 165g
• Camera: 48MP Fusion chất lượng cao'),
(1, 1, N'iPhone 16 Pro Max 256GB | Chính hãng VN/A', 26500000, 31490000, 22, 1, 'https://cdn2.cellphones.com.vn/insecure/rs:fill:358:358/q:90/plain/https://cellphones.com.vn/media/catalog/product/i/p/iphone-16-pro-max.png', N'iPhone 16 Pro Max 256GB Titan Sa Mạc - Phiên bản bán chạy nhất thị trường. Màn hình lớn 6.9 inch, chip A18 Pro tối tân, phím điều khiển Camera Control cảm ứng.

--- THÔNG SỐ KỸ THUẬT ---
• Màn hình: Super Retina XDR OLED 6.9 inch, 2868 x 1320 pixels, ProMotion 120Hz, 2000 nits
• Vi xử lý (CPU): Apple A18 Pro 6 nhân, GPU 6 nhân, Neural Engine 16 nhân
• RAM & Bộ nhớ: 8GB RAM, 256GB ROM NVMe
• Camera sau: Chính 48MP Fusion (f/1.78, OIS) + Góc rộng 48MP + Tele 12MP zoom 5x quang học
• Camera trước: TrueDepth 12MP f/1.9 autofocus
• Nút điều khiển: Phím Camera Control cảm biến lực + Action Button
• Pin & Sạc: Xem video 33 giờ, sạc nhanh 50% trong 30 phút, MagSafe 25W, USB-C 3 10Gbps
• Chuẩn kháng nước: IP68 (ngâm nước 6m trong 30 phút)
• Hệ điều hành: iOS 18 hỗ trợ Apple Intelligence tiếng Việt'),
(1, 1, N'iPhone 16 Pro 128GB | Chính hãng VN/A', 22500000, 26990000, 15, 1, 'https://24hstore.vn/images/products/2024/09/10/large/iphone-16-pro-titan-den_1725928702.jpg', N'iPhone 16 Pro 128GB Titan Đen nhỏ gọn đẳng cấp Pro, viền siêu mỏng, camera zoom quang 5x tetraprism, chip A18 Pro hiệu năng vô địch.

--- THÔNG SỐ KỸ THUẬT ---
• Màn hình: Super Retina XDR OLED 6.3 inch, 2622 x 1206 pixels, ProMotion 120Hz
• Vi xử lý (CPU): Apple A18 Pro 6 nhân
• RAM & Bộ nhớ: 8GB RAM, 128GB ROM
• Camera sau: Chính 48MP + Góc rộng 48MP + Tele 12MP 5x quang học
• Nút chức năng: Camera Control + Action Button
• Pin & Sạc: Xem video 27 giờ, sạc nhanh Type-C'),
(1, 1, N'iPhone 16 Plus 128GB | Chính hãng VN/A', 20500000, 24490000, 16, 1, 'https://cdn2.cellphones.com.vn/insecure/rs:fill:358:358/q:90/plain/https://cellphones.com.vn/media/catalog/product/i/p/iphone-16-plus-1.png', N'iPhone 16 Plus 128GB màn hình 6.7 inch siêu lớn, thời lượng pin bền bỉ nhất dòng iPhone 16 tiêu chuẩn, camera kép 48MP sắc nét.

--- THÔNG SỐ KỸ THUẬT ---
• Màn hình: Super Retina XDR OLED 6.7 inch, 2796 x 1290 pixels, độ sáng 2000 nits
• Vi xử lý (CPU): Apple A18 6 nhân, GPU 5 nhân, Neural Engine 16 nhân
• RAM & Bộ nhớ: 8GB RAM, 128GB ROM
• Camera sau: Kép 48MP Fusion + 12MP Ultra Wide
• Pin & Sạc: Xem video 27 giờ, sạc nhanh Type-C'),
(1, 1, N'iPhone 16 128GB | Chính hãng VN/A', 17900000, 21490000, 25, 1, 'https://cdn2.cellphones.com.vn/insecure/rs:fill:358:358/q:90/plain/https://cellphones.com.vn/media/catalog/product/i/p/iphone-16-1.png', N'iPhone 16 128GB sắc màu Lưu Ly độc đáo, nút Camera Control và nút Action tiện lợi, camera đặt dọc hỗ trợ quay video không gian Spatial Video.

--- THÔNG SỐ KỸ THUẬT ---
• Màn hình: Super Retina XDR OLED 6.1 inch, Dynamic Island, 2000 nits
• Vi xử lý (CPU): Apple A18 6 nhân
• RAM & Bộ nhớ: 8GB RAM, 128GB ROM
• Camera sau: 48MP Fusion + 12MP Ultra Wide
• Pin & Sạc: Xem video 22 giờ, sạc nhanh Type-C'),
(1, 1, N'iPhone 15 Pro Max 256GB | Chính hãng VN/A', 23500000, 27990000, 18, 1, 'https://24hstore.vn/images/products/2024/08/27/large/iphone-15-pro-max-titan-trang_1724723754.jpg', N'iPhone 15 Pro Max 256GB Titan Trắng chính hãng VN/A. Khung viền Titan hàng không vũ trụ siêu nhẹ, cổng USB-C 10Gbps, chip A17 Pro chơi game đồ họa đỉnh cao.

--- THÔNG SỐ KỸ THUẬT ---
• Màn hình: Super Retina XDR OLED 6.7 inch, 120Hz ProMotion, Always-On Display
• Vi xử lý (CPU): Apple A17 Pro 3nm, GPU 6 nhân hỗ trợ Ray Tracing
• RAM & Bộ nhớ: 8GB RAM, 256GB ROM
• Camera sau: 48MP chính OIS + 12MP siêu rộng + 12MP Tele 5x quang học
• Cổng sạc: USB-C chuẩn USB 3 (10Gbps)
• Pin: Xem video 29 giờ'),
(1, 1, N'iPhone 15 128GB | Chính hãng VN/A', 15000000, 17990000, 24, 1, 'https://cdn2.cellphones.com.vn/insecure/rs:fill:358:358/q:90/plain/https://cellphones.com.vn/media/catalog/product/i/p/iphone-15-plus_1_.png', N'iPhone 15 128GB Dynamic Island tiện ích, camera 48MP nâng cấp vượt trội, mặt lưng kính pha màu nhám mịn, cổng sạc Type-C thế hệ mới.

--- THÔNG SỐ KỸ THUẬT ---
• Màn hình: Super Retina XDR OLED 6.1 inch, Dynamic Island, 2000 nits
• Vi xử lý (CPU): Apple A16 Bionic 6 nhân
• RAM & Bộ nhớ: 6GB RAM, 128GB ROM
• Camera sau: Chính 48MP f/1.6 + Góc siêu rộng 12MP
• Cổng sạc: USB-C tiện lợi đa năng'),
(1, 1, N'iPhone 14 Pro Max 128GB | Cũ Like New', 15500000, 18490000, 12, 1, 'https://cdn2.cellphones.com.vn/insecure/rs:fill:358:358/q:90/plain/https://cellphones.com.vn/media/catalog/product/i/p/iphone-14-pro-max.png', N'iPhone 14 Pro Max 128GB Tím Deep Purple đẹp 99%, nguyên zin 100%, bảo hành 1 đổi 1 trong 30 ngày tại hệ thống KittyPhone.

--- THÔNG SỐ KỸ THUẬT ---
• Màn hình: Super Retina XDR OLED 6.7 inch, ProMotion 120Hz, Dynamic Island
• Vi xử lý (CPU): Apple A16 Bionic 6 nhân
• RAM & Bộ nhớ: 6GB RAM, 128GB ROM
• Camera sau: 48MP chính + 12MP góc rộng + 12MP Tele 3x
• Tình trạng: Máy đẹp 99% nguyên bản chính hãng VN/A'),
(1, 1, N'iPhone 13 128GB | Chính hãng VN/A', 10000000, 12490000, 20, 1, 'https://cdn2.cellphones.com.vn/insecure/rs:fill:358:358/q:90/plain/https://cellphones.com.vn/media/catalog/product/i/p/iphone-13.png', N'iPhone 13 128GB giá tốt nhất phân khúc, pin trâu, hiệu năng chip A15 Bionic vẫn cực kỳ mạnh mẽ cho mọi tác vụ và chơi game ổn định.

--- THÔNG SỐ KỸ THUẬT ---
• Màn hình: Super Retina XDR OLED 6.1 inch, độ sáng 1200 nits
• Vi xử lý (CPU): Apple A15 Bionic 6 nhân
• RAM & Bộ nhớ: 4GB RAM, 128GB ROM
• Camera sau: Cụm camera chéo 12MP kép, chống rung cảm biến Sensor-Shift
• Pin: Xem video 19 giờ, sạc nhanh 20W'),
(1, 1, N'iPhone 11 64GB | Chính hãng VN/A', 6800000, 8490000, 15, 1, 'https://cdn2.cellphones.com.vn/insecure/rs:fill:358:358/q:90/plain/https://cellphones.com.vn/media/catalog/product/i/p/iphone-11.png', N'iPhone 11 64GB - smartphone quốc dân giá rẻ dưới 10 triệu, màn hình 6.1 inch, camera góc siêu rộng, độ bền cao và pin khỏe cả ngày dài.

--- THÔNG SỐ KỸ THUẬT ---
• Màn hình: Liquid Retina IPS LCD 6.1 inch
• Vi xử lý (CPU): Apple A13 Bionic 6 nhân
• RAM & Bộ nhớ: 4GB RAM, 64GB ROM
• Camera sau: Kép 12MP góc rộng và góc siêu rộng, Night Mode
• Kháng nước: IP68'),
(2, 1, N'Samsung Galaxy S25 Ultra 5G 256GB', 27000000, 31990000, 15, 1, 'https://cdn2.cellphones.com.vn/insecure/rs:fill:358:358/q:90/plain/https://cellphones.com.vn/media/catalog/product/s/a/samsung-galaxy-s24-ultra_1__1.png', N'Flagship đỉnh cao Samsung Galaxy S25 Ultra trang bị chip Snapdragon 8 Elite, camera 200MP zoom 100x và bút S-Pen quyền năng.

--- THÔNG SỐ KỸ THUẬT ---
• Màn hình: Dynamic AMOLED 2X 6.9 inch, QHD+ (3120 x 1440), 120Hz thích ứng, 2600 nits
• Kính bảo vệ: Corning Gorilla Armor chống chói quang học 75%
• Vi xử lý (CPU): Snapdragon 8 Elite for Galaxy 3nm
• RAM & Bộ nhớ: 12GB RAM LPDDR5X, 256GB UFS 4.0
• Camera sau: 200MP OIS + 50MP Siêu rộng + 50MP Tiềm vọng 5x + 10MP Tele 3x (Zoom 100x)
• Bút cảm ứng: Bút S-Pen tích hợp trong thân máy độ trễ 2.8ms
• Pin & Sạc: 5000mAh, sạc nhanh 45W, sạc không dây 15W
• Trợ lý AI: Galaxy AI dịch trực tiếp cuộc gọi, khoanh vùng tìm kiếm'),
(2, 1, N'Samsung Galaxy Z Fold 6 5G 256GB', 36000000, 41990000, 8, 1, 'https://cdn2.cellphones.com.vn/insecure/rs:fill:0:358/q:90/plain/https://cellphones.com.vn/media/catalog/product/s/a/samsung-galaxy-z-fold-6_2.jpg', N'Smartphone màn hình gập cao cấp nhất, mỏng nhẹ vượt trội, bản lề Flex không nếp gấp, tính năng Galaxy AI thông minh.

--- THÔNG SỐ KỸ THUẬT ---
• Màn hình chính: Dynamic AMOLED 2X 7.6 inch, 120Hz, 2600 nits
• Màn hình phụ: Dynamic AMOLED 2X 6.3 inch, 120Hz
• Vi xử lý (CPU): Snapdragon 8 Gen 3 for Galaxy
• RAM & Bộ nhớ: 12GB RAM, 256GB ROM
• Chuẩn kháng nước: IP48
• Pin: 4400mAh, sạc nhanh 25W'),
(2, 1, N'Samsung Galaxy Z Flip 6 5G 256GB', 23000000, 26990000, 12, 1, 'https://cdn2.cellphones.com.vn/insecure/rs:fill:0:358/q:90/plain/https://cellphones.com.vn/media/catalog/product/f/r/frame_166_3.png', N'Thiết kế gập vỏ sò sành điệu, màn hình phụ FlexWindow đa năng, kháng nước IP48, camera 50MP sắc nét và tản nhiệt buồng hơi.

--- THÔNG SỐ KỸ THUẬT ---
• Màn hình chính: Dynamic AMOLED 2X 6.7 inch 120Hz FHD+
• Màn hình phụ: Super AMOLED 3.4 inch FlexWindow
• Vi xử lý: Snapdragon 8 Gen 3 for Galaxy
• RAM & Bộ nhớ: 12GB RAM, 256GB ROM
• Camera: 50MP chính OIS + 12MP góc rộng
• Pin: 4000mAh'),
(2, 1, N'Samsung Galaxy A55 5G 8GB/128GB', 7800000, 9490000, 25, 1, 'https://cdn2.cellphones.com.vn/insecure/rs:fill:358:358/q:90/plain/https://cellphones.com.vn/media/catalog/product/s/a/samsung-galaxy-a55_1.png', N'Thiết kế kim loại cao cấp, màn hình Super AMOLED 120Hz, bảo mật Knox Vault, chống nước bụi IP67.

--- THÔNG SỐ KỸ THUẬT ---
• Màn hình: Super AMOLED 6.6 inch, FHD+, 120Hz, 1000 nits
• Vi xử lý (CPU): Exynos 1480 8 nhân kèm đồ họa AMD Xclipse 530
• RAM & Bộ nhớ: 8GB RAM, 128GB ROM
• Camera: 50MP OIS + 12MP siêu rộng + 5MP macro
• Pin: 5000mAh, sạc 25W, kháng nước IP67'),
(2, 1, N'Samsung Galaxy A17 5G 8GB/128GB Cũ', 3200000, 4290000, 20, 1, 'https://cdn2.cellphones.com.vn/insecure/rs:fill:358:358/q:90/plain/https://cellphones.com.vn/media/catalog/product/s/a/samsung-galaxy-a55_1.png', N'Điện thoại 5G giá siêu tốt phân khúc phổ thông, màn hình lớn 90Hz, pin trâu 5000mAh, bảo hành 1 đổi 1 trong 30 ngày tại KittyPhone.

--- THÔNG SỐ KỸ THUẬT ---
• Màn hình: Super AMOLED 6.5 inch, 90Hz FHD+
• Vi xử lý (CPU): MediaTek Dimensity 6100+ 5G
• RAM & Bộ nhớ: 8GB RAM, 128GB ROM
• Camera: 50MP chính + 5MP góc rộng
• Pin: 5000mAh, sạc nhanh 25W'),
(3, 1, N'Xiaomi 14 Ultra 5G 16GB/512GB', 25500000, 29990000, 6, 1, 'https://cdn2.cellphones.com.vn/insecure/rs:fill:358:358/q:90/plain/https://cellphones.com.vn/media/catalog/product/x/i/xiaomi-14-ultra_1.png', N'Hệ thống 4 camera Leica đỉnh cao cảm biến 1 inch Sony LYT-900, ống kính Summilux, chip Snapdragon 8 Gen 3 mạnh mẽ hàng đầu.

--- THÔNG SỐ KỸ THUẬT ---
• Màn hình: AMOLED 6.73 inch 2K (3200 x 1440), 120Hz LTPO, 3000 nits
• Vi xử lý (CPU): Qualcomm Snapdragon 8 Gen 3
• RAM & Bộ nhớ: 16GB RAM LPDDR5X, 512GB UFS 4.0
• Camera Leica: 4 camera 50MP cảm biến 1 inch, khẩu độ biến thiên f/1.63 - f/4.0
• Pin & Sạc: 5000mAh, sạc 90W có dây, 80W không dây'),
(3, 1, N'Xiaomi 14T Pro 5G 12GB/512GB', 14200000, 16990000, 14, 1, 'https://cdn2.cellphones.com.vn/insecure/rs:fill:0:358/q:90/plain/https://cellphones.com.vn/media/catalog/product/x/i/xiaomi_14t_pro_1_.png', N'Hiệu năng đột phá chip Dimensity 9300+, camera Leica chuyên nghiệp, sạc siêu tốc 120W HyperCharge đầy pin trong 19 phút.

--- THÔNG SỐ KỸ THUẬT ---
• Màn hình: AMOLED 6.67 inch 1.5K, 144Hz siêu mượt, 4000 nits
• Vi xử lý (CPU): MediaTek Dimensity 9300+ 4nm
• Camera Leica: Light Fusion 900 50MP + Tele 50MP + Góc rộng 12MP
• Pin & Sạc: 5000mAh, sạc nhanh HyperCharge 120W'),
(3, 1, N'Xiaomi Redmi Note 13 Pro+ 5G', 7700000, 9290000, 18, 1, 'https://cdn2.cellphones.com.vn/insecure/rs:fill:0:358/q:90/plain/https://cellphones.com.vn/media/catalog/product/x/i/xiaomi-redmi-note-13-pro-4g_13__1.png', N'Màn hình cong AMOLED 1.5K 120Hz, camera 200MP chống rung OIS, chuẩn kháng nước IP68 cao cấp.

--- THÔNG SỐ KỸ THUẬT ---
• Màn hình: Cong AMOLED 6.67 inch 1.5K, 120Hz
• Vi xử lý: MediaTek Dimensity 7200-Ultra
• Camera: 200MP cảm biến Samsung HP3 OIS
• Pin: 5000mAh, sạc nhanh 120W, kháng nước IP68'),
(4, 1, N'OPPO Find X7 Ultra 5G 16GB/256GB', 21000000, 24990000, 5, 1, 'https://cdn2.cellphones.com.vn/insecure/rs:fill:0:358/q:90/plain/https://cellphones.com.vn/media/catalog/product/e/d/eda006276802c.jpg', N'Hệ thống camera kép tiềm vọng đầu tiên trên thế giới kết hợp Hasselblad, màn hình 2K LTPO 120Hz siêu nét.

--- THÔNG SỐ KỸ THUẬT ---
• Màn hình: LTPO AMOLED 6.82 inch 2K, 120Hz, 4500 nits
• Vi xử lý (CPU): Snapdragon 8 Gen 3
• Camera: 4 camera 50MP kép tiềm vọng zoom 3x và 6x
• Pin: 5000mAh, sạc 100W SUPERVOOC'),
(4, 1, N'OPPO Reno12 Pro 5G 12GB/512GB', 14800000, 17490000, 12, 1, 'https://cdn2.cellphones.com.vn/insecure/rs:fill:0:358/q:90/plain/https://cellphones.com.vn/media/catalog/product/d/i/dien-thoai-oppo-reno12-5g-_en-1_1_1.png', N'Thiết kế thời thượng mỏng nhẹ, tính năng chụp ảnh chân dung AI chuyên sâu, kết nối AI LinkBoost siêu mạnh.

--- THÔNG SỐ KỸ THUẬT ---
• Màn hình: Cong vô cực AMOLED 6.7 inch 120Hz
• Vi xử lý: Dimensity 7300-Energy
• Camera: 50MP Sony LYT-600 OIS + 50MP Tele chân dung
• Pin: 5000mAh, sạc nhanh 80W'),
(5, 1, N'Vivo X100 Pro 5G 16GB/512GB', 20000000, 23990000, 6, 1, 'https://cdn2.cellphones.com.vn/insecure/rs:fill:0:358/q:90/plain/https://cellphones.com.vn/media/catalog/product/v/i/vivo-x100.jpg', N'Cụm camera ống kính ZEISS APO danh tiếng, cảm biến 1 inch, chip xử lý hình ảnh Vivo V3 độc quyền.

--- THÔNG SỐ KỸ THUẬT ---
• Màn hình: LTPO AMOLED 6.78 inch, 1.5K, 120Hz
• Vi xử lý (CPU): MediaTek Dimensity 9300
• Camera ZEISS: 50MP 1 inch + 50MP Telephoto ZEISS APO
• Pin: 5400mAh, sạc nhanh 100W'),
(5, 1, N'Vivo V30 5G 12GB/512GB', 10200000, 12490000, 15, 1, 'https://cdn2.cellphones.com.vn/358x358,webp,q100/media/catalog/product/d/i/dien-thoai-vivo-v30-5g_1_.png', N'Vòng sáng Aura 2.0 chụp đêm thần sầu, thân máy siêu mỏng chỉ 7.45mm, màn hình 1.5K 120Hz.

--- THÔNG SỐ KỸ THUẬT ---
• Màn hình: AMOLED cong 6.78 inch, 1.5K, 120Hz
• Vi xử lý: Snapdragon 7 Gen 3
• Camera: Kép 50MP True Color + đèn vòng Aura Light 3.0
• Pin: 5000mAh, sạc nhanh 80W'),
(1, 2, N'iPad Pro M4 11 inch Wifi 256GB | Chính hãng Apple VN/A', 23800000, 27990000, 10, 1, 'https://cdn2.cellphones.com.vn/insecure/rs:fill:0:358/q:90/plain/https://cellphones.com.vn/media/catalog/product/f/r/frame_100_1_2__2_2.png', N'Đỉnh cao công nghệ máy tính bảng với chip Apple M4 siêu tốc độ, màn hình OLED 2 lớp Ultra Retina XDR, thiết kế siêu mỏng chỉ 5.3mm.

--- THÔNG SỐ KỸ THUẬT ---
• Màn hình: Ultra Retina XDR OLED 2 lớp Tandem OLED 11 inch, 2420 x 1668, 120Hz ProMotion
• Vi xử lý (CPU): Apple M4 9 nhân, GPU 10 nhân
• RAM & Bộ nhớ: 8GB RAM, 256GB ROM
• Độ mỏng: Chỉ 5.3mm - mỏng nhất lịch sử Apple
• Hỗ trợ: Apple Pencil Pro, Magic Keyboard thế hệ mới'),
(1, 2, N'iPad Air M2 11 inch Wifi 128GB | Chính hãng Apple VN/A', 13900000, 16490000, 12, 1, 'https://cdn2.cellphones.com.vn/insecure/rs:fill:0:358/q:90/plain/https://cellphones.com.vn/media/catalog/product/i/p/ipad-air-5.png', N'Hiệu năng chuẩn Pro nhờ vi xử lý Apple M2, hỗ trợ Apple Pencil Pro với tính năng xoay thân bút và phản hồi rung.

--- THÔNG SỐ KỸ THUẬT ---
• Màn hình: Liquid Retina IPS LCD 11 inch, 2360 x 1640
• Vi xử lý (CPU): Apple M2 8 nhân, GPU 9 nhân
• RAM & Bộ nhớ: 8GB RAM, 128GB ROM
• Hỗ trợ: Apple Pencil Pro, Magic Keyboard'),
(1, 2, N'iPad mini 7 Wifi 128GB | Chính hãng Apple', 11800000, 13990000, 14, 1, 'https://cdn2.cellphones.com.vn/insecure/rs:fill:358:358/q:90/plain/https://cellphones.com.vn/media/catalog/product/i/p/ipad-mini-6_2.png', N'Kích thước nhỏ gọn 8.3 inch cầm vừa tay, chip A17 Pro mạnh mẽ hỗ trợ Apple Intelligence, kết nối Wifi 6E tốc độ cao.

--- THÔNG SỐ KỸ THUẬT ---
• Màn hình: Liquid Retina 8.3 inch, True Tone, dải màu P3
• Vi xử lý (CPU): Apple A17 Pro
• Kết nối: Wi-Fi 6E, Bluetooth 5.3, USB-C 10Gbps
• Hỗ trợ: Apple Pencil Pro'),
(1, 2, N'iPad A16 11 inch 2025 Wifi 128GB | Chính hãng Apple', 9900000, 11890000, 15, 1, 'https://cdn2.cellphones.com.vn/insecure/rs:fill:358:358/q:90/plain/https://cellphones.com.vn/media/catalog/product/i/p/ipad-10-9-inch-2022_1.png', N'iPad thế hệ 2025 trang bị chip Apple A16 Bionic mạnh mẽ, màn hình Liquid Retina 11 inch sắc nét, hỗ trợ Apple Pencil và Magic Keyboard.

--- THÔNG SỐ KỸ THUẬT ---
• Màn hình: Liquid Retina 11 inch viền mỏng đều 4 cạnh
• Vi xử lý (CPU): Apple A16 Bionic 6 nhân
• RAM & Bộ nhớ: 6GB RAM, 128GB ROM
• Hỗ trợ: Apple Pencil, Magic Keyboard Folio'),
(2, 2, N'Samsung Galaxy Tab S10 Ultra 5G 256GB', 28500000, 33990000, 6, 1, 'https://cdn2.cellphones.com.vn/insecure/rs:fill:0:358/q:90/plain/https://cellphones.com.vn/media/catalog/product/s/s/ss-tab-s9-ultra_1_.png', N'Màn hình khổng lồ 14.6 inch Dynamic AMOLED 2X chống chói Anti-Reflection, kháng nước IP68, kèm sẵn bút S-Pen cao cấp.

--- THÔNG SỐ KỸ THUẬT ---
• Màn hình: Dynamic AMOLED 2X 14.6 inch, 120Hz, kính Anti-Reflection
• Vi xử lý: MediaTek Dimensity 9300+ 4nm
• Bút cảm ứng: S-Pen kèm sẵn trong hộp, kháng nước IP68
• Pin & Sạc: 11200mAh, sạc nhanh 45W'),
(2, 2, N'Samsung Galaxy Tab A11 WIFI 4GB/64GB Chính hãng', 2400000, 3090000, 25, 1, 'https://cdn2.cellphones.com.vn/insecure/rs:fill:358:358/q:90/plain/https://cellphones.com.vn/media/catalog/product/s/a/samsung-galaxy-tab-a9_1.png', N'Máy tính bảng giải trí và học tập online giá tốt nhất, màn hình 8.7 inch, loa kép Dolby Atmos sống động, pin 5100mAh bền bỉ.

--- THÔNG SỐ KỸ THUẬT ---
• Màn hình: LCD 8.7 inch sắc nét
• Vi xử lý: 8 nhân Helio G99
• Âm thanh: Hệ thống loa kép Dolby Atmos
• Pin: 5100mAh'),
(3, 2, N'Xiaomi Pad 6 6GB/128GB Chính Hãng', 5600000, 6990000, 18, 1, 'https://cdn2.cellphones.com.vn/insecure/rs:fill:0:358/q:90/plain/https://cellphones.com.vn/media/catalog/product/m/i/mi-pad-6-cps-doc-quyen-xanh.jpg', N'Màn hình 2.8K 144Hz siêu mượt mà, chip Snapdragon 870 hiệu năng mạnh mẽ, pin 8840mAh sạc nhanh 33W.

--- THÔNG SỐ KỸ THUẬT ---
• Màn hình: IPS LCD 11 inch 2.8K (2880 x 1800), 144Hz
• Vi xử lý (CPU): Snapdragon 870 5G
• Âm thanh: 4 loa vòm Dolby Atmos
• Pin & Sạc: 8840mAh, sạc 33W'),
(3, 2, N'Xiaomi Redmi Pad 2 Wifi 9.7 inch 4GB/64GB Chính Hãng', 2900000, 3690000, 20, 1, 'https://cdn2.cellphones.com.vn/insecure/rs:fill:0:358/q:90/plain/https://cellphones.com.vn/media/catalog/product/x/i/xiaomi-redmi-pad-se_1_3.png', N'Màn hình độ phân giải FHD+ tần số quét 90Hz, 4 loa âm thanh vòm Dolby Atmos, vỏ kim loại nguyên khối sang trọng.

--- THÔNG SỐ KỸ THUẬT ---
• Màn hình: IPS 9.7 inch FHD+, 90Hz
• Vi xử lý: Snapdragon 680 8 nhân
• Vỏ máy: Kim loại nguyên khối
• Pin: 8000mAh'),
(1, 4, N'Apple Watch Series 10 42mm GPS Viền Nhôm', 8800000, 10490000, 15, 1, 'https://cdn2.cellphones.com.vn/x/media/wysiwyg/Watch/Apple/Series-10/apple-watch-series-10-8.jpg', N'Màn hình OLED góc rộng sáng hơn 40%, thân máy mỏng nhất lịch sử Apple Watch, đo độ sâu dưới nước và nhiệt độ nước.

--- THÔNG SỐ KỸ THUẬT ---
• Màn hình: Retina LTPO3 OLED góc rộng Always-On Display
• Độ mỏng: Chỉ 9.7mm
• Cảm biến: ECG điện tâm đồ, SpO2 oxy máu, đo nhiệt độ cổ tay, phát hiện ngưng thở khi ngủ
• Kháng nước: 50m, đo độ sâu 6m và nhiệt độ nước
• Pin: 18 giờ, sạc 80% trong 30 phút'),
(1, 4, N'Apple Watch Ultra 2 Titanium 49mm Dây Alpine', 18200000, 21490000, 8, 1, 'https://cdn2.cellphones.com.vn/x/media/wysiwyg/Watch/Apple/Ultra/apple-watch-ultra-2-6_1.jpg', N'Đồng hồ thể thao chuyên nghiệp vỏ titan chuẩn quân đội, GPS băng tần kép chuẩn xác, thời lượng pin 72 giờ ở chế độ tiết kiệm.

--- THÔNG SỐ KỸ THUẬT ---
• Màn hình: Sapphire Crystal phẳng 49mm, 3000 nits
• Chất liệu: Titanium chuẩn hàng không vũ trụ
• GPS: Băng tần kép L1 và L5
• Kháng nước: 100m, chuẩn lặn EN13319 lặn sâu 40m
• Pin: 36 - 72 giờ'),
(1, 4, N'Apple Watch SE 3 44mm GPS - Viền nhôm, Dây Đeo Thể Thao', 5300000, 6590000, 20, 1, 'https://cdn2.cellphones.com.vn/insecure/rs:fill:0:358/q:90/plain/https://cellphones.com.vn/media/catalog/product/a/p/apple-watch-se-2023-40mm.png', N'Smartwatch bán chạy nhất của Apple với đầy đủ tính năng đo nhịp tim, phát hiện té ngã va chạm, theo dõi giấc ngủ, kháng nước 50m.

--- THÔNG SỐ KỸ THUẬT ---
• Màn hình: Retina OLED Always-On
• Vi xử lý: Apple S8 SiP 64-bit 2 nhân
• Tính năng an toàn: Phát hiện va chạm xe, té ngã, SOS
• Kháng nước: 50m'),
(2, 4, N'Samsung Galaxy Watch 7 40mm BT Chính hãng', 4800000, 5990000, 18, 1, 'https://cdn2.cellphones.com.vn/x/media/catalog/product/s/a/samsung-galaxy-watch-7-44mm_1_2_1.png', N'Cảm biến BioActive thế hệ mới theo dõi chỉ số sức khỏe và giấc ngủ chính xác, chip Exynos W1000 3nm siêu mượt mà.

--- THÔNG SỐ KỸ THUẬT ---
• Màn hình: Super AMOLED 1.3 inch Sapphire Crystal
• Vi xử lý: Exynos W1000 3nm
• Cảm biến: BioActive thế hệ mới theo dõi chỉ số AGEs, phân tích giấc ngủ
• Trợ lý AI: Phân tích thể chất và chỉ số Energy Score'),
(1, 3, N'Tai nghe Bluetooth Apple AirPods 4 Chống Ồn Chủ Động (ANC)', 3900000, 4790000, 25, 1, 'https://cdn2.cellphones.com.vn/insecure/rs:fill:0:358/q:90/plain/https://cellphones.com.vn/media/catalog/product/a/p/apple-airpods-4-chong-on-chu-dong-thumb.png', N'Thế hệ tai nghe AirPods 4 mới nhất tích hợp chip H2, khử tiếng ồn chủ động ANC, âm thanh không gian cá nhân hóa, sạc không dây và cổng USB-C.

--- THÔNG SỐ KỸ THUẬT ---
• Chip âm thanh: Apple H2
• Công nghệ: Chống ồn chủ động ANC, Spatial Audio
• Kháng nước/bụi: IP54 cho cả tai nghe và hộp sạc
• Cổng sạc: USB-C và sạc không dây Qi
• Thời lượng pin: Lên đến 30 giờ kèm hộp sạc'),
(1, 3, N'Tai nghe Bluetooth Apple AirPods Pro 2 MagSafe USB-C', 4500000, 5490000, 30, 1, 'https://cdn2.cellphones.com.vn/insecure/rs:fill:0:358/q:90/plain/https://cellphones.com.vn/media/catalog/product/a/p/apple-airpods-pro-2-usb-c_1_.png', N'Chống ồn chủ động vượt trội gấp 2 lần, tính năng nhận biết cuộc trò chuyện thông minh, hỗ trợ kiểm tra thính lực và hộp sạc có loa định vị.

--- THÔNG SỐ KỸ THUẬT ---
• Chip âm thanh: Apple H2 + Chip định vị U1
• Công nghệ: Chống ồn ANC vượt trội gấp 2x, Adaptive Audio
• Kháng nước/bụi: IP54
• Cổng sạc: USB-C, MagSafe, sạc qua đế sạc Apple Watch
• Pin: 6 giờ nghe liên tục (30 giờ kèm hộp sạc)'),
(1, 3, N'Tai nghe chụp tai Apple AirPods Max USB-C', 11800000, 13990000, 8, 1, 'https://cdn2.cellphones.com.vn/x/media/catalog/product/a/p/apple-airpods-max-1_1.png', N'Tai nghe Over-ear cao cấp với âm thanh trung thực tuyệt đỉnh, đệm tai thoáng khí, khung thép không gỉ sang trọng.

--- THÔNG SỐ KỸ THUẬT ---
• Màng loa: Dynamic 40mm do Apple tự thiết kế
• Chống ồn: ANC Pro với 8 microphone
• Cổng kết nối: USB-C hỗ trợ âm thanh Lossless
• Pin: 20 giờ nghe nhạc liên tục khi bật ANC'),
(6, 3, N'Tai nghe Bluetooth Baseus Bowie M2+ ANC Chống Ồn 42dB', 580000, 850000, 40, 1, '/images/products/baseus-bowie-m2.svg', N'Khử ồn chủ động Hybrid ANC sâu đến -42dB, độ trễ siêu thấp 0.038s chơi game, Bluetooth 5.2 kết nối ổn định 2 thiết bị cùng lúc.

--- THÔNG SỐ KỸ THUẬT ---
• Chống ồn: Hybrid ANC đến -42dB
• Độ trễ: 0.038s chế độ Game Mode
• Kết nối: Bluetooth 5.2 kết nối 2 thiết bị
• Pin: 30 giờ kèm hộp sạc'),
(6, 3, N'Tai nghe không dây Baseus Bowie WM02 Siêu Nhỏ Gọn', 220000, 350000, 50, 1, 'https://cdn2.cellphones.com.vn/x/media/catalog/product/t/a/tai-nghe-bluetooth-baseus-bowie-wm02-01.png', N'Thiết kế dạng viên thuốc con nhộng trong suốt cực xinh, pin trâu đến 25 giờ, âm bass chắc khỏe, Bluetooth 5.3.

--- THÔNG SỐ KỸ THUẬT ---
• Thiết kế: Dạng viên nhộng trong suốt mini
• Kết nối: Bluetooth 5.3 ổn định
• Pin: 25 giờ sử dụng liên tục kèm case'),
(7, 3, N'Tai nghe không dây Bluetooth Havit TW967', 180000, 290000, 60, 1, 'https://cdn2.cellphones.com.vn/insecure/rs:fill:0:358/q:90/plain/https://cellphones.com.vn/media/catalog/product/f/r/frame_4_2.png', N'Tai nghe True Wireless quốc dân giá siêu rẻ, kết nối Bluetooth 5.3 mượt mà, mic đàm thoại rõ ràng, cổng sạc Type-C.

--- THÔNG SỐ KỸ THUẬT ---
• Kết nối: Bluetooth 5.3
• Màng loa: Dynamic 10mm bass mạnh mẽ
• Cổng sạc: Type-C sạc nhanh'),
(1, 3, N'Bút cảm ứng Apple Pencil Pro Chính Hãng VN/A', 2800000, 3490000, 20, 1, 'https://cdn2.cellphones.com.vn/insecure/rs:fill:0:358/q:90/plain/https://cellphones.com.vn/media/catalog/product/a/p/apple-pencil-pro-36.png', N'Thế hệ Apple Pencil Pro với cử chỉ bóp Barrel Roll, phản hồi xúc giác Haptic, tính năng lướt Apple Pencil Hover và tìm kiếm Find My.

--- THÔNG SỐ KỸ THUẬT ---
• Cử chỉ: Bóp thân bút (Squeeze), Xoay thân bút (Barrel Roll)
• Phản hồi: Haptic feedback rung phản hồi
• Tính năng: Apple Pencil Hover, hỗ trợ tìm qua Find My
• Tương thích: iPad Pro M4, iPad Air M2'),
(1, 3, N'Bàn phím Apple Magic Keyboard cho iPad Pro 11 inch', 6600000, 7990000, 10, 1, '/images/products/apple-magic-keyboard.svg', N'Thiết kế lơ lửng cantilever thanh lịch, dãy phím chức năng 14 phím, bàn di chuột xúc giác bằng kính cỡ lớn phủ nhôm cao cấp.

--- THÔNG SỐ KỸ THUẬT ---
• Thiết kế: Lơ lửng cantilever góc nghiêng tùy chỉnh
• Bàn phím: 14 phím chức năng, phím cắt kéo hành trình 1mm
• Trackpad: Bàn di chuột kính lớn phủ nhôm
• Cổng: USB-C sạc chuyển tiếp tiện lợi'),
(1, 3, N'Củ sạc nhanh Apple 20W USB-C Power Adapter Chính Hãng', 380000, 520000, 50, 1, '/images/products/apple-20w-adapter.svg', N'Củ sạc chuẩn Apple Power Delivery 20W sạc 50% pin cho iPhone trong 30 phút, bảo vệ an toàn chống quá nhiệt đoản mạch.

--- THÔNG SỐ KỸ THUẬT ---
• Công suất: 20W Power Delivery (PD 3.0)
• Cổng ra: USB-C
• Tương thích: Toàn bộ iPhone, iPad, Apple Watch, AirPods'),
(1, 3, N'Cáp sạc Apple Type-C to Type-C 60W Dây Dù 1m', 380000, 520000, 50, 1, '/images/products/apple-typec-cable.svg', N'Cáp bện dù siêu bền chính hãng Apple, sạc nhanh công suất đến 60W và đồng bộ dữ liệu tốc độ cao.

--- THÔNG SỐ KỸ THUẬT ---
• Chiều dài: 1 mét
• Chất liệu: Dây bện dù chống gãy rối
• Công suất sạc: Hỗ trợ lên đến 60W'),
(1, 3, N'Ốp lưng iPhone 16 Pro Max MagSafe Trong Suốt Chính Hãng', 1100000, 1420000, 30, 1, '/images/products/iphone-magsafe-case.svg', N'Chất liệu polycarbonate trong suốt chống ố vàng, nam châm tích hợp hít sạc MagSafe chuẩn xác, bề mặt chống trầy xước.

--- THÔNG SỐ KỸ THUẬT ---
• Chất liệu: Polycarbonate quang học chống ố vàng
• Tính năng: Vòng nam châm MagSafe hít sạc chuẩn xác
• Tương thích: iPhone 16 Pro Max');

GO

-- Phiếu nhập kho ban đầu
INSERT INTO Import_Receipt (supplier_id, employee_id, import_date) VALUES
(1, 3, '2026-05-01'),
(2, 3, '2026-05-03');

INSERT INTO Import_Detail (import_id, product_id, quantity, import_price) VALUES
(1, 13, 10, 26500000),
(1, 14,  5, 22500000),
(1,  1,  8, 24500000),
(2, 21,  8, 27000000),
(2, 22,  5, 36000000);

-- Hóa đơn mẫu & thanh toán (Khởi tạo Pending -> Tự động chuyển Paid qua Trigger sau khi nạp Payment)
INSERT INTO Invoice (customer_id, employee_id, invoice_date, shipping_address, payment_method_selected, status) VALUES
(1, 2, '2026-05-10', N'123 Nguyễn Văn Cừ, Cần Thơ', 'Cash', 'Pending'),
(2, 2, '2026-05-11', N'456 Lê Duẩn, Đà Nẵng', 'Bank Transfer', 'Pending'),
(3, 2, '2026-05-12', N'789 Nguyễn Huệ, Quận 1, TP. HCM', 'Bank Transfer', 'Pending'),
(4, 2, '2026-05-13', N'101 Cầu Giấy, Hà Nội', 'Cash', 'Pending');

INSERT INTO Invoice_Detail (invoice_id, product_id, quantity, unit_price) VALUES
(1, 13, 1, 31490000),
(2, 21, 1, 31990000),
(3,  1, 1, 28990000),
(4, 22, 1, 41990000);

INSERT INTO Payment (invoice_id, payment_method, payment_date, amount) VALUES
(1, 'Cash', '2026-05-10', 31490000),
(2, 'Bank Transfer', '2026-05-11', 31990000),
(3, 'Bank Transfer', '2026-05-12', 28990000),
(4, 'Cash', '2026-05-13', 41990000);

-- Quản lý Serial / IMEI
INSERT INTO Product_Serial (product_id, imei_number, serial_number, status, warehouse_id) VALUES
(13, '356789123456001', 'APL-IP16PM-001', 'Sold',     1),
(21, '356789123456002', 'SMS-S25U-001',   'Sold',     1),
( 1, '356789123456004', 'APL-IP15PM-001', 'Sold',     1),
(22, '356789123456005', 'SMS-ZF6-001',    'Sold',     1),
(14, '356789123456003', 'APL-IP16P-001',  'In Stock', 2);

-- Đăng ký bảo hành thiết bị chính hãng
INSERT INTO Warranty (invoice_detail_id, serial_id, warranty_start, warranty_end, warranty_note) VALUES
(1, 1, '2026-05-10', '2027-05-10', N'Bảo hành chính hãng 12 tháng Apple VN/A'),
(2, 2, '2026-05-11', '2027-05-11', N'Bảo hành chính hãng 12 tháng Samsung VN'),
(3, 3, '2026-05-12', '2027-05-12', N'Bảo hành chính hãng 12 tháng Apple VN/A'),
(4, 4, '2026-05-13', '2027-05-13', N'Bảo hành chính hãng 12 tháng Samsung Care+');

-- Đánh giá sản phẩm mẫu (Khớp 100% với khách hàng & sản phẩm đã mua có hóa đơn thanh toán thành công - Verified Purchase)
INSERT INTO Review (product_id, customer_name, phone, rating, comment, reply, customer_id, invoice_id, is_verified_purchase, created_at) VALUES
(13, N'Nguyễn Văn An', '0911111111', 5, N'Màu Titan Sa Mạc quá xuất sắc, viền mỏng nhẹ hơn hẳn đời trước. Chụp hình zoom 5x nét căng.', N'KittyPhone cảm ơn anh An đã tin tưởng ủng hộ cửa hàng ạ!', 1, 1, 1, DATEADD(DAY, -5, GETDATE())),
(1,  N'Lê Hoàng Nam',  '0933333333', 5, N'Máy đẹp nguyên seal chính hãng VN/A, cầm rất đầm tay, sạc Type-C quá tiện. Giao hàng siêu nhanh chỉ trong 1 tiếng!', N'KittyPhone cảm ơn bạn Nam rất nhiều ạ! Chúc bạn có trải nghiệm tuyệt vời cùng máy mới nhé!', 3, 3, 1, DATEADD(DAY, -3, GETDATE())),
(21, N'Trần Thị Bích', '0922222222', 5, N'Màn hình phẳng dùng sướng hơn nhiều, AI dịch thuật hội nghị rất chuẩn. Pin trâu dùng cả ngày không hết.', N'KittyPhone cảm ơn chị Bích đã dành thời gian đánh giá sản phẩm ạ!', 2, 2, 1, DATEADD(DAY, -2, GETDATE())),
(22, N'Phạm Gia Bảo',  '0944444444', 5, N'Thiết kế gập sang trọng đỉnh cao, đa nhiệm 2 màn hình phục vụ công việc cực kỳ tiện lợi.', NULL, 4, 4, 1, DATEADD(HOUR, -6, GETDATE()));
GO

-- Nhân viên kỹ thuật & Tài khoản đăng nhập
INSERT INTO Employee (employee_name, phone, position) VALUES
(N'Nguyễn Văn Kỹ Thuật', '0987654321', N'Technician');

DECLARE @techEmpId INT = SCOPE_IDENTITY();
INSERT INTO Account (employee_id, username, password_hash, role, is_active) VALUES
(@techEmpId, 'kythuat', '$2a$11$N4k9RzBfH9F5tWk6RkQpEOb9xO2yZl9rP6p9Jz2U2Kx5R7Kz8Z1Xe', 'Technician', 1);

-- Phiếu bảo hành mẫu
INSERT INTO Warranty_Ticket (
    ticket_code, warranty_id, serial_id, customer_id, customer_name, customer_phone,
    technician_id, receive_date, expected_return_date, customer_issue,
    device_condition, diagnosis, repair_action, repair_cost, status, notes
)
VALUES 
(
    'BH-20261022-0001', 1, 1, 1, N'Nguyễn Văn An', '0901234567',
    @techEmpId, DATEADD(DAY, -4, GETDATE()), DATEADD(DAY, -1, GETDATE()),
    N'Màn hình chập chờn khi tăng độ sáng, có hiện tượng sọc xanh dọc mép phải',
    N'Thân máy còn mới 98%, viền không cấn móp, màn hình kính nguyên bản không nứt',
    N'Hỏng cáp màn hình OLED Super Retina XDR do lỗi phần cứng nhà sản xuất',
    N'Thay thế cụm màn hình OLED chính hãng Apple theo diện bảo hành tiêu chuẩn',
    0, 'Completed', N'Đã test chuẩn 24h, hiển thị sắc nét, cảm ứng mượt mà. Sẵn sàng giao khách'
),
(
    'BH-20261024-0002', 2, 2, 2, N'Trần Thị Bình', '0912345678',
    @techEmpId, DATEADD(DAY, -2, GETDATE()), DATEADD(DAY, 1, GETDATE()),
    N'Pin tụt nhanh bất thường, máy nóng khi sạc và tự tắt nguồn ở mức 15%',
    N'Máy trầy xước nhẹ nắp lưng, mặt kính đẹp, khay sim nguyên vẹn',
    N'Chai pin 68%, cell pin phồng nhẹ gây nghẽn nguồn điện',
    N'Thay cell pin chuẩn dung lượng cao chính hãng, dán lại keo chống nước chuẩn IP68',
    350000, 'Processing', N'Đang chạy chu trình sạc xả thử nghiệm hiệu năng pin'
),
(
    'BH-20261026-0003', NULL, NULL, NULL, N'Lê Hoàng Long', '0988776655',
    @techEmpId, GETDATE(), DATEADD(DAY, 3, GETDATE()),
    N'Rơi nước, mất loa thoại và mic đàm thoại, camera sau bị mờ do đọng hơi nước',
    N'Khung viền cấn nhẹ góc dưới, chân sạc có bụi ẩm',
    N'Nước vào buồng âm thanh loa thoại và ống kính camera, cần vệ sinh sấy khô và thay màng loa',
    N'Vệ sinh sấy khô bo mạch bằng cồn chuyên dụng, thay màng loa thoại chống nước',
    450000, 'Received', N'Khách đồng ý phí dịch vụ hỗ trợ ngoài hạn bảo hành'
);
GO

/* =====================================================================
   PHẦN 7. TEST & KIỂM TRA TỰ ĐỘNG
   ===================================================================== */
SELECT product_id, product_name, quantity, selling_price, stock_status FROM vw_ProductCatalog;
SELECT * FROM vw_InvoiceFull;
SELECT * FROM vw_CustomerSummary;
SELECT * FROM vw_AccountSecurity;
GO
