using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BTL_PTTKYC.Data;
using BTL_PTTKYC.Models;

namespace BTL_PTTKYC.Controllers
{
    [Authorize(Roles = "Warehouse,Admin")]
    public class WarehouseController : Controller
    {
        private readonly WarehouseRepository _warehouseRepo;
        private readonly ProductRepository _productRepo;

        public WarehouseController(WarehouseRepository warehouseRepo, ProductRepository productRepo)
        {
            _warehouseRepo = warehouseRepo;
            _productRepo = productRepo;
        }

        // 1. LIST OF IMPORT RECEIPTS
        public IActionResult Index()
        {
            var imports = _warehouseRepo.GetAllImports();
            return View(imports);
        }

        public IActionResult ImportDetail(int id)
        {
            var receipt = _warehouseRepo.GetImportById(id);
            if (receipt == null) return NotFound();
            return View(receipt);
        }

        // 2. CREATE NEW IMPORT RECEIPT
        [HttpGet]
        public IActionResult CreateImport(int? productId = null, int? quantity = null, decimal? price = null)
        {
            var vm = new CreateImportViewModel
            {
                Suppliers = _warehouseRepo.GetAllSuppliers(),
                WarehouseEmployees = _warehouseRepo.GetWarehouseEmployees(),
                AvailableProducts = _productRepo.GetAllProducts(includeInactive: true)
            };
            ViewBag.Brands = _productRepo.GetBrands();
            ViewBag.Categories = _productRepo.GetCategories();

            if (productId.HasValue)
            {
                var prod = vm.AvailableProducts.FirstOrDefault(p => p.ProductId == productId.Value);
                if (prod != null)
                {
                    ViewBag.SelectedProductId = prod.ProductId;
                    ViewBag.SelectedProduct = prod;
                    ViewBag.DefaultQuantity = quantity ?? 10;
                    ViewBag.DefaultPrice = price ?? (prod.ImportPrice > 0 ? prod.ImportPrice : 1000000m);

                    // Tự động tìm nhà cung cấp có tên trùng/chứa tên hãng máy
                    if (!string.IsNullOrEmpty(prod.BrandName) && vm.Suppliers.Any())
                    {
                        var matchingSupplier = vm.Suppliers.FirstOrDefault(s => s.SupplierName.Contains(prod.BrandName, StringComparison.OrdinalIgnoreCase))
                                              ?? vm.Suppliers.FirstOrDefault();
                        if (matchingSupplier != null)
                        {
                            ViewBag.SelectedSupplierId = matchingSupplier.SupplierId;
                        }
                    }
                }
            }

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CreateImport(int supplierId, int employeeId, List<int> productIds, List<int> quantities, List<decimal> prices)
        {
            if (productIds == null || productIds.Count == 0)
            {
                TempData["Error"] = "Vui lòng chọn ít nhất một sản phẩm để nhập kho.";
                return RedirectToAction("CreateImport");
            }

            var items = new List<ImportDetailItemInput>();
            decimal totalAmount = 0;

            for (int i = 0; i < productIds.Count; i++)
            {
                if (quantities == null || i >= quantities.Count || quantities[i] <= 0)
                {
                    TempData["Error"] = "Số lượng nhập của mỗi sản phẩm phải lớn hơn 0.";
                    return RedirectToAction("CreateImport");
                }

                if (prices == null || i >= prices.Count || prices[i] <= 0)
                {
                    TempData["Error"] = "Lỗi nghiệp vụ: Đơn giá nhập của từng sản phẩm bắt buộc phải lớn hơn 0đ. Hệ thống không cho phép nhập sản phẩm với giá 0đ!";
                    return RedirectToAction("CreateImport");
                }

                items.Add(new ImportDetailItemInput
                {
                    ProductId = productIds[i],
                    Quantity = quantities[i],
                    ImportPrice = prices[i]
                });

                totalAmount += quantities[i] * prices[i];
            }

            if (items.Count == 0 || totalAmount <= 0)
            {
                TempData["Error"] = "Lỗi nghiệp vụ: Tổng giá trị phiếu nhập không thể bằng 0đ!";
                return RedirectToAction("CreateImport");
            }

            int importId = _warehouseRepo.CreateImportReceipt(supplierId, employeeId, items, out string error);
            if (importId > 0)
            {
                TempData["Success"] = $"Lập phiếu nhập kho #{importId} thành công (Tổng giá trị: {totalAmount:N0}đ)! Số lượng tồn kho đã tự động cập nhật.";
                return RedirectToAction("ImportDetail", new { id = importId });
            }

            TempData["Error"] = "Lỗi khi lập phiếu nhập: " + error;
            return RedirectToAction("CreateImport");
        }

        // AJAX: Thêm nhanh sản phẩm mới hoàn toàn ngay khi đang lập phiếu nhập kho
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CreateProductAjax([FromBody] ProductViewModel model)
        {
            if (string.IsNullOrWhiteSpace(model.ProductName))
            {
                return Json(new { success = false, message = "Vui lòng nhập tên sản phẩm mới." });
            }
            if (model.ImportPrice <= 0)
            {
                return Json(new { success = false, message = "Đơn giá nhập phải lớn hơn 0đ (không được là 0đ)." });
            }
            if (model.SellingPrice <= 0)
            {
                return Json(new { success = false, message = "Giá bán niêm yết phải lớn hơn 0đ." });
            }
            if (model.SellingPrice < model.ImportPrice)
            {
                return Json(new { success = false, message = "Giá bán niêm yết không được nhỏ hơn giá nhập kho." });
            }

            try
            {
                model.IsActive = true;
                int newId = _productRepo.CreateProduct(model);
                var brands = _productRepo.GetBrands();
                var brandName = brands.FirstOrDefault(b => b.id == model.BrandId).name ?? "";

                return Json(new
                {
                    success = true,
                    productId = newId,
                    productName = model.ProductName,
                    brandName = brandName,
                    importPrice = model.ImportPrice,
                    sellingPrice = model.SellingPrice
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi khi lưu sản phẩm mới: " + ex.Message });
            }
        }

        // 3. INVENTORY & LOW STOCK WARNING
        public IActionResult Inventory()
        {
            var products = _productRepo.GetAllProducts(includeInactive: true);
            var lowStock = _productRepo.GetLowStockProducts();
            ViewBag.LowStock = lowStock;
            return View(products);
        }

        // 4. SERIAL / IMEI TRACKING
        public IActionResult Serials()
        {
            var serials = _warehouseRepo.GetSerials();
            ViewBag.Products = _productRepo.GetAllProducts();
            return View(serials);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AddSerial(int productId, string imeiNumber, string serialNumber, int warehouseId = 1)
        {
            if (string.IsNullOrWhiteSpace(imeiNumber) && string.IsNullOrWhiteSpace(serialNumber))
            {
                TempData["Error"] = "Vui lòng nhập IMEI hoặc Serial Number.";
                return RedirectToAction("Serials");
            }

            bool success = _warehouseRepo.AddSerial(productId, imeiNumber, serialNumber, warehouseId, out string error);
            if (success)
            {
                TempData["Success"] = "Đã thêm mã máy vật lý (IMEI/Serial) thành công!";
            }
            else
            {
                TempData["Error"] = "Lỗi khi thêm IMEI/Serial: " + error;
            }

            return RedirectToAction("Serials");
        }

        // 4.1. IMPORT BATCH SERIALS / IMEI VIA CSV/EXCEL (Feature 7)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ImportSerialsCsv(IFormFile csvFile, int warehouseId = 1)
        {
            if (csvFile == null || csvFile.Length == 0)
            {
                TempData["Error"] = "Vui lòng chọn file CSV chứa danh sách IMEI/Serial hợp lệ.";
                return RedirectToAction("Serials");
            }

            int successCount = 0;
            int errorCount = 0;

            try
            {
                using var reader = new System.IO.StreamReader(csvFile.OpenReadStream(), System.Text.Encoding.UTF8);
                string? line;
                bool isHeader = true;

                while ((line = await reader.ReadLineAsync()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    if (isHeader)
                    {
                        isHeader = false;
                        // Skip header row if it contains column names
                        if (line.Contains("ProductId", StringComparison.OrdinalIgnoreCase) || 
                            line.Contains("IMEI", StringComparison.OrdinalIgnoreCase) || 
                            line.Contains("Mã", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }
                    }

                    var parts = line.Split(',', ';');
                    if (parts.Length >= 2)
                    {
                        if (int.TryParse(parts[0].Trim(), out int prodId))
                        {
                            string imei = parts.Length > 1 ? parts[1].Trim() : "";
                            string serial = parts.Length > 2 ? parts[2].Trim() : "";

                            if (!string.IsNullOrEmpty(imei) || !string.IsNullOrEmpty(serial))
                            {
                                if (_warehouseRepo.AddSerial(prodId, imei, serial, warehouseId, out _))
                                {
                                    successCount++;
                                }
                                else
                                {
                                    errorCount++;
                                }
                            }
                        }
                    }
                }

                if (successCount > 0)
                {
                    TempData["Success"] = $"Nhập hàng loạt thành công: {successCount} mã IMEI/Serial vào kho! {(errorCount > 0 ? $"({errorCount} mã bị trùng hoặc lỗi)" : "")}";
                }
                else
                {
                    TempData["Error"] = $"Không nhập được mã nào. Vui lòng kiểm tra định dạng file CSV mẫu.";
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Lỗi khi xử lý file CSV: " + ex.Message;
            }

            return RedirectToAction("Serials");
        }

        // 4.2. EXPORT SERIALS / IMEI TO CSV (Feature 7)
        [HttpGet]
        public IActionResult ExportSerialsCsv()
        {
            var serials = _warehouseRepo.GetSerials();
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("SerialId,ProductId,Tên Sản Phẩm,IMEI,Số Serial,Kho Hàng,Trạng Thái");

            foreach (var s in serials)
            {
                sb.AppendLine($"{s.SerialId},{s.ProductId},\"{s.ProductName}\",{s.ImeiNumber},{s.SerialNumber},\"{s.WarehouseName}\",{s.Status}");
            }

            var preamble = System.Text.Encoding.UTF8.GetPreamble();
            var bytes = preamble.Concat(System.Text.Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
            return File(bytes, "text/csv; charset=utf-8", $"DanhSach_IMEI_KittyPhone_{DateTime.Now:yyyyMMdd_HHmm}.csv");
        }

        // 4.3. DOWNLOAD CSV TEMPLATE (Feature 7)
        [HttpGet]
        public IActionResult DownloadSerialTemplate()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("ProductId,IMEI,SerialNumber");
            sb.AppendLine("1,356789123456781,APL-IP16-DEMO01");
            sb.AppendLine("1,356789123456782,APL-IP16-DEMO02");
            sb.AppendLine("13,356789123456783,APL-IP16PM-DEMO03");

            var preamble = System.Text.Encoding.UTF8.GetPreamble();
            var bytes = preamble.Concat(System.Text.Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
            return File(bytes, "text/csv; charset=utf-8", "Mau_Import_IMEI_KittyPhone.csv");
        }

        // 5. STOCK MOVEMENTS HISTORY
        public IActionResult Movements()
        {
            var movements = _warehouseRepo.GetStockMovements();
            return View(movements);
        }
    }
}
