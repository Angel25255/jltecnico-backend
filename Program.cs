using System.Text;
using JLTecnico.Auth.Data;
using JLTecnico.Auth.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

// PostgreSQL es más estricto que SQL Server con las fechas: exige
// que el DateTime tenga explícitamente Kind=Utc. Muchas fechas que
// llegan del navegador (filtros "desde/hasta", fecha programada,
// etc.) llegan sin esa marca. Este interruptor restaura el
// comportamiento más flexible, evitando tener que tocar cada
// controller uno por uno bajo presión de tiempo.
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<JwtService>();
builder.Services.AddScoped<TotpService>();
builder.Services.AddScoped<EmailService>();
builder.Services.AddScoped<AuditoriaService>();
builder.Services.AddScoped<PermisosService>();
builder.Services.AddHttpClient<ConsultaDocumentoService>();
builder.Services.AddHttpClient<ConsultaTelefonoService>();
builder.Services.AddScoped<BoletaPdfService>();
builder.Services.AddScoped<CotizacionPdfService>();
builder.Services.AddScoped<ReportePdfService>();
builder.Services.AddScoped<ReporteExcelService>();

var jwtKey = builder.Configuration["Jwt:Key"]!;

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidateAudience = true,
        ValidAudience = builder.Configuration["Jwt:Audience"],
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromSeconds(30)
    };
});

builder.Services.AddAuthorization();

builder.Services.AddCors(options =>
{
    options.AddPolicy("PoliticaFrontend", policy =>
    {
        policy.WithOrigins(
                "http://localhost:5173",
                "http://localhost:5174",
                "http://localhost:3000",
                "https://jltecnico-frontend.vercel.app"
            )
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Description = "Escribe: Bearer {tu token}",
        Name = "Authorization",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey
    });
    options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] {}
        }
    });
});

var app = builder.Build();

// -----------------------------------------------------------
// Siembra automática del catálogo de Permisos (y los valores por
// defecto de Vendedor/Técnico). A diferencia de antes, ahora
// revisa PERMISO POR PERMISO (no la tabla completa) - así, si ya
// tenías permisos configurados en producción, esto solo AGREGA
// los que falten, sin tocar ni pisar los que ya existen.
// -----------------------------------------------------------
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    var catalogoCompleto = new List<(string Clave, string Modulo, string Descripcion, bool DefaultVendedor, bool DefaultTecnico)>
    {
        ("CLIENTES_VER", "Clientes", "Ver el listado de clientes", true, false),
        ("CLIENTES_GESTIONAR", "Clientes", "Crear y editar clientes", true, false),
        ("PRODUCTOS_VER", "Productos", "Ver el catálogo de productos", true, false),
        ("PRODUCTOS_GESTIONAR", "Productos", "Crear y editar productos", true, false),
        ("CATEGORIAS_VER", "Categorias", "Ver las categorías de productos", true, false),
        ("CATEGORIAS_GESTIONAR", "Categorias", "Crear y editar categorías", true, false),
        ("VENTAS_VER", "Ventas", "Ver el historial de ventas", true, false),
        ("VENTAS_CREAR", "Ventas", "Registrar nuevas ventas", true, false),
        ("COTIZACIONES_VER", "Cotizaciones", "Ver cotizaciones", true, false),
        ("COTIZACIONES_CREAR", "Cotizaciones", "Generar nuevas cotizaciones", true, false),
        ("REPORTES_VER", "Reportes", "Ver reportes comerciales", true, false),
        ("PROVEEDORES_VER", "Proveedores", "Ver el listado de proveedores", false, true),
        ("PROVEEDORES_GESTIONAR", "Proveedores", "Crear y editar proveedores", false, false),
        ("COMPRAS_VER", "Compras", "Ver el historial de compras", false, true),
        ("COMPRAS_GESTIONAR", "Compras", "Registrar nuevas compras", false, false),
        ("KARDEX_VER", "Kardex", "Ver el kardex e historial de movimientos", false, true),
        ("TECNICOS_VER", "GestionTecnicos", "Ver la gestión de técnicos", false, true),
        ("OS_VER", "OrdenesServicio", "Ver las órdenes de servicio", false, true),
        ("OS_GESTIONAR", "OrdenesServicio", "Crear y asignar órdenes de servicio", false, false),
        ("OS_ACTUALIZAR_CAMPO", "OrdenesServicio", "Actualizar el avance desde campo (técnico)", false, true),
        // Se dejan por compatibilidad con instalaciones viejas, aunque ya
        // no se usan directamente (Productos/Categorías/Proveedores/Compras/
        // Kardex/Técnicos ahora tienen su propio permiso separado arriba).
        ("INVENTARIO_VER", "Inventario", "Ver el stock del inventario (heredado)", false, false),
        ("INVENTARIO_GESTIONAR", "Inventario", "Registrar entradas, salidas y ajustes (heredado)", false, false),
    };

    var clavesExistentes = db.Permisos.Select(p => p.Clave).ToHashSet();
    var permisosNuevos = new List<JLTecnico.Auth.Models.Permiso>();

    foreach (var (clave, modulo, descripcion, _, _) in catalogoCompleto)
    {
        if (!clavesExistentes.Contains(clave))
        {
            var nuevo = new JLTecnico.Auth.Models.Permiso { Clave = clave, Modulo = modulo, Descripcion = descripcion };
            db.Permisos.Add(nuevo);
            permisosNuevos.Add(nuevo);
        }
    }

    if (permisosNuevos.Any())
    {
        db.SaveChanges(); // para que los permisos nuevos ya tengan Id

        foreach (var permiso in permisosNuevos)
        {
            var infoCatalogo = catalogoCompleto.First(c => c.Clave == permiso.Clave);

            db.RolPermisos.Add(new JLTecnico.Auth.Models.RolPermiso
            {
                Rol = "Vendedor",
                PermisoId = permiso.Id,
                Permitido = infoCatalogo.DefaultVendedor
            });
            db.RolPermisos.Add(new JLTecnico.Auth.Models.RolPermiso
            {
                Rol = "Tecnico",
                PermisoId = permiso.Id,
                Permitido = infoCatalogo.DefaultTecnico
            });
        }

        db.SaveChanges();
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("PoliticaFrontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();