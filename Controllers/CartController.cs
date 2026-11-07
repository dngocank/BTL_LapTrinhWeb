using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using BTL_PTTKYC.Data;
using BTL_PTTKYC.Models;
using BTL_PTTKYC.Services;

namespace BTL_PTTKYC.Controllers
{
    public class CartController : Controller
    {
        private const string CartSessionKey = "KittyPhone_Cart";
        private readonly ProductRepository _productRepo;
        private readonly OrderRepository _orderRepo;
        private readonly CustomerRepository _customerRepo;
        private readonly PromotionRepository _promotionRepo;
        private readonly IShippingService _shippingService;

        public CartController(
            ProductRepository productRepo,
            OrderRepository orderRepo,
            CustomerRepository customerRepo,
            PromotionRepository promotionRepo,
            IShippingService shippingService)
        {
            _productRepo = productRepo;
            _orderRepo = orderRepo;
            _customerRepo = customerRepo;
            _promotionRepo = promotionRepo;
            _shippingService = shippingService;
        }

        private List<CartItemModel> GetCart()
        {
            var sessionData = HttpContext.Session.GetString(CartSessionKey);
            if (string.IsNullOrEmpty(sessionData))
            {
                return new List<CartItemModel>();
            }
            return JsonConvert.DeserializeObject<List<CartItemModel>>(sessionData) ?? new List<CartItemModel>();
        }

        private void SaveCart(List<CartItemModel> cart)
        {
            HttpContext.Session.SetString(CartSessionKey, JsonConvert.SerializeObject(cart));
        }

        /// <summary>
        /// Kiểm tra vai trò nội bộ (Admin, Sales, Warehouse, Technician) và chuyển hướng về trang quản trị tương ứng.
        /// Ngăn chặn nhân viên nội bộ thực hiện hành vi mua sắm.
        /// </summary>
        private IActionResult? RedirectInternalStaff()
        {
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                if (User.IsInRole("Admin"))
                {
                    TempData["ErrorMessage"] = "Tài khoản Quản trị viên (Admin) không được phép thực hiện chức năng mua sắm.";
                    return RedirectToAction("Index", "Admin");
                }
                if (User.IsInRole("Sales"))
                {
                    TempData["ErrorMessage"] = "Tài khoản Nhân viên Bán hàng (Sales) không được phép thực hiện chức năng mua sắm.";
                    return RedirectToAction("Orders", "Sales");
                }
                if (User.IsInRole("Warehouse"))
                {
                    TempData["ErrorMessage"] = "Tài khoản Nhân viên Kho (Warehouse) không được phép thực hiện chức năng mua sắm.";
                    return RedirectToAction("Index", "Warehouse");
                }
                if (User.IsInRole("Technician"))
                {
                    TempData["ErrorMessage"] = "Tài khoản Kỹ thuật viên (Technician) không được phép thực hiện chức năng mua sắm.";
                    return RedirectToAction("Workbench", "Technician");
                }
            }
            return null;
        }

        public IActionResult Index()
        {
            // Kiểm tra và chuyển hướng nếu tài khoản mang vai trò nội bộ
            var staffRedirect = RedirectInternalStaff();
            if (staffRedirect != null) return staffRedirect;

            if (User.Identity == null || !User.Identity.IsAuthenticated || !User.IsInRole("Customer"))
            {
                TempData["AuthRequired"] = "Quý khách vui lòng đăng nhập tài khoản KittyMember để xem và quản lý giỏ hàng.";
                return RedirectToAction("Login", "Customer", new { returnUrl = "/Cart" });
            }

            var cart = GetCart();
            return View(cart);
        }

        [HttpPost]
        public IActionResult Add(int productId, int quantity = 1, bool buyNow = false)
        {
            return ProcessAddToCart(productId, quantity, buyNow);
        }

