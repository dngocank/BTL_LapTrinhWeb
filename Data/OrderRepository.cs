using Microsoft.Data.SqlClient;
using BTL_PTTKYC.Models;
using System.Data;

namespace BTL_PTTKYC.Data
{
    public class OrderRepository
    {
        private readonly string _connectionString;
        public OrderRepository(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")!;
        }

        public int CreateOnlineOrder(CheckoutViewModel model, out string errorMessage)
        {
            errorMessage = string.Empty;
            using var conn = new SqlConnection(_connectionString);
            conn.Open();
            using var trans = conn.BeginTransaction();

            try
            {
                // 1. Find or create customer by ID or phone number
                int customerId = model.CustomerId.GetValueOrDefault();
                if (customerId > 0)
                {
                    using var updateCust = new SqlCommand(
                        "UPDATE Customer SET customer_name = @name, phone = @phone, address = @addr WHERE customer_id = @id", conn, trans);
                    updateCust.Parameters.AddWithValue("@name", model.CustomerName.Trim());
                    updateCust.Parameters.AddWithValue("@phone", model.Phone.Trim());
                    updateCust.Parameters.AddWithValue("@addr", model.Address.Trim());
                    updateCust.Parameters.AddWithValue("@id", customerId);
                    updateCust.ExecuteNonQuery();
                }
                else
                {
                    var findCustSql = "SELECT customer_id FROM Customer WHERE phone = @phone";
                    using (var cmd = new SqlCommand(findCustSql, conn, trans))
                    {
                        cmd.Parameters.AddWithValue("@phone", model.Phone.Trim());
                        var existingId = cmd.ExecuteScalar();
                        if (existingId != null && existingId != DBNull.Value)
                        {
                            customerId = Convert.ToInt32(existingId);
                            // Update latest customer name and address
                            using var updateCust = new SqlCommand(
                                "UPDATE Customer SET customer_name = @name, address = @addr WHERE customer_id = @id", conn, trans);
                            updateCust.Parameters.AddWithValue("@name", model.CustomerName.Trim());
                            updateCust.Parameters.AddWithValue("@addr", model.Address.Trim());
                            updateCust.Parameters.AddWithValue("@id", customerId);
                            updateCust.ExecuteNonQuery();
                        }
                        else
                        {
                            using var insertCust = new SqlCommand(@"
                                INSERT INTO Customer (customer_name, phone, address, rank_id, total_spent, created_at)
                                VALUES (@name, @phone, @addr, 1, 0, GETDATE());
                                SELECT SCOPE_IDENTITY();", conn, trans);
                            insertCust.Parameters.AddWithValue("@name", model.CustomerName.Trim());
                            insertCust.Parameters.AddWithValue("@phone", model.Phone.Trim());
                            insertCust.Parameters.AddWithValue("@addr", model.Address.Trim());
                            customerId = Convert.ToInt32(insertCust.ExecuteScalar());
                        }
                    }
                }

                // 2. Select default sales employee
                int employeeId = 2; // Default to Pham Van Sales
                using (var empCmd = new SqlCommand("SELECT TOP 1 employee_id FROM Employee WHERE position = 'Sales' OR position = 'Manager'", conn, trans))
                {
                    var empObj = empCmd.ExecuteScalar();
                    if (empObj != null && empObj != DBNull.Value)
                    {
                        employeeId = Convert.ToInt32(empObj);
                    }
                }

                // 3. Create Invoice header (status = 'Pending' - Chờ Sale phê duyệt)
                // Tuân thủ 100% ràng buộc CHECK CK_Invoice_Status và Trigger trg_InvoiceDetail_CheckBeforeInsert
                string initialStatus = "Pending";

                string payMethodSelected = model.PaymentMethod;
                if (model.PaymentMethod == "Installment")
                {
                    payMethodSelected = $"Trả góp ({model.InstallmentPrepayPercent}% - {model.InstallmentMonths} tháng)";
                }

                string? orderNotes = model.OrderNotes;
                if (model.PaymentMethod == "Installment")
                {
                    string installmentInfo = $"[Hồ sơ trả góp 0%: Trả trước {model.InstallmentPrepayPercent}% ({model.InstallmentPrepayAmount:N0}đ), kỳ hạn {model.InstallmentMonths} tháng, mỗi tháng góp {model.InstallmentMonthlyAmount:N0}đ]";
                    orderNotes = string.IsNullOrEmpty(orderNotes) ? installmentInfo : $"{installmentInfo}. {orderNotes}";
                }

                int invoiceId = 0;
                var insertInvSql = @"
                    INSERT INTO Invoice (customer_id, employee_id, invoice_date, status, shipping_address, order_notes, payment_method_selected, promotion_id, shipping_carrier, shipping_fee, shipping_distance_km, shipping_service_type)
                    VALUES (@custId, @empId, CAST(GETDATE() AS DATE), @status, @shippingAddr, @notes, @payMethod, @promoId, @carrier, @shipFee, @distKm, @svcType);
                    SELECT SCOPE_IDENTITY();";

                using (var invCmd = new SqlCommand(insertInvSql, conn, trans))
                {
                    invCmd.Parameters.AddWithValue("@custId", customerId);
                    invCmd.Parameters.AddWithValue("@empId", employeeId);
                    var addr = model.Address?.Trim() ?? string.Empty;
                    invCmd.Parameters.AddWithValue("@shippingAddr", addr.Length > 255 ? addr.Substring(0, 255) : addr);
                    invCmd.Parameters.AddWithValue("@status", initialStatus);
                    invCmd.Parameters.AddWithValue("@notes", (object?)orderNotes ?? DBNull.Value);
                    invCmd.Parameters.AddWithValue("@payMethod", payMethodSelected);
                    invCmd.Parameters.AddWithValue("@promoId", model.PromotionId.HasValue && model.PromotionId.Value > 0 ? (object)model.PromotionId.Value : DBNull.Value);
                    
                    var carrierStr = model.ShippingCarrierName ?? model.ShippingCarrier;
                    if (carrierStr != null && carrierStr.Length > 50) carrierStr = carrierStr.Substring(0, 50);
                    invCmd.Parameters.AddWithValue("@carrier", (object?)carrierStr ?? DBNull.Value);
                    
                    invCmd.Parameters.AddWithValue("@shipFee", model.ShippingFee);
                    invCmd.Parameters.AddWithValue("@distKm", model.ShippingDistanceKm);
                    
                    var svcStr = model.ShippingServiceType;
                    if (svcStr != null && svcStr.Length > 50) svcStr = svcStr.Substring(0, 50);
                    invCmd.Parameters.AddWithValue("@svcType", (object?)svcStr ?? DBNull.Value);
                    invoiceId = Convert.ToInt32(invCmd.ExecuteScalar());
                }

                // 4. Insert Invoice Details (triggers check stock and adjust quantity automatically)
                var items = model.CartItems.Count > 0 
                    ? model.CartItems 
                    : (model.SingleProductId.HasValue ? new List<CartItemModel> { 
                        new CartItemModel { ProductId = model.SingleProductId.Value, Quantity = model.SingleQuantity } 
                      } : new List<CartItemModel>());

                if (items.Count == 0)
                {
                    trans.Rollback();
                    errorMessage = "Giỏ hàng rỗng hoặc không có sản phẩm nào.";
                    return 0;
                }

                foreach (var item in items)
                {
                    // Fetch current selling price from Product
                    decimal unitPrice = 0;
                    using (var priceCmd = new SqlCommand("SELECT selling_price, quantity FROM Product WHERE product_id = @pid", conn, trans))
                    {
                        priceCmd.Parameters.AddWithValue("@pid", item.ProductId);
                        using var reader = priceCmd.ExecuteReader();
                        if (!reader.Read())
                        {
                            trans.Rollback();
                            errorMessage = $"Sản phẩm ID #{item.ProductId} không tồn tại.";
                            return 0;
                        }
                        var stock = Convert.ToInt32(reader["quantity"]);
                        unitPrice = Convert.ToDecimal(reader["selling_price"]);
                        if (stock < item.Quantity)
                        {
                            trans.Rollback();
                            errorMessage = $"Sản phẩm trong kho không đủ số lượng (còn {stock} máy).";
                            return 0;
                        }
                    }

                    // Insert detail
                    using (var detailCmd = new SqlCommand(@"
                        INSERT INTO Invoice_Detail (invoice_id, product_id, quantity, unit_price)
                        VALUES (@invId, @pid, @qty, @price);", conn, trans))
                    {
                        detailCmd.Parameters.AddWithValue("@invId", invoiceId);
                        detailCmd.Parameters.AddWithValue("@pid", item.ProductId);
                        detailCmd.Parameters.AddWithValue("@qty", item.Quantity);
                        detailCmd.Parameters.AddWithValue("@price", unitPrice);
                        detailCmd.ExecuteNonQuery();
                    }
                }

                // Cộng thêm tiền cước vận chuyển vào total_amount nếu có
                if (model.ShippingFee > 0)
                {
                    using var addShipCmd = new SqlCommand("UPDATE Invoice SET total_amount = ISNULL(total_amount, 0) + @shipFee WHERE invoice_id = @invId", conn, trans);
                    addShipCmd.Parameters.AddWithValue("@shipFee", model.ShippingFee);
                    addShipCmd.Parameters.AddWithValue("@invId", invoiceId);
                    addShipCmd.ExecuteNonQuery();
                }

                trans.Commit();
                return invoiceId;
            }
            catch (Exception ex)
            {
                trans.Rollback();
                errorMessage = ex.Message;
                return 0;
            }
        }

        public List<InvoiceModel> GetOrders(string? status = null, string? keyword = null)
        {
            var list = new List<InvoiceModel>();
            using var conn = new SqlConnection(_connectionString);
            var sql = @"
                SELECT i.invoice_id, i.customer_id, c.customer_name, c.phone AS customer_phone,
                       i.shipping_address, i.order_notes, i.payment_method_selected,
                       i.employee_id, e.employee_name, i.promotion_id, pr.promotion_name,
                       i.invoice_date, i.sub_total, i.discount_amount, i.total_amount, i.status,
                       i.cancel_reason, i.cancel_requested_by, i.cancel_requested_at, i.cancel_approved_by, i.cancel_approved_at,
                       i.shipping_carrier, i.shipping_fee, i.shipping_distance_km, i.shipping_service_type,
                       ISNULL((SELECT SUM(amount) FROM Payment WHERE invoice_id = i.invoice_id), 0) AS paid_amount
                FROM Invoice i
                JOIN Customer c ON c.customer_id = i.customer_id
                JOIN Employee e ON e.employee_id = i.employee_id
                LEFT JOIN Promotion pr ON pr.promotion_id = i.promotion_id
                WHERE (@status IS NULL OR i.status = @status)
                  AND (@keyword IS NULL OR c.customer_name LIKE '%' + @keyword + '%' OR c.phone LIKE '%' + @keyword + '%' OR CAST(i.invoice_id AS VARCHAR) = @keyword)
                ORDER BY i.invoice_id DESC";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@status", string.IsNullOrEmpty(status) ? (object)DBNull.Value : status);
            cmd.Parameters.AddWithValue("@keyword", string.IsNullOrEmpty(keyword) ? (object)DBNull.Value : keyword);

            conn.Open();
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                list.Add(MapInvoiceFromReader(r));
            }
            return list;
        }

        public List<InvoiceModel> GetOrdersByCustomerId(int customerId)
        {
            var list = new List<InvoiceModel>();
            using var conn = new SqlConnection(_connectionString);
            var sql = @"
                SELECT i.invoice_id, i.customer_id, c.customer_name, c.phone AS customer_phone,
                       i.shipping_address, i.order_notes, i.payment_method_selected,
                       i.employee_id, e.employee_name, i.promotion_id, pr.promotion_name,
                       i.invoice_date, i.sub_total, i.discount_amount, i.total_amount, i.status,
                       i.cancel_reason, i.cancel_requested_by, i.cancel_requested_at, i.cancel_approved_by, i.cancel_approved_at,
                       i.shipping_carrier, i.shipping_fee, i.shipping_distance_km, i.shipping_service_type,
                       ISNULL((SELECT SUM(amount) FROM Payment WHERE invoice_id = i.invoice_id), 0) AS paid_amount
                FROM Invoice i
                JOIN Customer c ON c.customer_id = i.customer_id
                JOIN Employee e ON e.employee_id = i.employee_id
                LEFT JOIN Promotion pr ON pr.promotion_id = i.promotion_id
                WHERE i.customer_id = @custId
                ORDER BY i.invoice_id DESC";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@custId", customerId);

            conn.Open();
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                list.Add(MapInvoiceFromReader(r));
            }
            return list;
        }

