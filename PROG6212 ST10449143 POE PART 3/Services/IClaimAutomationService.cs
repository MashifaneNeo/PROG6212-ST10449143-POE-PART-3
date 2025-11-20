using PROG6212_ST10449143_POE_PART_1.Models;
using Microsoft.EntityFrameworkCore;

namespace PROG6212_ST10449143_POE_PART_1.Services
{
    public interface IClaimAutomationService
    {
        Task<AutomationResult> AutomaticallyVerifyClaimAsync(Claim claim);
        Task<List<Claim>> GetClaimsForAutomatedReviewAsync();
        Task ProcessAutomatedApprovalsAsync();
        Task<WorkflowStatus> GetClaimWorkflowStatusAsync(int claimId);
        Task<List<Claim>> GetClaimsForCoordinatorReviewAsync();
        Task<List<Claim>> GetClaimsForManagerReviewAsync();
    }

    public class ClaimAutomationService : IClaimAutomationService
    {
        private readonly AppDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public ClaimAutomationService(AppDbContext context, IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<AutomationResult> AutomaticallyVerifyClaimAsync(Claim claim)
        {
            var result = new AutomationResult();
            var session = _httpContextAccessor.HttpContext.Session;

            try
            {
                // Store verification session data
                session.SetString($"Claim_{claim.Id}_VerificationStart", DateTime.Now.ToString());
                var currentAttempts = session.GetInt32($"Claim_{claim.Id}_VerificationAttempts") ?? 0;
                session.SetInt32($"Claim_{claim.Id}_VerificationAttempts", currentAttempts + 1);

                // Enhanced verification criteria for workflow
                var criteria = new VerificationCriteria
                {
                    MaxHoursPerMonth = 180,
                    MinHoursPerClaim = 0.5m,
                    MaxHourlyRate = 1000,
                    RequireDocumentationForHighHours = 160,
                    AutoApproveThreshold = 120,
                    MaxAutoApproveAmount = 20000 // R20,000 maximum for auto-approval
                };

                // Basic validation checks
                if (claim.HoursWorked > criteria.MaxHoursPerMonth)
                {
                    result.AddError($"Hours worked ({claim.HoursWorked}) exceed maximum allowed ({criteria.MaxHoursPerMonth})");
                    result.RecommendedAction = "Reject";
                }
                else if (claim.HoursWorked < criteria.MinHoursPerClaim)
                {
                    result.AddError($"Hours worked ({claim.HoursWorked}) are below minimum required ({criteria.MinHoursPerClaim})");
                    result.RecommendedAction = "Reject";
                }

                // Check hourly rate
                if (claim.HourlyRate > criteria.MaxHourlyRate)
                {
                    result.AddError($"Hourly rate (R{claim.HourlyRate}) exceeds maximum allowed (R{criteria.MaxHourlyRate})");
                    result.RecommendedAction = "Review";
                }

                // Check for documentation requirements
                if (claim.HoursWorked >= criteria.RequireDocumentationForHighHours && string.IsNullOrEmpty(claim.SupportingDocument))
                {
                    result.AddWarning($"High hours worked ({claim.HoursWorked}) require supporting documentation");
                    result.RecommendedAction = "Review";
                }

                // High-value claim check
                if (claim.TotalAmount > criteria.MaxAutoApproveAmount)
                {
                    result.AddWarning($"High-value claim (R{claim.TotalAmount}) requires manual review");
                    result.RecommendedAction = "Review";
                    result.CanAutoApprove = false;
                }

                // Auto-approve criteria - ONLY for CoordinatorReview stage
                if (claim.CurrentStage == "CoordinatorReview" &&
                    claim.HoursWorked <= criteria.AutoApproveThreshold &&
                    claim.HourlyRate <= 500 &&
                    claim.TotalAmount <= criteria.MaxAutoApproveAmount &&
                    !string.IsNullOrEmpty(claim.SupportingDocument) &&
                    !result.Errors.Any())
                {
                    result.CanAutoApprove = true;
                    result.RecommendedAction = "Approve";
                    result.AutoApprovalReason = "Claim meets all criteria for automatic coordinator approval";
                }

                // For ManagerReview stage, always require manual review
                if (claim.CurrentStage == "ManagerReview")
                {
                    result.AddInfo("Claim at Manager stage requires manual review");
                    result.CanAutoApprove = false;
                    result.RecommendedAction = "Review";
                }

                if (!result.Errors.Any() && !result.CanAutoApprove && claim.CurrentStage == "CoordinatorReview")
                {
                    result.RecommendedAction = "Review";
                    result.AddInfo("Claim requires manual coordinator review");
                }

                result.IsValid = !result.Errors.Any();
                result.VerifiedAt = DateTime.Now;

                // Store result in session for tracking
                session.SetString($"Claim_{claim.Id}_LastVerification", DateTime.Now.ToString());
                session.SetString($"Claim_{claim.Id}_VerificationResult", System.Text.Json.JsonSerializer.Serialize(result));

            }
            catch (Exception ex)
            {
                result.AddError($"Automated verification failed: {ex.Message}");
                result.IsValid = false;
            }

            return result;
        }

        public async Task<List<Claim>> GetClaimsForAutomatedReviewAsync()
        {
            var session = _httpContextAccessor.HttpContext.Session;
            session.SetString("LastAutomationRun", DateTime.Now.ToString());

            // Get claims that are in CoordinatorReview stage for automation
            return await _context.Claims
                .Include(c => c.User)
                .Where(c => c.CurrentStage == "CoordinatorReview" && c.Status == "Under Review")
                .OrderBy(c => c.SubmittedDate)
                .ToListAsync();
        }

        public async Task<List<Claim>> GetClaimsForCoordinatorReviewAsync()
        {
            return await _context.Claims
                .Include(c => c.User)
                .Where(c => c.CurrentStage == "CoordinatorReview")
                .OrderBy(c => c.SubmittedDate)
                .ToListAsync();
        }

        public async Task<List<Claim>> GetClaimsForManagerReviewAsync()
        {
            return await _context.Claims
                .Include(c => c.User)
                .Where(c => c.CurrentStage == "ManagerReview" && c.IsCoordinatorApproved)
                .OrderBy(c => c.CoordinatorReviewDate)
                .ToListAsync();
        }

        public async Task ProcessAutomatedApprovalsAsync()
        {
            var claims = await GetClaimsForAutomatedReviewAsync();
            var session = _httpContextAccessor.HttpContext.Session;

            session.SetInt32("Automation_ProcessedCount", 0);
            session.SetInt32("Automation_ApprovedCount", 0);
            session.SetInt32("Automation_RejectedCount", 0);

            foreach (var claim in claims)
            {
                // Only process claims at coordinator review stage for auto-approval
                if (claim.CurrentStage == "CoordinatorReview")
                {
                    var verificationResult = await AutomaticallyVerifyClaimAsync(claim);

                    if (verificationResult.CanAutoApprove && verificationResult.IsValid)
                    {
                        // Auto-approve at coordinator level and move to manager
                        claim.IsCoordinatorApproved = true;
                        claim.CoordinatorApprover = "System Auto-Approval";
                        claim.CoordinatorReviewDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
                        claim.CurrentStage = "ManagerReview";
                        claim.Status = "Pending Manager Review";

                        var approvedCount = session.GetInt32("Automation_ApprovedCount") ?? 0;
                        session.SetInt32("Automation_ApprovedCount", approvedCount + 1);
                    }
                    else if (verificationResult.RecommendedAction == "Reject" && !verificationResult.IsValid)
                    {
                        claim.Status = "Rejected";
                        claim.RejectionReason = $"Auto-rejected: {string.Join("; ", verificationResult.Errors)}";
                        claim.CurrentStage = "Completed";

                        var rejectedCount = session.GetInt32("Automation_RejectedCount") ?? 0;
                        session.SetInt32("Automation_RejectedCount", rejectedCount + 1);
                    }

                    var processedCount = session.GetInt32("Automation_ProcessedCount") ?? 0;
                    session.SetInt32("Automation_ProcessedCount", processedCount + 1);
                }
            }

            await _context.SaveChangesAsync();
            session.SetString("LastAutomationCompletion", DateTime.Now.ToString());
        }

        public async Task<WorkflowStatus> GetClaimWorkflowStatusAsync(int claimId)
        {
            var session = _httpContextAccessor.HttpContext.Session;
            var claim = await _context.Claims
                .Include(c => c.User)
                .FirstOrDefaultAsync(c => c.Id == claimId);

            if (claim == null)
                return null;

            var status = new WorkflowStatus
            {
                ClaimId = claimId,
                CurrentStatus = claim.Status,
                CurrentStage = claim.CurrentStage,
                IsCoordinatorApproved = claim.IsCoordinatorApproved,
                IsManagerApproved = claim.IsManagerApproved,
                LastVerified = session.GetString($"Claim_{claimId}_LastVerification"),
                VerificationAttempts = session.GetInt32($"Claim_{claimId}_VerificationAttempts") ?? 0,
                IsEligibleForAutoApproval = false
            };

            // Check if claim is in session for tracking
            var verificationResultJson = session.GetString($"Claim_{claimId}_VerificationResult");
            if (!string.IsNullOrEmpty(verificationResultJson))
            {
                var previousResult = System.Text.Json.JsonSerializer.Deserialize<AutomationResult>(verificationResultJson);
                status.IsEligibleForAutoApproval = previousResult?.CanAutoApprove ?? false;
                status.LastVerificationResult = previousResult?.RecommendedAction;
            }

            return status;
        }
    }

