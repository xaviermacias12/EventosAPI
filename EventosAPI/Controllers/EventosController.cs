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

        // GET: api/Eventos (público)
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

        // GET: api/Eventos/admin (Admin ve todos, incluso inactivos)
        [Authorize(Roles = "Admin")]
        [HttpGet("admin")]
        public async Task<IActionResult> GetEventosAdmin()
        {
            var eventos = await _context.Eventos
                .Include(e => e.Categoria)
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

        // GET: api/Eventos/{id}
        [HttpGet("{id}")]
        public async Task<IActionResult> GetEvento(int id)
        {
            var evento = await _context.Eventos
                .Include(e => e.Categoria)
                .AsNoTracking()  // ← Agrega esto para evitar ciclos
                .FirstOrDefaultAsync(e => e.Id == id);

            if (evento == null)
                return NotFound(new { message = "Evento no encontrado" });

            // Crear un objeto anónimo para evitar ciclos de referencia
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
                Categoria = evento.Categoria != null ? new { evento.Categoria.Id, evento.Categoria.Nombre } : null
            };

            return Ok(result);
        }

        // POST: api/Eventos (solo Admin)
        [Authorize(Roles = "Admin")]
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

            // Manejo de imagen
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
                Console.WriteLine($"=== IMAGEN GUARDADA ===");
                Console.WriteLine($"URL guardada: {evento.ImagenUrl}");
                Console.WriteLine($"Ruta física: {filePath}");
            }

            _context.Eventos.Add(evento);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Evento creado exitosamente", id = evento.Id });
        }

        // PUT: api/Eventos/{id} (solo Admin)
        [Authorize(Roles = "Admin")]
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateEvento(int id, [FromForm] UpdateEventoRequest request)
        {
            Console.WriteLine($"=== UPDATE EVENTO {id} ===");
            Console.WriteLine($"Nombre: {request.Nombre}");
            Console.WriteLine($"Activo recibido: {request.Activo}");

            var evento = await _context.Eventos.FindAsync(id);
            if (evento == null)
                return NotFound(new { message = "Evento no encontrado" });

            // Actualizar campos básicos
            evento.Nombre = request.Nombre;
            evento.Descripcion = request.Descripcion;
            evento.Fecha = request.Fecha;
            evento.Lugar = request.Lugar;
            evento.Capacidad = request.Capacidad;
            evento.Precio = request.Precio;
            evento.CategoriaId = request.CategoriaId;
            evento.Activo = request.Activo;

            // Manejo de nueva imagen (si se sube una)
            if (request.Imagen != null && request.Imagen.Length > 0)
            {
                // Eliminar imagen anterior si existe
                if (!string.IsNullOrEmpty(evento.ImagenUrl))
                {
                    var oldImagePath = Path.Combine(_webHostEnvironment.WebRootPath,
                        evento.ImagenUrl.TrimStart('/'));
                    if (System.IO.File.Exists(oldImagePath))
                    {
                        System.IO.File.Delete(oldImagePath);
                        Console.WriteLine($"Imagen anterior eliminada: {oldImagePath}");
                    }
                }

                // Guardar nueva imagen
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
                Console.WriteLine($"Nueva imagen guardada: {evento.ImagenUrl}");
            }

            await _context.SaveChangesAsync();

            Console.WriteLine($"Evento actualizado correctamente. Activo: {evento.Activo}");

            return Ok(new { message = "Evento actualizado exitosamente" });
        }

        // // Soft Delete - Solo desactivar
        [Authorize(Roles = "Admin")]
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

        // Cancelar todas las entradas de un evento
        [HttpPost("cancelar-todas-entradas/{eventoId}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CancelarTodasEntradas(int eventoId)
        {
            var evento = await _context.Eventos
                .Include(e => e.Entradas)
                .FirstOrDefaultAsync(e => e.Id == eventoId);

            if (evento == null)
                return NotFound(new { message = "Evento no encontrado" });

            var entradasConfirmadas = evento.Entradas.Where(e => e.Estado == "Confirmada").ToList();

            if (!entradasConfirmadas.Any())
                return Ok(new { message = "No hay entradas confirmadas para cancelar", cantidad = 0 });

            foreach (var entrada in entradasConfirmadas)
            {
                entrada.Estado = "Cancelada";
                // entrada.FechaCancelacion = DateTime.Now; // Opcional
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = $"Se cancelaron {entradasConfirmadas.Count} entradas",
                cantidad = entradasConfirmadas.Count
            });
        }

        // Hard Delete - Elimina evento pero conserva entradas (solo las marca como canceladas)
        [HttpDelete("permanente/{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> EliminarPermanentemente(int id)
        {
            try
            {
                var evento = await _context.Eventos.FindAsync(id);

                if (evento == null)
                    return NotFound(new { message = "Evento no encontrado" });

                // Obtener y cancelar las entradas antes de eliminar
                var entradas = await _context.Entradas
                    .Where(e => e.EventoId == id)
                    .ToListAsync();

                        if (entradas.Any())
                        {
                            foreach (var entrada in entradas)
                            {
                                entrada.Estado = "Cancelada";  // ← CAMBIAR ESTADO
                                entrada.EventoId = null;        // ← Desvincular evento
                            }
                            await _context.SaveChangesAsync();
                            Console.WriteLine($"Se cancelaron {entradas.Count} entradas");
                        }

                // Eliminar imagen
                if (!string.IsNullOrEmpty(evento.ImagenUrl))
                {
                    var imagePath = Path.Combine(_webHostEnvironment.WebRootPath,
                        evento.ImagenUrl.TrimStart('/'));
                    if (System.IO.File.Exists(imagePath))
                        System.IO.File.Delete(imagePath);
                }

                // Eliminar evento
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

        // Agrega esto al final de tu EventosController
        [HttpGet("imagen/{nombre}")]
        [AllowAnonymous]
        public async Task<IActionResult> ObtenerImagen(string nombre)
        {
            try
            {
                // Buscar en la ruta correcta
                var rutaImagen = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "eventos", nombre);

                Console.WriteLine($"=== BUSCANDO IMAGEN ===");
                Console.WriteLine($"Archivo solicitado: {nombre}");
                Console.WriteLine($"Ruta completa: {rutaImagen}");
                Console.WriteLine($"El archivo existe: {System.IO.File.Exists(rutaImagen)}");

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

        // Endpoint para listar imágenes (debug)
        [HttpGet("listar-imagenes")]
        [AllowAnonymous]
        public IActionResult ListarImagenes()
        {
            var rutaCarpeta = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "eventos");

            if (!Directory.Exists(rutaCarpeta))
            {
                return Ok(new { mensaje = "La carpeta no existe", ruta = rutaCarpeta });
            }

            var archivos = Directory.GetFiles(rutaCarpeta)
                .Select(f => Path.GetFileName(f))
                .ToList();

            return Ok(new
            {
                rutaCarpeta,
                cantidad = archivos.Count,
                archivos = archivos
            });
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