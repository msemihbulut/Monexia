using Microsoft.AspNetCore.Mvc;

namespace Monexia.Controllers
{
    public class FAQController : Controller
    {
        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }
    }
}