using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PROG6212_ST10449143_POE_PART_1.Models;
using System.Linq.Dynamic.Core;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
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
                QuestPDF.Settings.License = LicenseType.Community;

                var document = Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin(2, Unit.Centimetre);
                        page.PageColor(Colors.White);
                        page.DefaultTextStyle(x => x.FontSize(11));

                        page.Header()
                            .AlignCenter()
                            .Text(reportTitle)
                            .SemiBold().FontSize(18).FontColor(Colors.Blue.Medium);

                        page.Content()
                            .PaddingVertical(1, Unit.Centimetre)
                            .Column(column =>
                            {
                                column.Spacing(10);

                                // Report Summary Section
                                column.Item().Background(Colors.Grey.Lighten3).Padding(10).Column(summaryColumn =>
                                {
                                    summaryColumn.Spacing(5);
                                    summaryColumn.Item().Text("REPORT SUMMARY").SemiBold().FontSize(14);

                                    var totalAmount = claims.Sum(c => c.TotalAmount);
                                    var approvedClaims = claims.Count(c => c.Status == "Approved");
                                    var pendingClaims = claims.Count(c => c.Status == "Submitted" || c.Status == "Under Review");
                                    var rejectedClaims = claims.Count(c => c.Status == "Rejected");

                                    summaryColumn.Item().Row(row =>
                                    {
                                        row.RelativeItem().Text($"Total Claims: {claims.Count}");
                                        row.RelativeItem().Text($"Approved: {approvedClaims}");
                                        row.RelativeItem().Text($"Pending: {pendingClaims}");
                                        row.RelativeItem().Text($"Rejected: {rejectedClaims}");
                                    });

                                    summaryColumn.Item().Row(row =>
                                    {
                                        row.RelativeItem().Text($"Total Amount: R {totalAmount:#,##0.00}");
                                        row.RelativeItem().Text($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm}");
                                    });
                                });

                                // Status Summary
                                var statusSummary = claims
                                    .GroupBy(c => c.Status)
                                    .Select(g => new { Status = g.Key, Count = g.Count(), Amount = g.Sum(c => c.TotalAmount) })
                                    .ToList();

                                if (statusSummary.Any())
                                {
                                    column.Item().Background(Colors.Grey.Lighten3).Padding(10).Column(statusColumn =>
                                    {
                                        statusColumn.Item().Text("SUMMARY BY STATUS").SemiBold().FontSize(14);

                                        foreach (var summary in statusSummary)
                                        {
                                            statusColumn.Item().Row(row =>
                                            {
                                                row.RelativeItem(2).Text(summary.Status);
                                                row.RelativeItem().Text($"{summary.Count} claims");
                                                row.RelativeItem().Text($"R {summary.Amount:#,##0.00}");
                                            });
                                        }
                                    });
                                }

                                // Department Summary
                                var deptSummary = claims
                                    .Where(c => c.User != null)
                                    .GroupBy(c => c.User.Department ?? "Unknown")
                                    .Select(g => new { Department = g.Key, Count = g.Count(), Amount = g.Sum(c => c.TotalAmount) })
                                    .OrderByDescending(g => g.Amount)
                                    .ToList();

                                if (deptSummary.Any())
                                {
                                    column.Item().Background(Colors.Grey.Lighten3).Padding(10).Column(deptColumn =>
                                    {
                                        deptColumn.Item().Text("SUMMARY BY DEPARTMENT").SemiBold().FontSize(14);

                                        foreach (var dept in deptSummary)
                                        {
                                            deptColumn.Item().Row(row =>
                                            {
                                                row.RelativeItem(2).Text(dept.Department);
                                                row.RelativeItem().Text($"{dept.Count} claims");
                                                row.RelativeItem().Text($"R {dept.Amount:#,##0.00}");
                                            });
                                        }
                                    });
                                }

                                // Detailed Claims Table
                                column.Item().Text("DETAILED CLAIMS LISTING").SemiBold().FontSize(14);

                                column.Item().Table(table =>
                                {
                                    table.ColumnsDefinition(columns =>
                                    {
                                        columns.ConstantColumn(25); // No.
                                        columns.RelativeColumn();   // Lecturer
                                        columns.ConstantColumn(80); // Month
                                        columns.ConstantColumn(60); // Hours
                                        columns.ConstantColumn(80); // Amount
                                        columns.ConstantColumn(80); // Status
                                        columns.ConstantColumn(80); // Submitted
                                    });

                                    table.Header(header =>
                                    {
                                        header.Cell().Background(Colors.Blue.Medium).Padding(5).Text("No.").FontColor(Colors.White);
                                        header.Cell().Background(Colors.Blue.Medium).Padding(5).Text("Lecturer").FontColor(Colors.White);
                                        header.Cell().Background(Colors.Blue.Medium).Padding(5).Text("Month").FontColor(Colors.White);
                                        header.Cell().Background(Colors.Blue.Medium).Padding(5).Text("Hours").FontColor(Colors.White);
                                        header.Cell().Background(Colors.Blue.Medium).Padding(5).Text("Amount").FontColor(Colors.White);
                                        header.Cell().Background(Colors.Blue.Medium).Padding(5).Text("Status").FontColor(Colors.White);
                                        header.Cell().Background(Colors.Blue.Medium).Padding(5).Text("Submitted").FontColor(Colors.White);
                                    });

                                    int counter = 1;
                                    foreach (var claim in claims.OrderByDescending(c => c.SubmittedDate))
                                    {
                                        var backgroundColor = counter % 2 == 0 ? Colors.Grey.Lighten4 : Colors.White;

                                        table.Cell().Background(backgroundColor).Padding(5).Text(counter.ToString());
                                        table.Cell().Background(backgroundColor).Padding(5).Text(claim.LecturerName);
                                        table.Cell().Background(backgroundColor).Padding(5).Text(claim.Month);
                                        table.Cell().Background(backgroundColor).Padding(5).Text(claim.HoursWorked.ToString("0.00"));
                                        table.Cell().Background(backgroundColor).Padding(5).Text($"R {claim.TotalAmount:#,##0.00}");

                                        var statusColor = claim.Status switch
                                        {
                                            "Approved" => Colors.Green.Lighten1,
                                            "Rejected" => Colors.Red.Lighten1,
                                            "Under Review" => Colors.Orange.Lighten1,
                                            _ => Colors.Grey.Lighten1
                                        };

                                        table.Cell().Background(statusColor).Padding(5).Text(claim.Status);
                                        table.Cell().Background(backgroundColor).Padding(5).Text(claim.SubmittedDate.ToString("yyyy-MM-dd"));

                                        counter++;
                                    }
                                });

                                // Notes section for claims with additional notes
                                var claimsWithNotes = claims.Where(c => !string.IsNullOrEmpty(c.AdditionalNotes)).ToList();
                                if (claimsWithNotes.Any())
                                {
                                    column.Item().Text("CLAIMS WITH ADDITIONAL NOTES").SemiBold().FontSize(14);

                                    foreach (var claim in claimsWithNotes)
                                    {
                                        column.Item().Background(Colors.Yellow.Lighten5).Padding(10).Column(noteColumn =>
                                        {
                                            noteColumn.Item().Row(row =>
                                            {
                                                row.RelativeItem().Text($"Claim #{claim.Id} - {claim.LecturerName}").SemiBold();
                                                row.RelativeItem().Text(claim.SubmittedDate.ToString("yyyy-MM-dd"));
                                            });
                                            noteColumn.Item().Text(claim.AdditionalNotes);
                                        });
                                    }
                                }
                            });

                        page.Footer()
                            .AlignCenter()
                            .Text(text =>
                            {
                                text.Span("Page ");
                                text.CurrentPageNumber();
                                text.Span(" of ");
                                text.TotalPages();
                                text.Span($" | Generated on {DateTime.Now:yyyy-MM-dd HH:mm} | Contract Claim Management System");
                            });
                    });
                });

                // Generate PDF as byte array
                return document.GeneratePdf();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error generating PDF report with QuestPDF: {ex.Message}");

                var fallbackContent = new StringBuilder();
                fallbackContent.AppendLine($"ERROR GENERATING PDF REPORT");
                fallbackContent.AppendLine($"Error: {ex.Message}");
                fallbackContent.AppendLine($"Please contact system administrator.");
                fallbackContent.AppendLine($"Report Title: {reportTitle}");
                fallbackContent.AppendLine($"Claims Count: {claims.Count}");
                fallbackContent.AppendLine($"Generation Time: {DateTime.Now:yyyy-MM-dd HH:mm}");

                return Encoding.UTF8.GetBytes(fallbackContent.ToString());
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