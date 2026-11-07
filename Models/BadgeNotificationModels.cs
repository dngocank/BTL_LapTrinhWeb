namespace BTL_PTTKYC.Models
{
    public class SystemBadgeCountsModel
    {
        // 1. Quản lý Đơn hàng
        public int PendingOrders { get; set; }           // Chờ Sale phê duyệt (status = 'Pending')
        public int CancelRequestedOrders { get; set; }   // Chờ Admin duyệt hủy (status = 'CancelRequested')
        public int PaidOrders { get; set; }              // Đã thanh toán (status = 'Paid')
        public int ShippingOrders { get; set; }          // Đang giao hàng (status = 'Shipping')
        public int DeliveredOrders { get; set; }         // Đã giao hàng (status = 'Delivered')
        public int TotalOrders { get; set; }             // Tổng số đơn
        
        // Tổng số đơn cần xử lý/duyệt (Admin = Pending + CancelRequested; Sales = Pending)
        public int OrdersNeedingAction => PendingOrders + CancelRequestedOrders;

        // 2. Nghiệp vụ nội bộ khác
        public int LowStockCount { get; set; }           // Tồn kho cảnh báo (<= 5)
        public int UnrepliedReviews { get; set; }        // Đánh giá chưa phản hồi
        public int ActiveWarrantyTickets { get; set; }   // Phiếu BH đang tiếp nhận / xử lý
        
        // Tổng thông báo khẩn cần nội bộ chú ý
        public int TotalAdminAlerts => OrdersNeedingAction + LowStockCount + UnrepliedReviews + ActiveWarrantyTickets;
        public int TotalSalesAlerts => PendingOrders + UnrepliedReviews;
        public int TotalWarehouseAlerts => LowStockCount + PaidOrders;
        public int TotalTechnicianAlerts => ActiveWarrantyTickets;

        // 3. Khách hàng cá nhân
        public int CustomerActiveOrders { get; set; }    // Đơn đang xử lý hoặc đang giao (Pending, Paid, Shipping, CancelRequested)
        public int CustomerActiveWarranty { get; set; }  // Phiếu BH đang tiếp nhận / xử lý của khách (status NOT IN ('Delivered', 'Cancelled', 'Completed'))
        public int CustomerWarrantedDevices { get; set; } // Tổng số thiết bị đang còn hạn bảo hành (<= 12 tháng)
        public int CustomerCartCount { get; set; }       // Số sản phẩm trong giỏ hàng
    }
}