        public InvoiceModel? GetOrderById(int invoiceId)
        {
            InvoiceModel? order = null;
            using var conn = new SqlConnection(_connectionString);
            var sql = @"
                SELECT i.invoice_id, i.customer_id, c.customer_name, c.phone AS customer_phone,
                       i.shipping_address, i.order_notes, i.payment_method_selected,
                       i.employee_id, e.employee_name, i.promotion_id, pr.promotion_name,
                       i.invoice_date, i.sub_total, i.discount_amount, i.total_amount, i.status,
                       i.cancel_reason, i.cancel_requested_by, i.cancel_requested_at, i.cancel_approved_by, i.cancel_approved_at,
                       i.shipping_carrier, i.shipping_fee, i.shipping_distance_km, i.shipping_service_type,
                       ISNULL((SELECT SUM(amount) FROM Payment WHERE invoice_id = i.invoice_id), 0) AS paid_amount
                FROM Invoice i
                JOIN Customer c ON c.customer_id = i.customer_id
                JOIN Employee e ON e.employee_id = i.employee_id
                LEFT JOIN Promotion pr ON pr.promotion_id = i.promotion_id
                WHERE i.invoice_id = @id";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@id", invoiceId);
            conn.Open();
            using var r = cmd.ExecuteReader();
            if (r.Read())
            {
                order = MapInvoiceFromReader(r);
            }
            r.Close();