        [HttpPost]
        public IActionResult AddToCart(int productId, int quantity = 1, bool buyNow = false)
        {
            return ProcessAddToCart(productId, quantity, buyNow);
        }

        [HttpGet]
        public IActionResult AddToCart()
        {
            return RedirectToAction("Index");
        }

        private IActionResult ProcessAddToCart(int productId, int quantity, bool buyNow)
        {
            var isAjax = Request.Headers["X-Requested-With"] == "XMLHttpRequest" ||
                         Request.Headers["Accept"].ToString().Contains("application/json");

            // Ngăn chặn nhân viên nội bộ thực hiện mua hàng
            var staffRedirect = RedirectInternalStaff();
            if (staffRedirect != null)
            {
                if (isAjax)
                {
                    return Json(new { success = false, message = "Tài khoản nhân viên nội bộ không được phép thực hiện chức năng mua sắm." });
                }
                return staffRedirect;
            }

            if (User.Identity == null || !User.Identity.IsAuthenticated || !User.IsInRole("Customer"))
            {
                var targetUrl = buyNow ? Url.Action("Checkout", "Cart", new { productId }) : Url.Action("Index", "Cart");
                if (isAjax)
                {
                    return Json(new
                    {
                        success = false,
                        requireLogin = true,
                        redirectUrl = Url.Action("Login", "Customer", new { returnUrl = targetUrl }),
                        message = "Quý khách vui lòng đăng nhập tài khoản KittyMember để thao tác giỏ hàng."
                    });
                }
                TempData["AuthRequired"] = "Quý khách vui lòng đăng nhập tài khoản KittyMember để thao tác giỏ hàng.";
                return RedirectToAction("Login", "Customer", new { returnUrl = targetUrl });
            }

            var product = _productRepo.GetProductById(productId);
            if (product == null || product.Quantity <= 0)
            {
                if (isAjax)
                {
                    return Json(new { success = false, message = "Sản phẩm tạm thời hết hàng." });
                }
                TempData["ErrorMessage"] = "Sản phẩm tạm thời hết hàng.";
                var referer = Request.Headers["Referer"].ToString();
                return !string.IsNullOrEmpty(referer) ? Redirect(referer) : RedirectToAction("Index", "Home");
            }

            var cart = GetCart();
            var existing = cart.FirstOrDefault(c => c.ProductId == productId);
            if (existing != null)
            {
                existing.Quantity += (quantity > 0 ? quantity : 1);
            }
            else
            {
                cart.Add(new CartItemModel
                {
                    ProductId = product.ProductId,
                    ProductName = product.ProductName,
                    ImageUrl = product.ImageUrl,
                    Price = product.SellingPrice,
                    Quantity = quantity > 0 ? quantity : 1
                });
            }

            SaveCart(cart);

            if (buyNow)
            {
                return RedirectToAction("Checkout", new { productId });
            }

            if (isAjax)
            {
                return Json(new { success = true, count = cart.Sum(c => c.Quantity), message = $"Đã thêm {product.ProductName} vào giỏ hàng!" });
            }

            TempData["SuccessMessage"] = $"Đã thêm {product.ProductName} vào giỏ hàng thành công!";
            var returnUrl = Request.Headers["Referer"].ToString();
            return !string.IsNullOrEmpty(returnUrl) ? Redirect(returnUrl) : RedirectToAction("Index");
        }

        [HttpPost]
        public IActionResult UpdateQuantity(int productId, int quantity)
        {
            var staffRedirect = RedirectInternalStaff();
            if (staffRedirect != null) return staffRedirect;

            if (User.Identity == null || !User.Identity.IsAuthenticated || !User.IsInRole("Customer"))
            {
                return RedirectToAction("Login", "Customer", new { returnUrl = "/Cart" });
            }

            var cart = GetCart();
            var item = cart.FirstOrDefault(c => c.ProductId == productId);
            if (item != null)
            {
                if (quantity <= 0)
                {
                    cart.Remove(item);
                }
                else
                {
                    item.Quantity = quantity;
                }
                SaveCart(cart);
            }
            return RedirectToAction("Index");
        }

