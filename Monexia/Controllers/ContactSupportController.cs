using Microsoft.AspNetCore.Mvc;

namespace Monexia.Controllers
{
    public class ContactSupportController : Controller
    {
        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }
    }
}