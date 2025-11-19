using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PROG6212_ST10449143_POE_PART_1.Models
{
    public class User : IdentityUser
    {
        [Required]
        [StringLength(50)]
        public string FirstName { get; set; }

        [Required]
        [StringLength(50)]
        public string LastName { get; set; }

        [Required]
        [Range(1, 10000)]
        [Column(TypeName = "decimal(10,2)")]
        public decimal HourlyRate { get; set; }

        [StringLength(20)]
        public string EmployeeId { get; set; }

        [StringLength(100)]
        public string Department { get; set; }

        public DateTime DateCreated { get; set; } = DateTime.Now;
        public bool IsActive { get; set; } = true;

        // Navigation property
        public virtual ICollection<Claim> Claims { get; set; } = new List<Claim>();
    }
}