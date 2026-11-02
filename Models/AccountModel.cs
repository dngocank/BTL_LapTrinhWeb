using System.ComponentModel.DataAnnotations;

namespace BTL_PTTKYC.Models
{
    public class LoginViewModel
    {
        [Required(ErrorMessage = "Vui lòng nhập tên tài khoản")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập mật khẩu")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        public string? ReturnUrl { get; set; }
    }

    public class AccountModel
    {
        public int AccountId { get; set; }
        public int EmployeeId { get; set; }
        public string EmployeeName { get; set; } = string.Empty;
        public string? Position { get; set; }
        public string? Phone { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty; // 'Admin', 'Sales', 'Warehouse', 'Technician'
        public bool IsActive { get; set; } = true;
    }

    public class CreateAccountViewModel
    {
        [Required(ErrorMessage = "Vui lòng chọn nhân viên")]
        public int EmployeeId { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập tên đăng nhập")]
        [StringLength(50, MinimumLength = 3, ErrorMessage = "Tên đăng nhập từ 3 đến 50 ký tự")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập mật khẩu")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "Mật khẩu phải từ 6 ký tự trở lên")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng chọn phân quyền")]
        public string Role { get; set; } = "Sales"; // 'Admin', 'Sales', 'Warehouse', 'Technician'

        public bool IsActive { get; set; } = true;
    }

    public class EditAccountViewModel
    {
        public int AccountId { get; set; }
        public int EmployeeId { get; set; }
        public string EmployeeName { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        public string? NewPassword { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn phân quyền")]
        public string Role { get; set; } = "Sales";

        public bool IsActive { get; set; } = true;
    }

    public class EmployeeOptionModel
    {
        public int EmployeeId { get; set; }
        public string EmployeeName { get; set; } = string.Empty;
        public string? Position { get; set; }
        public string? Phone { get; set; }
        public bool HasAccount { get; set; }
    }

    public class Verify2FAViewModel
    {
        [Required(ErrorMessage = "Vui lòng nhập mã xác thực OTP 6 số")]
        [StringLength(6, MinimumLength = 6, ErrorMessage = "Mã OTP gồm đúng 6 chữ số")]
        public string OtpCode { get; set; } = string.Empty;

        public string? ReturnUrl { get; set; }
    }
}
