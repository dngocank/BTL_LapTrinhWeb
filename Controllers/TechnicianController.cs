using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BTL_PTTKYC.Data;
using BTL_PTTKYC.Models;
using BTL_PTTKYC.Services;
using System.Security.Claims;

namespace BTL_PTTKYC.Controllers
{
    [Authorize(Roles = "Admin,Technician")]
    public class TechnicianController : Controller
    {
        private readonly WarrantyRepository _warrantyRepo;
        private readonly AccountRepository _accountRepo;
        private readonly INotificationService _notificationService;

        public TechnicianController(WarrantyRepository warrantyRepo, AccountRepository accountRepo, INotificationService notificationService)
        {
            _warrantyRepo = warrantyRepo;
            _accountRepo = accountRepo;
            _notificationService = notificationService;
        }

        // 1. DASHBOARD OVERVIEW
        public IActionResult Index()
        {
            var stats = _warrantyRepo.GetDashboardStats();
            return View(stats);
        }

        // 1b. ALL-IN-ONE TECHNICIAN WORKBENCH (BÀN LÀM VIỆC KỸ THUẬT TOÀN DIỆN)
        public IActionResult Workbench()
        {
            int? currentTechId = GetCurrentEmployeeId();
            var vm = _warrantyRepo.GetWorkbenchData(currentTechId);
            return View(vm);
        }

        // 2. TICKETS LIST WITH FILTER & SEARCH
        public IActionResult Tickets(string? status, string? keyword, int page = 1)
        {
            int pageSize = 12;
            var list = _warrantyRepo.GetTickets(status, keyword, page, pageSize, out int totalCount);

            ViewBag.CurrentStatus = status;
            ViewBag.Keyword = keyword;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling((double)totalCount / pageSize);
            ViewBag.TotalCount = totalCount;

            return View(list);
        }

        // 3. CREATE NEW WARRANTY TICKET
        [HttpGet]
        public IActionResult Create(string? imei = null)
        {
            ViewBag.Technicians = _warrantyRepo.GetAllTechnicians();

            var model = new WarrantyTicketCreateViewModel
            {
                SerialNumberOrImei = imei
            };

            // If IMEI provided, prefill customer info if available
            if (!string.IsNullOrWhiteSpace(imei))
            {
                var lookup = _warrantyRepo.LookupWarranty(imei);
                if (lookup.Found)
                {
                    model.CustomerName = lookup.CustomerName ?? string.Empty;
                    model.CustomerPhone = lookup.CustomerPhone ?? string.Empty;
                    ViewBag.LookupDevice = lookup;
                }
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(WarrantyTicketCreateViewModel model)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Technicians = _warrantyRepo.GetAllTechnicians();
                return View(model);
            }

            int? currentTechId = GetCurrentEmployeeId();

            if (_warrantyRepo.CreateTicket(model, currentTechId, out string ticketCode, out string errorMessage))
            {
                TempData["Success"] = $"Đã lập phiếu bảo hành mới thành công! Mã phiếu: {ticketCode}";
                return RedirectToAction("Workbench");
            }

            ModelState.AddModelError(string.Empty, "Không thể lập phiếu bảo hành: " + errorMessage);
            ViewBag.Technicians = _warrantyRepo.GetAllTechnicians();
            return View(model);
        }

        // 4. DETAIL OF TICKET
        public IActionResult Detail(int id)
        {
            var ticket = _warrantyRepo.GetTicketById(id);
            if (ticket == null)
            {
                TempData["Error"] = "Không tìm thấy phiếu bảo hành yêu cầu.";
                return RedirectToAction("Tickets");
            }

            return View(ticket);
        }

