using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PROG6212_ST10449143_POE_PART_1.Models;
using System.Linq.Dynamic.Core;

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

            if (filters.StartDate.HasValue)
                query = query.Where(c => c.SubmittedDate >= filters.StartDate.Value);

            if (filters.EndDate.HasValue)
                query = query.Where(c => c.SubmittedDate <= filters.EndDate.Value);

            if (!string.IsNullOrEmpty(filters.Department))
                query = query.Where(c => c.User.Department == filters.Department);

            if (!string.IsNullOrEmpty(filters.Status))
                query = query.Where(c => c.Status == filters.Status);

            return await query
                .OrderByDescending(c => c.SubmittedDate)
                .ToListAsync();
        }

        public async Task<byte[]> GeneratePdfReportAsync(List<Claim> claims, string reportTitle)
        {
            var reportContent = $"Report: {reportTitle}\n\n";
            reportContent += $"Generated: {DateTime.Now}\n";
            reportContent += $"Total Claims: {claims.Count}\n";
            reportContent += $"Total Amount: R {claims.Sum(c => c.TotalAmount):0.00}\n\n";

            foreach (var claim in claims)
            {
                reportContent += $"Claim #{claim.Id}: {claim.User?.FirstName} {claim.User?.LastName} - {claim.Month} - R {claim.TotalAmount:0.00}\n";
            }

            return System.Text.Encoding.UTF8.GetBytes(reportContent);
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