        [HttpPost]
        public IActionResult Remove(int productId)
        {
            var staffRedirect = RedirectInternalStaff();
            if (staffRedirect != null) return staffRedirect;

            if (User.Identity == null || !User.Identity.IsAuthenticated || !User.IsInRole("Customer"))
            {
                return RedirectToAction("Login", "Customer", new { returnUrl = "/Cart" });
            }

            var cart = GetCart();
            cart.RemoveAll(c => c.ProductId == productId);
            SaveCart(cart);
            return RedirectToAction("Index");
        }

        [HttpPost]
        public IActionResult ToggleSelection(int productId, bool isSelected)
        {
            var staffRedirect = RedirectInternalStaff();
            if (staffRedirect != null) return Json(new { success = false, message = "Nhân viên nội bộ không được thao tác giỏ hàng." });

            if (User.Identity == null || !User.Identity.IsAuthenticated || !User.IsInRole("Customer"))
            {
                return Json(new { success = false, requireLogin = true, redirectUrl = Url.Action("Login", "Customer", new { returnUrl = "/Cart" }) });
            }

            var cart = GetCart();
            var item = cart.FirstOrDefault(c => c.ProductId == productId);
            if (item != null)
            {
                item.IsSelected = isSelected;
                SaveCart(cart);
            }
            var selected = cart.Where(c => c.IsSelected).ToList();
            return Json(new
            {
                success = true,
                selectedCount = selected.Count,
                selectedQuantity = selected.Sum(c => c.Quantity),
                subTotal = selected.Sum(c => c.LineTotal),
                formattedSubTotal = selected.Sum(c => c.LineTotal).ToString("N0") + "đ"
            });
        }

        [HttpPost]
        public IActionResult ToggleSelectAll(bool isSelected)
        {
            var staffRedirect = RedirectInternalStaff();
            if (staffRedirect != null) return Json(new { success = false, message = "Nhân viên nội bộ không được thao tác giỏ hàng." });

            if (User.Identity == null || !User.Identity.IsAuthenticated || !User.IsInRole("Customer"))
            {
                return Json(new { success = false, requireLogin = true, redirectUrl = Url.Action("Login", "Customer", new { returnUrl = "/Cart" }) });
            }

            var cart = GetCart();
            foreach (var item in cart)
            {
                item.IsSelected = isSelected;
            }
            SaveCart(cart);
            var selected = cart.Where(c => c.IsSelected).ToList();
            return Json(new
            {
                success = true,
                selectedCount = selected.Count,
                selectedQuantity = selected.Sum(c => c.Quantity),
                subTotal = selected.Sum(c => c.LineTotal),
                formattedSubTotal = selected.Sum(c => c.LineTotal).ToString("N0") + "đ"
            });
        }

