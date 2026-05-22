using EventosAPI.Data;
using EventosAPI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace EventosAPI.Controllers
{
    [Authorize]
    public class ClienteEntradasController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ClienteEntradasController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: /ClienteEntradas
        public async Task<IActionResult> Index()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId == null)
                return RedirectToAction("Login", "Auth");

            var cliente = await _context.Clientes
                .FirstOrDefaultAsync(c => c.UsuarioId == userId);

            if (cliente == null)
                return View(new List<Entrada>());

            var entradas = await _context.Entradas
                .Include(e => e.Evento)
                .Where(e => e.ClienteId == cliente.Id)
                .OrderByDescending(e => e.FechaCompra)
                .ToListAsync();

            return View(entradas);
        }

        // POST: /ClienteEntradas/Cancelar/5
        [HttpPost]
        public async Task<IActionResult> Cancelar(int id)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var cliente = await _context.Clientes.FirstOrDefaultAsync(c => c.UsuarioId == userId);

            var entrada = await _context.Entradas
                .Include(e => e.Evento)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (entrada == null)
                return NotFound();

            if (entrada.ClienteId != cliente?.Id)
                return Unauthorized();

            if (entrada.Estado != "Confirmada")
            {
                TempData["Error"] = "Esta entrada no puede cancelarse";
                return RedirectToAction("Index");
            }

            var horasRestantes = (entrada.Evento.Fecha - DateTime.Now).TotalHours;
            if (horasRestantes < 24)
            {
                TempData["Error"] = "Solo se pueden cancelar entradas con 24+ horas de anticipación";
                return RedirectToAction("Index");
            }

            entrada.Estado = "Cancelada";
            await _context.SaveChangesAsync();

            TempData["Success"] = "Entrada cancelada exitosamente";
            return RedirectToAction("Index");
        }
    }
}