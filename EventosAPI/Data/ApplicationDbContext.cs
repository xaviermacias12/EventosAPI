using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using EventosAPI.Models;

namespace EventosAPI.Data
{
    public class ApplicationDbContext : IdentityDbContext<Usuario>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Evento> Eventos { get; set; }
        public DbSet<Cliente> Clientes { get; set; }
        public DbSet<Entrada> Entradas { get; set; }
        public DbSet<Categoria> Categorias { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Cliente>()
                .HasIndex(c => c.Email)
                .IsUnique();

            modelBuilder.Entity<Entrada>()
                .HasOne(e => e.Evento)
                .WithMany(e => e.Entradas)
                .HasForeignKey(e => e.EventoId);

            modelBuilder.Entity<Entrada>()
                .HasOne(e => e.Cliente)
                .WithMany(c => c.Entradas)
                .HasForeignKey(e => e.ClienteId);
        }
    }
}