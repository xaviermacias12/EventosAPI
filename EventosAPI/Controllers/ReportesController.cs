using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EventosAPI.Data;
using System.Text;
using EventosAPI.Services;

namespace EventosAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Admin")]
    public class ReportesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly PdfService _pdfService;

        public ReportesController(ApplicationDbContext context, PdfService pdfService)
        {
            _context = context;
            _pdfService = pdfService;
        }

        [HttpGet("estadisticas")]
        public async Task<IActionResult> GetEstadisticas()
        {
            var eventos = await _context.Eventos
                .Include(e => e.Categoria)
                .ToListAsync();

            var todasEntradas = await _context.Entradas
                .Where(e => e.Estado == "Confirmada")
                .ToListAsync();

            var ventasPorEvento = todasEntradas
                .Where(e => e.EventoId.HasValue)
                .GroupBy(e => e.EventoId)
                .Select(g => new
                {
                    eventoId = g.Key,
                    nombreEvento = eventos.FirstOrDefault(e => e.Id == g.Key)?.Nombre ?? "Desconocido",
                    ventas = g.Count()
                })
                .Select(x => new { evento = x.nombreEvento, ventas = x.ventas });

            var eventosPorCategoria = eventos
                .GroupBy(e => e.Categoria != null ? e.Categoria.Nombre : "Sin categoría")
                .Select(g => new
                {
                    categoria = g.Key,
                    cantidad = g.Count()
                })
                .ToList();

            var estadisticas = new
            {
                totalEventos = eventos.Count,
                totalEntradas = todasEntradas.Count,
                totalIngresos = todasEntradas.Sum(e => e.PrecioPagado),
                proximosEventos = eventos.Count(e => e.Fecha > DateTime.Now),
                ventasPorEvento = ventasPorEvento,
                eventosPorCategoria = eventosPorCategoria
            };

            return Ok(estadisticas);
        }

        [HttpGet("ventas/pdf")]
        public async Task<IActionResult> ReporteVentasPDF()
        {
            var eventosQuery = await _context.Eventos
                .Include(e => e.Entradas)
                .Select(e => new EventoReporte
                {
                    Id = e.Id,
                    Nombre = e.Nombre,
                    Fecha = e.Fecha,
                    Lugar = e.Lugar,
                    EntradasVendidas = e.Entradas!.Count(t => t.Estado != "Cancelada"),
                    Ingresos = e.Entradas!.Where(t => t.Estado != "Cancelada").Sum(t => t.PrecioPagado),
                    Activo = e.Activo
                })
                .OrderByDescending(e => e.Ingresos)
                .ToListAsync();

            var estadisticas = new EstadisticasReporte
            {
                totalEventos = eventosQuery.Count,
                totalEntradas = eventosQuery.Sum(e => e.EntradasVendidas),
                totalIngresos = eventosQuery.Sum(e => e.Ingresos),
                eventosConVentas = eventosQuery.Count(e => e.EntradasVendidas > 0)
            };

            var pdfBytes = _pdfService.GenerarReporteVentas(estadisticas, eventosQuery);

            return File(pdfBytes, "application/pdf", $"reporte_ventas_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");
        }

        [HttpGet("entrada/{id}/ticket")]
        [AllowAnonymous]
        public async Task<IActionResult> TicketEntrada(int id)
        {
            var entrada = await _context.Entradas
                .Include(e => e.Evento)
                .Include(e => e.Cliente)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (entrada == null)
                return NotFound(new { message = "Entrada no encontrada" });

            var pdfBytes = _pdfService.GenerarTicket(entrada);

            return File(pdfBytes, "application/pdf", $"ticket_{entrada.Id}.pdf");
        }
    }
}