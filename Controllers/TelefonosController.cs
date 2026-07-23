using JLTecnico.Auth.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JLTecnico.Auth.Controllers;

[ApiController]
[Route("api/telefonos")]
[Authorize] // cualquier usuario logueado puede usarlo, no hace falta un permiso especial
public class TelefonosController : ControllerBase
{
    private readonly ConsultaTelefonoService _consultaTelefono;

    public TelefonosController(ConsultaTelefonoService consultaTelefono)
    {
        _consultaTelefono = consultaTelefono;
    }

    // GET /api/telefonos/validar/987654321
    [HttpGet("validar/{numero}")]
    public async Task<ActionResult<TelefonoValidoResultado>> Validar(string numero)
    {
        if (string.IsNullOrWhiteSpace(numero) || numero.Length < 7)
            return BadRequest(new { mensaje = "Escribe un número de teléfono válido." });

        var resultado = await _consultaTelefono.Validar(numero);
        return Ok(resultado);
    }
}