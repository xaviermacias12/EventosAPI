using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EventosAPI.Models
{
    public class Entrada
    {
        [Key]
        public int Id { get; set; }

        public int? EventoId { get; set; }

        [Required]
        public int ClienteId { get; set; }

        [Required]
        [StringLength(20)]
        public string Asiento { get; set; } = string.Empty;

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal PrecioPagado { get; set; }

        public DateTime FechaCompra { get; set; } = DateTime.Now;

        public string Estado { get; set; } = "Confirmada";

        public string? CodigoQR { get; set; }

        [ForeignKey("EventoId")]
        public Evento? Evento { get; set; }

        [ForeignKey("ClienteId")]
        public Cliente? Cliente { get; set; }
    }
}