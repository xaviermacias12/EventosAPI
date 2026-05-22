using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EventosAPI.Controllers
{
    public class TestController : Controller
    {
        public IActionResult CheckAuth()
        {
            var isAuth = User.Identity.IsAuthenticated;
            var userName = User.Identity.Name ?? "null";
            return Content($"Autenticado: {isAuth}, Usuario: {userName}");
        }

        [Authorize]
        public IActionResult Protected()
        {
            return Content($"Bienvenido {User.Identity.Name}, estás autenticado!");
        }
    }
}