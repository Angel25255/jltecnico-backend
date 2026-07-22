using JLTecnico.Auth.Data;
using JLTecnico.Auth.DTOs;
using JLTecnico.Auth.Models;
using JLTecnico.Auth.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace JLTecnico.Auth.Controllers;

[ApiController]
[Route("api/usuarios")]
//[Authorize(Roles = "Administrador")]
public class UsuariosController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly TotpService _totp;
    private readonly AuditoriaService _auditoria;

    public UsuariosController(AppDbContext db, TotpService totp, AuditoriaService auditoria)
    {
        _db = db;
        _totp = totp;
        _auditoria = auditoria;
    }

    // -----------------------------------------------------------
    // Crear usuario nuevo (solo Administrador).
    // Genera el secreto TOTP y devuelve el QR UNA SOLA VEZ.
    // -----------------------------------------------------------
    [HttpPost]
    public async Task<ActionResult<CrearUsuarioResponse>> CrearUsuario(CrearUsuarioRequest request)
    {
        bool existe = await _db.Usuarios.AnyAsync(u => u.Correo == request.Correo);
        if (existe)
            return BadRequest(new { mensaje = "Ya existe un usuario con ese correo." });

        string secreto = _totp.GenerarSecreto();

        var usuario = new Usuario
        {
            NombreCompleto = request.NombreCompleto,
            Correo = request.Correo,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Rol = request.Rol,
            Direccion = request.Direccion,
            Activo = true,
            TotpSecret = secreto,
            TotpConfigurado = true
        };

        _db.Usuarios.Add(usuario);
        await _auditoria.Registrar("USUARIO_CREADO", null, request.Correo,
            HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", Request.Headers.UserAgent,
            $"Creado por administrador, rol {request.Rol}");
        await _db.SaveChangesAsync();

        string otpAuthUri = _totp.ConstruirOtpAuthUri(secreto, usuario.Correo);
        string qrBase64 = _totp.GenerarQrBase64(otpAuthUri);

        return Ok(new CrearUsuarioResponse
        {
            UsuarioId = usuario.Id,
            QrBase64 = qrBase64,
            OtpAuthUri = otpAuthUri
        });
    }

    // -----------------------------------------------------------
    // Editar datos de un usuario (nombre, correo, rol, dirección)
    // -----------------------------------------------------------
    [HttpPut("{id}")]
    public async Task<IActionResult> Editar(int id, EditarUsuarioRequest request)
    {
        var usuario = await _db.Usuarios.FindAsync(id);
        if (usuario == null) return NotFound();

        bool correoEnUso = await _db.Usuarios.AnyAsync(u => u.Correo == request.Correo && u.Id != id);
        if (correoEnUso)
            return BadRequest(new { mensaje = "Ese correo ya lo usa otro usuario." });

        usuario.NombreCompleto = request.NombreCompleto;
        usuario.Correo = request.Correo;
        usuario.Rol = request.Rol;
        usuario.Direccion = request.Direccion;
        usuario.FechaActualizacion = DateTime.UtcNow;

        await _auditoria.Registrar("USUARIO_EDITADO", usuario.Id, usuario.Correo,
            HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", Request.Headers.UserAgent, null);

        await _db.SaveChangesAsync();
        return Ok(new { mensaje = "Usuario actualizado." });
    }

    // -----------------------------------------------------------
    // Activar / desactivar usuario
    // -----------------------------------------------------------
    [HttpPatch("{id}/estado")]
    public async Task<IActionResult> CambiarEstado(int id, CambiarEstadoUsuarioRequest request)
    {
        var usuario = await _db.Usuarios.FindAsync(id);
        if (usuario == null) return NotFound();

        usuario.Activo = request.Activo;
        usuario.FechaActualizacion = DateTime.UtcNow;

        // Si lo desactivan, cerramos todas sus sesiones activas de inmediato
        if (!request.Activo)
        {
            var sesiones = await _db.Sesiones.Where(s => s.UsuarioId == id && s.Activa).ToListAsync();
            foreach (var s in sesiones) s.Activa = false;
        }

        await _auditoria.Registrar(
            request.Activo ? "USUARIO_ACTIVADO" : "USUARIO_DESACTIVADO",
            usuario.Id, usuario.Correo,
            HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", Request.Headers.UserAgent, null);

        await _db.SaveChangesAsync();

        return Ok(new { mensaje = request.Activo ? "Usuario activado." : "Usuario desactivado." });
    }

    // -----------------------------------------------------------
    // Restablecer contraseña: el Administrador le pone una
    // contraseña NUEVA al usuario (no puede ver ni recuperar la
    // vieja, eso es técnicamente imposible - solo puede reemplazarla).
    // De paso, desbloquea la cuenta si estaba bloqueada por
    // intentos fallidos.
    // -----------------------------------------------------------
    [HttpPatch("{id}/restablecer-password")]
    public async Task<IActionResult> RestablecerPassword(int id, RestablecerPasswordRequest request)
    {
        var usuario = await _db.Usuarios.FindAsync(id);
        if (usuario == null) return NotFound();

        usuario.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NuevaPassword);
        usuario.IntentosFallidos = 0;
        usuario.BloqueadoHasta = null;
        usuario.FechaActualizacion = DateTime.UtcNow;

        await _auditoria.Registrar("PASSWORD_RESTABLECIDA_POR_ADMIN", usuario.Id, usuario.Correo,
            HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", Request.Headers.UserAgent, null);

        await _db.SaveChangesAsync();
        return Ok(new { mensaje = "Contraseña restablecida. Ya puede ingresar con la nueva." });
    }

    // -----------------------------------------------------------
    // Regenerar el código 2FA: para cuando la persona perdió o
    // cambió de celular y ya no tiene el Authenticator vinculado.
    // Genera un secreto NUEVO y un QR nuevo para volver a escanear
    // (el anterior deja de servir automáticamente).
    // -----------------------------------------------------------
    [HttpPost("{id}/regenerar-2fa")]
    public async Task<ActionResult<CrearUsuarioResponse>> Regenerar2FA(int id)
    {
        var usuario = await _db.Usuarios.FindAsync(id);
        if (usuario == null) return NotFound();

        string nuevoSecreto = _totp.GenerarSecreto();
        usuario.TotpSecret = nuevoSecreto;
        usuario.TotpConfigurado = true;
        usuario.FechaActualizacion = DateTime.UtcNow;

        await _auditoria.Registrar("2FA_REGENERADO_POR_ADMIN", usuario.Id, usuario.Correo,
            HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", Request.Headers.UserAgent, null);

        await _db.SaveChangesAsync();

        string otpAuthUri = _totp.ConstruirOtpAuthUri(nuevoSecreto, usuario.Correo);
        string qrBase64 = _totp.GenerarQrBase64(otpAuthUri);

        return Ok(new CrearUsuarioResponse
        {
            UsuarioId = usuario.Id,
            QrBase64 = qrBase64,
            OtpAuthUri = otpAuthUri,
            Mensaje = "Código 2FA regenerado. El código anterior ya no funciona - escanea este nuevo QR ahora mismo."
        });
    }

    // -----------------------------------------------------------
    // Listado de usuarios (para la pantalla de administración)
    // -----------------------------------------------------------
    [HttpGet]
    public async Task<ActionResult<List<UsuarioListItem>>> Listar()
    {
        var usuarios = await _db.Usuarios
            .Select(u => new UsuarioListItem
            {
                Id = u.Id,
                NombreCompleto = u.NombreCompleto,
                Correo = u.Correo,
                Rol = u.Rol,
                Activo = u.Activo,
                Direccion = u.Direccion,
                FechaCreacion = u.FechaCreacion
            })
            .ToListAsync();

        return Ok(usuarios);
    }
}