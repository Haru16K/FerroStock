using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers
{
    public class ReportesController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
