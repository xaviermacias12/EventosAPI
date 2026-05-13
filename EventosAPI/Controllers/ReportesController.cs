using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EventosAPI.Data;
using System.Text;

namespace EventosAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    public class ReportesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public ReportesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/Reportes/estadisticas
        [Authorize(Roles = "Admin")]
        // GET: api/Reportes/estadisticas
        [HttpGet("estadisticas")]
        public async Task<IActionResult> GetEstadisticas()
        {
            var eventos = await _context.Eventos
                .ToListAsync();

            var todasEntradas = await _context.Entradas
                .ToListAsync();

            // Ventas por evento (sin incluir el objeto Evento completo)
            var ventasPorEvento = new List<object>();
            foreach (var entrada in todasEntradas)
            {
                var evento = eventos.FirstOrDefault(e => e.Id == entrada.EventoId);
                var nombreEvento = evento?.Nombre ?? "Sin evento";
                ventasPorEvento.Add(new { evento = nombreEvento, ventas = 1 });
            }

            var ventasAgrupadas = ventasPorEvento
                .GroupBy(v => v.GetType().GetProperty("evento").GetValue(v, null))
                .Select(g => new { evento = g.Key, ventas = g.Count() });

            // Eventos por categoría
            var eventosPorCategoria = eventos
                .GroupBy(e => e.CategoriaId ?? 0)
                .Select(g => new { categoriaId = g.Key, cantidad = g.Count() });

            var estadisticas = new
            {
                totalEventos = eventos.Count,
                totalEntradas = todasEntradas.Count,
                totalIngresos = todasEntradas.Sum(e => e.PrecioPagado),
                proximosEventos = eventos.Count(e => e.Fecha > DateTime.Now),
                ventasPorEvento = ventasAgrupadas,
                eventosPorCategoria = eventosPorCategoria
            };

            return Ok(estadisticas);
        }

        // GET: api/Reportes/ventas/pdf
        [Authorize(Roles = "Admin")]
        [HttpGet("ventas/pdf")]
        public async Task<IActionResult> ReporteVentasPDF()
        {
            // Usar todos los eventos (activos e inactivos) para el reporte de admin
            var eventos = await _context.Eventos
                .Include(e => e.Entradas)
                .Select(e => new
                {
                    e.Id,
                    e.Nombre,
                    e.Fecha,
                    e.Lugar,
                    CapacidadTotal = e.Capacidad,
                    EntradasVendidas = e.Entradas!.Count(t => t.Estado != "Cancelada"),
                    Ingresos = e.Entradas!.Where(t => t.Estado != "Cancelada").Sum(t => t.PrecioPagado),
                    e.Activo
                })
                .OrderByDescending(e => e.Ingresos)
                .ToListAsync();

            var totalIngresos = eventos.Sum(e => e.Ingresos);
            var totalEntradas = eventos.Sum(e => e.EntradasVendidas);
            var eventosConVentas = eventos.Where(e => e.EntradasVendidas > 0).Count();

            var html = $@"
    <!DOCTYPE html>
    <html>
    <head>
        <meta charset='utf-8'>
        <title>Reporte de Ventas</title>
        <style>
            body {{ font-family: Arial, sans-serif; margin: 20px; }}
            h1 {{ color: #1e293b; text-align: center; font-size: 24px; }}
            h3 {{ color: #334155; font-size: 18px; }}
            table {{ width: 100%; border-collapse: collapse; margin-top: 20px; }}
            th, td {{ border: 1px solid #ddd; padding: 10px; text-align: left; }}
            th {{ background-color: #1e293b; color: white; }}
            .total {{ font-weight: bold; background-color: #f1f5f9; }}
            .footer {{ margin-top: 30px; text-align: center; font-size: 12px; color: #666; }}
            .text-right {{ text-align: right; }}
        </style>
    </head>
    <body>
        <h1>📊 Reporte de Ventas</h1>
        <p>Fecha de generación: {DateTime.Now:dd/MM/yyyy HH:mm}</p>

        <h3>Resumen General</h3>
        <table style='width: 50%;'>
            <tr><td style='width: 60%;'><strong>Total de ingresos:</strong></td><td><strong>${totalIngresos:N2}</strong></td></tr>
            <tr><td><strong>Total de entradas vendidas:</strong></td><td><strong>{totalEntradas}</strong></td></tr>
            <tr><td><strong>Eventos con ventas:</strong></td><td><strong>{eventosConVentas}</strong></td></tr>
            <tr><td><strong>Total de eventos:</strong></td><td><strong>{eventos.Count}</strong></td></tr>
        </table>

        <h3>Detalle por Evento</h3>
        <table>
            <thead>
                <tr>
                    <th>Evento</th>
                    <th>Fecha</th>
                    <th>Lugar</th>
                    <th class='text-right'>Entradas Vendidas</th>
                    <th class='text-right'>Ingresos</th>
                </tr>
            </thead>
            <tbody>";

            foreach (var e in eventos)
            {
                var estadoTexto = e.Activo ? "Activo" : "Inactivo";
                var estadoColor = e.Activo ? "green" : "gray";
                html += $@"
            <tr>
                <td>{e.Nombre}</td>
                <td>{e.Fecha:dd/MM/yyyy}</td>
                <td>{e.Lugar}</td>
                <td class='text-right'>{e.EntradasVendidas} / {e.CapacidadTotal}</td>
                <td class='text-right'>${e.Ingresos:N2}</td>
            </tr>";
            }

            html += $@"
            </tbody>
            <tfoot>
                <tr>
                    <td colspan='3'><strong>TOTALES</strong></td>
                    <td class='text-right'><strong>{totalEntradas}</strong></td>
                    <td class='text-right'><strong>${totalIngresos:N2}</strong></td>
                </tr>
            </tfoot>
        </table>
        <div class='footer'>
            <p>Reporte generado por el sistema de gestión de eventos</p>
        </div>
    </body>
    </html>";

            var bytes = Encoding.UTF8.GetBytes(html);
            return File(bytes, "text/html", $"reporte_ventas_{DateTime.Now:yyyyMMdd_HHmmss}.html");
        }

        // GET: api/Reportes/entrada/{id}/ticket
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

            var html = $@"
            <!DOCTYPE html>
            <html>
            <head>
                <meta charset='utf-8'>
                <title>Ticket de Entrada</title>
                <style>
                    body {{ font-family: Arial, sans-serif; margin: 0; padding: 20px; background: #f1f5f9; }}
                    .ticket {{ max-width: 400px; margin: 0 auto; background: white; border-radius: 16px; overflow: hidden; box-shadow: 0 4px 12px rgba(0,0,0,0.1); }}
                    .header {{ background: #1e293b; color: white; padding: 20px; text-align: center; }}
                    .content {{ padding: 20px; }}
                    .qr {{ text-align: center; margin: 20px 0; }}
                    .qr-code {{ font-family: monospace; font-size: 20px; letter-spacing: 2px; background: #f1f5f9; padding: 10px; border-radius: 8px; }}
                    .footer {{ background: #f1f5f9; padding: 15px; text-align: center; font-size: 12px; color: #666; }}
                    .evento {{ font-size: 18px; font-weight: bold; margin-bottom: 5px; }}
                    .lugar {{ color: #666; margin-bottom: 15px; }}
                </style>
            </head>
            <body>
                <div class='ticket'>
                    <div class='header'>
                        <h2>🎟️ Ticket de Entrada</h2>
                        <p>#{entrada.Id}</p>
                    </div>
                    <div class='content'>
                        <div class='evento'>{entrada.Evento?.Nombre}</div>
                        <div class='lugar'>📅 {entrada.Evento?.Fecha:dd/MM/yyyy HH:mm} | 📍 {entrada.Evento?.Lugar}</div>
                        <hr>
                        <p><strong>Asiento:</strong> {entrada.Asiento}</p>
                        <p><strong>Cliente:</strong> {entrada.Cliente?.Nombre}</p>
                        <p><strong>Precio:</strong> ${entrada.PrecioPagado:N2}</p>
                        <p><strong>Estado:</strong> {entrada.Estado}</p>
                        <div class='qr'>
                            <div class='qr-code'>{entrada.CodigoQR}</div>
                            <p><small>Código de verificación</small></p>
                        </div>
                    </div>
                    <div class='footer'>
                        <p>Presenta este ticket en la entrada del evento</p>
                    </div>
                </div>
            </body>
            </html>";

            var bytes = Encoding.UTF8.GetBytes(html);
            return File(bytes, "text/html", $"ticket_{entrada.Id}.html");
        }
    }
}