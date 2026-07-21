using OtpNet;
using QRCoder;

namespace JLTecnico.Auth.Services;

public class TotpService
{
    // Genera un secreto único y aleatorio para un usuario nuevo
    public string GenerarSecreto()
    {
        var bytes = KeyGeneration.GenerateRandomKey(20);
        return Base32Encoding.ToString(bytes);
    }

    // Construye la URI otpauth:// que va dentro del QR
    // Esta es la que Google Authenticator / Microsoft Authenticator entienden
    public string ConstruirOtpAuthUri(string secreto, string correoUsuario, string emisor = "JLTecnicoEIRL")
    {
        string secretoEscapado = Uri.EscapeDataString(secreto);
        string correoEscapado = Uri.EscapeDataString(correoUsuario);
        string emisorEscapado = Uri.EscapeDataString(emisor);

        return $"otpauth://totp/{emisorEscapado}:{correoEscapado}?secret={secretoEscapado}&issuer={emisorEscapado}&digits=6&period=30";
    }

    // Genera el QR como imagen PNG en base64, listo para <img src="data:image/png;base64,..." />
    public string GenerarQrBase64(string otpAuthUri)
    {
        using var qrGenerator = new QRCodeGenerator();
        using var qrCodeData = qrGenerator.CreateQrCode(otpAuthUri, QRCodeGenerator.ECCLevel.Q);
        var qrCode = new PngByteQRCode(qrCodeData);
        byte[] qrBytes = qrCode.GetGraphic(10);
        return Convert.ToBase64String(qrBytes);
    }

    // Valida el código de 6 dígitos que escribió el usuario contra su secreto guardado
    // VerificationWindow permite +/- 1 intervalo de 30s para tolerar pequeños desfaces de reloj
    public bool ValidarCodigo(string secreto, string codigoIngresado)
    {
        if (string.IsNullOrWhiteSpace(codigoIngresado) || string.IsNullOrWhiteSpace(secreto))
            return false;

        var bytes = Base32Encoding.ToBytes(secreto);
        var totp = new Totp(bytes, step: 30, mode: OtpHashMode.Sha1, totpSize: 6);

        return totp.VerifyTotp(
            codigoIngresado.Trim(),
            out _,
            new VerificationWindow(previous: 1, future: 1)
        );
    }
}
