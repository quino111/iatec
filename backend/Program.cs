// ============================================================
// Program.cs  —  agenda2
// .NET 8 WebAPI  ·  MariaDB/XAMPP  ·  JWT  ·  Swagger
// ============================================================
using System.Text;
using agenda2.Data;
using agenda2.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// ── CONTROLLERS ───────────────────────────────────────────
builder.Services.AddControllers()
    .AddJsonOptions(opt =>
        opt.JsonSerializerOptions.PropertyNamingPolicy =
            System.Text.Json.JsonNamingPolicy.CamelCase);

builder.Services.AddEndpointsApiExplorer();

// ── SWAGGER con soporte JWT ───────────────────────────────
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Agenda Pro API",
        Version = "v1",
        Description = "API REST para gestión de agenda — IATec\n\n" +
                      "**Pasos para probar:**\n" +
                      "1. POST /api/auth/login con admin@iatec.com / Admin123!\n" +
                      "2. Copiar el token de la respuesta\n" +
                      "3. Clic en Authorize → pegar el token"
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Pega tu token JWT aquí (sin escribir 'Bearer')"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {{
        new OpenApiSecurityScheme
        {
            Reference = new OpenApiReference
            {
                Type = ReferenceType.SecurityScheme,
                Id   = "Bearer"
            }
        },
        Array.Empty<string>()
    }});
});

// ── BASE DE DATOS — MariaDB via Pomelo ────────────────────
var connStr = builder.Configuration.GetConnectionString("DefaultConnection")!;

builder.Services.AddDbContext<AgendaproDbContext>(options =>
    options.UseMySql(
        connStr,
        // Versión fija de MariaDB para XAMPP 8.x (evita AutoDetect que falla sin conexión)
        new MariaDbServerVersion(new Version(10, 4, 32)),
        mysql => mysql.EnableRetryOnFailure(3)
    )
    .EnableSensitiveDataLogging(builder.Environment.IsDevelopment())
);

// ── JWT AUTHENTICATION ────────────────────────────────────
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? "AgendaPro_SecretKey_32chars_min!!";   // clave por defecto si no está en config

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "AgendaPro",
            ValidAudience = builder.Configuration["Jwt:Audience"] ?? "AgendaProApp",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew = TimeSpan.Zero
        };

        // Respuesta JSON en lugar de HTML para 401
        opt.Events = new JwtBearerEvents
        {
            OnChallenge = ctx =>
            {
                ctx.HandleResponse();
                ctx.Response.StatusCode = 401;
                ctx.Response.ContentType = "application/json";
                return ctx.Response.WriteAsync(
                    "{\"message\":\"No autorizado. Token inválido o expirado.\"}");
            }
        };
    });

builder.Services.AddAuthorization();

// ── SERVICIOS DE NEGOCIO ──────────────────────────────────
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IEventService, EventService>();

// ── CORS ──────────────────────────────────────────────────
// Permite peticiones desde el frontend en XAMPP (puerto 80),
// Live Server (5500) y cualquier localhost común.
builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendPolicy", policy =>
    {
        policy.WithOrigins(
                "http://localhost",           // XAMPP htdocs
                "https://localhost",
                "http://localhost:80",
                "http://127.0.0.1",
                "http://127.0.0.1:80",
                "http://localhost:5500",      // VS Code Live Server
                "http://127.0.0.1:5500",
                "http://localhost:3000",
                "http://localhost:4200",
                "http://localhost:8080"
            )
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });
});

// ── BUILD ─────────────────────────────────────────────────
var app = builder.Build();

// ── PIPELINE ──────────────────────────────────────────────
// Swagger disponible siempre (facilita pruebas en dev)
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Agenda Pro API v1");
    // Swagger en la raíz: http://localhost:5000/
    c.RoutePrefix = string.Empty;
});

if (app.Environment.IsDevelopment())
    app.UseDeveloperExceptionPage();

// HTTPS solo en producción — XAMPP usa HTTP
if (!app.Environment.IsDevelopment())
    app.UseHttpsRedirection();

app.UseCors("FrontendPolicy");   // ← antes de Auth
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
