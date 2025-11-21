using PROG6212_ST10449143_POE_PART_1.Models;

namespace PROG6212_ST10449143_POE_PART_1.Services
{
    public interface IClaimService
    {
        Task AddClaimAsync(Claim claim);
        Task<List<Claim>> GetAllClaimsAsync();
        Task<Claim> GetClaimByIdAsync(int id);
        Task UpdateClaimStatusAsync(int id, string status, string rejectionReason = null);
        Task<List<Claim>> GetPendingClaimsAsync();
        Task<bool> DeleteClaimAsync(int id);

        Task<List<Claim>> GetClaimsForCoordinatorReviewAsync();
        Task<List<Claim>> GetClaimsForManagerReviewAsync();
    }
}