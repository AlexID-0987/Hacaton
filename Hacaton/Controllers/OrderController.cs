using Microsoft.AspNetCore.Mvc;

namespace Hacaton.Controllers
{
    public class OrderController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
