using System.ComponentModel.DataAnnotations;

namespace BTL_PTTKYC.Models
{
    public class WarrantyTicketModel
    {
        public int TicketId { get; set; }
        public string TicketCode { get; set; } = string.Empty;
        public int? WarrantyId { get; set; }
        public int? SerialId { get; set; }
        public int? CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerPhone { get; set; } = string.Empty;
        public int? TechnicianId { get; set; }
        public string? TechnicianName { get; set; }
        public string? TechnicianPhone { get; set; }

        public DateTime ReceiveDate { get; set; }
        public DateTime? ExpectedReturnDate { get; set; }
        public DateTime? CompletedDate { get; set; }

        public string CustomerIssue { get; set; } = string.Empty;
        public string? DeviceCondition { get; set; }
        public string? Diagnosis { get; set; }
        public string? RepairAction { get; set; }
        public decimal RepairCost { get; set; }
        public string Status { get; set; } = "Received"; // Received, Diagnosing, Processing, WaitingParts, Completed, Delivered, Cancelled
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; }

        // Associated device info
        public string? ImeiNumber { get; set; }
        public string? SerialNumber { get; set; }
        public int? ProductId { get; set; }
        public string? ProductName { get; set; }
        public string? BrandName { get; set; }

        // Original warranty info
        public DateTime? WarrantyStart { get; set; }
        public DateTime? WarrantyEnd { get; set; }
        public string? OriginalWarrantyStatus { get; set; }
        public string WarrantyCoverageText { get; set; } = "Chưa xác định";
        public int WarrantyDaysRemaining { get; set; }

        public string? CustomerEmail { get; set; }
        public List<WarrantyTicketComponentModel> Components { get; set; } = new();

        // Home Warranty Service & Shipping
        public bool IsHomeService { get; set; }
        public string? PickupAddress { get; set; }
        public DateTime? PickupTime { get; set; }
        public string? ServiceType { get; set; } // "Kỹ thuật viên tới tận nhà", "Ship hỏa tốc lấy máy tận nơi"
        public string? ShippingCarrier { get; set; } // "Xanh SM Express", "GrabExpress", "GHN", "Viettel Post"
        public decimal ShippingFee { get; set; }
        public decimal? ShippingDistanceKm { get; set; }
        public List<WarrantyNotificationModel> Notifications { get; set; } = new();

        // Payment tracking
        public string PaymentStatus { get; set; } = "Unpaid"; // Free, Unpaid, Paid
        public string? PaymentMethod { get; set; } // ZaloPay QR, Cash, POS
        public decimal PaidAmount { get; set; }
        public DateTime? PaidAt { get; set; }
        public decimal DiscountAmount { get; set; }
        public string? VoucherCode { get; set; }

        public bool IsPaid => PaymentStatus == "Paid" || (RepairCost == 0 && (Status == "Completed" || Status == "Delivered"));
        
        public string PaymentStatusVietnamese => RepairCost == 0 
            ? "Miễn phí BH" 
            : (PaymentStatus == "Paid" ? "Đã thanh toán" : "Chưa thanh toán");

        public string PaymentStatusBadgeClass => RepairCost == 0
            ? "badge bg-success bg-opacity-10 text-success border border-success"
            : (PaymentStatus == "Paid" ? "badge bg-success text-white" : "badge bg-danger text-white");

        public string StatusBadgeClass => Status switch
        {
            "Received" => "bg-primary text-white",
            "Diagnosing" => "bg-info text-dark",
            "Processing" => "bg-warning text-dark",
            "WaitingParts" => "bg-secondary text-white",
            "Completed" => "bg-success text-white",
            "Delivered" => "bg-dark text-white",
            "Cancelled" => "bg-danger text-white",
            _ => "bg-light text-dark"
        };

