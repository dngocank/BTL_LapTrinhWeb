using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using BTL_PTTKYC.Data;
using BTL_PTTKYC.Models;

using BTL_PTTKYC.Services;

namespace BTL_PTTKYC.Controllers
{
    public class CustomerController : Controller
    {
        private readonly CustomerRepository _customerRepo;
        private readonly OrderRepository _orderRepo;
        private readonly WarrantyRepository _warrantyRepo;
        private readonly IShippingService _shippingService;
        private readonly INotificationService _notificationService;

        public CustomerController(
            CustomerRepository customerRepo, 
            OrderRepository orderRepo,
            WarrantyRepository warrantyRepo,
            IShippingService shippingService,
            INotificationService notificationService)
        {
            _customerRepo = customerRepo;
            _orderRepo = orderRepo;
            _warrantyRepo = warrantyRepo;
            _shippingService = shippingService;
            _notificationService = notificationService;
        }

        [HttpGet]
        public IActionResult Register(string? returnUrl = null)
        {
            if (User.Identity != null && User.Identity.IsAuthenticated && User.IsInRole("Customer"))
            {
                return RedirectToAction("Profile");
            }

            ViewBag.ReturnUrl = returnUrl;
            return View(new CustomerRegisterViewModel { ReturnUrl = returnUrl });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Register(CustomerRegisterViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            if (_customerRepo.RegisterCustomer(model, out string otpCode, out string errorMessage))
            {
                TempData["SuccessMessage"] = "Đăng ký thành công! Vui lòng nhập mã OTP để xác thực Số điện thoại và Gmail của bạn.";
                // Cung cấp mã OTP mô phỏng để người dùng trải nghiệm ngay lập tức trong bài tập lớn
                TempData["SimulatedOtp"] = otpCode;
                TempData["TargetPhone"] = model.Phone;
                TempData["TargetEmail"] = model.Email;

                return RedirectToAction("VerifyOtp", new { target = model.Phone, returnUrl = model.ReturnUrl });
            }

            ModelState.AddModelError(string.Empty, errorMessage);
            return View(model);
        }

        [HttpGet]
        public IActionResult VerifyOtp(string? target, string? returnUrl = null)
        {
            if (string.IsNullOrEmpty(target))
            {
                target = TempData["TargetPhone"]?.ToString() ?? TempData["TargetEmail"]?.ToString() ?? "";
            }

            if (string.IsNullOrEmpty(target))
            {
                return RedirectToAction("Register");
            }

            var vm = new CustomerVerifyOtpViewModel
            {
                PhoneOrEmail = target,
                SimulatedOtp = TempData["SimulatedOtp"]?.ToString(),
                ReturnUrl = returnUrl
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyOtp(CustomerVerifyOtpViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            if (_customerRepo.VerifyOtp(model.PhoneOrEmail, model.OtpCode, out CustomerModel? customer, out string errorMessage))
            {
                if (customer != null)
                {
                    // Dọn dẹp và làm mới Session giỏ hàng để tránh thừa hưởng sản phẩm từ phiên ẩn danh trước đó
                    HttpContext.Session.Remove("KittyPhone_Cart");

                    // Tự động đăng nhập cho khách hàng
                    var claims = new List<Claim>
                    {
                        new Claim(ClaimTypes.NameIdentifier, customer.CustomerId.ToString()),
                        new Claim(ClaimTypes.Name, customer.CustomerName),
                        new Claim(ClaimTypes.Role, "Customer"),
                        new Claim(ClaimTypes.MobilePhone, customer.Phone ?? string.Empty),
                        new Claim(ClaimTypes.Email, customer.Email ?? string.Empty)
                    };

                    var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                    var authProperties = new AuthenticationProperties
                    {
                        IsPersistent = true,
                        ExpiresUtc = DateTimeOffset.UtcNow.AddDays(14)
                    };

                    await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity), authProperties);

                    TempData["SuccessMessage"] = $"Chúc mừng {customer.CustomerName}! Tài khoản của bạn đã được xác thực SĐT và Gmail thành công.";

                    if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
                    {
                        return Redirect(model.ReturnUrl);
                    }

                    return RedirectToAction("Profile");
                }
            }

            ModelState.AddModelError(string.Empty, errorMessage);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ResendOtp(string target, string? returnUrl = null)
        {
            if (_customerRepo.ResendOtp(target, out string newOtp, out string errorMessage))
            {
                TempData["SuccessMessage"] = "Mã xác thực OTP mới đã được gửi thành công.";
                TempData["SimulatedOtp"] = newOtp;
            }
            else
            {
                TempData["ErrorMessage"] = errorMessage;
            }

            return RedirectToAction("VerifyOtp", new { target = target, returnUrl = returnUrl });
        }

        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            if (User.Identity != null && User.Identity.IsAuthenticated && User.IsInRole("Customer"))
            {
                return RedirectToAction("Profile");
            }

            ViewBag.ReturnUrl = returnUrl;
            return View(new CustomerLoginViewModel { ReturnUrl = returnUrl });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(CustomerLoginViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var customer = _customerRepo.Authenticate(model.Identifier, model.Password, out string errorMessage);
            if (customer == null)
            {
                if (errorMessage.Contains("chưa hoàn tất xác thực OTP"))
                {
                    TempData["WarningMessage"] = errorMessage;
                    return RedirectToAction("VerifyOtp", new { target = model.Identifier, returnUrl = model.ReturnUrl });
                }

                ModelState.AddModelError(string.Empty, errorMessage);
                return View(model);
            }

            // Dọn dẹp và làm mới Session giỏ hàng để tài khoản mới đăng nhập không thừa hưởng sản phẩm từ phiên trước
            HttpContext.Session.Remove("KittyPhone_Cart");

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, customer.CustomerId.ToString()),
                new Claim(ClaimTypes.Name, customer.CustomerName),
                new Claim(ClaimTypes.Role, "Customer"),
                new Claim(ClaimTypes.MobilePhone, customer.Phone ?? string.Empty),
                new Claim(ClaimTypes.Email, customer.Email ?? string.Empty)
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var authProperties = new AuthenticationProperties
            {
                IsPersistent = model.RememberMe,
                ExpiresUtc = model.RememberMe ? DateTimeOffset.UtcNow.AddDays(30) : DateTimeOffset.UtcNow.AddHours(8)
            };

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity), authProperties);

