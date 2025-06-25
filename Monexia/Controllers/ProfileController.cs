using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Monexia.Models;
using System.Threading.Tasks;
using Monexia.Data;
using Microsoft.EntityFrameworkCore;
using Monexia.Helpers;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Jpeg;

namespace Monexia.Controllers
{
    [Authorize]
    public class ProfileController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IWebHostEnvironment _hostEnvironment;
        private readonly AppDbContext _context;

        public ProfileController(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager, IWebHostEnvironment hostEnvironment, AppDbContext context)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _hostEnvironment = hostEnvironment;
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound();
            }
            return View(user);
        }

        [HttpGet]
        public async Task<IActionResult> Edit()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound();
            }

            var model = new EditProfileViewModel
            {
                Name = user.Name,
                Surname = user.Surname,
                PhoneNumber = user.PhoneNumber,
                Address = user.Address,
                BirthDate = !string.IsNullOrEmpty(user.BirthDate) ? AESHelper.Decrypt(user.BirthDate) : null,
                ExistingProfilePictureUrl = user.ProfilePictureUrl
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(EditProfileViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound();
            }

            if (model.ProfilePicture != null)
            {
                string wwwRootPath = _hostEnvironment.WebRootPath;
                string profileImagesPath = Path.Combine(wwwRootPath, "img", "profile");

                // Klasörün var olup olmadığını kontrol et, yoksa oluştur.
                if (!Directory.Exists(profileImagesPath))
                {
                    Directory.CreateDirectory(profileImagesPath);
                }

                string fileName = Path.GetFileNameWithoutExtension(model.ProfilePicture.FileName);
                string extension = ".jpg"; // We will save as jpg
                string uniqueFileName = fileName + "_" + DateTime.Now.ToString("yymmssfff") + extension;
                string path = Path.Combine(profileImagesPath, uniqueFileName);

                // Eski resmi sil
                if (!string.IsNullOrEmpty(user.ProfilePictureUrl))
                {
                    var oldImagePath = Path.Combine(wwwRootPath, user.ProfilePictureUrl.TrimStart('/'));
                    if (System.IO.File.Exists(oldImagePath) && !oldImagePath.EndsWith("default.png"))
                    {
                        System.IO.File.Delete(oldImagePath);
                    }
                }

                using (var image = Image.Load(model.ProfilePicture.OpenReadStream()))
                {
                    // Resize the image to 256x256
                    image.Mutate(x => x.Resize(new ResizeOptions
                    {
                        Size = new Size(256, 256),
                        Mode = ResizeMode.Crop
                    }));

                    // Save the processed image
                    await image.SaveAsync(path, new JpegEncoder { Quality = 90 });
                }
                user.ProfilePictureUrl = "/img/profile/" + uniqueFileName;
            }

            user.Name = model.Name;
            user.Surname = model.Surname;
            user.PhoneNumber = model.PhoneNumber;
            user.Address = model.Address;
            user.BirthDate = !string.IsNullOrEmpty(model.BirthDate) ? AESHelper.Encrypt(model.BirthDate) : null;

            var result = await _userManager.UpdateAsync(user);

            if (result.Succeeded)
            {
                await _signInManager.RefreshSignInAsync(user);
                TempData["SuccessMessage"] = "Profiliniz başarıyla güncellendi.";
                return RedirectToAction("Index");
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return View(model);
        }

        [HttpGet]
        public IActionResult ChangePassword()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound();
            }

            var changePasswordResult = await _userManager.ChangePasswordAsync(user, model.OldPassword, model.NewPassword);

            if (!changePasswordResult.Succeeded)
            {
                foreach (var error in changePasswordResult.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
                return View(model);
            }

            await _signInManager.RefreshSignInAsync(user);
            TempData["SuccessMessage"] = "Şifreniz başarıyla değiştirildi.";
            return RedirectToAction("Index");
        }
    }
}