            if (order != null)
            {
                var detailSql = @"
                    SELECT d.invoice_detail_id, d.invoice_id, d.product_id, p.product_name, p.image_url, d.quantity, d.unit_price, d.line_total
                    FROM Invoice_Detail d
                    JOIN Product p ON p.product_id = d.product_id
                    WHERE d.invoice_id = @id";

                using var detailCmd = new SqlCommand(detailSql, conn);
                detailCmd.Parameters.AddWithValue("@id", invoiceId);
                using var detailReader = detailCmd.ExecuteReader();
                while (detailReader.Read())
                {
                    order.Details.Add(new InvoiceDetailModel
                    {
                        InvoiceDetailId = Convert.ToInt32(detailReader["invoice_detail_id"]),
                        InvoiceId = Convert.ToInt32(detailReader["invoice_id"]),
                        ProductId = Convert.ToInt32(detailReader["product_id"]),
                        ProductName = detailReader["product_name"].ToString()!,
                        ImageUrl = detailReader["image_url"] != DBNull.Value ? detailReader["image_url"].ToString() : null,
                        Quantity = Convert.ToInt32(detailReader["quantity"]),
                        UnitPrice = Convert.ToDecimal(detailReader["unit_price"]),
                        LineTotal = Convert.ToDecimal(detailReader["line_total"])
                    });
                }
            }

