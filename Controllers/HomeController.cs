using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using BTL_PTTKYC.Data;
using BTL_PTTKYC.Models;

namespace BTL_PTTKYC.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ProductRepository _productRepo;
        private readonly ReviewRepository _reviewRepo;
        private readonly PromotionRepository _promotionRepo;

        public HomeController(ILogger<HomeController> logger, ProductRepository productRepo, ReviewRepository reviewRepo, PromotionRepository promotionRepo)
        {
            _logger = logger;
            _productRepo = productRepo;
            _reviewRepo = reviewRepo;
            _promotionRepo = promotionRepo;
        }

        public IActionResult Index(string? keyword = null, int? brandId = null, int? categoryId = null, string? series = null, string? priceRange = null, string? sort = null)
        {
            try
            {
                if (categoryId == 5) categoryId = 3;

                var products = _productRepo.GetAllProducts(keyword, brandId, categoryId, false, series, priceRange, sort);
                ViewBag.Brands = _productRepo.GetBrands();
                ViewBag.Categories = _productRepo.GetCategories();
                ViewBag.Promotions = _promotionRepo.GetActivePromotions();
                ViewBag.CurrentKeyword = keyword;
                ViewBag.CurrentBrandId = brandId;
                ViewBag.CurrentCategoryId = categoryId;
                ViewBag.CurrentSeries = series;
                ViewBag.CurrentPriceRange = priceRange;
                ViewBag.CurrentSort = sort ?? "hot";

                // Dữ liệu cho các modal tương tác Hero: Đặt cọc, Deal Hot, Thu cũ đổi mới
                var allFlagships = _productRepo.GetAllProducts(series: "iPhone");
                ViewBag.PreorderDuo = allFlagships.Where(p => p.ProductName.Contains("iPhone Duo", StringComparison.OrdinalIgnoreCase)).ToList();
                ViewBag.Preorder18 = allFlagships.Where(p => p.ProductName.Contains("iPhone 18", StringComparison.OrdinalIgnoreCase)).ToList();
                ViewBag.HotDeals = _productRepo.GetAllProducts(sort: "discount").Take(6).ToList();
                ViewBag.TradeInProducts = _productRepo.GetAllProducts().Where(p => 
                    p.ProductName.Contains("iPhone 18") || 
                    p.ProductName.Contains("iPhone Duo") || 
                    p.ProductName.Contains("Galaxy S25") || 
                    p.ProductName.Contains("iPad Pro")
                ).Take(6).ToList();

                return View(products);
            }
            catch (Exception ex)
            {
                ViewBag.ErrorMessage = "Lỗi truy vấn CSDL: " + ex.Message;
                return View(new List<ProductViewModel>());
            }
        }

        public IActionResult Detail(int id)
        {
            var product = _productRepo.GetProductById(id);
            if (product == null)
            {
                return NotFound();
            }

            var reviews = _reviewRepo.GetReviewsByProductId(id);
            ViewBag.Reviews = reviews;

            // Kiểm tra trạng thái đăng nhập và quyền đánh giá (Chống spam: chỉ khách đã mua mới được đánh giá)
            bool isCustomerLoggedIn = false;
            bool canReview = false;
            string? customerName = null;
            string? customerPhone = null;

            if (User.Identity != null && User.Identity.IsAuthenticated && User.IsInRole("Customer"))
            {
                isCustomerLoggedIn = true;
                var custIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (int.TryParse(custIdClaim, out int customerId))
                {
                    canReview = _reviewRepo.CheckCustomerPurchasedProduct(customerId, id, out _);
                    customerName = User.Identity.Name;
                    customerPhone = User.FindFirst(System.Security.Claims.ClaimTypes.MobilePhone)?.Value;
                }
            }

            ViewBag.IsCustomerLoggedIn = isCustomerLoggedIn;
            ViewBag.CanReview = canReview;
            ViewBag.CustomerName = customerName;
            ViewBag.CustomerPhone = customerPhone;

            // Gợi ý phụ kiện mua kèm (Smart Cross-selling)
            ViewBag.Accessories = _productRepo.GetAllProducts(categoryId: 3).Take(4).ToList();

            // Related products in the same brand or category
            var related = _productRepo.GetAllProducts(brandId: product.BrandId)
                .Where(p => p.ProductId != id)
                .Take(4)
                .ToList();
            ViewBag.RelatedProducts = related;
            ViewBag.Promotions = _promotionRepo.GetActivePromotions();

            return View(product);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AddReview(ReviewModel model)
        {
            // Ràng buộc bảo mật chống spam: Bắt buộc đăng nhập tài khoản khách hàng
            if (User.Identity == null || !User.Identity.IsAuthenticated || !User.IsInRole("Customer"))
            {
                TempData["ReviewError"] = "Vui lòng đăng nhập tài khoản khách hàng đã mua sản phẩm này để gửi đánh giá.";
                return RedirectToAction("Detail", new { id = model.ProductId });
            }

            var custIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(custIdClaim, out int customerId))
            {
                TempData["ReviewError"] = "Không tìm thấy thông tin tài khoản hợp lệ. Vui lòng đăng nhập lại.";
                return RedirectToAction("Detail", new { id = model.ProductId });
            }

            if (!ModelState.IsValid)
            {
                TempData["ReviewError"] = "Vui lòng chọn số sao (1-5) và nhập nội dung đánh giá chi tiết.";
                return RedirectToAction("Detail", new { id = model.ProductId });
            }

            // Tự động gán thông tin chuẩn từ tài khoản đã xác thực để tránh mạo danh
            model.CustomerName = User.Identity.Name ?? model.CustomerName;
            model.Phone = User.FindFirst(System.Security.Claims.ClaimTypes.MobilePhone)?.Value ?? model.Phone;

            if (_reviewRepo.AddVerifiedReview(model, customerId, out string errorMessage))
            {
                TempData["ReviewSuccess"] = "Cảm ơn bạn đã gửi đánh giá! Đánh giá đã được gắn huy hiệu [Đã Mua Hàng] và hiển thị trên hệ thống.";
            }
            else
            {
                TempData["ReviewError"] = errorMessage;
            }

            return RedirectToAction("Detail", new { id = model.ProductId });
        }

        public IActionResult Policy(string? tab = null)
        {
            ViewBag.ActiveTab = string.IsNullOrEmpty(tab) ? "online-payment" : tab.ToLower();
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}