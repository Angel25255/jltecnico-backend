namespace JLTecnico.Auth.DTOs;

public class LoginRequest
{
    public string Correo { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class LoginResponse
{
    public bool Requiere2FA { get; set; }
    public string? PreAuthToken { get; set; }
    public string? Mensaje { get; set; }
}

public class Verificar2FARequest
{
    public string PreAuthToken { get; set; } = string.Empty;
    public string Codigo { get; set; } = string.Empty;
}

public class Verificar2FAResponse
{
    public string Token { get; set; } = string.Empty;
    public string NombreCompleto { get; set; } = string.Empty;
    public string Rol { get; set; } = string.Empty;
    public bool DispositivoNuevo { get; set; }
}

public class SesionActivaResponse
{
    public int Id { get; set; }
    public string IP { get; set; } = string.Empty;
    public string? UserAgent { get; set; }
    public DateTime FechaInicio { get; set; }
    public DateTime FechaUltimoUso { get; set; }
    public bool EsSesionActual { get; set; }
}

public class CrearUsuarioRequest
{
    public string NombreCompleto { get; set; } = string.Empty;
    public string Correo { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Rol { get; set; } = "Vendedor";
    public string? Direccion { get; set; }
}

public class CrearUsuarioResponse
{
    public int UsuarioId { get; set; }
    public string QrBase64 { get; set; } = string.Empty;
    public string OtpAuthUri { get; set; } = string.Empty;
    public string Mensaje { get; set; } = "Escanea este QR con Google Authenticator o Microsoft Authenticator. Este código no se volverá a mostrar.";
}

public class CambiarEstadoUsuarioRequest
{
    public bool Activo { get; set; }
}

public class UsuarioListItem
{
    public int Id { get; set; }
    public string NombreCompleto { get; set; } = string.Empty;
    public string Correo { get; set; } = string.Empty;
    public string Rol { get; set; } = string.Empty;
    public bool Activo { get; set; }
    public string? Direccion { get; set; }
    public DateTime FechaCreacion { get; set; }
}

public class RestablecerPasswordRequest
{
    public string NuevaPassword { get; set; } = string.Empty;
}
public class EditarUsuarioRequest
{
    public string NombreCompleto { get; set; } = string.Empty;
    public string Correo { get; set; } = string.Empty;
    public string Rol { get; set; } = string.Empty;
    public string? Direccion { get; set; }
}
