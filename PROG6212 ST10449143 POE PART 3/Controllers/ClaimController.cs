using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PROG6212_ST10449143_POE_PART_1.Models;
using PROG6212_ST10449143_POE_PART_1.Services;

namespace PROG6212_ST10449143_POE_PART_1.Controllers
{
    [Authorize(Roles = "Lecturer,Coordinator,HR")]
    public class ClaimsController : Controller
    {
        private readonly IClaimService _claimService;
        private readonly IWebHostEnvironment _environment;
        private readonly AppDbContext _context;

        public ClaimsController(IClaimService claimService, IWebHostEnvironment environment, AppDbContext context)
        {
            _claimService = claimService;
            _environment = environment;
            _context = context;
        }

        public IActionResult Submit()
        {
            var model = new ClaimViewModel();
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Submit(ClaimViewModel model, IFormFile supportingDocument)
        {
            ModelState.Remove("AdditionalNotes");

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
                        return View(model);
                    }

                    if (supportingDocument.Length > maxFileSize)
                    {
                        ModelState.AddModelError("", "File size must be less than 5MB.");
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
                        return View(model);
                    }
                }

                try
                {
                    var claim = new Claim
                    {
                        LecturerName = model.LecturerName,
                        Month = model.Month,
                        HoursWorked = model.HoursWorked,
                        HourlyRate = model.HourlyRate,
                        AdditionalNotes = model.AdditionalNotes ?? string.Empty,
                        SupportingDocument = fileName,
                        Status = "Submitted"
                    };

                    await _claimService.AddClaimAsync(claim);
                    TempData["SuccessMessage"] = "Claim submitted successfully!";
                    return RedirectToAction("Submit");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error saving claim: {ex.Message}");
                    ModelState.AddModelError("", "An error occurred while saving your claim. Please try again.");
                    return View(model);
                }
            }

            return View(model);
        }

        public async Task<IActionResult> ViewClaims()
        {
            try
            {
                var claims = await _claimService.GetAllClaimsAsync();
                return View(claims);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading claims: {ex.Message}");
                TempData["ErrorMessage"] = "Error loading claims. Please try again.";
                return View(new List<Claim>());
            }
        }

        public async Task<IActionResult> Approvals()
        {
            try
            {
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

        [HttpPost]
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

                Console.WriteLine($"Found claim: {claim.LecturerName}, Current status: {claim.Status}");

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
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
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

        public async Task<IActionResult> TrackStatus()
        {
            try
            {
                var claims = await _claimService.GetAllClaimsAsync();
                return View(claims);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading track status: {ex.Message}");
                TempData["ErrorMessage"] = "Error loading claim status. Please try again.";
                return View(new List<Claim>());
            }
        }
    }
}