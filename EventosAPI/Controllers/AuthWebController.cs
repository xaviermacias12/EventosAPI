using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using EventosAPI.Data;
using EventosAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace EventosAPI.Controllers
{
    public class AuthWebController : Controller
    {
        private readonly SignInManager<Usuario> _signInManager;
        private readonly UserManager<Usuario> _userManager;
        private readonly ApplicationDbContext _context;

        public AuthWebController(
            SignInManager<Usuario> signInManager,
            UserManager<Usuario> userManager,
            ApplicationDbContext context)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _context = context;
        }

        [HttpGet]
        public IActionResult Login(string returnUrl = "/ClienteEventos")
        {
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(string email, string password, string returnUrl = "/ClienteEventos")
        {
            var result = await _signInManager.PasswordSignInAsync(email, password, true, false);

            if (result.Succeeded)
            {
                return LocalRedirect(returnUrl);
            }

            ViewBag.Error = "Email o contraseña incorrectos";
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Register(string nombre, string email, string password, string telefono)
        {
            var existingUser = await _userManager.FindByEmailAsync(email);
            if (existingUser != null)
            {
                ViewBag.Error = "El email ya está registrado";
                return View();
            }

            var user = new Usuario
            {
                UserName = email,
                Email = email,
                Nombre = nombre,
                Rol = "Cliente",
                EmailConfirmed = true
            };

            var result = await _userManager.CreateAsync(user, password);

            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(user, "Cliente");

                var cliente = new Cliente
                {
                    Nombre = nombre,
                    Email = email,
                    Telefono = telefono ?? "",
                    UsuarioId = user.Id
                };
                _context.Clientes.Add(cliente);
                await _context.SaveChangesAsync();

                await _signInManager.SignInAsync(user, isPersistent: true);

                return RedirectToAction("Index", "ClienteEventos");
            }

            ViewBag.Error = result.Errors.FirstOrDefault()?.Description;
            return View();
        }

        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Login", "AuthWeb");
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> Perfil()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return RedirectToAction("Login");

            var cliente = await _context.Clientes
                .FirstOrDefaultAsync(c => c.UsuarioId == user.Id);

            ViewBag.Telefono = cliente?.Telefono ?? "";
            return View(user);
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> Perfil(string nombre, string telefono)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return RedirectToAction("Login");

            user.Nombre = nombre;
            await _userManager.UpdateAsync(user);

            var cliente = await _context.Clientes
                .FirstOrDefaultAsync(c => c.UsuarioId == user.Id);

            if (cliente != null)
            {
                cliente.Nombre = nombre;
                cliente.Telefono = telefono ?? "";
                await _context.SaveChangesAsync();
            }
            else
            {
                // Si no existe cliente, crearlo
                cliente = new Cliente
                {
                    Nombre = nombre,
                    Email = user.Email,
                    Telefono = telefono ?? "",
                    UsuarioId = user.Id
                };
                _context.Clientes.Add(cliente);
                await _context.SaveChangesAsync();
            }

            TempData["Success"] = "Perfil actualizado correctamente";
            return RedirectToAction("Perfil");
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> CambiarPassword(string passwordActual, string passwordNueva, string passwordConfirmar)
        {
            if (passwordNueva != passwordConfirmar)
            {
                TempData["Error"] = "Las contraseñas nuevas no coinciden";
                return RedirectToAction("Perfil");
            }

            if (passwordNueva.Length < 6)
            {
                TempData["Error"] = "La contraseña debe tener al menos 6 caracteres";
                return RedirectToAction("Perfil");
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return RedirectToAction("Login");

            var result = await _userManager.ChangePasswordAsync(user, passwordActual, passwordNueva);

            if (result.Succeeded)
            {
                TempData["Success"] = "Contraseña actualizada correctamente";
            }
            else
            {
                TempData["Error"] = "Contraseña actual incorrecta";
            }

            return RedirectToAction("Perfil");
        }
    }
}