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
        public async Task<IActionResult> UpdateEvento(int id, [FromBody] UpdateEventoRequest request)
        {
            Console.WriteLine($"=== UPDATE EVENTO {id} ===");
            Console.WriteLine($"Activo recibido: {request.Activo}");

            var evento = await _context.Eventos.FindAsync(id);
            if (evento == null)
                return NotFound(new { message = "Evento no encontrado" });

            evento.Nombre = request.Nombre;
            evento.Descripcion = request.Descripcion;
            evento.Fecha = request.Fecha;
            evento.Lugar = request.Lugar;
            evento.Capacidad = request.Capacidad;
            evento.Precio = request.Precio;
            evento.ImagenUrl = request.ImagenUrl;
            evento.CategoriaId = request.CategoriaId;
            evento.Activo = request.Activo;  // ← Asegura que esté aquí

            Console.WriteLine($"Activo antes de guardar: {evento.Activo}");

            await _context.SaveChangesAsync();

            Console.WriteLine($"Evento actualizado. Nuevo Activo: {evento.Activo}");

            return Ok(new { message = "Evento actualizado exitosamente" });
        }

        // DELETE: api/Eventos/{id} (solo Admin)
        [Authorize(Roles = "Admin")]
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteEvento(int id)
        {
            var evento = await _context.Eventos.FindAsync(id);
            if (evento == null)
                return NotFound(new { message = "Evento no encontrado" });

            evento.Activo = false;
            await _context.SaveChangesAsync();

            return Ok(new { message = "Evento eliminado exitosamente" });
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
    }
}