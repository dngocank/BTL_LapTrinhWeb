using System.ComponentModel.DataAnnotations;

namespace BTL_PTTKYC.Models
{
    public class CustomerModel
    {
        public int CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? Address { get; set; }
        public int RankId { get; set; } = 1;
        public string RankName { get; set; } = "Standard";
        public decimal TotalSpent { get; set; }
        public bool IsEmailVerified { get; set; }
        public bool IsPhoneVerified { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }

    public class CustomerRegisterViewModel
    {
        [Required(ErrorMessage = "Vui lòng nhập họ và tên của bạn")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Họ và tên phải từ 2 đến 100 ký tự")]
        public string CustomerName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập số điện thoại")]
        [RegularExpression(@"^(03|05|07|08|09)[0-9]{8}$", ErrorMessage = "Số điện thoại không hợp lệ (Phải gồm 10 chữ số và bắt đầu bằng 03, 05, 07, 08, 09)")]
        public string Phone { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập địa chỉ Gmail/Email")]
        [EmailAddress(ErrorMessage = "Định dạng email không hợp lệ")]
        [RegularExpression(@"^[a-zA-Z0-9._%+-]+@(gmail\.com|[a-zA-Z0-9.-]+\.[a-zA-Z]{2,})$", ErrorMessage = "Vui lòng nhập địa chỉ email hợp lệ (khuyến nghị dùng Gmail)")]
        [StringLength(100, ErrorMessage = "Email tối đa 100 ký tự")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập mật khẩu")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "Mật khẩu phải chứa ít nhất 6 ký tự")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng xác nhận lại mật khẩu")]
        [Compare("Password", ErrorMessage = "Mật khẩu xác nhận không khớp với mật khẩu đã nhập")]
        [DataType(DataType.Password)]
        public string ConfirmPassword { get; set; } = string.Empty;

        [StringLength(255, ErrorMessage = "Địa chỉ tối đa 255 ký tự")]
        public string? Address { get; set; }

        public string? ReturnUrl { get; set; }
    }

    public class CustomerVerifyOtpViewModel
    {
        [Required(ErrorMessage = "Thiếu thông tin tài khoản cần xác thực")]
        public string PhoneOrEmail { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập mã xác thực OTP")]
        [RegularExpression(@"^[0-9]{6}$", ErrorMessage = "Mã OTP phải bao gồm 6 chữ số")]
        public string OtpCode { get; set; } = string.Empty;

        public string? SimulatedOtp { get; set; }

        public string? ReturnUrl { get; set; }
    }

    public class CustomerLoginViewModel
    {
        [Required(ErrorMessage = "Vui lòng nhập Số điện thoại hoặc Gmail")]
        public string Identifier { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập mật khẩu")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        public bool RememberMe { get; set; } = true;

        public string? ReturnUrl { get; set; }
    }

    public class CustomerPurchasedProductItem
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string? ImageUrl { get; set; }
        public decimal Price { get; set; }
        public int InvoiceId { get; set; }
        public DateTime OrderDate { get; set; }
        public string OrderStatus { get; set; } = string.Empty;
        public bool HasReviewed { get; set; }
        public int? Rating { get; set; }
        public string? Comment { get; set; }
    }

    public class CustomerProfileViewModel
    {
        public CustomerModel Customer { get; set; } = new();
        public List<InvoiceModel> Orders { get; set; } = new();
        public List<CustomerPurchasedProductItem> PurchasedProducts { get; set; } = new();
        public List<WarrantyTicketModel> WarrantyTickets { get; set; } = new();
        public List<WarrantyCardModel> WarrantedDevices { get; set; } = new();
        public HomeWarrantyRegisterViewModel HomeWarrantyForm { get; set; } = new();
        public ShippingQuoteRequest ShippingEstimator { get; set; } = new();
        public List<WarrantyNotificationModel> RecentNotifications { get; set; } = new();
    }
}
