// ============================================================
// AuthService.cs  —  agenda2
// Autenticación con BCrypt + JWT simple
// Namespace: agenda2.Services
// ============================================================
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using agenda2.Data;
using agenda2.DTOs;
using agenda2.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace agenda2.Services;

public interface IAuthService
{
    Task<AuthResponse> LoginAsync(LoginRequest req);
    Task<AuthResponse> RegisterAsync(RegisterRequest req);
}

public class AuthService : IAuthService
{
    private readonly AgendaproDbContext _db;
    private readonly IConfiguration _cfg;

    public AuthService(AgendaproDbContext db, IConfiguration cfg)
    {
        _db = db;
        _cfg = cfg;
    }

    // ── LOGIN ─────────────────────────────────────────────
    public async Task<AuthResponse> LoginAsync(LoginRequest req)
    {
        var email = req.Email.Trim().ToLowerInvariant();

        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Email == email && u.IsActive == true);

        if (user == null || !BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
            throw new UnauthorizedAccessException("Credenciales incorrectas.");

        return BuildResponse(user);
    }

    // ── REGISTER ──────────────────────────────────────────
    public async Task<AuthResponse> RegisterAsync(RegisterRequest req)
    {
        var email = req.Email.Trim().ToLowerInvariant();

        if (await _db.Users.AnyAsync(u => u.Email == email))
            throw new InvalidOperationException("El correo ya está registrado.");

        var user = new User
        {
            Name = req.Name.Trim(),
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password, workFactor: 11),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        return BuildResponse(user);
    }

    // ── PRIVADOS ──────────────────────────────────────────
    private AuthResponse BuildResponse(User user)
    {
        var token = GenerateJwt(user);
        return new AuthResponse
        {
            Token = token,
            User = new UserDto { Id = user.Id, Name = user.Name, Email = user.Email }
        };
    }

    private string GenerateJwt(User user)
    {
        var jwtKey = _cfg["Jwt:Key"]
            ?? "AgendaPro_SecretKey_32chars_min!!";

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name,            user.Name),
            new Claim(ClaimTypes.Email,           user.Email),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        var token = new JwtSecurityToken(
            issuer: _cfg["Jwt:Issuer"] ?? "AgendaPro",
            audience: _cfg["Jwt:Audience"] ?? "AgendaProApp",
            claims: claims,
            expires: DateTime.UtcNow.AddDays(7),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
