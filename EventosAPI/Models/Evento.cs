using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EventosAPI.Models
{
    public class Evento
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "El nombre es obligatorio")]
        [StringLength(200)]
        public string Nombre { get; set; } = string.Empty;

        [StringLength(1000)]
        public string? Descripcion { get; set; }

        [Required(ErrorMessage = "La fecha es obligatoria")]
        public DateTime Fecha { get; set; }

        [Required(ErrorMessage = "El lugar es obligatorio")]
        [StringLength(200)]
        public string Lugar { get; set; } = string.Empty;

        [Required(ErrorMessage = "La capacidad es obligatoria")]
        [Range(1, 10000)]
        public int Capacidad { get; set; }

        [Required(ErrorMessage = "El precio es obligatorio")]
        [Range(0, 999999.99)]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Precio { get; set; }

        public string? ImagenUrl { get; set; }

        public bool Activo { get; set; } = true;

        public DateTime FechaCreacion { get; set; } = DateTime.Now;

        public int? CategoriaId { get; set; }

        [ForeignKey("CategoriaId")]
        public Categoria? Categoria { get; set; }

        public List<Entrada>? Entradas { get; set; }
    }
}