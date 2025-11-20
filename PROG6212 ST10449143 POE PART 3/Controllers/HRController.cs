// Controllers/HRController.cs
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
        public IActionResult Reports()
        {
            return View(new ReportFilterViewModel());
        }

        [HttpPost]
        public async Task<IActionResult> GenerateReport(ReportFilterViewModel filters)
        {
            var claims = await _hrService.GetClaimsForReportAsync(filters);
            var reportTitle = $"Claims Report - {DateTime.Now:yyyy-MM-dd}";

            var pdfBytes = await _hrService.GeneratePdfReportAsync(claims, reportTitle);

            return File(pdfBytes, "application/pdf", $"{reportTitle}.pdf");
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