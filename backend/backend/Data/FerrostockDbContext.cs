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
    public DbSet<Proveedor> Proveedores => Set<Proveedor>();
    public DbSet<ProductoProveedor> ProductoProveedores => Set<ProductoProveedor>();
    public DbSet<OrdenCompra> OrdenesCompra => Set<OrdenCompra>();
    public DbSet<DetalleOrdenCompra> DetallesOrdenCompra => Set<DetalleOrdenCompra>();
    public DbSet<Venta> Ventas => Set<Venta>();
    public DbSet<DetalleVenta> DetallesVenta => Set<DetalleVenta>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<EjecucionModelo> EjecucionesModelo => Set<EjecucionModelo>();
    public DbSet<Recomendacion> Recomendaciones => Set<Recomendacion>();
    public DbSet<ConfiguracionIa> ConfiguracionesIa => Set<ConfiguracionIa>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // ── Categoria ──────────────────────────────────────────────────
        builder.Entity<Categoria>(e =>
        {
            e.HasKey(c => c.Id);
            e.Property(c => c.Nombre).IsRequired().HasMaxLength(100);
            e.HasIndex(c => c.Nombre).IsUnique();   // HU-03: el nombre es único
            e.Property(c => c.Descripcion).HasMaxLength(250);
        });

        // ── Usuario ────────────────────────────────────────────────────
        builder.Entity<Usuario>(e =>
        {
            e.HasKey(u => u.Id);
            e.Property(u => u.Email).IsRequired().HasMaxLength(150);
            e.HasIndex(u => u.Email).IsUnique();
            e.Property(u => u.Nombre).IsRequired().HasMaxLength(150);
            e.Property(u => u.PasswordHash).IsRequired().HasMaxLength(100);   // un hash BCrypt mide 60; sin tope sería nvarchar(max)
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
            e.Property(p => p.PrecioVenta).HasColumnType("decimal(10,2)");
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

        // ── Proveedor ──────────────────────────────────────────────────
        builder.Entity<Proveedor>(e =>
        {
            e.HasKey(p => p.Id);
            e.Property(p => p.Ruc).IsRequired().HasMaxLength(11).IsFixedLength();
            e.HasIndex(p => p.Ruc).IsUnique();
            e.Property(p => p.RazonSocial).IsRequired().HasMaxLength(200);
            e.Property(p => p.Contacto).HasMaxLength(150);
            e.Property(p => p.Telefono).HasMaxLength(30);
            e.Property(p => p.Email).HasMaxLength(150);
            e.Property(p => p.Direccion).HasMaxLength(250);
            e.Property(p => p.DiasEntrega).HasDefaultValue(7);
            e.Property(p => p.Calificacion).HasColumnType("decimal(3,1)");
        });

        // ── ProductoProveedor (muchos a muchos) ────────────────────────
        builder.Entity<ProductoProveedor>(e =>
        {
            e.HasKey(x => new { x.ProductoId, x.ProveedorId });
            e.HasOne(x => x.Producto)
             .WithMany()
             .HasForeignKey(x => x.ProductoId)
             .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Proveedor)
             .WithMany(p => p.Productos)
             .HasForeignKey(x => x.ProveedorId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ── OrdenCompra ────────────────────────────────────────────────
        builder.Entity<OrdenCompra>(e =>
        {
            e.HasKey(o => o.Id);
            e.Property(o => o.Estado).HasConversion<string>().HasMaxLength(20);
            e.Property(o => o.Total).HasColumnType("decimal(12,2)");
            e.Property(o => o.Observaciones).HasMaxLength(500);
            e.Property(o => o.MotivoRechazo).HasMaxLength(300);
            e.Property(o => o.MotivoCancelacion).HasMaxLength(300);
            e.Property(o => o.RowVersion).IsRowVersion();
            e.HasIndex(o => o.FechaCreacion);
            e.HasIndex(o => new { o.ProveedorId, o.FechaCreacion });

            e.HasOne(o => o.Proveedor)
             .WithMany(p => p.Ordenes)
             .HasForeignKey(o => o.ProveedorId)
             .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(o => o.CreadoPor).WithMany()
             .HasForeignKey(o => o.CreadoPorId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(o => o.RevisadoPor).WithMany()
             .HasForeignKey(o => o.RevisadoPorId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(o => o.RecibidoPor).WithMany()
             .HasForeignKey(o => o.RecibidoPorId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(o => o.CanceladoPor).WithMany()
             .HasForeignKey(o => o.CanceladoPorId).OnDelete(DeleteBehavior.Restrict);
        });

        // ── DetalleOrdenCompra ─────────────────────────────────────────
        builder.Entity<DetalleOrdenCompra>(e =>
        {
            e.HasKey(d => d.Id);
            e.Property(d => d.PrecioUnitario).HasColumnType("decimal(10,2)");
            e.HasOne(d => d.OrdenCompra)
             .WithMany(o => o.Detalles)
             .HasForeignKey(d => d.OrdenCompraId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(d => d.Producto)
             .WithMany()
             .HasForeignKey(d => d.ProductoId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ── Venta ──────────────────────────────────────────────────────
        builder.Entity<Venta>(e =>
        {
            e.HasKey(v => v.Id);
            e.Property(v => v.Estado).HasConversion<string>().HasMaxLength(20);
            e.Property(v => v.ClienteNombre).HasMaxLength(150);
            e.Property(v => v.ClienteDocumento).HasMaxLength(20);
            e.Property(v => v.Subtotal).HasColumnType("decimal(12,2)");
            e.Property(v => v.DescuentoPorcentaje).HasColumnType("decimal(5,2)");
            e.Property(v => v.DescuentoMonto).HasColumnType("decimal(12,2)");
            e.Property(v => v.Total).HasColumnType("decimal(12,2)");
            e.Property(v => v.MotivoAnulacion).HasMaxLength(300);
            e.HasIndex(v => v.Fecha);
            e.HasIndex(v => new { v.VendedorId, v.Fecha });

            e.HasOne(v => v.Vendedor).WithMany()
             .HasForeignKey(v => v.VendedorId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(v => v.DescuentoAplicadoPor).WithMany()
             .HasForeignKey(v => v.DescuentoAplicadoPorId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(v => v.AnuladoPor).WithMany()
             .HasForeignKey(v => v.AnuladoPorId).OnDelete(DeleteBehavior.Restrict);
        });

        // ── DetalleVenta ───────────────────────────────────────────────
        builder.Entity<DetalleVenta>(e =>
        {
            e.HasKey(d => d.Id);
            e.Property(d => d.PrecioUnitario).HasColumnType("decimal(10,2)");
            e.HasOne(d => d.Venta)
             .WithMany(v => v.Detalles)
             .HasForeignKey(d => d.VentaId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(d => d.Producto)
             .WithMany()
             .HasForeignKey(d => d.ProductoId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ── PasswordResetToken ─────────────────────────────────────────
        builder.Entity<PasswordResetToken>(e =>
        {
            e.HasKey(t => t.Id);
            e.Property(t => t.TokenHash).IsRequired().HasMaxLength(64);
            e.HasIndex(t => t.TokenHash).IsUnique();
            e.HasIndex(t => new { t.UsuarioId, t.CreadoEn });
            e.HasOne(t => t.Usuario)
             .WithMany()
             .HasForeignKey(t => t.UsuarioId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ── EjecucionModelo ────────────────────────────────────────────
        builder.Entity<EjecucionModelo>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Origen).IsRequired().HasMaxLength(20);
            e.Property(x => x.Mes).IsRequired().HasMaxLength(7);
            e.Property(x => x.Estado).IsRequired().HasMaxLength(10);
            e.Property(x => x.Mensaje).HasMaxLength(500);
            e.Property(x => x.Mae).HasColumnType("decimal(10,2)");
            e.Property(x => x.Mape).HasColumnType("decimal(10,2)");
            e.HasIndex(x => new { x.Mes, x.Estado });
        });

        // ── Recomendacion ──────────────────────────────────────────────
        builder.Entity<Recomendacion>(e =>
        {
            e.HasKey(r => r.Id);
            e.Property(r => r.Tipo).IsRequired().HasMaxLength(20);
            e.Property(r => r.Clave).IsRequired().HasMaxLength(40);
            e.Property(r => r.Titulo).IsRequired().HasMaxLength(200);
            e.Property(r => r.Motivo).IsRequired();
            e.Property(r => r.Prioridad).IsRequired().HasMaxLength(10);
            e.Property(r => r.Estado).IsRequired().HasMaxLength(12);
            e.Property(r => r.MontoEstimado).HasColumnType("decimal(12,2)");
            e.HasIndex(r => new { r.Tipo, r.Estado });
            e.HasIndex(r => r.Clave);

            e.HasOne(r => r.Producto).WithMany()
             .HasForeignKey(r => r.ProductoId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(r => r.Proveedor).WithMany()
             .HasForeignKey(r => r.ProveedorId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(r => r.GestionadaPor).WithMany()
             .HasForeignKey(r => r.GestionadaPorId).OnDelete(DeleteBehavior.Restrict);
        });

        // ── ConfiguracionIa ────────────────────────────────────────────
        builder.Entity<ConfiguracionIa>(e =>
        {
            e.HasKey(c => c.Clave);
            e.Property(c => c.Clave).HasMaxLength(100);
            e.Property(c => c.Valor).IsRequired().HasMaxLength(200);
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
            new Producto { Id = 1, Codigo = "HER-001", Nombre = "Martillo carpintero 16oz", CategoriaId = 1, PrecioCompra = 18.50m, PrecioVenta = 25.00m, StockActual = 25, StockMinimo = 5, Activo = true },
            new Producto { Id = 2, Codigo = "HER-002", Nombre = "Destornillador estrella 6\"", CategoriaId = 1, PrecioCompra = 8.00m, PrecioVenta = 12.00m, StockActual = 40, StockMinimo = 10, Activo = true },
            new Producto { Id = 3, Codigo = "ELE-001", Nombre = "Cable NYM 2.5mm x metro", CategoriaId = 2, PrecioCompra = 3.20m, PrecioVenta = 4.50m, StockActual = 200, StockMinimo = 50, Activo = true },
            new Producto { Id = 4, Codigo = "ELE-002", Nombre = "Interruptor simple Bticino", CategoriaId = 2, PrecioCompra = 6.50m, PrecioVenta = 9.50m, StockActual = 60, StockMinimo = 15, Activo = true },
            new Producto { Id = 5, Codigo = "PLO-001", Nombre = "Tubo PVC 3/4\" x 3m", CategoriaId = 3, PrecioCompra = 12.00m, PrecioVenta = 17.00m, StockActual = 80, StockMinimo = 20, Activo = true },
            new Producto { Id = 6, Codigo = "PIN-001", Nombre = "Pintura latex blanca 4L", CategoriaId = 4, PrecioCompra = 35.00m, PrecioVenta = 48.00m, StockActual = 3, StockMinimo = 5, Activo = true }, // crítico
            new Producto { Id = 7, Codigo = "FIJ-001", Nombre = "Tornillo autorroscante 1\"", CategoriaId = 5, PrecioCompra = 0.10m, PrecioVenta = 0.20m, StockActual = 500, StockMinimo = 100, Activo = true }
        );

        // Proveedores de ejemplo (RUC ficticios, solo para pruebas)
        builder.Entity<Proveedor>().HasData(
            new Proveedor { Id = 1, Ruc = "20123456781", RazonSocial = "Distribuidora Ferretera del Norte S.A.C.", Contacto = "Área de ventas", Telefono = "044-200100", Email = "ventas@ferretera-norte.example", Direccion = "Av. España 123, Trujillo", Activo = true, DiasEntrega = 5, Calificacion = 4.2m },
            new Proveedor { Id = 2, Ruc = "20765432101", RazonSocial = "Importaciones Andina E.I.R.L.", Contacto = "Atención a clientes", Telefono = "01-5550200", Email = "pedidos@andina.example", Direccion = "Av. Industrial 456, Lima", Activo = true, DiasEntrega = 12, Calificacion = 3.5m }
        );

        builder.Entity<ProductoProveedor>().HasData(
            new ProductoProveedor { ProductoId = 1, ProveedorId = 1 },
            new ProductoProveedor { ProductoId = 2, ProveedorId = 1 },
            new ProductoProveedor { ProductoId = 5, ProveedorId = 1 },
            new ProductoProveedor { ProductoId = 3, ProveedorId = 2 },
            new ProductoProveedor { ProductoId = 4, ProveedorId = 2 },
            new ProductoProveedor { ProductoId = 6, ProveedorId = 2 },
            new ProductoProveedor { ProductoId = 7, ProveedorId = 2 }
        );
    }
}