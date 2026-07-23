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
// defecto de Vendedor/Técnico) - SOLO si la tabla está vacía.
// Así funciona igual la primera vez sin importar si la base de
// datos es SQL Server o PostgreSQL, sin depender de scripts SQL
// escritos a mano para un motor específico.
// -----------------------------------------------------------
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    if (!db.Permisos.Any())
    {
        var permisos = new List<JLTecnico.Auth.Models.Permiso>
        {
            new() { Clave = "CLIENTES_VER", Modulo = "Clientes", Descripcion = "Ver el listado de clientes" },
            new() { Clave = "CLIENTES_GESTIONAR", Modulo = "Clientes", Descripcion = "Crear y editar clientes" },
            new() { Clave = "VENTAS_VER", Modulo = "Ventas", Descripcion = "Ver el historial de ventas" },
            new() { Clave = "VENTAS_CREAR", Modulo = "Ventas", Descripcion = "Registrar nuevas ventas" },
            new() { Clave = "COTIZACIONES_VER", Modulo = "Cotizaciones", Descripcion = "Ver cotizaciones" },
            new() { Clave = "COTIZACIONES_CREAR", Modulo = "Cotizaciones", Descripcion = "Generar nuevas cotizaciones" },
            new() { Clave = "REPORTES_VER", Modulo = "Reportes", Descripcion = "Ver reportes comerciales" },
            new() { Clave = "INVENTARIO_VER", Modulo = "Inventario", Descripcion = "Ver el stock del inventario" },
            new() { Clave = "INVENTARIO_GESTIONAR", Modulo = "Inventario", Descripcion = "Registrar entradas, salidas y ajustes" },
            new() { Clave = "OS_VER", Modulo = "OrdenesServicio", Descripcion = "Ver las órdenes de servicio" },
            new() { Clave = "OS_GESTIONAR", Modulo = "OrdenesServicio", Descripcion = "Crear y asignar órdenes de servicio" },
            new() { Clave = "OS_ACTUALIZAR_CAMPO", Modulo = "OrdenesServicio", Descripcion = "Actualizar el avance desde campo (técnico)" },
        };

        db.Permisos.AddRange(permisos);
        db.SaveChanges();

        string[] clavesVendedor = { "CLIENTES_VER", "CLIENTES_GESTIONAR", "VENTAS_VER", "VENTAS_CREAR", "COTIZACIONES_VER", "COTIZACIONES_CREAR", "REPORTES_VER" };
        string[] clavesTecnico = { "OS_VER", "OS_ACTUALIZAR_CAMPO", "INVENTARIO_VER" };

        foreach (var permiso in permisos)
        {
            db.RolPermisos.Add(new JLTecnico.Auth.Models.RolPermiso
            {
                Rol = "Vendedor",
                PermisoId = permiso.Id,
                Permitido = clavesVendedor.Contains(permiso.Clave)
            });
            db.RolPermisos.Add(new JLTecnico.Auth.Models.RolPermiso
            {
                Rol = "Tecnico",
                PermisoId = permiso.Id,
                Permitido = clavesTecnico.Contains(permiso.Clave)
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