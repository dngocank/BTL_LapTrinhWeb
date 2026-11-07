using BTL_PTTKYC.Models;

namespace BTL_PTTKYC.Services
{
    public interface IBadgeService
    {
        SystemBadgeCountsModel GetBadgeCounts(int? customerId = null);
    }
}

