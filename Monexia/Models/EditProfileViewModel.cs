using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace Monexia.Models
{
    public class EditProfileViewModel
    {
        [Required]
        public string Name { get; set; }

        [Required]
        public string Surname { get; set; }

        [Required]
        [Phone]
        public string PhoneNumber { get; set; }

        [Required]
        public string Address { get; set; }

        [Required]
        [DataType(DataType.Date)]
        public string BirthDate { get; set; }

        public string? ExistingProfilePictureUrl { get; set; }

        public IFormFile? ProfilePicture { get; set; }
    }
}
