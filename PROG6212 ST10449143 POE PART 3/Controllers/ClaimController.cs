using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PROG6212_ST10449143_POE_PART_1.Models;
using PROG6212_ST10449143_POE_PART_1.Services;

namespace PROG6212_ST10449143_POE_PART_1.Controllers
{
    [Authorize(Roles = "Lecturer,Coordinator,AcademicManager,HR")]
    public class ClaimsController : Controller
    {
        private readonly IClaimService _claimService;
        private readonly IClaimAutomationService _automationService;
        private readonly IWebHostEnvironment _environment;
        private readonly AppDbContext _context;
        private readonly UserManager<User> _userManager;

        public ClaimsController(
            IClaimService claimService,
            IClaimAutomationService automationService,
            IWebHostEnvironment environment,
            AppDbContext context,
            UserManager<User> userManager)
        {
            _claimService = claimService;
            _automationService = automationService;
            _environment = environment;
            _context = context;
            _userManager = userManager;
        }

        [Authorize(Roles = "Lecturer")]
        public async Task<IActionResult> Submit()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var model = new ClaimViewModel
            {
                LecturerName = $"{currentUser.FirstName} {currentUser.LastName}",
                HourlyRate = currentUser.HourlyRate
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Lecturer")]
        public async Task<IActionResult> Submit(ClaimViewModel model, IFormFile supportingDocument)
        {
            ModelState.Remove("LecturerName");
            ModelState.Remove("HourlyRate");
            ModelState.Remove("AdditionalNotes");

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                TempData["ErrorMessage"] = "User not found. Please log in again.";
                return RedirectToAction("Login", "Account");
            }

            // Validate hours worked 
            if (model.HoursWorked > 180m)
            {
                ModelState.AddModelError("HoursWorked", "Hours worked cannot exceed 180 hours per month.");
            }

            if (model.HoursWorked < 0.5m)
            {
                ModelState.AddModelError("HoursWorked", "Hours worked must be at least 0.5 hours.");
            }

            if (ModelState.IsValid)
            {
                string fileName = null;

                if (supportingDocument != null && supportingDocument.Length > 0)
                {
                    var allowedExtensions = new[] { ".pdf", ".docx", ".xlsx", ".jpg", ".png", ".jpeg" };
                    var maxFileSize = 5 * 1024 * 1024;

                    var extension = Path.GetExtension(supportingDocument.FileName).ToLowerInvariant();
                    if (!allowedExtensions.Contains(extension))
                    {
                        ModelState.AddModelError("", "Only PDF, DOCX, XLSX, JPG, PNG files are allowed.");
                        model.LecturerName = $"{currentUser.FirstName} {currentUser.LastName}";
                        model.HourlyRate = currentUser.HourlyRate;
                        return View(model);
                    }

                    if (supportingDocument.Length > maxFileSize)
                    {
                        ModelState.AddModelError("", "File size must be less than 5MB.");
                        model.LecturerName = $"{currentUser.FirstName} {currentUser.LastName}";
                        model.HourlyRate = currentUser.HourlyRate;
                        return View(model);
                    }

                    try
                    {
                        var wwwrootPath = _environment.WebRootPath;
                        if (string.IsNullOrEmpty(wwwrootPath))
                        {
                            wwwrootPath = Path.Combine(_environment.ContentRootPath, "wwwroot");
                        }

                        var uploadsFolder = Path.Combine(wwwrootPath, "uploads");
                        if (!Directory.Exists(uploadsFolder))
                            Directory.CreateDirectory(uploadsFolder);

                        fileName = $"{Guid.NewGuid()}{extension}";
                        var filePath = Path.Combine(uploadsFolder, fileName);

                        using (var stream = new FileStream(filePath, FileMode.Create))
                        {
                            await supportingDocument.CopyToAsync(stream);
                        }

                        fileName = $"{fileName}|{supportingDocument.FileName}";
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"File upload error: {ex.Message}");
                        ModelState.AddModelError("", "Error uploading file. Please try again.");
                        model.LecturerName = $"{currentUser.FirstName} {currentUser.LastName}";
                        model.HourlyRate = currentUser.HourlyRate;
                        return View(model);
                    }
                }

                try
                {
                    var claim = new Claim
                    {
                        UserId = currentUser.Id,
                        Month = model.Month,
                        HoursWorked = model.HoursWorked,
                        HourlyRate = currentUser.HourlyRate,
                        AdditionalNotes = model.AdditionalNotes ?? string.Empty,
                        SupportingDocument = fileName ?? string.Empty,
                        Status = "Submitted",
                        SubmittedDate = DateTime.Now
                    };

                    Console.WriteLine($"Attempting to save claim for user: {currentUser.Id}");

                    await _claimService.AddClaimAsync(claim);

                    Console.WriteLine($"Claim saved successfully with ID: {claim.Id}");

                    // Run automated verification on new claim
                    try
                    {
                        if (_automationService != null)
                        {
                            var verificationResult = await _automationService.AutomaticallyVerifyClaimAsync(claim);
                            if (verificationResult.CanAutoApprove && verificationResult.IsValid)
                            {
                                await _claimService.UpdateClaimStatusAsync(claim.Id, "Approved", verificationResult.AutoApprovalReason);
                                TempData["SuccessMessage"] = "Claim submitted and automatically approved!";
                            }
                            else
                            {
                                TempData["SuccessMessage"] = "Claim submitted successfully! Awaiting review.";
                            }
                        }
                        else
                        {
                            TempData["SuccessMessage"] = "Claim submitted successfully!";
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Automated verification error: {ex.Message}");
                        TempData["SuccessMessage"] = "Claim submitted successfully!";
                    }

                    return RedirectToAction("Submit");
                }
                catch (Exception ex)
                {
                    
                    Console.WriteLine($"Error saving claim: {ex.Message}");
                    Console.WriteLine($"Inner exception: {ex.InnerException?.Message}");
                    Console.WriteLine($"Stack trace: {ex.StackTrace}");

                    // Provide more specific error messages
                    string errorMessage = ex.InnerException?.Message ?? ex.Message;

                    if (errorMessage.Contains("FK") || errorMessage.Contains("user"))
                    {
                        ModelState.AddModelError("", "User authentication error. Please log out and log in again.");
                    }
                    else if (errorMessage.Contains("required") || errorMessage.Contains("null"))
                    {
                        ModelState.AddModelError("", "Missing required information. Please check your input and try again.");
                    }
                    else
                    {
                        ModelState.AddModelError("", $"An error occurred while saving your claim: {errorMessage}");
                    }

                    model.LecturerName = $"{currentUser.FirstName} {currentUser.LastName}";
                    model.HourlyRate = currentUser.HourlyRate;
                    return View(model);
                }
            }

            // Repopulate user data if validation fails
            model.LecturerName = $"{currentUser.FirstName} {currentUser.LastName}";
            model.HourlyRate = currentUser.HourlyRate;
            return View(model);
        }