    public class AutomationResult
    {
        public bool IsValid { get; set; }
        public bool CanAutoApprove { get; set; }
        public string RecommendedAction { get; set; }
        public string AutoApprovalReason { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
        public List<string> Warnings { get; set; } = new List<string>();
        public List<string> Info { get; set; } = new List<string>();
        public DateTime VerifiedAt { get; set; }

        public void AddError(string error) => Errors.Add(error);
        public void AddWarning(string warning) => Warnings.Add(warning);
        public void AddInfo(string info) => Info.Add(info);
    }

    public class VerificationCriteria
    {
        public decimal MaxHoursPerMonth { get; set; }
        public decimal MinHoursPerClaim { get; set; }
        public decimal MaxHourlyRate { get; set; }
        public decimal RequireDocumentationForHighHours { get; set; }
        public decimal AutoApproveThreshold { get; set; }
        public decimal MaxAutoApproveAmount { get; set; }
    }

    public class WorkflowStatus
    {
        public int ClaimId { get; set; }
        public string CurrentStatus { get; set; }
        public string CurrentStage { get; set; }
        public string LastVerified { get; set; }
        public int VerificationAttempts { get; set; }
        public bool IsEligibleForAutoApproval { get; set; }
        public string LastVerificationResult { get; set; }
        public bool IsCoordinatorApproved { get; set; }
        public bool IsManagerApproved { get; set; }
    }
}