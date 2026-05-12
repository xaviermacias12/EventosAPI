using Microsoft.AspNetCore.Identity;

namespace EventosAPI.Models
{
    public class Usuario : IdentityUser
    {
        public string Nombre { get; set; } = string.Empty;
        public string? Rol { get; set; } = "Cliente";
        public DateTime FechaRegistro { get; set; } = DateTime.Now;
    }
}