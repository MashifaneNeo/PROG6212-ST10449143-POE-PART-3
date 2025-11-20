using PROG6212_ST10449143_POE_PART_1.Models;
using Microsoft.EntityFrameworkCore;

namespace PROG6212_ST10449143_POE_PART_1.Services
{
    public interface IAutomatedVerificationService
    {
        Task<VerificationResult> VerifyClaimAsync(Claim claim);
        Task ProcessPendingClaimsAsync();
        Task<WorkflowResult> ExecuteApprovalWorkflowAsync(int claimId);
    }

    public class AutomatedVerificationService : IAutomatedVerificationService
    {
        private readonly AppDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AutomatedVerificationService(AppDbContext context, IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<VerificationResult> VerifyClaimAsync(Claim claim)
        {
            var result = new VerificationResult();

            // Predefined criteria validation
            if (claim.HoursWorked < 0.5m)
            {
                result.AddError("Hours worked must be at least 0.5 hours");
            }

            if (claim.HoursWorked > 180m)
            {
                result.AddError("Hours worked cannot exceed 180 hours per month");
            }

            if (claim.HourlyRate <= 0)
            {
                result.AddError("Hourly rate must be greater than 0");
            }

            if (claim.HourlyRate > 1000m)
            {
                result.AddError("Hourly rate exceeds maximum allowed rate");
            }

            // Check for duplicate claims in same month
            var duplicateClaim = await _context.Claims
                .Where(c => c.UserId == claim.UserId &&
                           c.Month == claim.Month &&
                           c.Id != claim.Id &&
                           c.Status != "Rejected")
                .FirstOrDefaultAsync();

            if (duplicateClaim != null)
            {
                result.AddError($"A claim for {claim.Month} has already been submitted");
            }

            // Store verification session
            var session = _httpContextAccessor.HttpContext.Session;
            session.SetString($"ClaimVerification_{claim.Id}", System.Text.Json.JsonSerializer.Serialize(result));

            result.IsValid = !result.Errors.Any();
            return result;
        }

        public async Task ProcessPendingClaimsAsync()
        {
            var pendingClaims = await _context.Claims
                .Where(c => c.Status == "Submitted" || c.Status == "Under Review")
                .Include(c => c.User)
                .ToListAsync();

            foreach (var claim in pendingClaims)
            {
                await ExecuteApprovalWorkflowAsync(claim.Id);
            }
        }

        public async Task<WorkflowResult> ExecuteApprovalWorkflowAsync(int claimId)
        {
            var claim = await _context.Claims
                .Include(c => c.User)
                .FirstOrDefaultAsync(c => c.Id == claimId);

            if (claim == null)
                return new WorkflowResult { Success = false, Message = "Claim not found" };

            var verification = await VerifyClaimAsync(claim);

            if (!verification.IsValid)
            {
                claim.Status = "Rejected";
                claim.RejectionReason = $"Automated rejection: {string.Join(", ", verification.Errors)}";
                await _context.SaveChangesAsync();

                return new WorkflowResult
                {
                    Success = false,
                    Message = "Claim rejected by automated verification",
                    Details = verification.Errors
                };
            }

            // Auto-approve claims meeting specific criteria
            if (claim.HoursWorked <= 160m && claim.HourlyRate <= 500m)
            {
                claim.Status = "Approved";
                claim.RejectionReason = "Automatically approved by system";
                await _context.SaveChangesAsync();

                // Store workflow session
                var session = _httpContextAccessor.HttpContext.Session;
                session.SetString($"WorkflowResult_{claimId}", "AutoApproved");

                return new WorkflowResult
                {
                    Success = true,
                    Message = "Claim automatically approved",
                    ApprovedAmount = claim.TotalAmount
                };
            }

            // Move to manual review for claims exceeding auto-approval limits
            if (claim.Status == "Submitted")
            {
                claim.Status = "Under Review";
                await _context.SaveChangesAsync();
            }

            return new WorkflowResult
            {
                Success = true,
                Message = "Claim requires manual review",
                RequiresManualReview = true
            };
        }
    }

    public class VerificationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
        public List<string> Warnings { get; set; } = new List<string>();

        public void AddError(string error) => Errors.Add(error);
        public void AddWarning(string warning) => Warnings.Add(warning);
    }

    public class WorkflowResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public bool RequiresManualReview { get; set; }
        public decimal? ApprovedAmount { get; set; }
        public List<string> Details { get; set; } = new List<string>();
    }
}