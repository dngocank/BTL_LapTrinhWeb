using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using BTL_PTTKYC.Data;
using BTL_PTTKYC.Models;

namespace BTL_PTTKYC.Controllers
{
    public class AccountController : Controller
    {
        private readonly AccountRepository _accountRepo;

        public AccountController(AccountRepository accountRepo)
        {
            _accountRepo = accountRepo;
        }

        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                if (User.IsInRole("Admin")) return RedirectToAction("Index", "Admin");
                if (User.IsInRole("Sales")) return RedirectToAction("Orders", "Sales");
                if (User.IsInRole("Warehouse")) return RedirectToAction("Index", "Warehouse");
                if (User.IsInRole("Technician")) return RedirectToAction("Index", "Technician");
            }

            ViewBag.ReturnUrl = returnUrl;
            return View(new LoginViewModel { ReturnUrl = returnUrl });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var account = _accountRepo.Authenticate(model.Username, model.Password);
            if (account == null)
            {
                ModelState.AddModelError(string.Empty, "Tên đăng nhập hoặc mật khẩu không chính xác hoặc tài khoản đã bị khóa.");
                return View(model);
            }

            // Kích hoạt 2FA (Xác thực 2 bước) cho Quản trị viên (Feature 9)
            if (account.Role == "Admin")
            {
                var otp = Random.Shared.Next(100000, 999999).ToString();
                HttpContext.Session.SetString("Admin2FA_Otp", otp);
                HttpContext.Session.SetString("Admin2FA_AccountId", account.AccountId.ToString());
                HttpContext.Session.SetString("Admin2FA_Username", account.Username);
                HttpContext.Session.SetString("Admin2FA_EmployeeName", account.EmployeeName);
                HttpContext.Session.SetString("Admin2FA_Role", account.Role);
                HttpContext.Session.SetString("Admin2FA_ReturnUrl", model.ReturnUrl ?? "");

                TempData["DemoOtp"] = otp;
                return RedirectToAction("Verify2FA");
            }

            // Dọn dẹp sạch Session giỏ hàng để tài khoản nhân viên nội bộ không thừa hưởng session từ khách
            HttpContext.Session.Remove("KittyPhone_Cart");

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, account.AccountId.ToString()),
                new Claim(ClaimTypes.Name, account.Username),
                new Claim("EmployeeName", account.EmployeeName),
                new Claim(ClaimTypes.Role, account.Role)
            };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var authProperties = new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7)
            };

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                authProperties);

            if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
            {
                return Redirect(model.ReturnUrl);
            }

            if (account.Role == "Admin")
            {
                return RedirectToAction("Index", "Admin");
            }
            else if (account.Role == "Sales")
            {
                return RedirectToAction("Orders", "Sales");
            }
            else if (account.Role == "Warehouse")
            {
                return RedirectToAction("Index", "Warehouse");
            }
            else if (account.Role == "Technician")
            {
                return RedirectToAction("Workbench", "Technician");
            }

            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        public IActionResult Verify2FA()
        {
            var otp = HttpContext.Session.GetString("Admin2FA_Otp");
            var username = HttpContext.Session.GetString("Admin2FA_Username");
            if (string.IsNullOrEmpty(otp) || string.IsNullOrEmpty(username))
            {
                return RedirectToAction("Login");
            }

            ViewBag.Username = username;
            ViewBag.DemoOtp = TempData["DemoOtp"] ?? otp;
            return View(new Verify2FAViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Verify2FA(Verify2FAViewModel model)
        {
            var savedOtp = HttpContext.Session.GetString("Admin2FA_Otp");
            var accountIdStr = HttpContext.Session.GetString("Admin2FA_AccountId");
            var username = HttpContext.Session.GetString("Admin2FA_Username");
            var employeeName = HttpContext.Session.GetString("Admin2FA_EmployeeName");
            var role = HttpContext.Session.GetString("Admin2FA_Role");
            var returnUrl = HttpContext.Session.GetString("Admin2FA_ReturnUrl");

            if (string.IsNullOrEmpty(savedOtp) || string.IsNullOrEmpty(username))
            {
                TempData["Error"] = "Phiên xác thực 2FA đã hết hạn. Vui lòng đăng nhập lại.";
                return RedirectToAction("Login");
            }

            if (model.OtpCode?.Trim() != savedOtp)
            {
                ModelState.AddModelError(string.Empty, "Mã xác thực 2FA không chính xác. Vui lòng kiểm tra lại!");
                ViewBag.Username = username;
                ViewBag.DemoOtp = savedOtp;
                return View(model);
            }

            // Xóa session 2FA sau khi xác thực thành công và dọn sạch Session giỏ hàng
            HttpContext.Session.Remove("Admin2FA_Otp");
            HttpContext.Session.Remove("KittyPhone_Cart");

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, accountIdStr ?? "0"),
                new Claim(ClaimTypes.Name, username),
                new Claim("EmployeeName", employeeName ?? ""),
                new Claim(ClaimTypes.Role, role ?? "Admin")
            };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var authProperties = new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7)
            };

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                authProperties);

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction("Index", "Admin");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            // Dọn dẹp sạch Session giỏ hàng và dữ liệu phiên làm việc nội bộ
            HttpContext.Session.Remove("KittyPhone_Cart");
            HttpContext.Session.Clear();
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Index", "Home");
        }

        public IActionResult AccessDenied()
        {
            return View();
        }
    }
}
