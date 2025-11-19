using System.ComponentModel.DataAnnotations;

namespace PROG6212_ST10449143_POE_PART_1.Models
{
    public class ClaimViewModel
    {
        [Required(ErrorMessage = "Lecturer name is required")]
        [Display(Name = "Lecturer Name")]
        [StringLength(100, ErrorMessage = "Name cannot exceed 100 characters")]
        public string LecturerName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please select a month")]
        [Display(Name = "Month")]
        public string Month { get; set; } = string.Empty;

        [Required(ErrorMessage = "Hours worked is required")]
        [Display(Name = "Hours Worked")]
        [Range(0.5, 744, ErrorMessage = "Hours must be between 0.5 and 744")]
        public decimal HoursWorked { get; set; } = 0;

        [Required(ErrorMessage = "Hourly rate is required")]
        [Display(Name = "Hourly Rate")]
        [Range(1, 10000, ErrorMessage = "Hourly rate must be between R1 and R10,000")]
        public decimal HourlyRate { get; set; } = 0;

        [Display(Name = "Additional Notes")]
        public string? AdditionalNotes { get; set; } = string.Empty;

        public List<string> AvailableMonths => new List<string>
        {
            "January", "February", "March", "April", "May", "June",
            "July", "August", "September", "October", "November", "December"
        };
    }
}