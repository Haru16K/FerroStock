using System.Text;
using backend.Data;
using backend.Repositories;
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