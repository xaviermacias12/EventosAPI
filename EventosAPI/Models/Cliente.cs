using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EventosAPI.Models
{
    public class Cliente
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "El nombre es obligatorio")]
        [StringLength(100)]
        public string Nombre { get; set; } = string.Empty;

        [Required(ErrorMessage = "El email es obligatorio")]
        [EmailAddress]
        [StringLength(100)]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "El teléfono es obligatorio")]
        [Phone]
        [StringLength(15, MinimumLength = 8)]
        public string Telefono { get; set; } = string.Empty;

        [Required]
        public string? UsuarioId { get; set; } = string.Empty;

        [ForeignKey("UsuarioId")]
        public Usuario? Usuario { get; set; }

        public List<Entrada>? Entradas { get; set; }
    }
}