        [HttpGet]
        public IActionResult Checkout(int? productId = null, string? selectedIds = null)
        {
            // Kiểm tra và chuyển hướng nếu tài khoản mang vai trò nội bộ
            var staffRedirect = RedirectInternalStaff();
            if (staffRedirect != null) return staffRedirect;

            // RÀNG BUỘC NGHIỆP VỤ: Bắt buộc đăng nhập tài khoản khách hàng KittyMember mới được mua hàng
            if (User.Identity == null || !User.Identity.IsAuthenticated || !User.IsInRole("Customer"))
            {
                var returnUrl = productId.HasValue ? $"/Cart/Checkout?productId={productId.Value}" : "/Cart/Checkout";
                TempData["AuthRequired"] = "Quý khách vui lòng đăng nhập tài khoản KittyMember để tiếp tục đặt hàng và hưởng quyền lợi bảo hành, tích điểm.";
                return RedirectToAction("Login", "Customer", new { returnUrl });
            }

            var cart = GetCart();
            var vm = new CheckoutViewModel();

            if (productId.HasValue)
            {
                var p = _productRepo.GetProductById(productId.Value);
                if (p != null)
                {
                    vm.SingleProductId = p.ProductId;
                    vm.SingleQuantity = 1;
                    vm.CartItems = new List<CartItemModel>
                    {
                        new CartItemModel
                        {
                            ProductId = p.ProductId,
                            ProductName = p.ProductName,
                            ImageUrl = p.ImageUrl,
                            Price = p.SellingPrice,
                            Quantity = 1,
                            IsSelected = true
                        }
                    };
                    vm.SubTotal = p.SellingPrice;
                    vm.TotalAmount = p.SellingPrice;
                    vm.AvailablePromotions = _promotionRepo.GetActivePromotions();
                    PopulateCustomerInfo(vm);
                    return View(vm);
                }
            }

            if (cart.Count == 0)
            {
                TempData["CartEmpty"] = "Giỏ hàng của bạn đang trống. Vui lòng chọn sản phẩm trước khi thanh toán.";
                return RedirectToAction("Index", "Home");
            }

            if (!string.IsNullOrEmpty(selectedIds))
            {
                var idSet = selectedIds.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                       .Select(s => int.TryParse(s.Trim(), out int id) ? id : 0)
                                       .Where(id => id > 0).ToHashSet();
                foreach (var item in cart)
                {
                    item.IsSelected = idSet.Contains(item.ProductId);
                }
                SaveCart(cart);
            }

            var selectedItems = cart.Where(c => c.IsSelected).ToList();
            if (selectedItems.Count == 0)
            {
                // Mặc định chọn tất cả nếu chưa có món nào được tích
                foreach (var item in cart) item.IsSelected = true;
                SaveCart(cart);
                selectedItems = cart;
            }

            vm.CartItems = selectedItems;
            vm.SubTotal = selectedItems.Sum(c => c.LineTotal);
            vm.TotalAmount = vm.SubTotal;
            vm.AvailablePromotions = _promotionRepo.GetActivePromotions();
            PopulateCustomerInfo(vm);
            return View(vm);
        }

