using EventosAPI.Data;
using EventosAPI.Models;
using EventosAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace EventosAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<Usuario> _userManager;
        private readonly SignInManager<Usuario> _signInManager;
        private readonly TokenService _tokenService;
        private readonly ApplicationDbContext _context;

        public AuthController(UserManager<Usuario> userManager, SignInManager<Usuario> signInManager, TokenService tokenService, ApplicationDbContext context)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _tokenService = tokenService;
            _context = context;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var user = await _userManager.FindByEmailAsync(request.Email);
            if (user == null)
                return Unauthorized(new { message = "Credenciales incorrectas" });

            var result = await _signInManager.CheckPasswordSignInAsync(user, request.Password, false);
            if (!result.Succeeded)
                return Unauthorized(new { message = "Credenciales incorrectas" });

            var roles = await _userManager.GetRolesAsync(user);
            var rol = roles.FirstOrDefault() ?? "Cliente";

            var token = _tokenService.GenerarToken(user, rol);

            return Ok(new
            {
                token = token,
                email = user.Email,
                nombre = user.Nombre,
                rol = rol
            });
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            var existingUser = await _userManager.FindByEmailAsync(request.Email);
            if (existingUser != null)
                return BadRequest(new { message = "El email ya está registrado" });

            var user = new Usuario
            {
                UserName = request.Email,
                Email = request.Email,
                Nombre = request.Nombre,
                Rol = "Cliente",
                EmailConfirmed = true
            };

            var result = await _userManager.CreateAsync(user, request.Password);
            if (!result.Succeeded)
                return BadRequest(new { message = "Error al crear usuario", errors = result.Errors });

            await _userManager.AddToRoleAsync(user, "Cliente");

            var cliente = new Cliente
            {
                Nombre = request.Nombre,
                Email = request.Email,
                Telefono = request.Telefono ?? "",
                UsuarioId = user.Id
            };
            _context.Clientes.Add(cliente);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Usuario registrado exitosamente" });
        }

        [HttpPost("cambiar-password")]
        [Authorize]
        public async Task<IActionResult> CambiarPassword([FromBody] CambiarPasswordRequest request)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId == null)
                return Unauthorized();

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return NotFound(new { message = "Usuario no encontrado" });

            var result = await _userManager.ChangePasswordAsync(user, request.PasswordActual, request.PasswordNueva);

            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                return BadRequest(new { message = "Error al cambiar contraseña", errors });
            }

            return Ok(new { message = "Contraseña actualizada correctamente" });
        }
    }

    public class LoginRequest
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string Password { get; set; } = string.Empty;
    }

    public class RegisterRequest
    {
        [Required]
        public string Nombre { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [MinLength(6)]
        public string Password { get; set; } = string.Empty;

        public string? Telefono { get; set; }
    }

    public class CambiarPasswordRequest
    {
        public string PasswordActual { get; set; } = string.Empty;
        public string PasswordNueva { get; set; } = string.Empty;
    }
}