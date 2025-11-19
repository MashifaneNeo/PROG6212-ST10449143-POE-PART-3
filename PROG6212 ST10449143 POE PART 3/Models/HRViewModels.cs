using System.ComponentModel.DataAnnotations;

namespace PROG6212_ST10449143_POE_PART_1.Models
{
    public class CreateUserViewModel
    {
        [Required]
        [Display(Name = "First Name")]
        public string FirstName { get; set; }

        [Required]
        [Display(Name = "Last Name")]
        public string LastName { get; set; }

        [Required]
        [EmailAddress]
        [Display(Name = "Email")]
        public string Email { get; set; }

        [Required]
        [Display(Name = "Hourly Rate")]
        [Range(1, 10000)]
        public decimal HourlyRate { get; set; }

        [Display(Name = "Employee ID")]
        public string EmployeeId { get; set; }

        [Display(Name = "Department")]
        public string Department { get; set; }

        [Display(Name = "Generate Temporary Password")]
        public bool GenerateTempPassword { get; set; } = true;

        [Display(Name = "Temporary Password")]
        public string TempPassword { get; set; }
    }

    public class UpdateUserViewModel
    {
        public string Id { get; set; }

        [Required]
        [Display(Name = "First Name")]
        public string FirstName { get; set; }

        [Required]
        [Display(Name = "Last Name")]
        public string LastName { get; set; }

        [Required]
        [EmailAddress]
        [Display(Name = "Email")]
        public string Email { get; set; }

        [Required]
        [Display(Name = "Hourly Rate")]
        [Range(1, 10000)]
        public decimal HourlyRate { get; set; }

        [Display(Name = "Employee ID")]
        public string EmployeeId { get; set; }

        [Display(Name = "Department")]
        public string Department { get; set; }

        [Display(Name = "Active")]
        public bool IsActive { get; set; }
    }

    public class ReportFilterViewModel
    {
        [Display(Name = "Start Date")]
        public DateTime? StartDate { get; set; }

        [Display(Name = "End Date")]
        public DateTime? EndDate { get; set; }

        [Display(Name = "Department")]
        public string Department { get; set; }

        [Display(Name = "Status")]
        public string Status { get; set; }

        [Display(Name = "Report Type")]
        public string ReportType { get; set; } 
    }
}