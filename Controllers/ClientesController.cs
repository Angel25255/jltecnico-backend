using System.Security.Claims;
using JLTecnico.Auth.Data;
using JLTecnico.Auth.DTOs;
using JLTecnico.Auth.Models;
using JLTecnico.Auth.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace JLTecnico.Auth.Controllers;

[ApiController]
[Route("api/clientes")]
[Authorize]
public class ClientesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ConsultaDocumentoService _consultaDocumento;
    private readonly PermisosService _permisosService;
    private readonly AuditoriaService _auditoria;

    public ClientesController(AppDbContext db, ConsultaDocumentoService consultaDocumento,
        PermisosService permisosService, AuditoriaService auditoria)
    {
        _db = db;
        _consultaDocumento = consultaDocumento;
        _permisosService = permisosService;
        _auditoria = auditoria;
    }

    private string RolActual() => User.FindFirst(ClaimTypes.Role)?.Value ?? "";

    // -----------------------------------------------------------
    // Consulta DNI o RUC (proxy, el token nunca llega al navegador)
    // y devuelve el nombre/razón social listos para autocompletar
    // el formulario.
    // GET /api/clientes/consultar-documento/DNI/12345678
    // GET /api/clientes/consultar-documento/RUC/20123456789
    // -----------------------------------------------------------
    [HttpGet("consultar-documento/{tipo}/{numero}")]
    public async Task<ActionResult<ConsultaDocumentoResponse>> ConsultarDocumento(string tipo, string numero)
    {
        if (!await _permisosService.TienePermiso(RolActual(), "CLIENTES_VER") &&
            !await _permisosService.TienePermiso(RolActual(), "CLIENTES_GESTIONAR"))
        {
            return Forbid();
        }

        tipo = tipo.ToUpper();

        if (tipo == "DNI")
        {
            if (numero.Length != 8 || !numero.All(char.IsDigit))
                return BadRequest(new ConsultaDocumentoResponse { Encontrado = false, Mensaje = "El DNI debe tener 8 dígitos." });

            return Ok(await _consultaDocumento.ConsultarDni(numero));
        }

        if (tipo == "RUC")
        {
            if (numero.Length != 11 || !numero.All(char.IsDigit))
                return BadRequest(new ConsultaDocumentoResponse { Encontrado = false, Mensaje = "El RUC debe tener 11 dígitos." });

            return Ok(await _consultaDocumento.ConsultarRuc(numero));
        }

        return BadRequest(new ConsultaDocumentoResponse { Encontrado = false, Mensaje = "Tipo de documento inválido." });
    }

    // -----------------------------------------------------------
    // Listado de clientes (con búsqueda simple opcional)
    // -----------------------------------------------------------
    [HttpGet]
    public async Task<ActionResult<List<ClienteItem>>> Listar([FromQuery] string? busqueda)
    {
        if (!await _permisosService.TienePermiso(RolActual(), "CLIENTES_VER"))
            return Forbid();

        var query = _db.Clientes.AsQueryable();

        if (!string.IsNullOrWhiteSpace(busqueda))
        {
            query = query.Where(c =>
                c.NombreORazonSocial.Contains(busqueda) ||
                c.NumeroDocumento.Contains(busqueda));
        }

        var clientes = await query
            .OrderByDescending(c => c.FechaCreacion)
            .Select(c => new ClienteItem
            {
                Id = c.Id,
                TipoDocumento = c.TipoDocumento,
                NumeroDocumento = c.NumeroDocumento,
                NombreORazonSocial = c.NombreORazonSocial,
                Telefono = c.Telefono,
                Correo = c.Correo,
                Direccion = c.Direccion,
                Activo = c.Activo,
                FechaCreacion = c.FechaCreacion
            })
            .ToListAsync();

        return Ok(clientes);
    }

    // -----------------------------------------------------------
    // Crear cliente
    // -----------------------------------------------------------
    [HttpPost]
    public async Task<ActionResult<ClienteItem>> Crear(CrearClienteRequest request)
    {
        if (!await _permisosService.TienePermiso(RolActual(), "CLIENTES_GESTIONAR"))
            return Forbid();

        bool existe = await _db.Clientes.AnyAsync(c => c.NumeroDocumento == request.NumeroDocumento);
        if (existe)
            return BadRequest(new { mensaje = "Ya existe un cliente con ese número de documento." });

        int? usuarioId = int.TryParse(User.FindFirst("userId")?.Value, out var uid) ? uid : null;

        var cliente = new Cliente
        {
            TipoDocumento = request.TipoDocumento,
            NumeroDocumento = request.NumeroDocumento,
            NombreORazonSocial = request.NombreORazonSocial,
            Telefono = request.Telefono,
            Correo = request.Correo,
            Direccion = request.Direccion,
            CreadoPorUsuarioId = usuarioId
        };

        _db.Clientes.Add(cliente);

        await _auditoria.Registrar("CLIENTE_CREADO", usuarioId, null,
            HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", Request.Headers.UserAgent,
            $"{request.TipoDocumento} {request.NumeroDocumento} - {request.NombreORazonSocial}");

        await _db.SaveChangesAsync();

        return Ok(new ClienteItem
        {
            Id = cliente.Id,
            TipoDocumento = cliente.TipoDocumento,
            NumeroDocumento = cliente.NumeroDocumento,
            NombreORazonSocial = cliente.NombreORazonSocial,
            Telefono = cliente.Telefono,
            Correo = cliente.Correo,
            Direccion = cliente.Direccion,
            Activo = cliente.Activo,
            FechaCreacion = cliente.FechaCreacion
        });
    }

    // -----------------------------------------------------------
    // Editar cliente (no se permite cambiar tipo/número de documento)
    // -----------------------------------------------------------
    [HttpPut("{id}")]
    public async Task<IActionResult> Editar(int id, EditarClienteRequest request)
    {
        if (!await _permisosService.TienePermiso(RolActual(), "CLIENTES_GESTIONAR"))
            return Forbid();

        var cliente = await _db.Clientes.FindAsync(id);
        if (cliente == null) return NotFound();

        cliente.NombreORazonSocial = request.NombreORazonSocial;
        cliente.Telefono = request.Telefono;
        cliente.Correo = request.Correo;
        cliente.Direccion = request.Direccion;

        await _db.SaveChangesAsync();

        return Ok(new { mensaje = "Cliente actualizado." });
    }

    // -----------------------------------------------------------
    // Activar / desactivar cliente
    // -----------------------------------------------------------
    [HttpPatch("{id}/estado")]
    public async Task<IActionResult> CambiarEstado(int id, [FromBody] bool activo)
    {
        if (!await _permisosService.TienePermiso(RolActual(), "CLIENTES_GESTIONAR"))
            return Forbid();

        var cliente = await _db.Clientes.FindAsync(id);
        if (cliente == null) return NotFound();

        cliente.Activo = activo;
        await _db.SaveChangesAsync();

        return Ok(new { mensaje = activo ? "Cliente activado." : "Cliente desactivado." });
    }
}