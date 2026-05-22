using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EventosAPI.Data;
using EventosAPI.Models;
using System.Security.Claims;

namespace EventosAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class ClientesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public ClientesController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet("perfil")]
        public async Task<IActionResult> GetPerfil()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId == null)
                return Unauthorized();

            var cliente = await _context.Clientes
                .FirstOrDefaultAsync(c => c.UsuarioId == userId);

            if (cliente == null)
                return NotFound(new { message = "Perfil no encontrado" });

            var user = await _context.Users.FindAsync(userId);

            return Ok(new
            {
                cliente.Id,
                cliente.Nombre,
                cliente.Email,
                cliente.Telefono,
                Usuario = new
                {
                    user?.Email,
                    user?.Nombre
                }
            });
        }

        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<IActionResult> GetAllClientes()
        {
            var clientes = await _context.Clientes.ToListAsync();
            return Ok(clientes);
        }

        [HttpPut("perfil")]
        public async Task<IActionResult> UpdatePerfil([FromBody] UpdatePerfilRequest request)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId == null)
                return Unauthorized();

            var user = await _context.Users.FindAsync(userId);
            if (user == null)
                return NotFound(new { message = "Usuario no encontrado" });

            var cliente = await _context.Clientes
                .FirstOrDefaultAsync(c => c.UsuarioId == userId);

            if (cliente == null)
                return NotFound(new { message = "Perfil no encontrado" });

            if (!string.IsNullOrEmpty(request.Nombre))
            {
                user.Nombre = request.Nombre;
                cliente.Nombre = request.Nombre;
            }

            if (request.Telefono != null)
            {
                cliente.Telefono = request.Telefono;
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Perfil actualizado",
                nombre = user.Nombre,
                telefono = cliente.Telefono
            });
        }
    }

    public class UpdatePerfilRequest
    {
        public string? Nombre { get; set; }
        public string? Telefono { get; set; }
    }
}