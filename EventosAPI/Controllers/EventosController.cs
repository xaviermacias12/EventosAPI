using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EventosAPI.Data;
using EventosAPI.Models;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Hosting;

namespace EventosAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class EventosController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public EventosController(ApplicationDbContext context, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
        }

        // PÚBLICO - No requiere autenticación
        [HttpGet]
        public async Task<IActionResult> GetEventos()
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
                    e.Activo,
                    e.CategoriaId,
                    CategoriaNombre = e.Categoria != null ? e.Categoria.Nombre : null
                })
                .ToListAsync();

            return Ok(eventos);
        }

        // ADMIN - Requiere JWT y rol Admin
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Admin")]
        [HttpGet("admin")]
        public async Task<IActionResult> GetEventosAdmin()
        {
            var eventos = await _context.Eventos
                .Include(e => e.Categoria)
                .OrderByDescending(e => e.Fecha)
                .ToListAsync();

            var resultado = new List<object>();

            foreach (var e in eventos)
            {
                var entradasVendidas = await _context.Entradas
                    .CountAsync(t => t.EventoId == e.Id && t.Estado != "Cancelada");

                var capacidadRestante = e.Capacidad - entradasVendidas;

                resultado.Add(new
                {
                    e.Id,
                    e.Nombre,
                    e.Descripcion,
                    e.Fecha,
                    e.Lugar,
                    e.Capacidad,
                    CapacidadRestante = capacidadRestante,
                    EntradasVendidas = entradasVendidas,
                    e.Precio,
                    e.ImagenUrl,
                    e.Activo,
                    e.CategoriaId,
                    CategoriaNombre = e.Categoria != null ? e.Categoria.Nombre : null
                });
            }

            return Ok(resultado);
        }

        // PÚBLICO
        [HttpGet("{id}")]
        public async Task<IActionResult> GetEvento(int id)
        {
            var evento = await _context.Eventos
                .Include(e => e.Categoria)
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == id);

            if (evento == null)
                return NotFound(new { message = "Evento no encontrado" });

            var entradasVendidas = await _context.Entradas
                .CountAsync(e => e.EventoId == id && e.Estado != "Cancelada");

            var capacidadRestante = evento.Capacidad - entradasVendidas;

            var result = new
            {
                evento.Id,
                evento.Nombre,
                evento.Descripcion,
                evento.Fecha,
                evento.Lugar,
                evento.Capacidad,
                evento.Precio,
                evento.ImagenUrl,
                evento.Activo,
                evento.FechaCreacion,
                evento.CategoriaId,
                CapacidadRestante = capacidadRestante,
                EntradasVendidas = entradasVendidas,
                Categoria = evento.Categoria != null ? new { evento.Categoria.Id, evento.Categoria.Nombre } : null
            };

            return Ok(result);
        }

        // ADMIN - Requiere JWT
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Admin")]
        [HttpPost]
        public async Task<IActionResult> CreateEvento([FromForm] CreateEventoRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var evento = new Evento
            {
                Nombre = request.Nombre,
                Descripcion = request.Descripcion,
                Fecha = request.Fecha,
                Lugar = request.Lugar,
                Capacidad = request.Capacidad,
                Precio = request.Precio,
                CategoriaId = request.CategoriaId,
                Activo = true
            };

            if (request.Imagen != null && request.Imagen.Length > 0)
            {
                var uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images", "eventos");
                if (!Directory.Exists(uploadsFolder))
                    Directory.CreateDirectory(uploadsFolder);

                var uniqueFileName = $"{Guid.NewGuid()}_{Path.GetFileName(request.Imagen.FileName)}";
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await request.Imagen.CopyToAsync(stream);
                }

                evento.ImagenUrl = $"/images/eventos/{uniqueFileName}";
            }

            _context.Eventos.Add(evento);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Evento creado exitosamente", id = evento.Id });
        }

        // ADMIN
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Admin")]
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateEvento(int id, [FromForm] UpdateEventoRequest request)
        {
            var evento = await _context.Eventos.FindAsync(id);
            if (evento == null)
                return NotFound(new { message = "Evento no encontrado" });

            evento.Nombre = request.Nombre;
            evento.Descripcion = request.Descripcion;
            evento.Fecha = request.Fecha;
            evento.Lugar = request.Lugar;
            evento.Capacidad = request.Capacidad;
            evento.Precio = request.Precio;
            evento.CategoriaId = request.CategoriaId;
            evento.Activo = request.Activo;

            if (request.Imagen != null && request.Imagen.Length > 0)
            {
                if (!string.IsNullOrEmpty(evento.ImagenUrl))
                {
                    var oldImagePath = Path.Combine(_webHostEnvironment.WebRootPath,
                        evento.ImagenUrl.TrimStart('/'));
                    if (System.IO.File.Exists(oldImagePath))
                    {
                        System.IO.File.Delete(oldImagePath);
                    }
                }

                var uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images", "eventos");
                if (!Directory.Exists(uploadsFolder))
                    Directory.CreateDirectory(uploadsFolder);

                var uniqueFileName = $"{Guid.NewGuid()}_{Path.GetFileName(request.Imagen.FileName)}";
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await request.Imagen.CopyToAsync(stream);
                }

                evento.ImagenUrl = $"/images/eventos/{uniqueFileName}";
            }

            await _context.SaveChangesAsync();

            return Ok(new { message = "Evento actualizado exitosamente" });
        }

        // ADMIN - Soft Delete
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Admin")]
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteEvento(int id)
        {
            var evento = await _context.Eventos.FindAsync(id);
            if (evento == null)
                return NotFound(new { message = "Evento no encontrado" });

            evento.Activo = false;
            await _context.SaveChangesAsync();

            return Ok(new { message = "Evento desactivado exitosamente" });
        }

        // ADMIN - Hard Delete
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Admin")]
        [HttpDelete("permanente/{id}")]
        public async Task<IActionResult> EliminarPermanentemente(int id)
        {
            try
            {
                var evento = await _context.Eventos.FindAsync(id);

                if (evento == null)
                    return NotFound(new { message = "Evento no encontrado" });

                var entradas = await _context.Entradas
                    .Where(e => e.EventoId == id)
                    .ToListAsync();

                if (entradas.Any())
                {
                    foreach (var entrada in entradas)
                    {
                        entrada.Estado = "Cancelada";
                        entrada.EventoId = null;
                    }
                    await _context.SaveChangesAsync();
                }

                if (!string.IsNullOrEmpty(evento.ImagenUrl))
                {
                    var imagePath = Path.Combine(_webHostEnvironment.WebRootPath,
                        evento.ImagenUrl.TrimStart('/'));
                    if (System.IO.File.Exists(imagePath))
                        System.IO.File.Delete(imagePath);
                }

                _context.Eventos.Remove(evento);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    message = "Evento eliminado permanentemente",
                    entradasCanceladas = entradas?.Count ?? 0
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Error al eliminar: {ex.Message}" });
            }
        }

        // PÚBLICO - Servir imágenes
        [HttpGet("imagen/{nombre}")]
        [AllowAnonymous]
        public async Task<IActionResult> ObtenerImagen(string nombre)
        {
            try
            {
                var rutaImagen = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "eventos", nombre);

                if (!System.IO.File.Exists(rutaImagen))
                {
                    return NotFound(new { mensaje = "Imagen no encontrada", ruta = rutaImagen });
                }

                var bytes = await System.IO.File.ReadAllBytesAsync(rutaImagen);
                var extension = Path.GetExtension(nombre).ToLower();

                var contentType = extension switch
                {
                    ".jpg" or ".jpeg" => "image/jpeg",
                    ".png" => "image/png",
                    ".gif" => "image/gif",
                    ".webp" => "image/webp",
                    _ => "image/jpeg"
                };

                return File(bytes, contentType);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }
    }

    public class CreateEventoRequest
    {
        [Required]
        public string Nombre { get; set; } = string.Empty;
        public string? Descripcion { get; set; }
        [Required]
        public DateTime Fecha { get; set; }
        [Required]
        public string Lugar { get; set; } = string.Empty;
        [Required]
        [Range(1, 10000)]
        public int Capacidad { get; set; }
        [Required]
        [Range(0, 999999.99)]
        public decimal Precio { get; set; }
        public string? ImagenUrl { get; set; }
        public int? CategoriaId { get; set; }
        public IFormFile? Imagen { get; set; }
    }

    public class UpdateEventoRequest
    {
        [Required]
        public string Nombre { get; set; } = string.Empty;
        public string? Descripcion { get; set; }
        [Required]
        public DateTime Fecha { get; set; }
        [Required]
        public string Lugar { get; set; } = string.Empty;
        [Required]
        [Range(1, 10000)]
        public int Capacidad { get; set; }
        [Required]
        [Range(0, 999999.99)]
        public decimal Precio { get; set; }
        public string? ImagenUrl { get; set; }
        public int? CategoriaId { get; set; }
        public bool Activo { get; set; } = true;
        public IFormFile? Imagen { get; set; }
    }
}