using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BTL_PTTKYC.Data;
using BTL_PTTKYC.Models;

namespace BTL_PTTKYC.Controllers
{
    [Authorize(Roles = "Sales,Admin")]
    public class SalesController : Controller
    {
        private readonly OrderRepository _orderRepo;
        private readonly ReviewRepository _reviewRepo;
        private readonly ProductRepository _productRepo;
        private readonly PromotionRepository _promotionRepo;

        public SalesController(OrderRepository orderRepo, ReviewRepository reviewRepo, ProductRepository productRepo, PromotionRepository promotionRepo)
        {
            _orderRepo = orderRepo;
            _reviewRepo = reviewRepo;
            _productRepo = productRepo;
            _promotionRepo = promotionRepo;
        }

        // 1. ORDERS
        public IActionResult Orders(string? status = null, string? keyword = null)
        {
            ViewBag.CurrentStatus = status;
            ViewBag.CurrentKeyword = keyword;
            var orders = _orderRepo.GetOrders(status, keyword);
            return View(orders);
        }

        public IActionResult OrderDetail(int id)
        {
            var order = _orderRepo.GetOrderById(id);
            if (order == null)
            {
                return NotFound();
            }
            return View(order);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ConfirmPayment(int invoiceId, decimal amount, string paymentMethod = "Bank Transfer")
        {
            if (amount <= 0)
            {
                TempData["Error"] = "Số tiền xác nhận phải lớn hơn 0.";
                return RedirectToAction("OrderDetail", new { id = invoiceId });
            }

            var order = _orderRepo.GetOrderById(invoiceId);
            if (order == null)
            {
                TempData["Error"] = "Đơn hàng không tồn tại.";
                return RedirectToAction("OrderDetail", new { id = invoiceId });
            }

            // Chống tạo giao dịch thanh toán trùng lặp khi đơn đã thanh toán đủ 100%
            if (order.IsFullyPaid || order.RemainingAmount <= 0)
            {
                TempData["Error"] = "Đơn hàng này đã được thanh toán đủ 100%, không thể tạo thêm giao dịch thu tiền để tránh trùng lặp dòng tiền.";
                return RedirectToAction("OrderDetail", new { id = invoiceId });
            }

            bool success = _orderRepo.ConfirmPayment(invoiceId, amount, paymentMethod, out string error);
            if (success)
            {
                TempData["Success"] = $"Xác nhận thanh toán thành công {amount:N0} VNĐ cho đơn #{invoiceId}.";
            }
            else
            {
                TempData["Error"] = "Lỗi khi xác nhận thanh toán: " + error;
            }

            return RedirectToAction("OrderDetail", new { id = invoiceId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult UpdateShippingStatus(int invoiceId, string newStatus)
        {
            if (string.IsNullOrWhiteSpace(newStatus))
            {
                TempData["Error"] = "Trạng thái mới không hợp lệ.";
                return RedirectToAction("OrderDetail", new { id = invoiceId });
            }

            if (_orderRepo.UpdateOrderStatus(invoiceId, newStatus, out string error))
            {
                string statusText = newStatus switch
                {
                    "Shipping" => "Đang giao hàng (Shipping)",
                    "Delivered" => "Đã giao hàng thành công (Delivered)",
                    "Paid" => "Đã thanh toán (Paid)",
                    _ => newStatus
                };
                TempData["Success"] = $"Đã cập nhật trạng thái đơn hàng #{invoiceId} sang [{statusText}] thành công!";
            }
            else
            {
                TempData["Error"] = "Lỗi khi cập nhật trạng thái vận chuyển: " + error;
            }

            return RedirectToAction("OrderDetail", new { id = invoiceId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AdminConfirmDeposit(int invoiceId)
        {
            var order = _orderRepo.GetOrderById(invoiceId);
            if (order == null)
            {
                TempData["Error"] = "Đơn hàng không tồn tại.";
                return RedirectToAction("OrderDetail", new { id = invoiceId });
            }

            decimal depositAmount = Math.Round(order.TotalAmount * 0.5m / 1000m) * 1000m;
            decimal needed = Math.Min(depositAmount, order.RemainingAmount);
            if (needed <= 0)
            {
                TempData["Error"] = "Đơn hàng này đã được xác nhận đủ số tiền cọc hoặc đã thanh toán 100%.";
                return RedirectToAction("OrderDetail", new { id = invoiceId });
            }

            if (_orderRepo.ConfirmPayment(invoiceId, needed, "Bank Transfer", out string error))
            {
                TempData["Success"] = $"Đã duyệt xác nhận nhận cọc {needed:N0}đ (50%) cho đơn hàng #{invoiceId} thành công!";
            }
            else
            {
                TempData["Error"] = "Lỗi xác nhận cọc: " + error;
            }
            return RedirectToAction("OrderDetail", new { id = invoiceId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AdminConfirmFullPayment(int invoiceId)
        {
            if (_orderRepo.AdminConfirmFullPayment(invoiceId, out string error))
            {
                TempData["Success"] = $"Admin đã xác nhận nhận đủ 100% tiền chuyển khoản cho đơn #{invoiceId} -> Đơn hàng chuyển sang Paid (Đã thanh toán)!";
            }
            else
            {
                TempData["Error"] = "Lỗi xác nhận thanh toán 100%: " + error;
            }
            return RedirectToAction("OrderDetail", new { id = invoiceId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CancelOrder(int invoiceId, string cancelReason)
        {
            if (string.IsNullOrWhiteSpace(cancelReason))
            {
                TempData["Error"] = "Vui lòng nhập lý do hủy đơn chi tiết để gửi Quản trị viên (Admin) phê duyệt.";
                return RedirectToAction("OrderDetail", new { id = invoiceId });
            }

            string requester = User.Identity?.Name ?? "Nhân viên Sales";
            bool success = _orderRepo.RequestCancelOrder(invoiceId, cancelReason, requester, out string error);
            if (success)
            {
                TempData["Success"] = $"Đã gửi yêu cầu hủy đơn hàng #{invoiceId} kèm lý do chi tiết. Yêu cầu đang chờ Quản trị viên (Admin) phê duyệt để đảm bảo tính minh bạch và chống gian lận.";
            }
            else
            {
                TempData["Error"] = "Lỗi khi gửi yêu cầu hủy đơn: " + error;
            }

            return RedirectToAction("OrderDetail", new { id = invoiceId });
        }

        // 2. REVIEWS
        public IActionResult Reviews(int? productId = null, int? rating = null)
        {
            ViewBag.Products = _productRepo.GetAllProducts();
            ViewBag.SelectedProductId = productId;
            ViewBag.SelectedRating = rating;
            var reviews = _reviewRepo.GetAllReviews(productId, rating);
            return View(reviews);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ReplyReview(int reviewId, string reply)
        {
            if (string.IsNullOrWhiteSpace(reply))
            {
                TempData["Error"] = "Nội dung phản hồi không được để trống.";
            }
            else
            {
                _reviewRepo.ReplyReview(reviewId, reply.Trim());
                TempData["Success"] = "Đã gửi phản hồi thành công!";
            }

            return RedirectToAction("Reviews");
        }

        // 3. SELLING CONTENT / PRODUCTS
        public IActionResult Products()
        {
            var products = _productRepo.GetAllProducts(includeInactive: true);
            return View(products);
        }

        [HttpGet]
        public IActionResult EditProduct(int id)
        {
            var product = _productRepo.GetProductById(id);
            if (product == null) return NotFound();

            ViewBag.Brands = _productRepo.GetBrands();
            ViewBag.Categories = _productRepo.GetCategories();
            return View(product);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult EditProduct(ProductViewModel model)
        {
            if (model.SellingPrice <= 0)
            {
                ModelState.AddModelError("SellingPrice", "Đơn giá bán niêm yết phải lớn hơn 0đ.");
            }
            else if (model.SellingPrice < model.ImportPrice)
            {
                ModelState.AddModelError("SellingPrice", "Giá bán niêm yết không được thấp hơn giá nhập kho.");
            }

            if (!ModelState.IsValid)
            {
                ViewBag.Brands = _productRepo.GetBrands();
                ViewBag.Categories = _productRepo.GetCategories();
                return View(model);
            }

            _productRepo.UpdateProduct(model);
            TempData["Success"] = $"Đã cập nhật thông tin sản phẩm \"{model.ProductName}\" thành công!";
            return RedirectToAction("Products");
        }

        // 5. PROMOTIONS
        public IActionResult Promotions()
        {
            var list = _promotionRepo.GetAllPromotions();
            return View(list);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CreatePromotion(PromotionModel model)
        {
            if (string.IsNullOrWhiteSpace(model.PromotionName))
            {
                TempData["Error"] = "Vui lòng nhập tên chương trình khuyến mãi.";
                return RedirectToAction("Promotions");
            }

            if (model.DiscountPercent <= 0 || model.DiscountPercent > 100)
            {
                TempData["Error"] = "Phần trăm giảm giá phải từ 1% đến 100%.";
                return RedirectToAction("Promotions");
            }

            if (model.EndDate < model.StartDate)
            {
                TempData["Error"] = "Ngày kết thúc không được nhỏ hơn ngày bắt đầu.";
                return RedirectToAction("Promotions");
            }

            bool success = _promotionRepo.CreatePromotion(model, out string error);
            if (success)
            {
                TempData["Success"] = $"Đã tạo thành công chương trình khuyến mãi: \"{model.PromotionName}\" (-{model.DiscountPercent}%)!";
            }
            else
            {
                TempData["Error"] = "Lỗi tạo khuyến mãi: " + error;
            }

            return RedirectToAction("Promotions");
        }
    }
}
