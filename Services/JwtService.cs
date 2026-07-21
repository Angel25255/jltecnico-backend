using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using JLTecnico.Auth.Models;
using Microsoft.IdentityModel.Tokens;

namespace JLTecnico.Auth.Services;

public class JwtService
{
    private readonly IConfiguration _config;

    public JwtService(IConfiguration config)
    {
        _config = config;
    }

    // Token temporal (5 min) que se entrega después de validar correo+password,
    // pero ANTES de validar el código TOTP. Solo sirve para el endpoint /verify-2fa.
    public string GenerarPreAuthToken(Usuario usuario)
    {
        var claims = new[]
        {
            new Claim("userId", usuario.Id.ToString()),
            new Claim("tipo", "pre2fa")
        };

        return GenerarToken(claims, TimeSpan.FromMinutes(
            _config.GetValue<int>("Jwt:PreAuthExpiraMinutos", 5)));
    }

    // Token final de acceso, ya con los dos factores validados
    public (string token, string jti) GenerarAccessToken(Usuario usuario)
    {
        string jti = Guid.NewGuid().ToString();

        var claims = new[]
        {
            new Claim("userId", usuario.Id.ToString()),
            new Claim(ClaimTypes.Name, usuario.NombreCompleto),
            new Claim(ClaimTypes.Email, usuario.Correo),
            new Claim(ClaimTypes.Role, usuario.Rol),
            new Claim("tipo", "acceso_completo"),
            new Claim(JwtRegisteredClaimNames.Jti, jti)
        };

        string token = GenerarToken(claims, TimeSpan.FromMinutes(
            _config.GetValue<int>("Jwt:ExpiraMinutos", 120)));

        return (token, jti);
    }

    private string GenerarToken(Claim[] claims, TimeSpan duracion)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.Add(duracion),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    // Lee el userId de un pre-auth token y confirma que sea de tipo "pre2fa"
    public int? ValidarPreAuthTokenYObtenerUserId(string token)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler();
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));

            var principal = handler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = _config["Jwt:Issuer"],
                ValidateAudience = true,
                ValidAudience = _config["Jwt:Audience"],
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = key,
                ValidateLifetime = true
            }, out _);

            string? tipo = principal.FindFirst("tipo")?.Value;
            if (tipo != "pre2fa") return null;

            string? userId = principal.FindFirst("userId")?.Value;
            return userId != null ? int.Parse(userId) : null;
        }
        catch
        {
            return null; // token vencido, inválido o manipulado
        }
    }
}
