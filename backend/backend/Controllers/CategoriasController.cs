using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers
{
    public class CategoriasController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
