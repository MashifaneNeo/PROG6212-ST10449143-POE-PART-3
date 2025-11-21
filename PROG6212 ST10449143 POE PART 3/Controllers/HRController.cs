using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PROG6212_ST10449143_POE_PART_1.Models;
using PROG6212_ST10449143_POE_PART_1.Services;

namespace PROG6212_ST10449143_POE_PART_1.Controllers
{
    [Authorize(Roles = "HR")]
    public class HRController : Controller
    {
        private readonly IHRService _hrService;
        private readonly AppDbContext _context;
        private readonly UserManager<User> _userManager;

        public HRController(IHRService hrService, AppDbContext context, UserManager<User> userManager)
        {
            _hrService = hrService;
            _context = context;
            _userManager = userManager;
        }

        public IActionResult Dashboard()
        {
            return View();
        }

        [HttpGet]
        public IActionResult CreateUser()
        {
            return View(new CreateUserViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateUser(CreateUserViewModel model)
        {
            if (ModelState.IsValid)
            {
                var result = await _hrService.CreateUserAsync(model);
                if (result.Success)
                {
                    TempData["SuccessMessage"] = $"User created successfully! Temporary password: {result.Password}";
                    return RedirectToAction("UserManagement");
                }
                TempData["ErrorMessage"] = $"Error creating user: {result.Error}";
            }
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> UserManagement()
        {
            var users = await _hrService.GetAllUsersAsync();
            return View(users);
        }

        [HttpGet]
        public async Task<IActionResult> EditUser(string id)
        {
            var user = await _hrService.GetUserByIdAsync(id);
            if (user == null)
            {
                TempData["ErrorMessage"] = "User not found";
                return RedirectToAction("UserManagement");
            }

            var model = new UpdateUserViewModel
            {
                Id = user.Id,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email,
                HourlyRate = user.HourlyRate,
                EmployeeId = user.EmployeeId,
                Department = user.Department,
                IsActive = user.IsActive
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditUser(UpdateUserViewModel model)
        {
            if (ModelState.IsValid)
            {
                var success = await _hrService.UpdateUserAsync(model);
                if (success)
                {
                    TempData["SuccessMessage"] = "User updated successfully!";
                    return RedirectToAction("UserManagement");
                }
                TempData["ErrorMessage"] = "Error updating user";
            }
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Reports()
        {
            try
            {
                var allClaims = await _context.Claims.Include(c => c.User).ToListAsync();
                var allUsers = await _userManager.Users.ToListAsync();

                ViewBag.TotalClaims = allClaims.Count;
                ViewBag.ApprovedClaims = allClaims.Count(c => c.Status == "Approved");
                ViewBag.PendingClaims = allClaims.Count(c =>
                    c.Status == "Submitted" ||
                    c.Status == "Under Review" ||
                    c.Status == "Pending Manager Review" ||
                    (c.CurrentStage != "Completed" && c.Status != "Approved" && c.Status != "Rejected")
                );

                ViewBag.RejectedClaims = allClaims.Count(c => c.Status == "Rejected");
                ViewBag.TotalUsers = allUsers.Count;

                // Calculate total amount for approved claims only
                var approvedClaimsAmount = allClaims.Where(c => c.Status == "Approved").Sum(c => c.TotalAmount);
                ViewBag.TotalAmount = approvedClaimsAmount.ToString("N2");

                var approvedClaims = allClaims.Where(c => c.Status == "Approved").ToList();
                ViewBag.AverageClaim = approvedClaims.Any() ?
                    (approvedClaims.Sum(c => c.TotalAmount) / approvedClaims.Count).ToString("N2") : "0.00";

                // Department statistics
                var departmentStats = allClaims
                    .Where(c => c.User != null && !string.IsNullOrEmpty(c.User.Department))
                    .GroupBy(c => c.User.Department)
                    .Select(g => new { Department = g.Key, Count = g.Count() })
                    .OrderByDescending(g => g.Count)
                    .ToList();

                ViewBag.DepartmentStats = departmentStats;

                var sixMonthsAgo = DateTime.Now.AddMonths(-6);
                var recentClaims = allClaims.Where(c => c.SubmittedDate >= sixMonthsAgo).ToList();

                var monthlyTrends = recentClaims
                    .GroupBy(c => new { c.SubmittedDate.Year, c.SubmittedDate.Month })
                    .Select(g => new
                    {
                        Month = new DateTime(g.Key.Year, g.Key.Month, 1).ToString("MMM yyyy"),
                        Claims = g.Count(),
                        Amount = g.Where(c => c.Status == "Approved").Sum(c => c.TotalAmount)
                    })
                    .OrderBy(g => g.Month)
                    .ToList();

                ViewBag.MonthlyTrends = monthlyTrends;

            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading report statistics: {ex.Message}");
                ViewBag.TotalClaims = 0;
                ViewBag.ApprovedClaims = 0;
                ViewBag.PendingClaims = 0;
                ViewBag.RejectedClaims = 0;
                ViewBag.TotalUsers = 0;
                ViewBag.TotalAmount = "0.00";
                ViewBag.AverageClaim = "0.00";
                ViewBag.DepartmentStats = new List<object>();
                ViewBag.MonthlyTrends = new List<object>();
            }

            return View(new ReportFilterViewModel());
        }

        [HttpPost]
        public async Task<IActionResult> GenerateReport(ReportFilterViewModel filters)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    TempData["ErrorMessage"] = "Please provide valid filter criteria.";
                    return RedirectToAction("Reports");
                }

                var claims = await _hrService.GetClaimsForReportAsync(filters);

                if (!claims.Any())
                {
                    TempData["ErrorMessage"] = "No claims found matching the specified criteria.";
                    return RedirectToAction("Reports");
                }

                var reportTitle = $"Claims Report - {DateTime.Now:yyyy-MM-dd}";
                if (filters.StartDate.HasValue && filters.EndDate.HasValue)
                {
                    reportTitle = $"Claims Report - {filters.StartDate.Value:yyyy-MM-dd} to {filters.EndDate.Value:yyyy-MM-dd}";
                }
                else if (!string.IsNullOrEmpty(filters.ReportType))
                {
                    reportTitle = $"{filters.ReportType} Claims Report - {DateTime.Now:yyyy-MM-dd}";
                }

                Console.WriteLine($"Generating PDF report with {claims.Count} claims...");

                var pdfBytes = await _hrService.GeneratePdfReportAsync(claims, reportTitle);

                if (pdfBytes == null || pdfBytes.Length == 0)
                {
                    throw new Exception("PDF generation returned empty result");
                }

                Console.WriteLine($"PDF generated successfully: {pdfBytes.Length} bytes");

                return File(pdfBytes, "application/pdf", $"{reportTitle}.pdf");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error generating report: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");

                TempData["ErrorMessage"] = $"Error generating PDF report: {ex.Message}. Please check if QuestPDF is properly installed.";
                return RedirectToAction("Reports");
            }
        }

        [HttpPost]
        public async Task<IActionResult> DeleteUser(string id)
        {
            try
            {
                var result = await _hrService.DeleteUserAsync(id);
                if (result)
                {
                    TempData["SuccessMessage"] = "User deleted successfully!";
                }
                else
                {
                    TempData["ErrorMessage"] = "Error deleting user. User may have existing claims or user not found.";
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error deleting user: {ex.Message}";
            }

            return RedirectToAction("UserManagement");
        }

        [HttpPost]
        public async Task<IActionResult> ResetPassword(string userId, string newPassword)
        {
            try
            {
                if (string.IsNullOrEmpty(newPassword) || newPassword.Length < 6)
                {
                    TempData["ErrorMessage"] = "Password must be at least 6 characters long.";
                    return RedirectToAction("UserManagement");
                }

                var result = await _hrService.ResetPasswordAsync(userId, newPassword);
                if (result.Success)
                {
                    TempData["SuccessMessage"] = "Password reset successfully!";
                }
                else
                {
                    TempData["ErrorMessage"] = $"Error resetting password: {result.Error}";
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error resetting password: {ex.Message}";
            }

            return RedirectToAction("UserManagement");
        }

        [HttpPost]
        public async Task<IActionResult> ToggleUserStatus(string userId, bool isActive)
        {
            try
            {
                var result = await _hrService.ToggleUserStatusAsync(userId, isActive);
                if (result)
                {
                    TempData["SuccessMessage"] = $"User {(isActive ? "activated" : "deactivated")} successfully!";
                }
                else
                {
                    TempData["ErrorMessage"] = "Error updating user status.";
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error updating user status: {ex.Message}";
            }

            return RedirectToAction("UserManagement");
        }

        [HttpPost]
        public async Task<IActionResult> ResetAcademicManagerPassword()
        {
            try
            {
                var manager = await _userManager.FindByEmailAsync("david.wilson@university.ac.za");
                if (manager == null)
                {
                    return Json(new { success = false, error = "Academic Manager not found" });
                }

                var newPassword = "Academic123!";
                var result = await _hrService.ResetPasswordAsync(manager.Id, newPassword);

                if (result.Success)
                {
                    return Json(new { success = true, password = newPassword });
                }
                else
                {
                    return Json(new { success = false, error = result.Error });
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        [HttpGet]
        [Authorize(Roles = "HR")]
        public async Task<IActionResult> GetDashboardStats()
        {
            try
            {
                var totalUsers = await _userManager.Users.CountAsync();

                var currentMonth = DateTime.Now.Month;
                var currentYear = DateTime.Now.Year;

                var claims = await _context.Claims
                    .Where(c => c.SubmittedDate.Month == currentMonth && c.SubmittedDate.Year == currentYear)
                    .ToListAsync();

                var pendingClaims = claims.Count(c => c.Status == "Submitted" || c.Status == "Under Review");
                var approvedClaims = claims.Count(c => c.Status == "Approved");
                var totalAmount = claims.Where(c => c.Status == "Approved").Sum(c => c.TotalAmount);

                return Json(new
                {
                    totalUsers,
                    pendingClaims,
                    approvedClaims,
                    totalAmount
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting dashboard stats: {ex.Message}");
                return Json(new
                {
                    totalUsers = 0,
                    pendingClaims = 0,
                    approvedClaims = 0,
                    totalAmount = 0m
                });
            }
        }

        [HttpGet]
        public async Task<IActionResult> AutomatedProcessing()
        {
            var pendingClaims = await _context.Claims
                .Where(c => c.Status == "Under Review" && c.HoursWorked <= 160)
                .ToListAsync();

            foreach (var claim in pendingClaims)
            {
                claim.Status = "Approved";
                claim.RejectionReason = "Automatically approved by system";
            }

            if (pendingClaims.Any())
            {
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Automatically approved {pendingClaims.Count} claims.";
            }
            else
            {
                TempData["InfoMessage"] = "No claims eligible for automatic approval.";
            }

            return RedirectToAction("Dashboard");
        }
    }
}