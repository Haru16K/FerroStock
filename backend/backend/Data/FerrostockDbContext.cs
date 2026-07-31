using backend.Models;
using Microsoft.EntityFrameworkCore;

namespace backend.Data;

public class FerroStockDbContext : DbContext
{
    public FerroStockDbContext(DbContextOptions<FerroStockDbContext> options)
        : base(options) { }

    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Categoria> Categorias => Set<Categoria>();
    public DbSet<Producto> Productos => Set<Producto>();
    public DbSet<Movimiento> Movimientos => Set<Movimiento>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // ── Categoria ──────────────────────────────────────────────────
        builder.Entity<Categoria>(e =>
        {
            e.HasKey(c => c.Id);
            e.Property(c => c.Nombre).IsRequired().HasMaxLength(100);
        });

        // ── Usuario ────────────────────────────────────────────────────
        builder.Entity<Usuario>(e =>
        {
            e.HasKey(u => u.Id);
            e.Property(u => u.Email).IsRequired().HasMaxLength(150);
            e.HasIndex(u => u.Email).IsUnique();
            e.Property(u => u.Nombre).IsRequired().HasMaxLength(150);
            e.Property(u => u.PasswordHash).IsRequired();
            e.Property(u => u.Rol).HasConversion<string>();
        });

        // ── Producto ───────────────────────────────────────────────────
        builder.Entity<Producto>(e =>
        {
            e.HasKey(p => p.Id);
            e.Property(p => p.Codigo).IsRequired().HasMaxLength(50);
            e.HasIndex(p => p.Codigo).IsUnique();
            e.Property(p => p.Nombre).IsRequired().HasMaxLength(200);
            e.Property(p => p.PrecioCompra).HasColumnType("decimal(10,2)");
            e.HasOne(p => p.Categoria)
             .WithMany(c => c.Productos)
             .HasForeignKey(p => p.CategoriaId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ── Movimiento ─────────────────────────────────────────────────
        builder.Entity<Movimiento>(e =>
        {
            e.HasKey(m => m.Id);
            e.Property(m => m.Tipo).HasConversion<string>();
            e.Property(m => m.PrecioUnitario).HasColumnType("decimal(10,2)");
            e.HasIndex(m => m.Fecha);
            e.HasOne(m => m.Producto)
             .WithMany(p => p.Movimientos)
             .HasForeignKey(m => m.ProductoId)
             .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(m => m.Usuario)
             .WithMany(u => u.Movimientos)
             .HasForeignKey(m => m.UsuarioId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ── Seed de datos iniciales ────────────────────────────────────
        builder.Entity<Categoria>().HasData(
            new Categoria { Id = 1, Nombre = "Herramientas", Descripcion = "Martillos, destornilladores, llaves" },
            new Categoria { Id = 2, Nombre = "Electricidad", Descripcion = "Cables, interruptores, tomacorrientes" },
            new Categoria { Id = 3, Nombre = "Plomería", Descripcion = "Tubos, llaves de paso, uniones" },
            new Categoria { Id = 4, Nombre = "Pinturas", Descripcion = "Pinturas, diluyentes, brochas" },
            new Categoria { Id = 5, Nombre = "Fijaciones", Descripcion = "Tornillos, clavos, tacos" }
        );

        // Admin inicial — contraseña: Admin123!
        builder.Entity<Usuario>().HasData(new Usuario
        {
            Id = 1,
            Nombre = "Administrador FerroTech",
            Email = "admin@ferrotech.pe",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin123!"),
            Rol = RolUsuario.Administrador,
            Activo = true
        });

        builder.Entity<Producto>().HasData(
            new Producto { Id = 1, Codigo = "HER-001", Nombre = "Martillo carpintero 16oz", CategoriaId = 1, PrecioCompra = 18.50m, StockActual = 25, StockMinimo = 5, Activo = true },
            new Producto { Id = 2, Codigo = "HER-002", Nombre = "Destornillador estrella 6\"", CategoriaId = 1, PrecioCompra = 8.00m, StockActual = 40, StockMinimo = 10, Activo = true },
            new Producto { Id = 3, Codigo = "ELE-001", Nombre = "Cable NYM 2.5mm x metro", CategoriaId = 2, PrecioCompra = 3.20m, StockActual = 200, StockMinimo = 50, Activo = true },
            new Producto { Id = 4, Codigo = "ELE-002", Nombre = "Interruptor simple Bticino", CategoriaId = 2, PrecioCompra = 6.50m, StockActual = 60, StockMinimo = 15, Activo = true },
            new Producto { Id = 5, Codigo = "PLO-001", Nombre = "Tubo PVC 3/4\" x 3m", CategoriaId = 3, PrecioCompra = 12.00m, StockActual = 80, StockMinimo = 20, Activo = true },
            new Producto { Id = 6, Codigo = "PIN-001", Nombre = "Pintura latex blanca 4L", CategoriaId = 4, PrecioCompra = 35.00m, StockActual = 3, StockMinimo = 5, Activo = true }, // crítico
            new Producto { Id = 7, Codigo = "FIJ-001", Nombre = "Tornillo autorroscante 1\"", CategoriaId = 5, PrecioCompra = 0.10m, StockActual = 500, StockMinimo = 100, Activo = true }
        );
    }
}