            if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
            {
                return Redirect(model.ReturnUrl);
            }

            return RedirectToAction("Profile");
        }

        [Authorize(Roles = "Customer")]
        [HttpGet]
        public IActionResult Profile()
        {
            var customerIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(customerIdClaim, out int customerId))
            {
                return RedirectToAction("Login");
            }

            var customer = _customerRepo.GetCustomerById(customerId);
            if (customer == null)
            {
                return RedirectToAction("Login");
            }

            var orders = _orderRepo.GetOrdersByCustomerId(customerId);
            var purchased = _customerRepo.GetPurchasedProducts(customerId);
            var warrantyTickets = _warrantyRepo.GetTicketsByCustomerId(customerId, customer.Phone);
            var warrantedDevices = _warrantyRepo.GetWarrantedDevicesByCustomerId(customerId);
            var notifications = _notificationService.GetNotificationsByCustomerId(customerId);

            var firstWarranted = warrantedDevices.FirstOrDefault();
            var homeWarrantyForm = new HomeWarrantyRegisterViewModel
            {
                CustomerId = customer.CustomerId,
                CustomerName = customer.CustomerName,
                CustomerPhone = customer.Phone ?? string.Empty,
                CustomerEmail = customer.Email ?? string.Empty,
                PickupAddress = customer.Address ?? string.Empty,
                PurchasedDevices = purchased,
                DeviceName = firstWarranted?.ProductName ?? purchased.FirstOrDefault()?.ProductName ?? "Điện thoại thông minh",
                SerialNumberOrImei = firstWarranted?.ImeiNumber ?? firstWarranted?.SerialNumber ?? string.Empty,
                PickupDate = DateTime.Today.AddDays(1)
            };

            var shippingEstimator = new ShippingQuoteRequest
            {
                DestinationProvince = "Hà Nội",
                DestinationDistrict = "Đống Đa",
                DestinationAddress = customer.Address ?? "123 Thái Hà, Đống Đa, Hà Nội",
                CustomerRank = customer.RankName,
                DistanceKm = 3.5m
            };

            var vm = new CustomerProfileViewModel
            {
                Customer = customer,
                Orders = orders,
                PurchasedProducts = purchased,
                WarrantyTickets = warrantyTickets,
                WarrantedDevices = warrantedDevices,
                HomeWarrantyForm = homeWarrantyForm,
                ShippingEstimator = shippingEstimator,
                RecentNotifications = notifications
            };

