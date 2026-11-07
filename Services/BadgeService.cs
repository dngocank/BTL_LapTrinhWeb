using Microsoft.Data.SqlClient;
using BTL_PTTKYC.Models;

namespace BTL_PTTKYC.Services
{
    public class BadgeService : IBadgeService
    {
        private readonly string _connectionString;
        private readonly ILogger<BadgeService> _logger;

        public BadgeService(IConfiguration configuration, ILogger<BadgeService> logger)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")!;
            _logger = logger;
        }

        public SystemBadgeCountsModel GetBadgeCounts(int? customerId = null)
        {
            var model = new SystemBadgeCountsModel();

            try
            {
                using var conn = new SqlConnection(_connectionString);
                conn.Open();

                // 1. Phân loại và đếm đơn hàng theo trạng thái
                using (var cmd = new SqlCommand("SELECT status, COUNT(*) AS cnt FROM Invoice GROUP BY status", conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string status = reader["status"]?.ToString() ?? string.Empty;
                        int cnt = Convert.ToInt32(reader["cnt"]);
                        model.TotalOrders += cnt;

                        if (status.Equals("Pending", StringComparison.OrdinalIgnoreCase))
                        {
                            model.PendingOrders = cnt;
                        }
                        else if (status.Equals("CancelRequested", StringComparison.OrdinalIgnoreCase))
                        {
                            model.CancelRequestedOrders = cnt;
                        }
                        else if (status.Equals("Paid", StringComparison.OrdinalIgnoreCase))
                        {
                            model.PaidOrders = cnt;
                        }
                        else if (status.Equals("Shipping", StringComparison.OrdinalIgnoreCase))
                        {
                            model.ShippingOrders = cnt;
                        }
                        else if (status.Equals("Delivered", StringComparison.OrdinalIgnoreCase))
                        {
                            model.DeliveredOrders = cnt;
                        }
                    }
                }

                // 2. Đếm số lượng sản phẩm tồn kho thấp (<= 5 máy)
                using (var cmd = new SqlCommand("SELECT COUNT(*) FROM vw_LowStockProducts", conn))
                {
                    var obj = cmd.ExecuteScalar();
                    if (obj != null && obj != DBNull.Value)
                    {
                        model.LowStockCount = Convert.ToInt32(obj);
                    }
                }

                // 3. Đếm số đánh giá khách gửi chưa được phản hồi
                using (var cmd = new SqlCommand("SELECT COUNT(*) FROM Review WHERE reply IS NULL OR LTRIM(RTRIM(reply)) = ''", conn))
                {
                    var obj = cmd.ExecuteScalar();
                    if (obj != null && obj != DBNull.Value)
                    {
                        model.UnrepliedReviews = Convert.ToInt32(obj);
                    }
                }

                // 4. Đếm số phiếu bảo hành đang tiếp nhận / xử lý
                using (var cmd = new SqlCommand("SELECT COUNT(*) FROM Warranty_Ticket WHERE status IN ('Received', 'Diagnosing', 'Processing', 'WaitingParts')", conn))
                {
                    var obj = cmd.ExecuteScalar();
                    if (obj != null && obj != DBNull.Value)
                    {
                        model.ActiveWarrantyTickets = Convert.ToInt32(obj);
                    }
                }

                // 5. Nếu có thông tin tài khoản khách hàng đang đăng nhập
                if (customerId.HasValue && customerId.Value > 0)
                {
                    // Đơn hàng đang xử lý hoặc đang giao của khách (Pending, Paid, Shipping, CancelRequested)
                    using (var cmd = new SqlCommand("SELECT COUNT(*) FROM Invoice WHERE customer_id = @cid AND status IN ('Pending', 'Paid', 'Shipping', 'CancelRequested')", conn))
                    {
                        cmd.Parameters.AddWithValue("@cid", customerId.Value);
                        var obj = cmd.ExecuteScalar();
                        if (obj != null && obj != DBNull.Value)
                        {
                            model.CustomerActiveOrders = Convert.ToInt32(obj);
                        }
                    }

                    // Phiếu sửa chữa bảo hành đang tiếp nhận / xử lý của khách (khác Delivered, Cancelled, Completed)
                    using (var cmd = new SqlCommand("SELECT COUNT(*) FROM Warranty_Ticket WHERE customer_id = @cid AND status NOT IN ('Delivered', 'Cancelled', 'Completed')", conn))
                    {
                        cmd.Parameters.AddWithValue("@cid", customerId.Value);
                        var obj = cmd.ExecuteScalar();
                        if (obj != null && obj != DBNull.Value)
                        {
                            model.CustomerActiveWarranty = Convert.ToInt32(obj);
                        }
                    }

                    // Tổng số thiết bị đang còn trong thời hạn bảo hành chính hãng (12 tháng kể từ ngày giao hàng / thanh toán)
                    using (var cmd = new SqlCommand(@"
                        SELECT COUNT(*) 
                        FROM Invoice i 
                        JOIN Invoice_Detail id ON i.invoice_id = id.invoice_id 
                        WHERE i.customer_id = @cid 
                          AND i.status IN ('Delivered', 'Paid') 
                          AND DATEADD(month, 12, i.invoice_date) >= CAST(GETDATE() AS DATE)", conn))
                    {
                        cmd.Parameters.AddWithValue("@cid", customerId.Value);
                        var obj = cmd.ExecuteScalar();
                        if (obj != null && obj != DBNull.Value)
                        {
                            model.CustomerWarrantedDevices = Convert.ToInt32(obj);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi truy vấn số lượng thông báo badge từ CSDL");
            }

            return model;
        }
    }
}

