using System.ComponentModel.DataAnnotations;
using System.Diagnostics;

namespace EventosAPI.Models
{
    public class Categoria
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "El nombre es obligatorio")]
        [StringLength(100)]
        public string Nombre { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Descripcion { get; set; }

        public List<Evento>? Eventos { get; set; }
    }
}