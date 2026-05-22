using Microsoft.AspNetCore.Authentication.JwtBearer;
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
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class EntradasController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public EntradasController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetAllEntradas([FromQuery] int? eventoId = null)
        {
            var query = _context.Entradas
                .Include(e => e.Evento)
                .Include(e => e.Cliente)
                .AsQueryable();

            if (eventoId.HasValue)
            {
                query = query.Where(e => e.EventoId == eventoId);
            }

            var entradas = await query
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
                    } : null
                })
                .ToListAsync();

            return Ok(entradas);
        }

        [HttpPost("comprar")]
        public async Task<IActionResult> ComprarEntrada([FromBody] ComprarEntradaRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId == null)
                return Unauthorized();

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

            var evento = await _context.Eventos.FindAsync(request.EventoId);
            if (evento == null)
                return BadRequest(new { message = "Evento no encontrado" });

            if (!evento.Activo)
                return BadRequest(new { message = "Evento no disponible" });

            var entradasVendidas = await _context.Entradas
                .CountAsync(e => e.EventoId == request.EventoId && e.Estado != "Cancelada");

            var disponibles = evento.Capacidad - entradasVendidas;

            if (request.Cantidad > disponibles)
            {
                return BadRequest(new
                {
                    message = $"No hay suficientes lugares disponibles. Quedan {disponibles} lugares.",
                    disponibles = disponibles,
                    solicitadas = request.Cantidad
                });
            }

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

            var nuevaCapacidadRestante = evento.Capacidad - entradasVendidas - request.Cantidad;

            return Ok(new
            {
                message = $"Compra exitosa. {request.Cantidad} entrada(s) adquirida(s).",
                capacidadRestante = nuevaCapacidadRestante,
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

        [HttpPost("cancelar/{id}")]
        public async Task<IActionResult> CancelarEntrada(int id)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId == null)
                return Unauthorized(new { message = "Usuario no autenticado" });

            var cliente = await _context.Clientes
                .FirstOrDefaultAsync(c => c.UsuarioId == userId);

            if (cliente == null)
                return NotFound(new { message = "Cliente no encontrado" });

            var entrada = await _context.Entradas
                .Include(e => e.Evento)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (entrada == null)
                return NotFound(new { message = "Entrada no encontrada" });

            if (entrada.ClienteId != cliente.Id)
                return Unauthorized(new { message = "No puedes cancelar una entrada de otro usuario" });

            if (entrada.Estado == "Cancelada")
                return BadRequest(new { message = "La entrada ya está cancelada" });

            if (entrada.Estado == "Usada")
                return BadRequest(new { message = "No se puede cancelar una entrada ya usada" });

            var horasRestantes = (entrada.Evento.Fecha - DateTime.Now).TotalHours;
            if (horasRestantes < 24)
                return BadRequest(new { message = "Solo se pueden cancelar compras con 24+ horas de anticipación" });

            entrada.Estado = "Cancelada";
            await _context.SaveChangesAsync();

            var entradasVendidas = await _context.Entradas
                .CountAsync(e => e.EventoId == entrada.EventoId && e.Estado != "Cancelada");

            var capacidadRestante = entrada.Evento.Capacidad - entradasVendidas;

            return Ok(new
            {
                message = "Entrada cancelada exitosamente",
                capacidadRestante = capacidadRestante,
                entradaId = entrada.Id
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