using Microsoft.EntityFrameworkCore;
using PROG6212_ST10449143_POE_PART_1.Models;

namespace PROG6212_ST10449143_POE_PART_1.Services
{
    public class DatabaseClaimService : IClaimService
    {
        private readonly AppDbContext _context;

        public DatabaseClaimService(AppDbContext context)
        {
            _context = context;
        }

        public async Task AddClaimAsync(Claim claim)
        {
            try
            {
                // Ensure required fields are set
                if (string.IsNullOrEmpty(claim.UserId))
                {
                    throw new ArgumentException("UserId is required for claim");
                }

                if (string.IsNullOrEmpty(claim.Month))
                {
                    throw new ArgumentException("Month is required for claim");
                }

                claim.Status ??= "Submitted";
                claim.SubmittedDate = DateTime.Now;
                claim.AdditionalNotes ??= string.Empty;
                claim.SupportingDocument ??= string.Empty;
                claim.RejectionReason ??= string.Empty;

                Console.WriteLine($"Adding claim: UserId={claim.UserId}, Month={claim.Month}, Hours={claim.HoursWorked}, Rate={claim.HourlyRate}");

                _context.Claims.Add(claim);
                await _context.SaveChangesAsync();

                Console.WriteLine($"Claim added successfully with ID: {claim.Id}");
            }
            catch (DbUpdateException dbEx)
            {
                Console.WriteLine($"Database error adding claim: {dbEx.Message}");
                Console.WriteLine($"Inner exception: {dbEx.InnerException?.Message}");

                // Check for specific constraint violations
                if (dbEx.InnerException != null && dbEx.InnerException.Message.Contains("FK"))
                {
                    throw new Exception("Invalid user reference. Please ensure you are logged in correctly.");
                }

                throw new Exception("Database error occurred while saving claim. Please try again.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error adding claim to database: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                throw new Exception("An unexpected error occurred while saving your claim. Please try again.");
            }
        }

        public async Task<List<Claim>> GetAllClaimsAsync()
        {
            try
            {
                return await _context.Claims
                    .Include(c => c.User) 
                    .OrderByDescending(c => c.SubmittedDate)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting claims from database: {ex.Message}");
                return new List<Claim>();
            }
        }

        public async Task<Claim> GetClaimByIdAsync(int id)
        {
            try
            {
                return await _context.Claims
                    .Include(c => c.User) 
                    .FirstOrDefaultAsync(c => c.Id == id);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting claim by ID from database: {ex.Message}");
                return null;
            }
        }

        public async Task UpdateClaimStatusAsync(int id, string status, string rejectionReason = null)
        {
            try
            {
                var claim = await _context.Claims.FirstOrDefaultAsync(c => c.Id == id);
                if (claim != null)
                {
                    claim.Status = status;
                    claim.RejectionReason = rejectionReason ?? string.Empty;

                    await _context.SaveChangesAsync();
                    Console.WriteLine($"Claim {id} status updated to {status}");
                }
                else
                {
                    Console.WriteLine($"Claim {id} not found");
                    throw new ArgumentException($"Claim with ID {id} not found");
                }
            }
            catch (DbUpdateException dbEx)
            {
                Console.WriteLine($"Database error updating claim {id}: {dbEx.Message}");
                Console.WriteLine($"Inner exception: {dbEx.InnerException?.Message}");
                throw;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating claim status in database: {ex.Message}");
                throw;
            }
        }

        public async Task<List<Claim>> GetPendingClaimsAsync()
        {
            try
            {
                return await _context.Claims
                    .Include(c => c.User)
                    .Where(c => c.Status == "Under Review")
                    .OrderByDescending(c => c.SubmittedDate)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting pending claims from database: {ex.Message}");
                return new List<Claim>();
            }
        }

        public async Task<bool> DeleteClaimAsync(int id)
        {
            try
            {
                var claim = await _context.Claims.FirstOrDefaultAsync(c => c.Id == id);
                if (claim != null)
                {
                    _context.Claims.Remove(claim);
                    await _context.SaveChangesAsync();
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting claim from database: {ex.Message}");
                return false;
            }
        }
    }
}