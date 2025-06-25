using Microsoft.AspNetCore.Mvc;

namespace Monexia.Controllers
{
    public class LegalController : Controller
    {
        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }
    }
}