        // 5. PROCESS / UPDATE TICKET
        [HttpGet]
        public IActionResult Process(int id)
        {
            var ticket = _warrantyRepo.GetTicketById(id);
            if (ticket == null)
            {
                TempData["Error"] = "Không tìm thấy phiếu bảo hành.";
                return RedirectToAction("Tickets");
            }

            ViewBag.Technicians = _warrantyRepo.GetAllTechnicians();
            ViewBag.Ticket = ticket;

            var model = new WarrantyTicketUpdateViewModel
            {
                TicketId = ticket.TicketId,
                TicketCode = ticket.TicketCode,
                CustomerName = ticket.CustomerName,
                CustomerPhone = ticket.CustomerPhone,
                ProductName = ticket.ProductName,
                ImeiNumber = ticket.ImeiNumber,
                TechnicianId = ticket.TechnicianId,
                Diagnosis = ticket.Diagnosis,
                RepairAction = ticket.RepairAction,
                RepairCost = ticket.RepairCost,
                Status = ticket.Status,
                Notes = ticket.Notes,
                Components = ticket.Components
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Process(WarrantyTicketUpdateViewModel model)
        {
            var oldTicket = _warrantyRepo.GetTicketById(model.TicketId);

            if (!ModelState.IsValid)
            {
                ViewBag.Technicians = _warrantyRepo.GetAllTechnicians();
                ViewBag.Ticket = oldTicket;
                return View(model);
            }

            if (_warrantyRepo.UpdateTicket(model, out string errorMessage))
            {
                var updatedTicket = _warrantyRepo.GetTicketById(model.TicketId);
                string notifMsg = string.Empty;

                if (updatedTicket != null)
                {
                    // Tự động gửi Email thông báo cập nhật trạng thái tới khách hàng
                    _notificationService.SendWarrantyStatusUpdateEmail(updatedTicket, oldTicket?.Status ?? "Received", model.Status, out notifMsg);
                }

                TempData["Success"] = $"Đã cập nhật tiến độ xử lý phiếu {model.TicketCode} thành công! {(string.IsNullOrEmpty(notifMsg) ? "" : $"[{notifMsg}]")}";
                return RedirectToAction("Detail", new { id = model.TicketId });
            }

            ModelState.AddModelError(string.Empty, "Lỗi khi cập nhật phiếu: " + errorMessage);
            ViewBag.Technicians = _warrantyRepo.GetAllTechnicians();
            ViewBag.Ticket = oldTicket;
            return View(model);
        }

        // 6. DELIVER TICKET (BÀN GIAO MÁY TRẢ KHÁCH)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Deliver(int id, string? notes, string? returnUrl = null)
        {
            var oldTicket = _warrantyRepo.GetTicketById(id);

            if (_warrantyRepo.DeliverTicket(id, notes, out string errorMessage))
            {
                var updatedTicket = _warrantyRepo.GetTicketById(id);
                string notifMsg = string.Empty;
                if (updatedTicket != null)
                {
                    _notificationService.SendWarrantyStatusUpdateEmail(updatedTicket, oldTicket?.Status ?? "Completed", "Delivered", out notifMsg);
                }

                TempData["Success"] = $"Đã hoàn tất bàn giao và trả thiết bị cho khách hàng thành công! {(string.IsNullOrEmpty(notifMsg) ? "" : $"[{notifMsg}]")}";
            }
            else
            {
                TempData["Error"] = "Lỗi khi bàn giao: " + errorMessage;
            }

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction("Workbench");
        }

        // 6b. PROCESS PAYMENT (THANH TOÁN CHI PHÍ BẢO HÀNH / SỬA CHỮA)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ProcessPayment(int ticketId, string paymentMethod, decimal paidAmount, decimal discountAmount, string? voucherCode, string? returnUrl = null)
        {
            if (_warrantyRepo.UpdatePayment(ticketId, paymentMethod, paidAmount, discountAmount, voucherCode, out string errorMessage))
            {
                TempData["Success"] = $"Xác nhận thanh toán thành công {paidAmount:N0}đ qua {paymentMethod} cho phiếu bảo hành!";
            }
            else
            {
                TempData["Error"] = "Lỗi khi cập nhật thanh toán: " + errorMessage;
            }

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction("Workbench");
        }

        // 7. LOOKUP WARRANTY BY IMEI / SERIAL / PHONE (CÔNG KHAI CHO KHÁCH HÀNG TRA CỨU)
        [AllowAnonymous]
        public IActionResult Lookup(string? query, string? keyword)
        {
            var search = !string.IsNullOrWhiteSpace(query) ? query : keyword;
            var res = new WarrantyLookupResultViewModel();
            if (!string.IsNullOrWhiteSpace(search))
            {
                res = _warrantyRepo.LookupWarranty(search);
            }
            return View(res);
        }

        // 8. MASTER WARRANTY CARDS
        public IActionResult WarrantyCards(string? keyword, string? status)
        {
            var list = _warrantyRepo.GetAllWarrantyCards(keyword, status);
            ViewBag.CurrentKeyword = keyword;
            ViewBag.CurrentStatus = status;
            return View(list);
        }

        // 9. PRINT TICKET (PHIẾU BIÊN NHẬN / BẢO HÀNH)
        public IActionResult PrintTicket(int id)
        {
            var ticket = _warrantyRepo.GetTicketById(id);
            if (ticket == null)
            {
                return NotFound();
            }

            return View(ticket);
        }

        private int? GetCurrentEmployeeId()
        {
            var accountIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (int.TryParse(accountIdClaim, out int accId))
            {
                var acc = _accountRepo.GetAccountById(accId);
                return acc?.EmployeeId;
            }
            return null;
        }
    }
}