            return View(vm);
        }

        // 1. THEO DÕI & TRACKING BẢO HÀNH RIÊNG
        [Authorize(Roles = "Customer")]
        [HttpGet]
        public IActionResult Warranty()
        {
            var customerIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(customerIdClaim, out int customerId))
            {
                return RedirectToAction("Login");
            }

            var customer = _customerRepo.GetCustomerById(customerId);
            if (customer == null) return RedirectToAction("Login");

            var tickets = _warrantyRepo.GetTicketsByCustomerId(customerId, customer.Phone);
            var warrantedDevices = _warrantyRepo.GetWarrantedDevicesByCustomerId(customerId);
            ViewBag.Customer = customer;
            ViewBag.WarrantedDevices = warrantedDevices;
            return View(tickets);
        }

        // 2. CHI TIẾT PHIẾU BẢO HÀNH & TIMELINE TIẾN ĐỘ
        [Authorize(Roles = "Customer")]
        [HttpGet]
        public IActionResult WarrantyDetail(int id)
        {
            var customerIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(customerIdClaim, out int customerId))
            {
                return RedirectToAction("Login");
            }

            var customer = _customerRepo.GetCustomerById(customerId);
            var ticket = _warrantyRepo.GetTicketById(id);
            if (ticket == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy phiếu bảo hành yêu cầu.";
                return RedirectToAction("Profile");
            }

            // Bảo mật: Kiểm tra quyền sở hữu phiếu của khách hàng
            if (ticket.CustomerId.HasValue && ticket.CustomerId.Value != customerId && ticket.CustomerPhone != customer?.Phone)
            {
                TempData["ErrorMessage"] = "Bạn không có quyền xem thông tin phiếu bảo hành này.";
                return RedirectToAction("Profile");
            }

            ViewBag.Customer = customer;
            return View(ticket);
        }

        // 3. ĐĂNG KÝ BẢO HÀNH TẠI NHÀ
        [Authorize(Roles = "Customer")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult RegisterHomeWarranty([Bind(Prefix = "HomeWarrantyForm")] HomeWarrantyRegisterViewModel model)
        {
            var customerIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (int.TryParse(customerIdClaim, out int customerId))
            {
                model.CustomerId = customerId;
            }

            if (!ModelState.IsValid)
            {
                var errors = string.Join(", ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).Where(m => !string.IsNullOrEmpty(m)));
                TempData["ErrorMessage"] = string.IsNullOrEmpty(errors)
                    ? "Vui lòng kiểm tra lại các trường thông tin đăng ký bảo hành."
                    : $"Thông tin đăng ký chưa hợp lệ: {errors}";
                TempData["ActiveTab"] = "homewarranty-pane";
                return RedirectToAction("Profile");
            }


            if (_warrantyRepo.CreateHomeWarrantyTicket(model, out string ticketCode, out int newTicketId, out string errorMessage))
            {
                var newTicket = _warrantyRepo.GetTicketById(newTicketId);
                string emailResult = string.Empty;
                string smsResult = string.Empty;

                if (newTicket != null)
                {
                    // Tự động gửi Email và SMS xác nhận cho khách hàng
                    _notificationService.SendHomeWarrantyConfirmation(newTicket, model.CustomerEmail, model.CustomerPhone, out emailResult, out smsResult);
                }

                TempData["SuccessMessage"] = $"Đăng ký bảo hành tại nhà thành công! Mã phiếu của bạn: {ticketCode}. Đã gửi Email và SMS thông báo xác nhận.";
                TempData["ActiveTab"] = "warranty-pane";
                return RedirectToAction("WarrantyDetail", new { id = newTicketId });
            }

            TempData["ErrorMessage"] = "Không thể đăng ký bảo hành tại nhà: " + errorMessage;
            TempData["ActiveTab"] = "homewarranty-pane";
            return RedirectToAction("Profile");
        }

        // 4. API TÍNH CƯỚC VẬN CHUYỂN TẬN NHÀ (DÙNG CHO AJAX)
        [HttpPost]
        public IActionResult CalculateShipping([FromBody] ShippingQuoteRequest request)
        {
            if (request == null)
            {
                return Json(new { success = false, message = "Dữ liệu yêu cầu không hợp lệ." });
            }

            var quote = _shippingService.CalculateShipping(request);
            return Json(new { success = true, quote });
        }

        // 5. TRANG TRA CỨU & ƯỚC TÍNH TIỀN SHIP TẬN NHÀ
        [HttpGet]
        public IActionResult ShippingCalculator(string? province, string? district, decimal? distance)
        {
            var req = new ShippingQuoteRequest
            {
                DestinationProvince = province ?? "Hà Nội",
                DestinationDistrict = district ?? "Đống Đa",
                DistanceKm = distance ?? 5.0m
            };

            var quote = _shippingService.CalculateShipping(req);
            ViewBag.Districts = _shippingService.GetAllDistricts();
            return View(quote);
        }

        [HttpGet]
        public IActionResult InvoicePdf(int id)
        {
            var order = _orderRepo.GetOrderById(id);
            if (order == null) return NotFound("Không tìm thấy đơn hàng");

            if (User.IsInRole("Customer"))
            {
                var customerIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (int.TryParse(customerIdClaim, out int cid) && order.CustomerId != cid)
                {
                    return Forbid();
                }
            }

            return View(order);
        }

        [Authorize(Roles = "Customer")]
        [HttpGet]
        public IActionResult Orders()
        {
            var customerIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(customerIdClaim, out int customerId))
            {
                return RedirectToAction("Login");
            }

            var orders = _orderRepo.GetOrdersByCustomerId(customerId);
            return View(orders);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            // Dọn dẹp sạch Session giỏ hàng và toàn bộ dữ liệu phiên làm việc của khách hàng
            HttpContext.Session.Remove("KittyPhone_Cart");
            HttpContext.Session.Clear();
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Index", "Home");
        }
    }
}
