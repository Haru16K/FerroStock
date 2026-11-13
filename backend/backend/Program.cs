using System.Security.Claims;
using System.Text;
using backend.Data;
using backend.Repositories;
using backend.Services;
using backend.Services.Ia;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// ── Base de datos ─────────────────────────────────────────────────────────────
builder.Services.AddDbContext<FerroStockDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// ── Repositorios (inyección de dependencias) ──────────────────────────────────
builder.Services.AddScoped<IProductoRepository, ProductoRepository>();
builder.Services.AddScoped<IMovimientoRepository, MovimientoRepository>();
builder.Services.AddScoped<IUsuarioRepository, UsuarioRepository>();
builder.Services.AddScoped<IProveedorRepository, ProveedorRepository>();
builder.Services.AddScoped<IOrdenCompraRepository, OrdenCompraRepository>();
builder.Services.AddScoped<IVentaRepository, VentaRepository>();
builder.Services.AddScoped<IEmailSender, EmailSender>();
builder.Services.AddScoped<PrediccionService>();
builder.Services.AddScoped<DatosDemoService>();
builder.Services.AddScoped<ConfiguracionIaService>();
builder.Services.AddScoped<RecomendacionService>();
builder.Services.AddHostedService<RecomendacionesSemanalesService>();
builder.Services.AddHostedService<ModeloMensualService>();

// ── JWT ───────────────────────────────────────────────────────────────────────
var jwtKey = builder.Configuration["Jwt:Key"]!;
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };

        // En cada petición se consulta el usuario en la BD, de modo que:
        //  - una cuenta desactivada pierde el acceso de inmediato, aunque conserve un token vigente
        //  - un cambio de rol rige de inmediato (se usa el rol de la BD, no el del token)
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async ctx =>
            {
                var idClaim = ctx.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
                if (!int.TryParse(idClaim, out var userId))
                {
                    ctx.Fail("Token inválido");
                    return;
                }

                var db = ctx.HttpContext.RequestServices.GetRequiredService<FerroStockDbContext>();
                var usuario = await db.Usuarios
                    .AsNoTracking()
                    .Where(u => u.Id == userId)
                    .Select(u => new { u.Activo, u.Rol, u.SesionesValidasDesde })
                    .FirstOrDefaultAsync();

                if (usuario is null || !usuario.Activo)
                {
                    ctx.Fail("Usuario inactivo");
                    return;
                }

                // Sesión anterior a un cambio de contraseña: ya no es válida.
                // Un token sin hora de emisión (anterior a esta mejora) también se descarta.
                if (usuario.SesionesValidasDesde is DateTime desde)
                {
                    long.TryParse(ctx.Principal?.FindFirstValue("iat"), out var emitido);
                    var limite = new DateTimeOffset(DateTime.SpecifyKind(desde, DateTimeKind.Utc)).ToUnixTimeSeconds();
                    if (emitido < limite)
                    {
                        ctx.Fail("Sesión cerrada por cambio de contraseña");
                        return;
                    }
                }

                if (ctx.Principal?.Identity is ClaimsIdentity identity)
                {
                    foreach (var claim in identity.FindAll(ClaimTypes.Role).ToList())
                        identity.RemoveClaim(claim);
                    identity.AddClaim(new Claim(ClaimTypes.Role, usuario.Rol.ToString()));
                }
            }
        };
    });

builder.Services.AddAuthorization();

// ── CORS para Angular ─────────────────────────────────────────────────────────
builder.Services.AddCors(options =>
{
    options.AddPolicy("AngularPolicy", policy =>
    {
        policy.WithOrigins(
                "http://localhost:4200",
                "https://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// ── Swagger con botón Authorize ───────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "FerroStock API",
        Version = "v1",
        Description = "Sistema de gestión de inventario — FerroTech S.A.C."
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Ingresa el token JWT. Ejemplo: Bearer eyJhbGci..."
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id   = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddControllers();

var app = builder.Build();

// ── Aplicar migraciones y seed automáticamente al iniciar ─────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<FerroStockDbContext>();
    db.Database.Migrate();
}

// ── Pipeline ──────────────────────────────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "FerroStock API v1"));
}

app.UseHttpsRedirection();
app.UseCors("AngularPolicy");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();


app.Run();