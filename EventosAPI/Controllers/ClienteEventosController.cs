using EventosAPI.Data;
using EventosAPI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace EventosAPI.Controllers
{
    public class ClienteEventosController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ClienteEventosController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: /ClienteEventos
        public async Task<IActionResult> Index()
        {
            var eventos = await _context.Eventos
                .Include(e => e.Categoria)
                .Where(e => e.Activo)
                .OrderByDescending(e => e.Fecha)
                .Select(e => new
                {
                    e.Id,
                    e.Nombre,
                    e.Descripcion,
                    e.Fecha,
                    e.Lugar,
                    e.Capacidad,
                    e.Precio,
                    e.ImagenUrl,
                    CategoriaNombre = e.Categoria != null ? e.Categoria.Nombre : null
                })
                .ToListAsync();

            return View(eventos);
        }

        // GET: /ClienteEventos/Detalle/5
        public async Task<IActionResult> Detalle(int id)
        {
            var evento = await _context.Eventos
                .Include(e => e.Categoria)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (evento == null)
                return NotFound();

            // Calcular lugares disponibles
            var entradasVendidas = await _context.Entradas
                .CountAsync(e => e.EventoId == id && e.Estado != "Cancelada");

            var disponibles = evento.Capacidad - entradasVendidas;

            ViewBag.Disponibles = disponibles;
            ViewBag.CategoriaNombre = evento.Categoria?.Nombre;

            return View(evento);
        }

        // POST: /ClienteEventos/Reservar
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> Reservar(int eventoId, int cantidad)
        {
            // ========== VERIFICAR AUTENTICACIÓN PRIMERO ==========
            if (!User.Identity.IsAuthenticated)
            {
                TempData["Error"] = "Debes iniciar sesión para reservar entradas";
                return RedirectToAction("Login", "AuthWeb", new { returnUrl = $"/ClienteEventos/Detalle/{eventoId}" });
            }

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId == null)
            {
                return RedirectToAction("Login", "AuthWeb");
            }

            var user = await _context.Users.FindAsync(userId);
            var cliente = await _context.Clientes
                .FirstOrDefaultAsync(c => c.UsuarioId == userId);

            if (cliente == null)
            {
                cliente = new Cliente
                {
                    Nombre = user?.Nombre ?? "Cliente",
                    Email = user?.Email ?? "",
                    Telefono = "",
                    UsuarioId = userId
                };
                _context.Clientes.Add(cliente);
                await _context.SaveChangesAsync();
            }

            var evento = await _context.Eventos.FindAsync(eventoId);
            if (evento == null)
                return NotFound();

            // Verificar disponibilidad
            var entradasVendidas = await _context.Entradas
                .CountAsync(e => e.EventoId == eventoId && e.Estado != "Cancelada");

            if (cantidad > evento.Capacidad - entradasVendidas)
            {
                TempData["Error"] = "No hay suficientes lugares disponibles";
                return RedirectToAction("Detalle", new { id = eventoId });
            }

            // Crear entradas
            for (int i = 0; i < cantidad; i++)
            {
                var entrada = new Entrada
                {
                    EventoId = eventoId,
                    ClienteId = cliente.Id,
                    Asiento = GenerarAsiento(eventoId, entradasVendidas + i + 1),
                    PrecioPagado = evento.Precio,
                    CodigoQR = GenerarCodigoQR(),
                    Estado = "Confirmada"
                };
                _context.Entradas.Add(entrada);
            }

            await _context.SaveChangesAsync();

            TempData["Success"] = $"¡Reserva exitosa! {cantidad} entrada(s) apartada(s). Paga en taquilla.";
            return RedirectToAction("Index", "ClienteEntradas");
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
}