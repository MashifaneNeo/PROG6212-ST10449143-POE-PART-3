using Microsoft.EntityFrameworkCore;

namespace PROG6212_ST10449143_POE_PART_1.Models
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<Claim> Claims { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configured Claim entity
            modelBuilder.Entity<Claim>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.LecturerName).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Month).IsRequired().HasMaxLength(20);
                entity.Property(e => e.HoursWorked).HasColumnType("decimal(10,2)");
                entity.Property(e => e.HourlyRate).HasColumnType("decimal(10,2)");
                entity.Property(e => e.AdditionalNotes).HasMaxLength(500);
                entity.Property(e => e.SupportingDocument).HasMaxLength(500);
                entity.Property(e => e.Status).IsRequired().HasMaxLength(50);
                entity.Property(e => e.RejectionReason).HasMaxLength(1000);
                entity.Property(e => e.SubmittedDate).IsRequired();
            });
        }
    }
}