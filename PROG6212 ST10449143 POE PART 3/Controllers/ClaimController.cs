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
        private readonly IWebHostEnvironment _environment;
        private readonly AppDbContext _context;
        private readonly UserManager<User> _userManager;
        private readonly IAutomatedVerificationService _verificationService;

        public ClaimsController(
            IClaimService claimService,
            IWebHostEnvironment environment,
            AppDbContext context,
            UserManager<User> userManager,
            IAutomatedVerificationService verificationService)
        {
            _claimService = claimService;
            _environment = environment;
            _context = context;
            _userManager = userManager;
            _verificationService = verificationService;
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
            // Session-based form validation
            var session = HttpContext.Session;
            var formSessionKey = $"FormSubmission_{DateTime.Now:yyyyMMddHHmmss}";

            if (session.GetString(formSessionKey) != null)
            {
                ModelState.AddModelError("", "Duplicate form submission detected.");
                return View(model);
            }

            ModelState.Remove("LecturerName");
            ModelState.Remove("HourlyRate");
            ModelState.Remove("AdditionalNotes");

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return RedirectToAction("Login", "Account");
            }

            // Enhanced validation using automated service
            var tempClaim = new Claim
            {
                UserId = currentUser.Id,
                Month = model.Month,
                HoursWorked = model.HoursWorked,
                HourlyRate = currentUser.HourlyRate
            };

            var verification = await _verificationService.VerifyClaimAsync(tempClaim);
            if (!verification.IsValid)
            {
                foreach (var error in verification.Errors)
                {
                    ModelState.AddModelError("", error);
                }
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
                        SupportingDocument = fileName,
                        Status = "Submitted",
                        SubmittedDate = DateTime.Now
                    };

                    await _claimService.AddClaimAsync(claim);

                    // Execute automated workflow
                    var workflowResult = await _verificationService.ExecuteApprovalWorkflowAsync(claim.Id);

                    // Store session for workflow result
                    session.SetString($"Workflow_{claim.Id}", System.Text.Json.JsonSerializer.Serialize(workflowResult));
                    session.SetString(formSessionKey, "submitted"); // Prevent duplicate submissions

                    if (workflowResult.Success && !workflowResult.RequiresManualReview)
                    {
                        TempData["SuccessMessage"] = $"Claim submitted and automatically approved! Amount: R {workflowResult.ApprovedAmount:0.00}";
                    }
                    else if (workflowResult.RequiresManualReview)
                    {
                        TempData["SuccessMessage"] = "Claim submitted successfully and is under review.";
                    }
                    else
                    {
                        TempData["ErrorMessage"] = $"Claim submitted but requires attention: {workflowResult.Message}";
                    }

                    return RedirectToAction("Submit");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error saving claim: {ex.Message}");
                    ModelState.AddModelError("", "An error occurred while saving your claim. Please try again.");
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

                // Store view access in session
                HttpContext.Session.SetString($"ViewClaimsAccess_{User.Identity.Name}", DateTime.Now.ToString("yyyyMMddHHmmss"));

                return View(claims);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading claims: {ex.Message}");
                TempData["ErrorMessage"] = "Error loading claims. Please try again.";
                return View(new List<Claim>());
            }
        }

        [Authorize(Roles = "Coordinator")]
        public async Task<IActionResult> CoordinatorApprovals()
        {
            // Session tracking for coordinators
            var session = HttpContext.Session;
            session.SetString("CoordinatorAccess", $"{User.Identity.Name}_{DateTime.Now:yyyyMMddHHmmss}");

            try
            {
                // Get claims that need coordinator review (normal limits)
                var claims = await _context.Claims
                    .Include(c => c.User)
                    .Where(c => c.Status == "Under Review" &&
                               c.HoursWorked <= 160 &&
                               c.HourlyRate <= 400)
                    .OrderByDescending(c => c.SubmittedDate)
                    .ToListAsync();

                // Store claims count in session
                session.SetString("CoordinatorPendingCount", claims.Count.ToString());

                return View(claims);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading coordinator approvals: {ex.Message}");
                TempData["ErrorMessage"] = "Error loading claims for coordinator review.";
                return View(new List<Claim>());
            }
        }

        [Authorize(Roles = "AcademicManager")]
        public async Task<IActionResult> AcademicManagerApprovals()
        {
            // Session tracking for academic managers
            var session = HttpContext.Session;
            session.SetString("AcademicManagerAccess", $"{User.Identity.Name}_{DateTime.Now:yyyyMMddHHmmss}");

            try
            {
                // Get claims that need final approval 
                var claims = await _context.Claims
                    .Include(c => c.User)
                    .Where(c => c.Status == "Under Review" &&
                               (c.HoursWorked > 160 || c.HourlyRate > 400 || c.TotalAmount > 10000 || c.Status == "Coordinator Recommended"))
                    .OrderByDescending(c => c.TotalAmount)
                    .ToListAsync();

                // Store claims count in session
                session.SetString("AcademicManagerPendingCount", claims.Count.ToString());

                return View(claims);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading academic manager approvals: {ex.Message}");
                TempData["ErrorMessage"] = "Error loading claims for final approval.";
                return View(new List<Claim>());
            }
        }

        [HttpPost]
        [Authorize(Roles = "Coordinator")]
        public async Task<IActionResult> CoordinatorApprove(int id, string reviewNotes)
        {
            // Session-based approval tracking
            var session = HttpContext.Session;
            var approvalKey = $"CoordinatorApproval_{id}";

            if (session.GetString(approvalKey) != null)
            {
                TempData["ErrorMessage"] = "This claim has already been processed by coordinator.";
                return RedirectToAction("CoordinatorApprovals");
            }

            try
            {
                var claim = await _claimService.GetClaimByIdAsync(id);
                if (claim == null)
                {
                    TempData["ErrorMessage"] = $"Claim with ID {id} not found.";
                    return RedirectToAction("CoordinatorApprovals");
                }

                // Additional verification before approval
                var verification = await _verificationService.VerifyClaimAsync(claim);
                if (!verification.IsValid)
                {
                    TempData["ErrorMessage"] = $"Cannot recommend approval: {string.Join(", ", verification.Errors)}";
                    return RedirectToAction("CoordinatorApprovals");
                }

                claim.Status = "Coordinator Recommended";

                if (!string.IsNullOrEmpty(reviewNotes))
                {
                    session.SetString($"CoordinatorNotes_{id}", reviewNotes);
                }

                await _context.SaveChangesAsync();

                // Record approval in session
                session.SetString(approvalKey, $"recommended_by_{User.Identity.Name}_{DateTime.Now:yyyyMMddHHmmss}");

                TempData["SuccessMessage"] = "Claim recommended for approval to Academic Manager.";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in coordinator approval {id}: {ex.Message}");
                TempData["ErrorMessage"] = $"Error processing claim: {ex.Message}";
            }

            return RedirectToAction("CoordinatorApprovals");
        }

        [HttpPost]
        [Authorize(Roles = "Coordinator")]
        public async Task<IActionResult> CoordinatorReject(int id, string rejectionReason)
        {
            var session = HttpContext.Session;
            var rejectionKey = $"CoordinatorRejection_{id}";

            if (session.GetString(rejectionKey) != null)
            {
                TempData["ErrorMessage"] = "This claim has already been processed by coordinator.";
                return RedirectToAction("CoordinatorApprovals");
            }

            try
            {
                var claim = await _claimService.GetClaimByIdAsync(id);
                if (claim == null)
                {
                    TempData["ErrorMessage"] = $"Claim with ID {id} not found.";
                    return RedirectToAction("CoordinatorApprovals");
                }

                if (string.IsNullOrEmpty(rejectionReason))
                {
                    TempData["ErrorMessage"] = "Rejection reason is required.";
                    return RedirectToAction("CoordinatorApprovals");
                }

                await _claimService.UpdateClaimStatusAsync(id, "Rejected", $"Coordinator rejection: {rejectionReason}");

                session.SetString(rejectionKey, $"rejected_by_{User.Identity.Name}_{DateTime.Now:yyyyMMddHHmmss}");

                TempData["SuccessMessage"] = "Claim rejected successfully!";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in coordinator rejection {id}: {ex.Message}");
                TempData["ErrorMessage"] = $"Error rejecting claim: {ex.Message}";
            }

            return RedirectToAction("CoordinatorApprovals");
        }

        [HttpPost]
        [Authorize(Roles = "AcademicManager")]
        public async Task<IActionResult> FinalApprove(int id)
        {
            var session = HttpContext.Session;
            var finalApprovalKey = $"FinalApproval_{id}";

            if (session.GetString(finalApprovalKey) != null)
            {
                TempData["ErrorMessage"] = "This claim has already been finally approved.";
                return RedirectToAction("AcademicManagerApprovals");
            }

            try
            {
                var claim = await _claimService.GetClaimByIdAsync(id);
                if (claim == null)
                {
                    TempData["ErrorMessage"] = $"Claim with ID {id} not found.";
                    return RedirectToAction("AcademicManagerApprovals");
                }

                // Academic Manager has final approval authority
                await _claimService.UpdateClaimStatusAsync(id, "Approved", "Finally approved by Academic Manager");

                session.SetString(finalApprovalKey, $"approved_by_{User.Identity.Name}_{DateTime.Now:yyyyMMddHHmmss}");

                TempData["SuccessMessage"] = "Claim finally approved by Academic Manager!";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in final approval {id}: {ex.Message}");
                TempData["ErrorMessage"] = $"Error finally approving claim: {ex.Message}";
            }

            return RedirectToAction("AcademicManagerApprovals");
        }

        [HttpPost]
        [Authorize(Roles = "AcademicManager")]
        public async Task<IActionResult> FinalReject(int id, string finalRejectionReason)
        {
            var session = HttpContext.Session;
            var finalRejectionKey = $"FinalRejection_{id}";

            if (session.GetString(finalRejectionKey) != null)
            {
                TempData["ErrorMessage"] = "This claim has already been finally rejected.";
                return RedirectToAction("AcademicManagerApprovals");
            }

            try
            {
                var claim = await _claimService.GetClaimByIdAsync(id);
                if (claim == null)
                {
                    TempData["ErrorMessage"] = $"Claim with ID {id} not found.";
                    return RedirectToAction("AcademicManagerApprovals");
                }

                if (string.IsNullOrEmpty(finalRejectionReason))
                {
                    TempData["ErrorMessage"] = "Final rejection reason is required.";
                    return RedirectToAction("AcademicManagerApprovals");
                }

                await _claimService.UpdateClaimStatusAsync(id, "Rejected", $"Finally rejected by Academic Manager: {finalRejectionReason}");

                session.SetString(finalRejectionKey, $"rejected_by_{User.Identity.Name}_{DateTime.Now:yyyyMMddHHmmss}");

                TempData["SuccessMessage"] = "Claim finally rejected by Academic Manager!";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in final rejection {id}: {ex.Message}");
                TempData["ErrorMessage"] = $"Error finally rejecting claim: {ex.Message}";
            }

            return RedirectToAction("AcademicManagerApprovals");
        }

        [Authorize(Roles = "Coordinator,AcademicManager,HR")]
        public async Task<IActionResult> AllApprovals()
        {
            // Combined view for HR and overview purposes
            var session = HttpContext.Session;
            session.SetString("AllApprovalsAccess", $"{User.Identity.Name}_{DateTime.Now:yyyyMMddHHmmss}");

            try
            {
                var allClaims = await _claimService.GetAllClaimsAsync();
                var pendingClaims = allClaims.Where(c => c.Status == "Under Review" ||
                                                       c.Status == "Coordinator Recommended" ||
                                                       c.Status == "Submitted").ToList();

                return View(pendingClaims);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading all approvals: {ex.Message}");
                TempData["ErrorMessage"] = "Error loading claims for approval.";
                return View(new List<Claim>());
            }
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

                // Session tracking for deletions
                var session = HttpContext.Session;
                var deleteKey = $"Delete_{id}";
                session.SetString(deleteKey, $"deleted_by_{User.Identity.Name}_{DateTime.Now:yyyyMMddHHmmss}");

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

                // Store tracking session
                HttpContext.Session.SetString($"TrackStatus_{currentUser.Id}", DateTime.Now.ToString("yyyyMMddHHmmss"));

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

                // Enhanced security check with session tracking
                var currentUser = await _userManager.GetUserAsync(User);
                if (!await ValidateUserAccess(claim))
                {
                    TempData["ErrorMessage"] = "Access denied.";
                    return RedirectToAction("TrackStatus");
                }

                // Store details view in session
                HttpContext.Session.SetString($"DetailsView_{id}", $"{User.Identity.Name}_{DateTime.Now:yyyyMMddHHmmss}");

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

                // Validate hours
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
                var remainingHours = 180m - hoursWorked;
                var progressPercentage = (double)(hoursWorked / 180m) * 100;

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

        private async Task<bool> ValidateUserAccess(Claim claim = null)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return false;

            // Lecturers can only access their own claims
            if (User.IsInRole("Lecturer") && claim != null && claim.UserId != currentUser.Id)
            {
                return false;
            }

            // Coordinators and Academic Managers can access all claims
            if (User.IsInRole("Coordinator") || User.IsInRole("AcademicManager") || User.IsInRole("HR"))
            {
                return true;
            }

            // Store access session
            HttpContext.Session.SetString($"UserAccess_{currentUser.Id}", DateTime.Now.ToString("yyyyMMddHHmmss"));

            return true;
        }

        [Authorize(Roles = "HR,AcademicManager")]
        [HttpPost]
        public async Task<IActionResult> RunAutomatedVerification()
        {
            try
            {
                var session = HttpContext.Session;
                var processingKey = $"AutoVerify_{DateTime.Now:yyyyMMdd}";

                if (session.GetInt32(processingKey) >= 5)
                {
                    return Json(new { success = false, message = "Daily automated processing limit reached" });
                }

                await _verificationService.ProcessPendingClaimsAsync();

                var currentCount = session.GetInt32(processingKey) ?? 0;
                session.SetInt32(processingKey, currentCount + 1);

                return Json(new { success = true, message = "Automated verification completed successfully" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }
    }
}