        [Authorize(Roles = "Lecturer,Coordinator,AcademicManager,HR")]
        public async Task<IActionResult> ViewClaims()
        {
            try
            {
                var currentUser = await _userManager.GetUserAsync(User);
                List<Claim> claims;

                if (User.IsInRole("Lecturer"))
                {
                    // Lecturers only see their own claims
                    var allClaims = await _claimService.GetAllClaimsAsync();
                    claims = allClaims.Where(c => c.UserId == currentUser?.Id).ToList();
                }
                else
                {
                    // Coordinators, Academic Managers and HR see all claims
                    claims = await _claimService.GetAllClaimsAsync();
                }

                return View(claims);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading claims: {ex.Message}");
                TempData["ErrorMessage"] = "Error loading claims. Please try again.";
                return View(new List<Claim>());
            }
        }

        [Authorize(Roles = "Coordinator,AcademicManager,HR")]
        public async Task<IActionResult> Approvals()
        {
            try
            {
                // Security check using sessions
                HttpContext.Session.SetString("LastAccess_Approvals", DateTime.Now.ToString());
                HttpContext.Session.SetString("CurrentUser_Approvals", User.Identity.Name);

                // Automatically move submitted claims to Under Review when viewing approvals
                var submittedClaims = await _context.Claims
                    .Where(c => c.Status == "Submitted")
                    .ToListAsync();

                foreach (var claim in submittedClaims)
                {
                    claim.Status = "Under Review";
                }

                if (submittedClaims.Any())
                {
                    await _context.SaveChangesAsync();
                }

                var pendingClaims = await _claimService.GetPendingClaimsAsync();
                return View(pendingClaims);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading approvals: {ex.Message}");
                TempData["ErrorMessage"] = "Error loading claims for approval. Please try again.";
                return View(new List<Claim>());
            }
        }

