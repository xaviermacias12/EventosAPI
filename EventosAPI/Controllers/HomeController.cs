using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EventosAPI.Controllers
{
    public class HomeController : Controller
    {
        // GET: / o /Home/Index
        public IActionResult Index()
        {
            if (User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Index", "ClienteEventos");
            }
            return RedirectToAction("Login", "AuthWeb");
        }
    }
}