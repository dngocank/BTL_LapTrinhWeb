using System.ComponentModel.DataAnnotations;

namespace BTL_PTTKYC.Models
{
    public class CheckoutViewModel
    {
        public int? CustomerId { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập họ và tên người nhận")]
        [StringLength(100)]
        public string CustomerName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập số điện thoại")]
        [RegularExpression(@"^0[0-9]{9}$", ErrorMessage = "Số điện thoại phải gồm 10 chữ số (bắt đầu bằng 0)")]
        public string Phone { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập địa chỉ nhận hàng")]
        [StringLength(255)]
        public string Address { get; set; } = string.Empty;

        public string? OrderNotes { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn hình thức thanh toán")]
        public string PaymentMethod { get; set; } = "COD"; // "COD" (cọc 50%), "BankTransfer" (100%), "Installment" (Trả góp)

        // Installment configuration (Trả góp 0%)
        public int InstallmentPrepayPercent { get; set; } = 30; // 30, 50, 70
        public int InstallmentMonths { get; set; } = 6; // 3, 6, 9, 12
        public decimal InstallmentPrepayAmount => Math.Round(TotalAmount * InstallmentPrepayPercent / 100m / 10000m) * 10000m;
        public decimal InstallmentRemaining => TotalAmount - InstallmentPrepayAmount;
        public decimal InstallmentMonthlyAmount => InstallmentMonths > 0 ? Math.Round(InstallmentRemaining / InstallmentMonths / 1000m) * 1000m : 0;

        public int? SingleProductId { get; set; }
        public int SingleQuantity { get; set; } = 1;

        public List<CartItemModel> CartItems { get; set; } = new();
        public decimal SubTotal { get; set; }
        public int? PromotionId { get; set; }
        public decimal DiscountPercent { get; set; } = 0;
        public decimal DiscountAmount { get; set; } = 0;
        public decimal TotalAmount { get; set; }
        public decimal MinimumDeposit => Math.Round(TotalAmount * 0.5m / 10000m) * 10000m;
        public decimal CodRemaining => TotalAmount - MinimumDeposit;
        public List<PromotionModel> AvailablePromotions { get; set; } = new();

        // Dịch vụ giao hàng tận nhà & cước phí
        public string DeliveryMethod { get; set; } = "HomeDelivery"; // "HomeDelivery" hoặc "StorePickup"
        public string? ShippingMethod
        {
            get => DeliveryMethod;
            set => DeliveryMethod = value ?? "HomeDelivery";
        }
        public string ShippingCarrier { get; set; } = "xanhsm"; // "xanhsm", "grab", "viettelpost", "ghn"
        public string ShippingCarrierName { get; set; } = "Xanh SM Express";
        public decimal ShippingFee { get; set; } = 0;
        public decimal ShippingDistanceKm { get; set; } = 5.0m;
        public string? ShippingServiceType { get; set; } = "Giao hỏa tốc xe máy điện 30 - 60 phút";
    }

    public class InvoiceDetailModel
    {
        public int InvoiceDetailId { get; set; }
        public int InvoiceId { get; set; }
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string? ImageUrl { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal LineTotal { get; set; }
    }

    public class InvoiceModel
    {
        public int InvoiceId { get; set; }
        public int CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerPhone { get; set; } = string.Empty;
        public string? ShippingAddress { get; set; }
        public string? OrderNotes { get; set; }
        public string? PaymentMethodSelected { get; set; }
        public int EmployeeId { get; set; }
        public string EmployeeName { get; set; } = string.Empty;
        public int? PromotionId { get; set; }
        public string? PromotionName { get; set; }
        public DateTime InvoiceDate { get; set; }
        public decimal SubTotal { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal TotalAmount { get; set; }
        public string Status { get; set; } = "Pending"; // 'Pending', 'Paid', 'Cancelled', 'CancelRequested'
        public decimal PaidAmount { get; set; }
        public decimal RemainingAmount => Math.Max(0, TotalAmount - PaidAmount);
        public decimal RequiredDeposit => Math.Round(TotalAmount * 0.5m / 10000m) * 10000m;

        // Cancellation & anti-fraud audit fields
        public string? CancelReason { get; set; }
        public string? CancelRequestedBy { get; set; }
        public DateTime? CancelRequestedAt { get; set; }
        public string? CancelApprovedBy { get; set; }
        public DateTime? CancelApprovedAt { get; set; }

        // Shipping Info
        public string? ShippingCarrier { get; set; }
        public decimal ShippingFee { get; set; }
        public decimal? ShippingDistanceKm { get; set; }
        public string? ShippingServiceType { get; set; }
        public string ShippingCarrierName => !string.IsNullOrEmpty(ShippingServiceType) ? ShippingServiceType : (!string.IsNullOrEmpty(ShippingCarrier) ? ShippingCarrier : "Xanh SM Express / GrabExpress");

        public bool IsInstallment => PaymentMethodSelected != null && PaymentMethodSelected.StartsWith("Trả góp");

        public bool IsFullyPaid => Status == "Paid" || Status == "Shipping" || Status == "Delivered" || (PaidAmount >= TotalAmount && TotalAmount > 0);

        public string StatusBadgeClass => Status switch
        {
            "Paid" => "bg-success text-white",
            "Shipping" => "bg-info text-dark",
            "Delivered" => "bg-primary text-white",
            "Pending" => "bg-warning text-dark",
            "Cancelled" => "bg-danger text-white",
            "CancelRequested" => "bg-warning text-dark",
            _ => "bg-secondary text-white"
        };

        public string StatusVietnamese => Status switch
        {
            "Pending" => (PaidAmount > 0 ? $"Chờ duyệt (Đã cọc {PaidAmount:N0}đ)" : "Chờ Sale phê duyệt"),
            "Paid" => "Đã thanh toán (Sẵn sàng giao)",
            "Shipping" => "Đang giao hàng (Shipping)",
            "Delivered" => "Đã giao hàng thành công (Delivered)",
            "Cancelled" => "Đã hủy đơn (Cancelled)",
            "CancelRequested" => "Chờ Admin duyệt yêu cầu hủy",
            _ => Status
        };

        public string PaymentStatusText
        {
            get
            {
                if (Status == "Cancelled") return $"Đã hủy đơn{(string.IsNullOrEmpty(CancelReason) ? "" : $" (Lý do: {CancelReason})")}";
                if (Status == "CancelRequested") return "Chờ Quản trị viên (Admin) phê duyệt yêu cầu hủy";
                if (Status == "Delivered") return "Đã giao hàng thành công (Delivered)";
                if (Status == "Shipping") return "Đang giao hàng (Shipping)";
                if (Status == "Paid" || (PaidAmount >= TotalAmount && TotalAmount > 0)) return "Đã thanh toán đủ 100% (Paid)";
                if (Status == "Pending")
                {
                    if (PaidAmount > 0) return $"Chờ Sale phê duyệt (Đã thanh toán {PaidAmount:N0}đ)";
                    return "Chờ Sale phê duyệt";
                }
                return Status;
            }
        }

        public List<InvoiceDetailModel> Details { get; set; } = new();
    }

    public class OrderConfirmationViewModel
    {
        public InvoiceModel Invoice { get; set; } = new();
        public string BankName { get; set; } = "ZaloPay";
        public string AccountNumber { get; set; } = "0396166509";
        public string AccountHolder { get; set; } = "DUONG NGOC ANH";
        public decimal DepositNeeded => Invoice.RequiredDeposit;
        public string TransferSyntax => $"KITTY #{Invoice.InvoiceId} {Invoice.CustomerPhone}";
        
        public decimal AmountToPay => IsInstallment 
            ? (PrepayAmount > 0 ? PrepayAmount : Math.Round(Invoice.TotalAmount * PrepayPercent / 100m / 10000m) * 10000m) 
            : (Invoice.PaymentMethodSelected == "COD" ? DepositNeeded : Invoice.TotalAmount);

        private string? _qrCodeUrl;
        public string QrCodeUrl
        {
            get => _qrCodeUrl ?? $"https://img.vietqr.io/image/970454-0396166509-compact2.png?amount={(long)AmountToPay}&addInfo={Uri.EscapeDataString(TransferSyntax)}&accountName=DUONG%20NGOC%20ANH";
            set => _qrCodeUrl = value;
        }

        // Installment details
        public bool IsInstallment => Invoice.IsInstallment;
        public int PrepayPercent { get; set; } = 30;
        public int Months { get; set; } = 6;
        public decimal PrepayAmount { get; set; }
        public decimal MonthlyAmount { get; set; }
    }
}