        private void PopulateCustomerInfo(CheckoutViewModel vm)
        {
            if (User.Identity != null && User.Identity.IsAuthenticated && User.IsInRole("Customer"))
            {
                var custIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (int.TryParse(custIdClaim, out int customerId))
                {
                    vm.CustomerId = customerId;
                    var cust = _customerRepo.GetCustomerById(customerId);
                    if (cust != null)
                    {
                        vm.CustomerName = cust.CustomerName;
                        vm.Phone = cust.Phone ?? string.Empty;
                        vm.Address = cust.Address ?? string.Empty;
                    }
                }
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Checkout(CheckoutViewModel model)
        {
            // Kiểm tra và chuyển hướng nếu tài khoản mang vai trò nội bộ
            var staffRedirect = RedirectInternalStaff();
            if (staffRedirect != null) return staffRedirect;

            // RÀNG BUỘC NGHIỆP VỤ: Bắt buộc đăng nhập tài khoản khách hàng KittyMember
            if (User.Identity == null || !User.Identity.IsAuthenticated || !User.IsInRole("Customer"))
            {
                TempData["AuthRequired"] = "Quý khách vui lòng đăng nhập tài khoản KittyMember để tiếp tục đặt hàng.";
                return RedirectToAction("Login", "Customer", new { returnUrl = "/Cart/Checkout" });
            }

            var custIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(custIdClaim, out int customerId))
            {
                model.CustomerId = customerId;
            }

            // Populate items from Cart if not single product
            var cart = GetCart();
            if (model.SingleProductId.HasValue)
            {
                var p = _productRepo.GetProductById(model.SingleProductId.Value);
                if (p != null)
                {
                    model.CartItems = new List<CartItemModel>
                    {
                        new CartItemModel { ProductId = p.ProductId, ProductName = p.ProductName, Price = p.SellingPrice, Quantity = model.SingleQuantity, IsSelected = true }
                    };
                    model.TotalAmount = p.SellingPrice * model.SingleQuantity;
                }
            }
            else
            {
                var selectedItems = cart.Where(c => c.IsSelected).ToList();
                if (selectedItems.Count == 0) selectedItems = cart;
                model.CartItems = selectedItems;
                model.TotalAmount = selectedItems.Sum(c => c.LineTotal);
            }

            if (model.CartItems.Count == 0)
            {
                ModelState.AddModelError(string.Empty, "Giỏ hàng của bạn đang trống.");
                model.AvailablePromotions = _promotionRepo.GetActivePromotions();
                return View(model);
            }

            if (model.PromotionId.HasValue && model.PromotionId.Value > 0)
            {
                var promo = _promotionRepo.GetPromotionById(model.PromotionId.Value);
                if (promo == null)
                {
                    ModelState.AddModelError(string.Empty, "Mã khuyến mãi không tồn tại.");
                }
                else
                {
                    var cust = _customerRepo.GetCustomerById(customerId);
                    if (!promo.CanCustomerApply(cust?.RankId, out string failureReason))
                    {
                        ModelState.AddModelError(string.Empty, $"Khuyến mãi '{promo.PromotionName}' không đủ điều kiện: {failureReason}");
                    }
                    else
                    {
                        model.DiscountPercent = promo.DiscountPercent;
                        model.DiscountAmount = Math.Round(model.SubTotal * promo.DiscountPercent / 100m / 1000m) * 1000m;
                    }
                }
            }

            // Xử lý phương thức nhận hàng: Lấy tại cửa hàng (StorePickup) vs Giao tận nhà (HomeDelivery)
            if (string.Equals(model.DeliveryMethod, "StorePickup", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(model.ShippingMethod, "StorePickup", StringComparison.OrdinalIgnoreCase))
            {
                model.DeliveryMethod = "StorePickup";
                model.ShippingFee = 0;
                model.ShippingCarrier = "store_pickup";
                model.ShippingCarrierName = "Nhận tại Showroom KittyPhone";
                model.ShippingServiceType = "Nhận tại Showroom 123 Thái Hà";
                if (string.IsNullOrWhiteSpace(model.Address))
                {
                    model.Address = "Showroom KittyPhone - 123 Thái Hà, Đống Đa, Hà Nội";
                }
                ModelState.Remove(nameof(model.Address));
            }

            model.TotalAmount = Math.Max(0, model.SubTotal - model.DiscountAmount) + model.ShippingFee;

            if (!ModelState.IsValid)
            {
                model.AvailablePromotions = _promotionRepo.GetActivePromotions();
                return View(model);
            }

            int invoiceId = _orderRepo.CreateOnlineOrder(model, out string errorMessage);
            if (invoiceId > 0)
            {
                // CHỈ XÓA CÁC SẢN PHẨM ĐÃ THANH TOÁN KHỎI GIỎ HÀNG
                // Các sản phẩm không được chọn VẪN NẰM NGUYÊN TRONG GIỎ HÀNG!
                if (!model.SingleProductId.HasValue)
                {
                    var purchasedIds = model.CartItems.Select(c => c.ProductId).ToHashSet();
                    cart.RemoveAll(c => purchasedIds.Contains(c.ProductId));
                    SaveCart(cart);
                }
                return RedirectToAction("Confirmation", new { id = invoiceId });
            }
            else
            {
                ModelState.AddModelError(string.Empty, "Đặt hàng thất bại: " + errorMessage);
                model.AvailablePromotions = _promotionRepo.GetActivePromotions();
                return View(model);
            }
        }

        [HttpGet]
        public IActionResult CalculateDiscount(int promotionId, decimal subTotal)
        {
            var promo = _promotionRepo.GetPromotionById(promotionId);
            if (promo == null)
            {
                return Json(new { success = false, message = "Chương trình khuyến mãi không tồn tại." });
            }

            int? customerRankId = null;
            if (User.Identity != null && User.Identity.IsAuthenticated && User.IsInRole("Customer"))
            {
                var custIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (int.TryParse(custIdClaim, out int cid))
                {
                    var cust = _customerRepo.GetCustomerById(cid);
                    customerRankId = cust?.RankId;
                }
            }

            if (!promo.CanCustomerApply(customerRankId, out string failureReason))
            {
                return Json(new { success = false, message = failureReason });
            }

            decimal discountAmount = Math.Round(subTotal * promo.DiscountPercent / 100m / 1000m) * 1000m;
            decimal totalAmount = Math.Max(0, subTotal - discountAmount);

            return Json(new
            {
                success = true,
                promotionId = promo.PromotionId,
                promotionName = promo.PromotionName,
                discountPercent = promo.DiscountPercent,
                discountAmount,
                totalAmount
            });
        }

        /// <summary>
        /// Tính toán cước vận chuyển tự động theo khoảng cách và phương thức nhận hàng (AJAX).
        /// Bắt ngoại lệ an toàn và trả về phí vận chuyển + tổng tiền thanh toán sau khi cộng phí.
        /// </summary>
        [HttpPost]
        public IActionResult CalculateShipping([FromBody] ShippingCalculationRequest? request)
        {
            try
            {
                request ??= new ShippingCalculationRequest();
                string method = !string.IsNullOrWhiteSpace(request.ShippingMethod)
                    ? request.ShippingMethod
                    : (!string.IsNullOrWhiteSpace(request.DeliveryMethod) ? request.DeliveryMethod : "HomeDelivery");

                // Khi chọn "Lấy tại cửa hàng": Cước phí = 0đ
                if (method.Equals("StorePickup", StringComparison.OrdinalIgnoreCase))
                {
                    decimal grandTotal = Math.Max(0, request.SubTotal - request.DiscountAmount);
                    return Json(new
                    {
                        success = true,
                        deliveryMethod = "StorePickup",
                        shippingFee = 0m,
                        formattedShippingFee = "0đ (Tại cửa hàng)",
                        subTotal = request.SubTotal,
                        discountAmount = request.DiscountAmount,
                        totalAmount = grandTotal,
                        formattedTotalAmount = grandTotal.ToString("N0") + "đ",
                        carrier = "store_pickup",
                        carrierName = "Nhận tại Showroom KittyPhone",
                        serviceType = "Nhận tại Showroom 123 Thái Hà",
                        options = new List<object>()
                    });
                }

                // Khi chọn "Giao hàng tận nhà": Gọi IShippingService tính toán theo khoảng cách
                decimal netValue = Math.Max(0, request.SubTotal - request.DiscountAmount);
                var quoteReq = new ShippingQuoteRequest
                {
                    DestinationProvince = string.IsNullOrWhiteSpace(request.Province) ? "Hà Nội" : request.Province,
                    DestinationDistrict = request.District,
                    DestinationAddress = request.Address,
                    DistanceKm = request.DistanceKm > 0 ? request.DistanceKm : 5.0m,
                    DeclaredValue = netValue > 0 ? netValue : 15000000m
                };

                var quoteResult = _shippingService.CalculateShipping(quoteReq);
                var feasibleCarriers = quoteResult.AvailableCarriers.Where(c => c.IsFeasible).ToList();

                var selectedCarrier = feasibleCarriers.FirstOrDefault(c => !string.IsNullOrEmpty(request.CarrierId) && c.CarrierId.Equals(request.CarrierId, StringComparison.OrdinalIgnoreCase))
                                      ?? quoteResult.BestCarrier
                                      ?? feasibleCarriers.FirstOrDefault();

                decimal shippingFee = selectedCarrier?.FinalFee ?? 30000m;
                decimal totalWithShipping = Math.Max(0, request.SubTotal - request.DiscountAmount) + shippingFee;

                return Json(new
                {
                    success = true,
                    deliveryMethod = "HomeDelivery",
                    distanceKm = quoteResult.Request.DistanceKm,
                    shippingFee = shippingFee,
                    formattedShippingFee = shippingFee == 0 ? "0đ (Freeship)" : shippingFee.ToString("N0") + "đ",
                    subTotal = request.SubTotal,
                    discountAmount = request.DiscountAmount,
                    totalAmount = totalWithShipping,
                    formattedTotalAmount = totalWithShipping.ToString("N0") + "đ",
                    selectedCarrier = selectedCarrier?.CarrierId ?? "xanhsm",
                    selectedCarrierName = selectedCarrier?.CarrierName ?? "Giao hàng tiêu chuẩn",
                    serviceType = selectedCarrier?.ServiceName ?? "Hỏa tốc",
                    options = feasibleCarriers.Select(c => new
                    {
                        carrierCode = c.CarrierId,
                        carrierName = c.CarrierName,
                        serviceType = c.ServiceName,
                        estimatedTime = c.EstimatedDeliveryTime,
                        finalFee = c.FinalFee,
                        storeSubsidy = c.StoreSubsidy,
                        isRecommended = c.IsRecommended
                    })
                });
            }
            catch (Exception ex)
            {
                // Bắt ngoại lệ an toàn, fallback về cước chuẩn
                decimal fallbackFee = 30000m;
                decimal grandTotal = Math.Max(0, (request?.SubTotal ?? 0) - (request?.DiscountAmount ?? 0)) + fallbackFee;
                return Json(new
                {
                    success = false,
                    message = "Không thể tính cước tự động: " + ex.Message,
                    shippingFee = fallbackFee,
                    formattedShippingFee = fallbackFee.ToString("N0") + "đ",
                    totalAmount = grandTotal,
                    formattedTotalAmount = grandTotal.ToString("N0") + "đ"
                });
            }
        }

        public IActionResult Confirmation(int id)
        {
            var invoice = _orderRepo.GetOrderById(id);
            if (invoice == null)
            {
                return NotFound();
            }

            var vm = new OrderConfirmationViewModel
            {
                Invoice = invoice,
                BankName = "ZaloPay",
                AccountNumber = "0396166509",
                AccountHolder = "DUONG NGOC ANH"
            };

            if (invoice.IsInstallment && !string.IsNullOrEmpty(invoice.PaymentMethodSelected))
            {
                try
                {
                    var match = System.Text.RegularExpressions.Regex.Match(invoice.PaymentMethodSelected, @"(\d+)%\s*-\s*(\d+)\s*tháng");
                    if (match.Success)
                    {
                        vm.PrepayPercent = int.Parse(match.Groups[1].Value);
                        vm.Months = int.Parse(match.Groups[2].Value);
                    }
                }
                catch { }

                vm.PrepayAmount = Math.Round(invoice.TotalAmount * vm.PrepayPercent / 100m / 10000m) * 10000m;
                vm.MonthlyAmount = vm.Months > 0 ? Math.Round((invoice.TotalAmount - vm.PrepayAmount) / vm.Months / 1000m) * 1000m : 0;
            }

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ConfirmDeposit(int invoiceId)
        {
            if (_orderRepo.CustomerConfirmDeposit(invoiceId, out string error))
            {
                TempData["SuccessMessage"] = "Quý khách đã xác nhận chuyển cọc thành công! Đơn hàng đã chuyển sang trạng thái ĐẶT HÀNG THÀNH CÔNG.";
            }
            else
            {
                TempData["ErrorMessage"] = error;
            }
            return RedirectToAction("Confirmation", new { id = invoiceId });
        }
    }
}
