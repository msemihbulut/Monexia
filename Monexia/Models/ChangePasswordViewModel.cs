using System.ComponentModel.DataAnnotations;

namespace Monexia.Models
{
    public class ChangePasswordViewModel
    {
        [Required, DataType(DataType.Password)]
        [Display(Name = "Mevcut Şifre")]
        public string OldPassword { get; set; }

        [Required, DataType(DataType.Password)]
        [Display(Name = "Yeni Şifre")]
        public string NewPassword { get; set; }

        [Required, DataType(DataType.Password)]
        [Compare("NewPassword", ErrorMessage = "Yeni şifreler eşleşmiyor.")]
        [Display(Name = "Yeni Şifre (Tekrar)")]
        public string ConfirmPassword { get; set; }
    }
}
