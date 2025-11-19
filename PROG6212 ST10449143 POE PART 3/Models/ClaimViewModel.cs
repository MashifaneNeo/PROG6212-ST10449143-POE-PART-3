using System.ComponentModel.DataAnnotations;

namespace PROG6212_ST10449143_POE_PART_1.Models
{
    public class ClaimViewModel
    {
        [Display(Name = "Lecturer Name")]
        public string LecturerName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please select a month")]
        [Display(Name = "Month")]
        public string Month { get; set; } = string.Empty;

        [Required(ErrorMessage = "Hours worked is required")]
        [Display(Name = "Hours Worked")]
        [Range(0.5, 180, ErrorMessage = "Hours must be between 0.5 and 180")]
        public decimal HoursWorked { get; set; } = 0;

        [Display(Name = "Hourly Rate")]
        public decimal HourlyRate { get; set; } = 0; // Make this read-only

        [Display(Name = "Total Amount")]
        public decimal TotalAmount => HoursWorked * HourlyRate;

        [Display(Name = "Additional Notes")]
        public string? AdditionalNotes { get; set; } = string.Empty;

        public List<string> AvailableMonths => new List<string>
    {
        "January", "February", "March", "April", "May", "June",
        "July", "August", "September", "October", "November", "December"
    };
    }
}