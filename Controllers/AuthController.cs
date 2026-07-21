using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using JLTecnico.Auth.Data;
using JLTecnico.Auth.DTOs;
using JLTecnico.Auth.Models;
using JLTecnico.Auth.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace JLTecnico.Auth.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly JwtService _jwt;
    private readonly TotpService _totp;
    private readonly EmailService _email;
    private readonly AuditoriaService _auditoria;

    private const int MAX_INTENTOS_FALLIDOS = 5;
    private const int MINUTOS_BLOQUEO = 15;

    public AuthController(AppDbContext db, JwtService jwt, TotpService totp,
        EmailService email, AuditoriaService auditoria)
    {
        _db = db;
        _jwt = jwt;
        _totp = totp;
        _email = email;
        _auditoria = auditoria;
    }

    // -----------------------------------------------------------
    // PASO 1: correo + contraseña
    // -----------------------------------------------------------
    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request)
    {
        string ip = ObtenerIp();
        string? userAgent = Request.Headers.UserAgent.ToString();

        var usuario = await _db.Usuarios.FirstOrDefaultAsync(u => u.Correo == request.Correo);

        if (usuario == null)
        {
            await _auditoria.Registrar("LOGIN_FALLIDO", null, request.Correo, ip, userAgent,
                "Correo no existe");
            return Unauthorized(new { mensaje = "Correo o contraseña incorrectos." });
        }

        // Usuario desactivado por el administrador
        if (!usuario.Activo)
        {
            await _auditoria.Registrar("USUARIO_DESACTIVADO_INTENTO", usuario.Id, usuario.Correo, ip, userAgent,
                "Intento de login de cuenta desactivada");
            return StatusCode(403, new { mensaje = "Tu cuenta está desactivada. Contacta al administrador del sistema." });
        }

        // Bloqueo temporal por intentos fallidos
        if (usuario.BloqueadoHasta.HasValue && usuario.BloqueadoHasta > DateTime.UtcNow)
        {
            var minutosRestantes = Math.Ceiling((usuario.BloqueadoHasta.Value - DateTime.UtcNow).TotalMinutes);
            await _auditoria.Registrar("LOGIN_BLOQUEADO", usuario.Id, usuario.Correo, ip, userAgent,
                $"Cuenta bloqueada, quedan {minutosRestantes} min");
            return StatusCode(429, new { mensaje = $"Cuenta bloqueada temporalmente. Intenta de nuevo en {minutosRestantes} minutos." });
        }

        // Validar contraseña
        bool passwordValido = BCrypt.Net.BCrypt.Verify(request.Password, usuario.PasswordHash);
        if (!passwordValido)
        {
            usuario.IntentosFallidos++;
            if (usuario.IntentosFallidos >= MAX_INTENTOS_FALLIDOS)
            {
                usuario.BloqueadoHasta = DateTime.UtcNow.AddMinutes(MINUTOS_BLOQUEO);
                await _auditoria.Registrar("CUENTA_BLOQUEADA", usuario.Id, usuario.Correo, ip, userAgent,
                    $"{MAX_INTENTOS_FALLIDOS} intentos fallidos seguidos");
            }
            else
            {
                await _auditoria.Registrar("LOGIN_FALLIDO", usuario.Id, usuario.Correo, ip, userAgent,
                    "Contraseña incorrecta");
            }
            await _db.SaveChangesAsync();
            return Unauthorized(new { mensaje = "Correo o contraseña incorrectos." });
        }

        // Contraseña correcta: reiniciar contador de intentos fallidos
        usuario.IntentosFallidos = 0;
        usuario.BloqueadoHasta = null;
        await _db.SaveChangesAsync();

        if (!usuario.TotpConfigurado)
        {
            // Caso raro: el admin no completó la configuración de 2FA al crear la cuenta
            return BadRequest(new { mensaje = "Tu cuenta no tiene configurado el doble factor. Contacta al administrador." });
        }

        // Contraseña OK -> pedir el segundo factor (TOTP)
        string preAuthToken = _jwt.GenerarPreAuthToken(usuario);

        return Ok(new LoginResponse
        {
            Requiere2FA = true,
            PreAuthToken = preAuthToken,
            Mensaje = "Ingresa el código de tu app de autenticación."
        });
    }

    // -----------------------------------------------------------
    // PASO 2: código TOTP de 6 dígitos
    // -----------------------------------------------------------
    [HttpPost("verify-2fa")]
    public async Task<ActionResult<Verificar2FAResponse>> Verificar2FA(Verificar2FARequest request)
    {
        string ip = ObtenerIp();
        string? userAgent = Request.Headers.UserAgent.ToString();

        int? userId = _jwt.ValidarPreAuthTokenYObtenerUserId(request.PreAuthToken);
        if (userId == null)
        {
            return Unauthorized(new { mensaje = "Sesión de login expirada. Vuelve a ingresar tu correo y contraseña." });
        }

        var usuario = await _db.Usuarios.FindAsync(userId.Value);
        if (usuario == null || !usuario.Activo)
        {
            return Unauthorized(new { mensaje = "No se pudo completar el inicio de sesión." });
        }

        bool codigoValido = _totp.ValidarCodigo(usuario.TotpSecret!, request.Codigo);
        if (!codigoValido)
        {
            await _auditoria.Registrar("2FA_FALLIDO", usuario.Id, usuario.Correo, ip, userAgent,
                "Código TOTP incorrecto");
            return Unauthorized(new { mensaje = "Código incorrecto. Verifica tu app de autenticación." });
        }

        // ----- Los dos factores están validados: generar acceso -----
        var (token, jti) = _jwt.GenerarAccessToken(usuario);

        // Registrar la sesión
        _db.Sesiones.Add(new Sesion
        {
            UsuarioId = usuario.Id,
            TokenId = jti,
            IP = ip,
            UserAgent = userAgent,
            Activa = true
        });

        // Detectar si es un dispositivo/IP nuevo para este usuario
        string dispositivoHash = CalcularHashDispositivo(ip, userAgent);
        var dispositivoConocido = await _db.DispositivosConocidos
            .FirstOrDefaultAsync(d => d.UsuarioId == usuario.Id && d.DispositivoHash == dispositivoHash);

        bool esDispositivoNuevo = dispositivoConocido == null;

        if (esDispositivoNuevo)
        {
            _db.DispositivosConocidos.Add(new DispositivoConocido
            {
                UsuarioId = usuario.Id,
                DispositivoHash = dispositivoHash,
                IP = ip,
                UserAgent = userAgent
            });

            await _auditoria.Registrar("DISPOSITIVO_NUEVO", usuario.Id, usuario.Correo, ip, userAgent,
                "Primer login desde este dispositivo/IP");

            // Enviar alerta por correo (no bloquea el login si falla el envío)
            _ = _email.EnviarAlertaDispositivoNuevo(usuario.Correo, usuario.NombreCompleto, ip, userAgent ?? "desconocido");
        }
        else
        {
            dispositivoConocido!.FechaUltimoUso = DateTime.UtcNow;
        }

        await _auditoria.Registrar("LOGIN_OK", usuario.Id, usuario.Correo, ip, userAgent, null);
        await _db.SaveChangesAsync();

        return Ok(new Verificar2FAResponse
        {
            Token = token,
            NombreCompleto = usuario.NombreCompleto,
            Rol = usuario.Rol,
            DispositivoNuevo = esDispositivoNuevo
        });
    }

    // -----------------------------------------------------------
    // Sesiones activas del usuario logueado
    // -----------------------------------------------------------
    [Authorize]
    [HttpGet("sesiones")]
    public async Task<ActionResult<List<SesionActivaResponse>>> ObtenerSesiones()
    {
        int userId = ObtenerUserIdDelToken();
        string jtiActual = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Jti)?.Value ?? "";

        var sesiones = await _db.Sesiones
            .Where(s => s.UsuarioId == userId && s.Activa)
            .OrderByDescending(s => s.FechaUltimoUso)
            .Select(s => new SesionActivaResponse
            {
                Id = s.Id,
                IP = s.IP,
                UserAgent = s.UserAgent,
                FechaInicio = s.FechaInicio,
                FechaUltimoUso = s.FechaUltimoUso,
                EsSesionActual = s.TokenId == jtiActual
            })
            .ToListAsync();

        return Ok(sesiones);
    }

    // -----------------------------------------------------------
    // Cerrar una sesión remota (o la propia)
    // -----------------------------------------------------------
    [Authorize]
    [HttpPost("sesiones/{id}/cerrar")]
    public async Task<IActionResult> CerrarSesion(int id)
    {
        int userId = ObtenerUserIdDelToken();
        string ip = ObtenerIp();

        var sesion = await _db.Sesiones.FirstOrDefaultAsync(s => s.Id == id && s.UsuarioId == userId);
        if (sesion == null) return NotFound();

        sesion.Activa = false;
        await _auditoria.Registrar("SESION_CERRADA", userId, null, ip, null, $"Sesion Id {id}");
        await _db.SaveChangesAsync();

        return Ok(new { mensaje = "Sesión cerrada correctamente." });
    }

    // -----------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------
    private string ObtenerIp()
    {
        return HttpContext.Connection.RemoteIpAddress?.ToString() ?? "desconocida";
    }

    private int ObtenerUserIdDelToken()
    {
        return int.Parse(User.FindFirst("userId")!.Value);
    }

    private string CalcularHashDispositivo(string ip, string? userAgent)
    {
        using var sha256 = SHA256.Create();
        byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes($"{ip}|{userAgent}"));
        return Convert.ToBase64String(bytes);
    }
}