            return order;
        }

        public bool ConfirmPayment(int invoiceId, decimal amount, string paymentMethod, out string errorMessage)
        {
            errorMessage = string.Empty;
            try
            {
                using var conn = new SqlConnection(_connectionString);
                // CK_Payment_Method checks: 'Cash', 'Bank Transfer', 'Momo', 'Credit Card'
                string validMethod = "Bank Transfer";
                if (paymentMethod == "Cash" || paymentMethod == "Momo" || paymentMethod == "Credit Card")
                    validMethod = paymentMethod;

                using var cmd = new SqlCommand("usp_AddPayment", conn);
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@invoice_id", invoiceId);
                cmd.Parameters.AddWithValue("@payment_method", validMethod);
                cmd.Parameters.AddWithValue("@amount", amount);

                conn.Open();
                cmd.ExecuteNonQuery();
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }
        }

        // 1. Sales nhân viên yêu cầu hủy đơn kèm lý do chi tiết (Chống gian lận - Chờ Admin duyệt)
        public bool RequestCancelOrder(int invoiceId, string reason, string requestedBy, out string errorMessage)
        {
            errorMessage = string.Empty;
            try
            {
                using var conn = new SqlConnection(_connectionString);
                var sql = @"
                    UPDATE Invoice 
                    SET status = 'CancelRequested',
                        cancel_reason = @reason,
                        cancel_requested_by = @requestedBy,
                        cancel_requested_at = GETDATE()
                    WHERE invoice_id = @id AND status <> 'Cancelled'";

                using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@id", invoiceId);
                cmd.Parameters.AddWithValue("@reason", reason.Trim());
                cmd.Parameters.AddWithValue("@requestedBy", requestedBy);

                conn.Open();
                int rows = cmd.ExecuteNonQuery();
                if (rows == 0)
                {
                    errorMessage = "Đơn hàng không tồn tại hoặc đã bị hủy trước đó.";
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }
        }

        // 2. Admin phê duyệt hủy đơn (Kích hoạt hoàn kho và đổi trạng thái thành Cancelled)
        public bool ApproveCancelOrder(int invoiceId, string approvedBy, out string errorMessage)
        {
            errorMessage = string.Empty;
            try
            {
                using var conn = new SqlConnection(_connectionString);
                using var cmd = new SqlCommand("usp_CancelInvoice", conn);
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@invoice_id", invoiceId);
                cmd.Parameters.AddWithValue("@approved_by", approvedBy);

                conn.Open();
                cmd.ExecuteNonQuery();
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }
        }

        // 3. Admin từ chối yêu cầu hủy đơn (Đưa trạng thái về lại Pending)
        public bool RejectCancelOrder(int invoiceId, string rejectReason, out string errorMessage)
        {
            errorMessage = string.Empty;
            try
            {
                using var conn = new SqlConnection(_connectionString);
                var sql = @"
                    UPDATE Invoice 
                    SET status = 'Pending',
                        order_notes = ISNULL(order_notes + ' | ', '') + N'[Admin từ chối hủy: ' + @rejectReason + ']',
                        cancel_reason = NULL,
                        cancel_requested_by = NULL,
                        cancel_requested_at = NULL
                    WHERE invoice_id = @id AND status = 'CancelRequested'";

                using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@id", invoiceId);
                cmd.Parameters.AddWithValue("@rejectReason", rejectReason.Trim());

                conn.Open();
                int rows = cmd.ExecuteNonQuery();
                if (rows == 0)
                {
                    errorMessage = "Đơn hàng không ở trạng thái chờ duyệt hủy.";
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }
        }

        // 4. Hủy trực tiếp (Admin có quyền hủy ngay kèm lý do chi tiết)
        public bool CancelOrder(int invoiceId, out string errorMessage)
        {
            return CancelOrder(invoiceId, "Quản trị viên hủy đơn hàng trực tiếp", "Admin", out errorMessage);
        }

        public bool CancelOrder(int invoiceId, string reason, string approvedBy, out string errorMessage)
        {
            errorMessage = string.Empty;
            try
            {
                using var conn = new SqlConnection(_connectionString);
                using var cmd = new SqlCommand("usp_CancelInvoice", conn);
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@invoice_id", invoiceId);
                cmd.Parameters.AddWithValue("@reason", string.IsNullOrWhiteSpace(reason) ? "Không có lý do chi tiết" : reason.Trim());
                cmd.Parameters.AddWithValue("@approved_by", approvedBy);

                conn.Open();
                cmd.ExecuteNonQuery();
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }
        }
    
        // Khách hàng hoặc Sales xác nhận đã chuyển cọc 50% / trả trước -> Đặt hàng thành công
        public bool CustomerConfirmDeposit(int invoiceId, out string errorMessage)
        {
            errorMessage = string.Empty;
            try
            {
                using var conn = new SqlConnection(_connectionString);
                conn.Open();

                // Kiểm tra trạng thái và xem đã gửi xác nhận trước đó chưa (Chống spam liên tục)
                var checkSql = "SELECT order_notes, status FROM Invoice WHERE invoice_id = @id";
                using var checkCmd = new SqlCommand(checkSql, conn);
                checkCmd.Parameters.AddWithValue("@id", invoiceId);
                using var reader = checkCmd.ExecuteReader();
                if (!reader.Read())
                {
                    errorMessage = "Đơn hàng không tồn tại.";
                    return false;
                }

                var status = reader["status"]?.ToString();
                var notes = reader["order_notes"]?.ToString() ?? "";
                reader.Close();

                if (status != "Pending")
                {
                    errorMessage = "Đơn hàng không ở trạng thái chờ duyệt hoặc đã được xử lý.";
                    return false;
                }

                if (notes.Contains("[Khách hàng đã bấm xác nhận chuyển khoản"))
                {
                    errorMessage = "Quý khách đã gửi yêu cầu xác nhận chuyển khoản trước đó rồi. Vui lòng chờ nhân viên Sale / Admin đối soát và phê duyệt.";
                    return false;
                }

                var sql = @"
                    UPDATE Invoice 
                    SET order_notes = ISNULL(order_notes + ' | ', '') + N'[Khách hàng đã bấm xác nhận chuyển khoản cọc qua ZaloPay/VietQR]'
                    WHERE invoice_id = @id AND status = 'Pending'";
                using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@id", invoiceId);
                int rows = cmd.ExecuteNonQuery();
                if (rows == 0)
                {
                    errorMessage = "Đơn hàng không ở trạng thái chờ duyệt hoặc không tồn tại.";
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }
        }

        // Admin xác nhận đã nhận chuyển khoản 100% -> Chuyển sang Paid (Đã thanh toán)
        public bool AdminConfirmFullPayment(int invoiceId, out string errorMessage)
        {
            errorMessage = string.Empty;
            try
            {
                var order = GetOrderById(invoiceId);
                if (order == null)
                {
                    errorMessage = "Đơn hàng không tồn tại.";
                    return false;
                }

                decimal amountToAdd = order.RemainingAmount > 0 ? order.RemainingAmount : order.TotalAmount;
                return ConfirmPayment(invoiceId, amountToAdd, "Bank Transfer", out errorMessage);
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }
        }

        // Cập nhật trạng thái đơn hàng (Shipping, Delivered, Paid, Pending)
        public bool UpdateOrderStatus(int invoiceId, string newStatus, out string errorMessage)
        {
            errorMessage = string.Empty;
            var validStatuses = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Pending", "Paid", "Shipping", "Delivered", "Cancelled", "CancelRequested" };
            if (!validStatuses.Contains(newStatus))
            {
                errorMessage = $"Trạng thái '{newStatus}' không hợp lệ trong quy trình xử lý đơn hàng.";
                return false;
            }

            try
            {
                using var conn = new SqlConnection(_connectionString);
                conn.Open();

                // Kiểm tra trạng thái hiện tại
                using var checkCmd = new SqlCommand("SELECT status, total_amount, (SELECT ISNULL(SUM(amount), 0) FROM Payment WHERE invoice_id = @id) AS paid_amount FROM Invoice WHERE invoice_id = @id", conn);
                checkCmd.Parameters.AddWithValue("@id", invoiceId);
                using var reader = checkCmd.ExecuteReader();
                if (!reader.Read())
                {
                    errorMessage = "Đơn hàng không tồn tại.";
                    return false;
                }

                string currentStatus = reader["status"].ToString()!;
                decimal totalAmount = Convert.ToDecimal(reader["total_amount"]);
                decimal paidAmount = Convert.ToDecimal(reader["paid_amount"]);
                reader.Close();

                if (currentStatus == "Cancelled")
                {
                    errorMessage = "Không thể cập nhật trạng thái của đơn hàng đã bị hủy.";
                    return false;
                }

                // Nếu chuyển sang Delivered và phương thức thanh toán là COD nhưng chưa thanh toán đủ -> Tự động thu nốt tiền COD
                if (newStatus.Equals("Delivered", StringComparison.OrdinalIgnoreCase) && paidAmount < totalAmount)
                {
                    decimal remaining = totalAmount - paidAmount;
                    if (remaining > 0)
                    {
                        using var payCmd = new SqlCommand("INSERT INTO Payment (invoice_id, payment_method, amount) VALUES (@id, 'Cash', @amt)", conn);
                        payCmd.Parameters.AddWithValue("@id", invoiceId);
                        payCmd.Parameters.AddWithValue("@amt", remaining);
                        payCmd.ExecuteNonQuery();
                    }
                }

                string noteSuffix = newStatus switch
                {
                    "Shipping" => "[Bắt đầu giao hàng - Đơn vị vận chuyển đang xử lý]",
                    "Delivered" => "[Đã giao hàng thành công đến khách hàng]",
                    _ => $"[Cập nhật trạng thái sang {newStatus}]"
                };

                var updateSql = @"
                    UPDATE Invoice 
                    SET status = @newStatus,
                        order_notes = ISNULL(order_notes + ' | ', '') + @note
                    WHERE invoice_id = @id";

                using var cmd = new SqlCommand(updateSql, conn);
                cmd.Parameters.AddWithValue("@newStatus", newStatus);
                cmd.Parameters.AddWithValue("@note", noteSuffix);
                cmd.Parameters.AddWithValue("@id", invoiceId);
                cmd.ExecuteNonQuery();

                return true;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }
        }

        private static InvoiceModel MapInvoiceFromReader(SqlDataReader r)
        {
            return new InvoiceModel
            {
                InvoiceId = Convert.ToInt32(r["invoice_id"]),
                CustomerId = Convert.ToInt32(r["customer_id"]),
                CustomerName = r["customer_name"].ToString()!,
                CustomerPhone = r["customer_phone"].ToString()!,
                ShippingAddress = r["shipping_address"] != DBNull.Value ? r["shipping_address"].ToString() : null,
                OrderNotes = r["order_notes"] != DBNull.Value ? r["order_notes"].ToString() : null,
                PaymentMethodSelected = r["payment_method_selected"] != DBNull.Value ? r["payment_method_selected"].ToString() : null,
                EmployeeId = Convert.ToInt32(r["employee_id"]),
                EmployeeName = r["employee_name"].ToString()!,
                PromotionId = r["promotion_id"] != DBNull.Value ? Convert.ToInt32(r["promotion_id"]) : null,
                PromotionName = r["promotion_name"] != DBNull.Value ? r["promotion_name"].ToString() : null,
                InvoiceDate = Convert.ToDateTime(r["invoice_date"]),
                SubTotal = Convert.ToDecimal(r["sub_total"]),
                DiscountAmount = Convert.ToDecimal(r["discount_amount"]),
                TotalAmount = Convert.ToDecimal(r["total_amount"]),
                Status = r["status"].ToString()!,
                CancelReason = r["cancel_reason"] != DBNull.Value ? r["cancel_reason"].ToString() : null,
                CancelRequestedBy = r["cancel_requested_by"] != DBNull.Value ? r["cancel_requested_by"].ToString() : null,
                CancelRequestedAt = r["cancel_requested_at"] != DBNull.Value ? Convert.ToDateTime(r["cancel_requested_at"]) : null,
                CancelApprovedBy = r["cancel_approved_by"] != DBNull.Value ? r["cancel_approved_by"].ToString() : null,
                CancelApprovedAt = r["cancel_approved_at"] != DBNull.Value ? Convert.ToDateTime(r["cancel_approved_at"]) : null,
                PaidAmount = Convert.ToDecimal(r["paid_amount"]),
                ShippingCarrier = HasColumn(r, "shipping_carrier") && r["shipping_carrier"] != DBNull.Value ? r["shipping_carrier"].ToString() : null,
                ShippingFee = HasColumn(r, "shipping_fee") && r["shipping_fee"] != DBNull.Value ? Convert.ToDecimal(r["shipping_fee"]) : 0,
                ShippingDistanceKm = HasColumn(r, "shipping_distance_km") && r["shipping_distance_km"] != DBNull.Value ? Convert.ToDecimal(r["shipping_distance_km"]) : null,
                ShippingServiceType = HasColumn(r, "shipping_service_type") && r["shipping_service_type"] != DBNull.Value ? r["shipping_service_type"].ToString() : null
            };
        }

        private static bool HasColumn(SqlDataReader reader, string columnName)
        {
            for (int i = 0; i < reader.FieldCount; i++)
            {
                if (reader.GetName(i).Equals(columnName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }
    }
}
