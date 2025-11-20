using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PROG6212_ST10449143_POE_PART_1.Models;
using System.Linq.Dynamic.Core;
using System.Text;

namespace PROG6212_ST10449143_POE_PART_1.Services
{
    public interface IHRService
    {
        Task<(bool Success, string Password, string Error)> CreateUserAsync(CreateUserViewModel model);
        Task<bool> UpdateUserAsync(UpdateUserViewModel model);
        Task<List<User>> GetAllUsersAsync();
        Task<User> GetUserByIdAsync(string id);
        Task<List<Claim>> GetClaimsForReportAsync(ReportFilterViewModel filters);
        Task<byte[]> GeneratePdfReportAsync(List<Claim> claims, string reportTitle);
        Task<bool> DeleteUserAsync(string userId);
        Task<(bool Success, string Error)> ResetPasswordAsync(string userId, string newPassword);
        Task<bool> ToggleUserStatusAsync(string userId, bool isActive);
    }

    public class HRService : IHRService
    {
        private readonly UserManager<User> _userManager;
        private readonly AppDbContext _context;

        public HRService(UserManager<User> userManager, AppDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        public async Task<(bool Success, string Password, string Error)> CreateUserAsync(CreateUserViewModel model)
        {
            try
            {
                var tempPassword = model.GenerateTempPassword
                    ? GenerateTemporaryPassword()
                    : model.TempPassword;

                var user = new User
                {
                    FirstName = model.FirstName,
                    LastName = model.LastName,
                    UserName = model.Email,
                    Email = model.Email,
                    HourlyRate = model.HourlyRate,
                    EmployeeId = model.EmployeeId ?? string.Empty,
                    Department = model.Department ?? string.Empty,
                    DateCreated = DateTime.Now,
                    IsActive = true
                };

                Console.WriteLine($"Creating user: {user.FirstName} {user.LastName}, Email: {user.Email}");
                Console.WriteLine($"EmployeeId: {user.EmployeeId}, Department: {user.Department}");
                Console.WriteLine($"HourlyRate: {user.HourlyRate}, IsActive: {user.IsActive}");

                var result = await _userManager.CreateAsync(user, tempPassword);

                if (result.Succeeded)
                {
                    await _userManager.AddToRoleAsync(user, "Lecturer");
                    Console.WriteLine($"User created successfully with ID: {user.Id}");
                    return (true, tempPassword, null);
                }

                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                Console.WriteLine($"UserManager errors: {errors}");
                return (false, null, errors);
            }
            catch (Exception ex)
            {
                var errorMessage = ex.Message;
                if (ex.InnerException != null)
                {
                    errorMessage += $" | Inner Exception: {ex.InnerException.Message}";
                }

                Console.WriteLine($"Exception in CreateUserAsync: {errorMessage}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");

                return (false, null, errorMessage);
            }
        }


        public async Task<bool> UpdateUserAsync(UpdateUserViewModel model)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(model.Id);
                if (user == null) return false;

                user.FirstName = model.FirstName;
                user.LastName = model.LastName;
                user.Email = model.Email;
                user.UserName = model.Email;
                user.HourlyRate = model.HourlyRate;
                user.EmployeeId = model.EmployeeId;
                user.Department = model.Department;
                user.IsActive = model.IsActive;

                var result = await _userManager.UpdateAsync(user);
                return result.Succeeded;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task<List<User>> GetAllUsersAsync()
        {
            return await _userManager.Users
                .OrderBy(u => u.LastName)
                .ThenBy(u => u.FirstName)
                .ToListAsync();
        }

        public async Task<User> GetUserByIdAsync(string id)
        {
            return await _userManager.FindByIdAsync(id);
        }

        public async Task<List<Claim>> GetClaimsForReportAsync(ReportFilterViewModel filters)
        {
            var query = _context.Claims
                .Include(c => c.User)
                .AsQueryable();

            // Apply date filters
            if (filters.StartDate.HasValue)
            {
                var startDate = filters.StartDate.Value.Date;
                query = query.Where(c => c.SubmittedDate >= startDate);
            }

            if (filters.EndDate.HasValue)
            {
                var endDate = filters.EndDate.Value.Date.AddDays(1).AddSeconds(-1);
                query = query.Where(c => c.SubmittedDate <= endDate);
            }

            // Apply department filter
            if (!string.IsNullOrEmpty(filters.Department))
            {
                query = query.Where(c => c.User.Department == filters.Department);
            }

            // Apply status filter
            if (!string.IsNullOrEmpty(filters.Status))
            {
                query = query.Where(c => c.Status == filters.Status);
            }

            // Apply report type specific filters
            if (!string.IsNullOrEmpty(filters.ReportType))
            {
                switch (filters.ReportType)
                {
                    case "Monthly":
                        var currentMonth = DateTime.Now.Month;
                        var currentYear = DateTime.Now.Year;
                        query = query.Where(c => c.SubmittedDate.Month == currentMonth && c.SubmittedDate.Year == currentYear);
                        break;
                    case "User":
                        break;
                    case "Department":
                        break;
                }
            }

            return await query
                .OrderByDescending(c => c.SubmittedDate)
                .ToListAsync();
        }

        public async Task<byte[]> GeneratePdfReportAsync(List<Claim> claims, string reportTitle)
        {
            try
            {
                var reportContent = new StringBuilder();

                reportContent.AppendLine($"CONTRACT CLAIM MANAGEMENT SYSTEM - {reportTitle.ToUpper()}");
                reportContent.AppendLine("=".PadRight(60, '='));
                reportContent.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm}");
                reportContent.AppendLine($"Total Claims in Report: {claims.Count}");
                reportContent.AppendLine($"Total Amount: R {claims.Sum(c => c.TotalAmount):#,##0.00}");
                reportContent.AppendLine();

                // Summary by status
                var statusSummary = claims
                    .GroupBy(c => c.Status)
                    .Select(g => new { Status = g.Key, Count = g.Count(), Amount = g.Sum(c => c.TotalAmount) })
                    .ToList();

                reportContent.AppendLine("SUMMARY BY STATUS:");
                reportContent.AppendLine("-".PadRight(40, '-'));
                foreach (var summary in statusSummary)
                {
                    reportContent.AppendLine($"{summary.Status}: {summary.Count} claims, R {summary.Amount:#,##0.00}");
                }
                reportContent.AppendLine();

                // Summary by department
                var deptSummary = claims
                    .Where(c => c.User != null)
                    .GroupBy(c => c.User.Department ?? "Unknown")
                    .Select(g => new { Department = g.Key, Count = g.Count(), Amount = g.Sum(c => c.TotalAmount) })
                    .OrderByDescending(g => g.Amount)
                    .ToList();

                if (deptSummary.Any())
                {
                    reportContent.AppendLine("SUMMARY BY DEPARTMENT:");
                    reportContent.AppendLine("-".PadRight(40, '-'));
                    foreach (var dept in deptSummary)
                    {
                        reportContent.AppendLine($"{dept.Department}: {dept.Count} claims, R {dept.Amount:#,##0.00}");
                    }
                    reportContent.AppendLine();
                }

                // Detailed claim listing
                reportContent.AppendLine("DETAILED CLAIM LISTING:");
                reportContent.AppendLine("-".PadRight(80, '-'));

                int counter = 1;
                foreach (var claim in claims.OrderByDescending(c => c.SubmittedDate))
                {
                    reportContent.AppendLine($"{counter}. Claim #{claim.Id}");
                    reportContent.AppendLine($"   Lecturer: {claim.LecturerName}");
                    reportContent.AppendLine($"   Month: {claim.Month}");
                    reportContent.AppendLine($"   Hours: {claim.HoursWorked:#0.00} @ R {claim.HourlyRate:#0.00}/hr");
                    reportContent.AppendLine($"   Amount: R {claim.TotalAmount:#,##0.00}");
                    reportContent.AppendLine($"   Status: {claim.Status}");
                    reportContent.AppendLine($"   Submitted: {claim.SubmittedDate:yyyy-MM-dd}");

                    if (!string.IsNullOrEmpty(claim.AdditionalNotes))
                    {
                        reportContent.AppendLine($"   Notes: {claim.AdditionalNotes}");
                    }

                    reportContent.AppendLine();
                    counter++;
                }

                reportContent.AppendLine();
                reportContent.AppendLine("END OF REPORT");
                reportContent.AppendLine($"Total Records: {claims.Count}");
                reportContent.AppendLine($"Report Generated By: HR System");
                reportContent.AppendLine($"Generation Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

                return Encoding.UTF8.GetBytes(reportContent.ToString());
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error generating PDF report: {ex.Message}");
                // Return a basic error report
                var errorContent = $"Error generating report: {ex.Message}\nPlease try again or contact system administrator.";
                return Encoding.UTF8.GetBytes(errorContent);
            }
        }

        public async Task<bool> DeleteUserAsync(string userId)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(userId);
                if (user == null) return false;

                // Check if user has any claims before deleting
                var userClaims = await _context.Claims.Where(c => c.UserId == userId).ToListAsync();
                if (userClaims.Any())
                {
                    return false;
                }

                var result = await _userManager.DeleteAsync(user);
                return result.Succeeded;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task<(bool Success, string Error)> ResetPasswordAsync(string userId, string newPassword)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(userId);
                if (user == null) return (false, "User not found");

                // Generate reset token and reset password
                var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
                var result = await _userManager.ResetPasswordAsync(user, resetToken, newPassword);

                if (result.Succeeded)
                {
                    return (true, null);
                }
                else
                {
                    return (false, string.Join(", ", result.Errors.Select(e => e.Description)));
                }
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        public async Task<bool> ToggleUserStatusAsync(string userId, bool isActive)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(userId);
                if (user == null) return false;

                user.IsActive = isActive;
                var result = await _userManager.UpdateAsync(user);
                return result.Succeeded;
            }
            catch (Exception)
            {
                return false;
            }
        }


        private string GenerateTemporaryPassword()
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$%";
            var random = new Random();
            return new string(Enumerable.Repeat(chars, 8)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }
    }
}