        [Authorize(Roles = "Coordinator,AcademicManager")]
        public async Task<IActionResult> CoordinatorDashboard()
        {
            // Security check using sessions
            var currentUser = await _userManager.GetUserAsync(User);
            HttpContext.Session.SetString("LastAccess_Coordinator", DateTime.Now.ToString());
            HttpContext.Session.SetString("CurrentUserRole", "Coordinator");

            if (!User.IsInRole("Coordinator") && !User.IsInRole("AcademicManager"))
            {
                TempData["ErrorMessage"] = "Access denied. Coordinator or Academic Manager role required.";
                return RedirectToAction("AccessDenied", "Account");
            }

            var claims = await _automationService.GetClaimsForAutomatedReviewAsync();
            var dashboardModel = new CoordinatorDashboardViewModel
            {
                PendingClaims = claims,
                AutomationStats = new AutomationStatistics
                {
                    TotalProcessed = HttpContext.Session.GetInt32("Automation_ProcessedCount") ?? 0,
                    AutoApproved = HttpContext.Session.GetInt32("Automation_ApprovedCount") ?? 0,
                    AutoRejected = HttpContext.Session.GetInt32("Automation_RejectedCount") ?? 0,
                    LastRun = HttpContext.Session.GetString("LastAutomationCompletion")
                }
            };

            return View(dashboardModel);
        }

        [Authorize(Roles = "AcademicManager")]
        public async Task<IActionResult> AcademicManagerDashboard()
        {
            // Security check using sessions
            var currentUser = await _userManager.GetUserAsync(User);
            HttpContext.Session.SetString("LastAccess_AcademicManager", DateTime.Now.ToString());
            HttpContext.Session.SetString("CurrentUserRole", "AcademicManager");

            if (!User.IsInRole("AcademicManager"))
            {
                TempData["ErrorMessage"] = "Access denied. Academic Manager role required.";
                return RedirectToAction("AccessDenied", "Account");
            }

            var claims = await _context.Claims
                .Include(c => c.User)
                .Where(c => c.Status == "Under Review" || c.Status == "Submitted")
                .OrderByDescending(c => c.HoursWorked)
                .ThenByDescending(c => c.TotalAmount)
                .ToListAsync();

            var highValueClaims = claims.Where(c => c.TotalAmount > 10000).ToList();
            var departmentSummary = claims.GroupBy(c => c.User.Department)
                .Select(g => new DepartmentSummary
                {
                    Department = g.Key ?? "Unknown",
                    TotalClaims = g.Count(),
                    TotalAmount = g.Sum(c => c.TotalAmount),
                    AverageHours = g.Average(c => c.HoursWorked)
                }).ToList();

            var managerModel = new AcademicManagerDashboardViewModel
            {
                AllPendingClaims = claims,
                HighValueClaims = highValueClaims,
                DepartmentSummaries = departmentSummary,
                TotalPendingAmount = claims.Sum(c => c.TotalAmount),
                AutomationEfficiency = CalculateAutomationEfficiency()
            };

            return View(managerModel);
        }

        [HttpPost]
        [Authorize(Roles = "Coordinator,HR")]
        public async Task<IActionResult> Approve(int id)
        {
            try
            {
                Console.WriteLine($"=== APPROVE ACTION CALLED for claim {id} ===");

                // Verify claim exists first
                var claim = await _claimService.GetClaimByIdAsync(id);
                if (claim == null)
                {
                    TempData["ErrorMessage"] = $"Claim with ID {id} not found.";
                    return RedirectToAction("Approvals");
                }

                Console.WriteLine($"Found claim for user: {claim.UserId}, Current status: {claim.Status}");

                await _claimService.UpdateClaimStatusAsync(id, "Approved");

                Console.WriteLine($"Claim {id} approved successfully");
                TempData["ApprovalMessage"] = "Claim approved successfully!";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error approving claim {id}: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                TempData["ErrorMessage"] = $"Error approving claim: {ex.Message}";
            }

            return RedirectToAction("Approvals");
        }

        [HttpPost]
        [Authorize(Roles = "Coordinator,HR")]
        public async Task<IActionResult> Reject(int id, string rejectionReason)
        {
            try
            {
                Console.WriteLine($"=== REJECT ACTION CALLED for claim {id} ===");

                var claim = await _claimService.GetClaimByIdAsync(id);
                if (claim == null)
                {
                    TempData["ErrorMessage"] = $"Claim with ID {id} not found.";
                    return RedirectToAction("Approvals");
                }

                if (string.IsNullOrEmpty(rejectionReason))
                {
                    TempData["ErrorMessage"] = "Rejection reason is required.";
                    return RedirectToAction("Approvals");
                }

                await _claimService.UpdateClaimStatusAsync(id, "Rejected", rejectionReason);

                Console.WriteLine($"Claim {id} rejected successfully");
                TempData["ApprovalMessage"] = "Claim rejected successfully!";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error rejecting claim {id}: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                TempData["ErrorMessage"] = $"Error rejecting claim: {ex.Message}";
            }

            return RedirectToAction("Approvals");
        }

