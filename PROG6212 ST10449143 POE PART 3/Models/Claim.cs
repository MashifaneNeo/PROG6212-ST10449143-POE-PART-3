using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PROG6212_ST10449143_POE_PART_1.Models
{
    public class Claim
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string LecturerName { get; set; } = string.Empty;

        [Required]
        [StringLength(20)]
        public string Month { get; set; }

        [Required]
        [Range(0.5, 744)]
        [Column(TypeName = "decimal(10,2)")]
        public decimal HoursWorked { get; set; }

        [Required]
        [Range(1, 10000)]
        [Column(TypeName = "decimal(10,2)")]
        public decimal HourlyRate { get; set; }

        [NotMapped]
        public decimal TotalAmount => HoursWorked * HourlyRate;

        [Column(TypeName = "nvarchar(500)")]
        [StringLength(500, ErrorMessage = "Additional notes cannot exceed 500 characters")]
        public string AdditionalNotes { get; set; }

        [Column(TypeName = "nvarchar(500)")]
        public string SupportingDocument { get; set; }

        [Required]
        [StringLength(50)]
        public string Status { get; set; } = "Under Review"; 

        public DateTime SubmittedDate { get; set; } = DateTime.Now;

        [Column(TypeName = "nvarchar(1000)")]
        public string RejectionReason { get; set; }

        [Required]
        public string UserId { get; set; }
        public virtual User? User { get; set; }

        [StringLength(50)]
        public string CurrentStage { get; set; } = "CoordinatorReview";

        public string? CoordinatorReviewDate { get; set; } 

        public string? ManagerReviewDate { get; set; } 

        [StringLength(100)]
        public string? CoordinatorApprover { get; set; } 

        [StringLength(100)]
        public string? ManagerApprover { get; set; } 

        // Added workflow tracking
        public bool IsCoordinatorApproved { get; set; } = false;
        public bool IsManagerApproved { get; set; } = false;

        public Claim()
        {
            Status ??= "Under Review"; 
            CurrentStage ??= "CoordinatorReview";
            AdditionalNotes ??= string.Empty;
            SupportingDocument ??= string.Empty;
            RejectionReason ??= string.Empty;
        }
    }
}