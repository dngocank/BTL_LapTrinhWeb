using System.ComponentModel.DataAnnotations;

namespace BTL_PTTKYC.Models
{
    public class ShippingQuoteRequest
    {
        public string OriginAddress { get; set; } = "Kho Tổng KittyPhone - 123 Thái Hà, Đống Đa, Hà Nội";

        [Required(ErrorMessage = "Vui lòng chọn Tỉnh/Thành phố nhận hàng")]
        [Display(Name = "Tỉnh / Thành phố")]
        public string DestinationProvince { get; set; } = "Hà Nội";

        [Display(Name = "Quận / Huyện")]
        public string? DestinationDistrict { get; set; } = "Đống Đa";

        [Display(Name = "Địa chỉ chi tiết nhận hàng")]
        public string? DestinationAddress { get; set; } = string.Empty;

        [Display(Name = "Khoảng cách (km)")]
        [Range(0.1, 2500, ErrorMessage = "Khoảng cách từ 0.1km đến 2500km")]
        public decimal DistanceKm { get; set; } = 5.0m;

        [Display(Name = "Cân nặng thực tế (gam)")]
        [Range(50, 30000, ErrorMessage = "Khối lượng từ 50g đến 30kg")]
        public int WeightGrams { get; set; } = 500; // Điện thoại + phụ kiện thường ~500g

        [Display(Name = "Chiều dài (cm)")]
        [Range(5, 200)]
        public decimal LengthCm { get; set; } = 20m;

        [Display(Name = "Chiều rộng (cm)")]
        [Range(5, 200)]
        public decimal WidthCm { get; set; } = 12m;

        [Display(Name = "Chiều cao (cm)")]
        [Range(2, 200)]
        public decimal HeightCm { get; set; } = 8m;

        [Display(Name = "Giá trị kiện hàng (VNĐ để tính phí bảo hiểm bảo đảm)")]
        [Range(0, 200000000)]
        public decimal DeclaredValue { get; set; } = 15000000m; // 15 triệu

        public bool IsWarrantyService { get; set; } = false;
        public bool IsUnderWarranty { get; set; } = true;
        public string CustomerRank { get; set; } = "Standard"; // Standard, Silver, Gold, Diamond
    }

    public class ShippingCarrierOption
    {
        public string CarrierId { get; set; } = string.Empty; // xanhsm, grab, be, ghn, viettelpost
        public string CarrierName { get; set; } = string.Empty;
        public string ServiceName { get; set; } = string.Empty;
        public string ServiceTypeBadge { get; set; } = string.Empty; // Hỏa tốc xe máy, Chuyển phát bưu cục, Bay hỏa tốc
        public string EstimatedDeliveryTime { get; set; } = string.Empty; // 30-45 phút, 1-2 giờ, 1-2 ngày
        public string LogoUrl { get; set; } = string.Empty;
        public string BadgeColor { get; set; } = "primary";

        // Cấu thành chi phí minh bạch
        public decimal BaseFare { get; set; }
        public decimal DistanceFare { get; set; }
        public decimal WeightFare { get; set; }
        public decimal VolumetricWeightKg { get; set; }
        public decimal ChargeableWeightKg { get; set; }
        public decimal InsuranceFee { get; set; } // Phí bảo hiểm hàng công nghệ giá trị cao
        public decimal PackagingFee { get; set; } // Phí đóng gói thùng xốp, bóng khí, tem niêm phong chống tráo

        public decimal CarrierRawTotal => BaseFare + DistanceFare + WeightFare + InsuranceFee + PackagingFee;

        // Trợ giá từ cửa hàng KittyPhone (để khách không bị quá đắt)
        public decimal StoreSubsidy { get; set; }
        public string SubsidyDescription { get; set; } = string.Empty;

        // Cước cuối cùng khách hàng thanh toán
        public decimal FinalFee => Math.Max(0, CarrierRawTotal - StoreSubsidy);

        public bool IsFeasible { get; set; } = true;
        public string? InfeasibleReason { get; set; }
        public bool IsRecommended { get; set; }
        public List<string> Highlights { get; set; } = new();
        public string RiskPreventionNote { get; set; } = string.Empty;
    }

    public class ShippingQuoteResult
    {
        public ShippingQuoteRequest Request { get; set; } = new();
        public decimal VolumetricWeightKg => Math.Round((Request.LengthCm * Request.WidthCm * Request.HeightCm) / 5000m, 2);
        public decimal ActualWeightKg => Math.Round(Request.WeightGrams / 1000m, 2);
        public decimal ChargeableWeightKg => Math.Max(ActualWeightKg, VolumetricWeightKg);
        public string CalculatedScope { get; set; } = "Nội thành Hà Nội";
        public List<ShippingCarrierOption> AvailableCarriers { get; set; } = new();
        public ShippingCarrierOption? BestCarrier => AvailableCarriers.Where(c => c.IsFeasible).OrderBy(c => c.FinalFee).FirstOrDefault();
    }

    public class DistrictDistanceInfo
    {
        public string Province { get; set; } = "Hà Nội";
        public string District { get; set; } = string.Empty;
        public decimal EstimatedDistanceKm { get; set; }
        public bool IsInnerCity { get; set; }
        public string Region { get; set; } = "North"; // North, Central, South
    }

    public class ShippingCalculationRequest
    {
        public decimal DistanceKm { get; set; } = 5.0m;
        public string? Province { get; set; } = "Hà Nội";
        public string? District { get; set; } = "Cầu Giấy";
        public string? Address { get; set; }
        public decimal SubTotal { get; set; }
        public decimal DiscountAmount { get; set; }
        public string? CarrierId { get; set; }
        public string? DeliveryMethod { get; set; } = "HomeDelivery";
        public string? ShippingMethod { get; set; }
    }
}