        [HttpPost]
        [Authorize(Roles = "Coordinator,AcademicManager")]
        public async Task<IActionResult> RunAutomatedVerification(int claimId)
        {
            var claim = await _claimService.GetClaimByIdAsync(claimId);
            if (claim == null)
            {
                return Json(new { success = false, error = "Claim not found" });
            }

            var result = await _automationService.AutomaticallyVerifyClaimAsync(claim);

            // Store verification result in session
            HttpContext.Session.SetString($"VerificationResult_{claimId}",
                System.Text.Json.JsonSerializer.Serialize(result));

            return Json(new
            {
                success = true,
                isValid = result.IsValid,
                canAutoApprove = result.CanAutoApprove,
                recommendedAction = result.RecommendedAction,
                errors = result.Errors,
                warnings = result.Warnings,
                info = result.Info
            });
        }

        [HttpPost]
        [Authorize(Roles = "Coordinator,AcademicManager")]
        public async Task<IActionResult> BulkAutomatedProcessing()
        {
            try
            {
                HttpContext.Session.SetString("BulkProcessing_Started", DateTime.Now.ToString());
                await _automationService.ProcessAutomatedApprovalsAsync();

                var processed = HttpContext.Session.GetInt32("Automation_ProcessedCount") ?? 0;
                var approved = HttpContext.Session.GetInt32("Automation_ApprovedCount") ?? 0;
                var rejected = HttpContext.Session.GetInt32("Automation_RejectedCount") ?? 0;

                HttpContext.Session.SetString("BulkProcessing_Completed", DateTime.Now.ToString());

                TempData["SuccessMessage"] =
                    $"Bulk processing completed: {processed} claims processed, {approved} approved, {rejected} rejected.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Bulk processing failed: {ex.Message}";
            }

            return RedirectToAction("CoordinatorDashboard");
        }

        [HttpGet]
        [Authorize(Roles = "Coordinator,AcademicManager")]
        public async Task<IActionResult> GetClaimVerificationStatus(int claimId)
        {
            var status = await _automationService.GetClaimWorkflowStatusAsync(claimId);
            return Json(new
            {
                claimId = status.ClaimId,
                currentStatus = status.CurrentStatus,
                lastVerified = status.LastVerified,
                verificationAttempts = status.VerificationAttempts,
                eligibleForAutoApproval = status.IsEligibleForAutoApproval,
                lastResult = status.LastVerificationResult
            });
        }

        [HttpPost]
        [Authorize(Roles = "AcademicManager")]
        public async Task<IActionResult> OverrideApproval(int claimId, string reason)
        {
            // Security check - only AcademicManager can override
            if (!User.IsInRole("AcademicManager"))
            {
                return Json(new { success = false, error = "Access denied" });
            }

            var claim = await _claimService.GetClaimByIdAsync(claimId);
            if (claim == null)
            {
                return Json(new { success = false, error = "Claim not found" });
            }

            // Store override in session for audit
            HttpContext.Session.SetString($"Override_{claimId}",
                $"{DateTime.Now}: {User.Identity.Name} - {reason}");

            await _claimService.UpdateClaimStatusAsync(claimId, "Approved",
                $"Manually approved by Academic Manager: {reason}");

            return Json(new { success = true, message = "Claim approved manually" });
        }

        [HttpPost]
        [Authorize(Roles = "Lecturer,HR")]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var currentUser = await _userManager.GetUserAsync(User);
                var claim = await _claimService.GetClaimByIdAsync(id);

                // Security check: Lecturers can only delete their own claims
                if (User.IsInRole("Lecturer") && claim?.UserId != currentUser?.Id)
                {
                    TempData["ErrorMessage"] = "You can only delete your own claims.";
                    return RedirectToAction("ViewClaims");
                }

                var result = await _claimService.DeleteClaimAsync(id);
                if (result)
                {
                    TempData["SuccessMessage"] = "Claim deleted successfully!";
                }
                else
                {
                    TempData["ErrorMessage"] = "Error deleting claim. Claim not found.";
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "An error occurred while deleting the claim.";
                Console.WriteLine($"Delete error: {ex.Message}");
            }

            return RedirectToAction("ViewClaims");
        }