        public string StatusVietnamese => Status switch
        {
            "Received" => "Tiếp nhận mới",
            "Diagnosing" => "Đang kiểm tra",
            "Processing" => "Đang sửa chữa",
            "WaitingParts" => "Chờ linh kiện",
            "Completed" => "Đã sửa xong",
            "Delivered" => "Đã trả máy",
            "Cancelled" => "Hủy / Từ chối",
            _ => Status
        };
    }

    public class WarrantyPaymentViewModel
    {
        public int TicketId { get; set; }
        public string TicketCode { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerPhone { get; set; } = string.Empty;
        public decimal RepairCost { get; set; }
        public decimal DiscountAmount { get; set; }
        public string? VoucherCode { get; set; }
        public decimal FinalAmount => Math.Max(0, RepairCost - DiscountAmount);
        public string PaymentMethod { get; set; } = "ZaloPay QR";
        public string BankName { get; set; } = "ZaloPay";
        public string AccountNumber { get; set; } = "0396166509";
        public string AccountHolder { get; set; } = "DUONG NGOC ANH";
        public string TransferSyntax => $"BH {TicketCode}";
        public string QrCodeUrl => $"https://img.vietqr.io/image/970454-0396166509-compact2.png?amount={(long)FinalAmount}&addInfo={Uri.EscapeDataString(TransferSyntax)}&accountName=DUONG%20NGOC%20ANH";
    }

    public class WarrantyTicketComponentModel
    {
        public int ComponentId { get; set; }
        public int TicketId { get; set; }
        public string ComponentName { get; set; } = string.Empty;
        public int Quantity { get; set; } = 1;
        public decimal Price { get; set; } = 0;
        public string? Note { get; set; }
        public decimal LineTotal => Quantity * Price;
    }

    public class WarrantyTicketCreateViewModel
    {
        [Display(Name = "Mã Serial / IMEI Thiết Bị")]
        public string? SerialNumberOrImei { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập họ tên khách hàng")]
        [Display(Name = "Họ tên khách hàng")]
        public string CustomerName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập số điện thoại khách hàng")]
        [Phone(ErrorMessage = "Số điện thoại không hợp lệ")]
        [Display(Name = "Số điện thoại liên hệ")]
        public string CustomerPhone { get; set; } = string.Empty;

        [Display(Name = "Kỹ thuật viên phụ trách")]
        public int? TechnicianId { get; set; }

        [Required(ErrorMessage = "Vui lòng mô tả tình trạng lỗi khách gặp phải")]
        [Display(Name = "Tình trạng lỗi do khách báo")]
        public string CustomerIssue { get; set; } = string.Empty;

        [Display(Name = "Ngoại quan & Tình trạng khi nhận máy")]
        public string? DeviceCondition { get; set; }

        [Display(Name = "Số ngày dự kiến hoàn thành")]
        [Range(1, 60, ErrorMessage = "Số ngày dự kiến từ 1 đến 60 ngày")]
        public int ExpectedDays { get; set; } = 3;

        [Display(Name = "Ghi chú thêm")]
        public string? Notes { get; set; }
    }

    public class WarrantyTicketUpdateViewModel
    {
        public int TicketId { get; set; }
        public string TicketCode { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerPhone { get; set; } = string.Empty;
        public string? ProductName { get; set; }
        public string? ImeiNumber { get; set; }

        [Display(Name = "Kỹ thuật viên phụ trách")]
        public int? TechnicianId { get; set; }

        [Display(Name = "Kết quả kiểm tra & Chẩn đoán")]
        public string? Diagnosis { get; set; }

        [Display(Name = "Phương án xử lý / Sửa chữa")]
        public string? RepairAction { get; set; }

        [Display(Name = "Chi phí sửa chữa (VNĐ - 0 nếu bảo hành miễn phí)")]
        [Range(0, 100000000, ErrorMessage = "Chi phí không hợp lệ")]
        public decimal RepairCost { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn trạng thái phiếu")]
        [Display(Name = "Trạng thái xử lý")]
        public string Status { get; set; } = "Processing";

        [Display(Name = "Ghi chú kỹ thuật")]
        public string? Notes { get; set; }

        // Component list input for dynamic form
        public List<WarrantyTicketComponentModel> Components { get; set; } = new();
    }

    public class WarrantyLookupResultViewModel
    {
        public string SearchQuery { get; set; } = string.Empty;
        public bool Found { get; set; }

        // Product info
        public int? ProductId { get; set; }
        public string? ProductName { get; set; }
        public string? BrandName { get; set; }
        public string? ImeiNumber { get; set; }
        public string? SerialNumber { get; set; }
        public string? SerialStatus { get; set; }

        // Warranty Card
        public int? WarrantyId { get; set; }
        public DateTime? WarrantyStart { get; set; }
        public DateTime? WarrantyEnd { get; set; }
        public string? WarrantyStatus { get; set; }
        public string? WarrantyNote { get; set; }
        public bool IsEligible { get; set; }
        public int DaysLeft { get; set; }

        // Invoice & Customer
        public int? InvoiceId { get; set; }
        public DateTime? InvoiceDate { get; set; }
        public int? CustomerId { get; set; }
        public string? CustomerName { get; set; }
        public string? CustomerPhone { get; set; }

        // Ticket history
        public List<WarrantyTicketModel> TicketHistory { get; set; } = new();
    }

    public class TechnicianDashboardViewModel
    {
        public int TotalTickets { get; set; }
        public int ReceivedCount { get; set; }
        public int DiagnosingCount { get; set; }
        public int ProcessingCount { get; set; }
        public int WaitingPartsCount { get; set; }
        public int CompletedCount { get; set; }
        public int DeliveredCount { get; set; }
        public int CancelledCount { get; set; }

        public decimal TotalRepairRevenue { get; set; }
        public int TotalWarrantiesActive { get; set; }

        public List<WarrantyTicketModel> PendingTickets { get; set; } = new();
        public List<WarrantyTicketModel> RecentCompletedTickets { get; set; } = new();
    }

    public class WarrantyCardModel
    {
        public int WarrantyId { get; set; }
        public int InvoiceDetailId { get; set; }
        public int? SerialId { get; set; }
        public string? ImeiNumber { get; set; }
        public string? SerialNumber { get; set; }
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string? ImageUrl { get; set; }
        public string BrandName { get; set; } = string.Empty;
        public int InvoiceId { get; set; }
        public DateTime PurchaseDate { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerPhone { get; set; } = string.Empty;
        public DateTime WarrantyStart { get; set; }
        public DateTime WarrantyEnd { get; set; }
        public string WarrantyStatus { get; set; } = "Active";
        public string? WarrantyNote { get; set; }

        public bool IsExpired => WarrantyEnd < DateTime.Today;
        public int DaysLeft => IsExpired ? 0 : (WarrantyEnd - DateTime.Today).Days;
        public string StatusBadgeText => IsExpired ? "Hết hạn bảo hành" : $"Còn hạn ({DaysLeft} ngày)";
        public string StatusBadgeClass => IsExpired ? "badge bg-secondary" : "badge bg-success";
    }

    public class TechnicianWorkbenchViewModel
    {
        public TechnicianDashboardViewModel Stats { get; set; } = new();
        public List<WarrantyTicketModel> AllActiveTickets { get; set; } = new();
        public List<WarrantyTicketModel> ReceivedTickets { get; set; } = new();
        public List<WarrantyTicketModel> ProcessingTickets { get; set; } = new();
        public List<WarrantyTicketModel> CompletedTickets { get; set; } = new();
        public List<WarrantyTicketModel> UnpaidFeeTickets { get; set; } = new();
        public List<WarrantyTicketModel> DeliveredTickets { get; set; } = new();
        public List<EmployeeOptionModel> Technicians { get; set; } = new();
        public int? CurrentTechnicianId { get; set; }
        public string? CurrentTechnicianName { get; set; }
    }

    public class WarrantyNotificationModel
    {
        public int NotificationId { get; set; }
        public int TicketId { get; set; }
        public string TicketCode { get; set; } = string.Empty;
        public int? CustomerId { get; set; }
        public string Channel { get; set; } = "Email"; // Email, SMS
        public string Recipient { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string MessageBody { get; set; } = string.Empty;
        public string Status { get; set; } = "Sent";
        public DateTime SentAt { get; set; } = DateTime.Now;

        public string ChannelBadgeClass => Channel == "Email" ? "bg-primary text-white" : "bg-success text-white";
        public string ChannelIcon => Channel == "Email" ? "bi-envelope-at-fill" : "bi-chat-dots-fill";
    }

    public class HomeWarrantyRegisterViewModel
    {
        public int? CustomerId { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập họ tên của bạn")]
        [Display(Name = "Họ và tên người yêu cầu")]
        public string CustomerName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập số điện thoại liên hệ")]
        [Phone(ErrorMessage = "Số điện thoại không hợp lệ")]
        [Display(Name = "Số điện thoại liên hệ")]
        public string CustomerPhone { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập địa chỉ Email để nhận tiến độ")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ")]
        [Display(Name = "Email nhận thông báo tiến độ")]
        public string CustomerEmail { get; set; } = string.Empty;

        [Display(Name = "Sản phẩm đã mua (nếu có)")]
        public int? SelectedProductId { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập tên thiết bị cần bảo hành")]
        [Display(Name = "Tên thiết bị / Dòng máy")]
        public string DeviceName { get; set; } = string.Empty;

        [Display(Name = "Mã Serial / IMEI Thiết Bị")]
        public string? SerialNumberOrImei { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập địa chỉ lấy máy tận nơi")]
        [Display(Name = "Địa chỉ lấy máy tận nhà")]
        public string PickupAddress { get; set; } = string.Empty;

        [Display(Name = "Quận / Huyện")]
        public string PickupDistrict { get; set; } = "Đống Đa";

        [Display(Name = "Tỉnh / Thành phố")]
        public string PickupProvince { get; set; } = "Hà Nội";

        [Required(ErrorMessage = "Vui lòng chọn ngày hẹn lấy máy")]
        [Display(Name = "Ngày hẹn lấy máy")]
        [DataType(DataType.Date)]
        public DateTime PickupDate { get; set; } = DateTime.Today.AddDays(1);

        [Required(ErrorMessage = "Vui lòng chọn khung giờ hẹn lấy máy")]
        [Display(Name = "Khung giờ hẹn")]
        public string PickupTimeSlot { get; set; } = "08:30 - 11:30"; // "08:30 - 11:30", "14:00 - 17:30", "18:00 - 20:30"

        [Required(ErrorMessage = "Vui lòng mô tả lỗi hoặc hiện tượng máy gặp phải")]
        [Display(Name = "Mô tả chi tiết lỗi gặp phải")]
        public string CustomerIssue { get; set; } = string.Empty;

        [Display(Name = "Tình trạng ngoại quan thiết bị")]
        public string? DeviceCondition { get; set; } = "Máy nguyên vẹn, không nứt vỡ ngoại quan";

        [Display(Name = "Hình thức dịch vụ")]
        public string ServiceType { get; set; } = "Kỹ thuật viên KittyPhone qua tận nơi"; // "Kỹ thuật viên KittyPhone qua tận nơi" hoặc "Book Shipper lấy máy về trung tâm"

        [Display(Name = "Đơn vị vận chuyển lấy máy")]
        public string ShippingCarrier { get; set; } = "Xanh SM Express"; // "Xanh SM Express", "GrabExpress", "GHN", "Viettel Post"

        [Display(Name = "Khoảng cách ước tính (km)")]
        public decimal EstimatedDistanceKm { get; set; } = 5.0m;

        [Display(Name = "Cước phí ship ước tính")]
        public decimal EstimatedShippingFee { get; set; } = 0; // Được hỗ trợ hoặc tính theo bảng cước

        [Display(Name = "Ghi chú thêm")]
        public string? Notes { get; set; }

        // Danh sách thiết bị đã mua để khách hàng chọn nhanh
        public List<CustomerPurchasedProductItem> PurchasedDevices { get; set; } = new();
    }
}
