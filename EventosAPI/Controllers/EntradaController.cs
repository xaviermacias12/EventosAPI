using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EventosAPI.Data;
using EventosAPI.Models;
using System.Security.Claims;
using System.ComponentModel.DataAnnotations;

namespace EventosAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class EntradasController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public EntradasController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetAllEntradas()
        {
            var entradas = await _context.Entradas
                .Include(e => e.Evento)
                .Include(e => e.Cliente)
                .Select(e => new
                {
                    e.Id,
                    e.EventoId,
                    e.ClienteId,
                    e.Asiento,
                    e.PrecioPagado,
                    e.FechaCompra,
                    e.Estado,
                    e.CodigoQR,
                    Evento = e.Evento != null ? new { e.Evento.Id, e.Evento.Nombre } : null
                })
                .ToListAsync();
            return Ok(entradas);
        }

        // GET: api/Entradas/mis-entradas
        [HttpGet("mis-entradas")]
        public async Task<IActionResult> GetMisEntradas()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId == null)
                return Unauthorized();

            var cliente = await _context.Clientes
                .FirstOrDefaultAsync(c => c.UsuarioId == userId);

            if (cliente == null)
                return Ok(new List<object>());

            var entradas = await _context.Entradas
                .Include(e => e.Evento)
                .Where(e => e.ClienteId == cliente.Id)
                .OrderByDescending(e => e.FechaCompra)
                .Select(e => new
                {
                    e.Id,
                    e.Asiento,
                    e.PrecioPagado,
                    e.FechaCompra,
                    e.Estado,
                    e.CodigoQR,
                    Evento = e.Evento != null ? new
                    {
                        e.Evento.Id,
                        e.Evento.Nombre,
                        e.Evento.Fecha,
                        e.Evento.Lugar,
                        e.Evento.ImagenUrl
                    } : null  // ← Permitir evento null
                })
                .ToListAsync();

            return Ok(entradas);
        }

        // POST: api/Entradas/comprar
        [HttpPost("comprar")]
        public async Task<IActionResult> ComprarEntrada([FromBody] ComprarEntradaRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId == null)
                return Unauthorized();

            // Obtener o crear cliente
            var user = await _context.Users.FindAsync(userId);
            var cliente = await _context.Clientes
                .FirstOrDefaultAsync(c => c.UsuarioId == userId);

            if (cliente == null)
            {
                cliente = new Cliente
                {
                    Nombre = user?.Nombre ?? "Cliente",
                    Email = user?.Email ?? "",
                    Telefono = request.Telefono ?? "",
                    UsuarioId = userId
                };
                _context.Clientes.Add(cliente);
                await _context.SaveChangesAsync();
            }

            // Verificar evento
            var evento = await _context.Eventos.FindAsync(request.EventoId);
            if (evento == null)
                return BadRequest(new { message = "Evento no encontrado" });

            if (!evento.Activo)
                return BadRequest(new { message = "Evento no disponible" });

            // Verificar disponibilidad
            var entradasVendidas = await _context.Entradas
                .CountAsync(e => e.EventoId == request.EventoId && e.Estado != "Cancelada");

            if (entradasVendidas + request.Cantidad > evento.Capacidad)
                return BadRequest(new { message = "No hay suficientes lugares disponibles" });

            var entradas = new List<Entrada>();
            for (int i = 0; i < request.Cantidad; i++)
            {
                var asiento = GenerarAsiento(evento.Id, entradasVendidas + i + 1);
                var entrada = new Entrada
                {
                    EventoId = request.EventoId,
                    ClienteId = cliente.Id,
                    Asiento = asiento,
                    PrecioPagado = evento.Precio,
                    CodigoQR = GenerarCodigoQR(),
                    Estado = "Confirmada"
                };
                _context.Entradas.Add(entrada);
                entradas.Add(entrada);
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = $"Compra exitosa. {request.Cantidad} entrada(s) adquirida(s).",
                entradas = entradas.Select(e => new
                {
                    e.Id,
                    e.Asiento,
                    e.PrecioPagado,
                    e.CodigoQR,
                    e.Estado
                })
            });
        }

        // POST: api/Entradas/validar-qr
        [Authorize(Roles = "Admin")]
        [HttpPost("validar-qr")]
        public async Task<IActionResult> ValidarQR([FromBody] ValidarQRRequest request)
        {
            var entrada = await _context.Entradas
                .Include(e => e.Evento)
                .Include(e => e.Cliente)
                .FirstOrDefaultAsync(e => e.CodigoQR == request.CodigoQR);

            if (entrada == null)
                return NotFound(new { message = "Entrada no encontrada" });

            if (entrada.Estado == "Usada")
                return BadRequest(new { message = "Esta entrada ya fue utilizada" });

            if (entrada.Estado == "Cancelada")
                return BadRequest(new { message = "Esta entrada fue cancelada" });

            entrada.Estado = "Usada";
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Entrada válida",
                entrada = new
                {
                    entrada.Id,
                    entrada.Asiento,
                    Evento = entrada.Evento?.Nombre,
                    Cliente = entrada.Cliente?.Nombre
                }
            });
        }

        private string GenerarAsiento(int eventoId, int numero)
        {
            var fila = (char)('A' + (numero - 1) / 10);
            var asiento = (numero - 1) % 10 + 1;
            return $"{fila}{asiento}";
        }

        private string GenerarCodigoQR()
        {
            return Guid.NewGuid().ToString().ToUpper().Substring(0, 8);
        }
    }

    public class ComprarEntradaRequest
    {
        [Required]
        public int EventoId { get; set; }

        [Required]
        [Range(1, 10)]
        public int Cantidad { get; set; } = 1;

        public string? Telefono { get; set; }
    }

    public class ValidarQRRequest
    {
        [Required]
        public string CodigoQR { get; set; } = string.Empty;
    }
}