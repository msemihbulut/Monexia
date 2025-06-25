using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace Monexia.Models
{
    public class ApplicationUser : IdentityUser
    {
        [Required]
        public string Name { get; set; }

        [Required]
        public string Surname { get; set; }


        public string? Address { get; set; } // AES ile şifrelenmiş tutulabilir


        public string? BirthDate { get; set; } // string tutuluyorsa şifreli metin olarak

        public string? ProfilePictureUrl { get; set; }

        [Required]
        public DateTime RegistrationDate { get; set; } = DateTime.Now;
    }
}