        [Authorize(Roles = "Lecturer")]
        public async Task<IActionResult> TrackStatus()
        {
            try
            {
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser == null)
                {
                    return RedirectToAction("Login", "Account");
                }

                var allClaims = await _claimService.GetAllClaimsAsync();
                var userClaims = allClaims.Where(c => c.UserId == currentUser.Id)
                                         .OrderByDescending(c => c.SubmittedDate)
                                         .ToList();

                return View(userClaims);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading track status: {ex.Message}");
                TempData["ErrorMessage"] = "Error loading claim status. Please try again.";
                return View(new List<Claim>());
            }
        }

        [Authorize(Roles = "Lecturer,Coordinator,AcademicManager,HR")]
        public async Task<IActionResult> Details(int id)
        {
            try
            {
                var claim = await _claimService.GetClaimByIdAsync(id);
                if (claim == null)
                {
                    TempData["ErrorMessage"] = "Claim not found.";
                    return RedirectToAction("ViewClaims");
                }

                // Security check: Lecturers can only view their own claim details
                var currentUser = await _userManager.GetUserAsync(User);
                if (User.IsInRole("Lecturer") && claim.UserId != currentUser?.Id)
                {
                    TempData["ErrorMessage"] = "Access denied.";
                    return RedirectToAction("TrackStatus");
                }

                return View(claim);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading claim details: {ex.Message}");
                TempData["ErrorMessage"] = "Error loading claim details.";
                return RedirectToAction("ViewClaims");
            }
        }

        [Authorize(Roles = "Lecturer")]
        [HttpPost]
        public async Task<IActionResult> CalculateTotal(decimal hoursWorked)
        {
            try
            {
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser == null)
                {
                    return Json(new { success = false, error = "User not found" });
                }

                // Validate hours - Fixed decimal comparisons
                if (hoursWorked < 0.5m)
                {
                    return Json(new
                    {
                        success = false,
                        error = "Hours must be at least 0.5",
                        isValid = false
                    });
                }

                if (hoursWorked > 180m)
                {
                    return Json(new
                    {
                        success = false,
                        error = "Hours cannot exceed 180 per month",
                        isValid = false
                    });
                }

                var hourlyRate = currentUser.HourlyRate;
                var totalAmount = hoursWorked * hourlyRate;
                var remainingHours = 180m - hoursWorked; // Fixed: using 180m for decimal
                var progressPercentage = (double)(hoursWorked / 180m) * 100; // Fixed: cast to double for percentage

                return Json(new
                {
                    success = true,
                    hourlyRate = hourlyRate,
                    totalAmount = totalAmount,
                    remainingHours = remainingHours,
                    progressPercentage = progressPercentage,
                    isValid = true
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        [Authorize(Roles = "Lecturer")]
        [HttpGet]
        public async Task<IActionResult> GetUserProfileData()
        {
            try
            {
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser == null)
                {
                    return Json(new { success = false, error = "User not found" });
                }

                return Json(new
                {
                    success = true,
                    lecturerName = $"{currentUser.FirstName} {currentUser.LastName}",
                    hourlyRate = currentUser.HourlyRate
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        private AutomationEfficiency CalculateAutomationEfficiency()
        {
            var processed = HttpContext.Session.GetInt32("Automation_ProcessedCount") ?? 1;
            var approved = HttpContext.Session.GetInt32("Automation_ApprovedCount") ?? 0;
            var rejected = HttpContext.Session.GetInt32("Automation_RejectedCount") ?? 0;

            return new AutomationEfficiency
            {
                TotalProcessed = processed,
                AutoApprovalRate = (decimal)approved / processed * 100,
                AutoRejectionRate = (decimal)rejected / processed * 100,
                ManualReviewRate = (decimal)(processed - approved - rejected) / processed * 100
            };
        }
    }

    public class CoordinatorDashboardViewModel
    {
        public List<Claim> PendingClaims { get; set; }
        public AutomationStatistics AutomationStats { get; set; }
    }

    public class AcademicManagerDashboardViewModel
    {
        public List<Claim> AllPendingClaims { get; set; }
        public List<Claim> HighValueClaims { get; set; }
        public List<DepartmentSummary> DepartmentSummaries { get; set; }
        public decimal TotalPendingAmount { get; set; }
        public AutomationEfficiency AutomationEfficiency { get; set; }
    }

    public class AutomationStatistics
    {
        public int TotalProcessed { get; set; }
        public int AutoApproved { get; set; }
        public int AutoRejected { get; set; }
        public string LastRun { get; set; }
    }

    public class DepartmentSummary
    {
        public string Department { get; set; }
        public int TotalClaims { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal AverageHours { get; set; }
    }

    public class AutomationEfficiency
    {
        public int TotalProcessed { get; set; }
        public decimal AutoApprovalRate { get; set; }
        public decimal AutoRejectionRate { get; set; }
        public decimal ManualReviewRate { get; set; }
    }
}