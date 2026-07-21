using JLTecnico.Auth.Models;
using Microsoft.EntityFrameworkCore;

namespace JLTecnico.Auth.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<DispositivoConocido> DispositivosConocidos => Set<DispositivoConocido>();
    public DbSet<Sesion> Sesiones => Set<Sesion>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Permiso> Permisos => Set<Permiso>();
    public DbSet<RolPermiso> RolPermisos => Set<RolPermiso>();
    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<Producto> Productos => Set<Producto>();
    public DbSet<Categoria> Categorias => Set<Categoria>();
    public DbSet<Venta> Ventas => Set<Venta>();
    public DbSet<VentaDetalle> VentaDetalles => Set<VentaDetalle>();
    public DbSet<Cotizacion> Cotizaciones => Set<Cotizacion>();
    public DbSet<CotizacionDetalle> CotizacionDetalles => Set<CotizacionDetalle>();
    public DbSet<Proveedor> Proveedores => Set<Proveedor>();
    public DbSet<Compra> Compras => Set<Compra>();
    public DbSet<CompraDetalle> CompraDetalles => Set<CompraDetalle>();
    public DbSet<PerfilTecnico> PerfilesTecnicos => Set<PerfilTecnico>();
    public DbSet<OrdenServicio> OrdenesServicio => Set<OrdenServicio>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Usuario>()
            .HasIndex(u => u.Correo)
            .IsUnique();

        modelBuilder.Entity<Sesion>()
            .HasIndex(s => s.TokenId)
            .IsUnique();

        modelBuilder.Entity<Permiso>()
            .HasIndex(p => p.Clave)
            .IsUnique();

        modelBuilder.Entity<RolPermiso>()
            .HasIndex(rp => new { rp.Rol, rp.PermisoId })
            .IsUnique();

        modelBuilder.Entity<Cliente>()
            .HasIndex(c => c.NumeroDocumento)
            .IsUnique();

        modelBuilder.Entity<Proveedor>()
            .HasIndex(p => p.Ruc)
            .IsUnique();

        modelBuilder.Entity<PerfilTecnico>()
            .HasIndex(p => p.UsuarioId)
            .IsUnique();

        base.OnModelCreating(modelBuilder);
    }
}