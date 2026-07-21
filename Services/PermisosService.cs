using JLTecnico.Auth.Data;
using Microsoft.EntityFrameworkCore;

namespace JLTecnico.Auth.Services;

public class PermisosService
{
    private readonly AppDbContext _db;

    public PermisosService(AppDbContext db)
    {
        _db = db;
    }

    // El Administrador siempre tiene TODOS los permisos, sin excepción.
    // Para Vendedor/Tecnico se consulta la tabla RolPermisos.
    public async Task<bool> TienePermiso(string rol, string claveP)
    {
        if (rol == "Administrador") return true;

        var permiso = await _db.Permisos.FirstOrDefaultAsync(p => p.Clave == claveP);
        if (permiso == null) return false;

        var rolPermiso = await _db.RolPermisos
            .FirstOrDefaultAsync(rp => rp.Rol == rol && rp.PermisoId == permiso.Id);

        return rolPermiso?.Permitido ?? false;
    }

    // Lista de claves de permisos habilitados para un rol (para mandar al frontend tras el login)
    public async Task<List<string>> ObtenerClavesPermitidas(string rol)
    {
        if (rol == "Administrador")
        {
            return await _db.Permisos.Select(p => p.Clave).ToListAsync();
        }

        return await _db.RolPermisos
            .Where(rp => rp.Rol == rol && rp.Permitido)
            .Include(rp => rp.Permiso)
            .Select(rp => rp.Permiso!.Clave)
            .ToListAsync();
    }
}