// ============================================================
// UsersController.cs  —  agenda2
// GET  /api/users        → lista (sin el usuario actual)
// GET  /api/users/me     → perfil
// PUT  /api/users/me     → actualizar nombre
// GET  /api/users/{id}   → perfil público
// ============================================================
using System.Security.Claims;
using agenda2.Data;
using agenda2.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace agenda2.Controllers;

[ApiController]
[Route("api/users")]
[Authorize]
[Produces("application/json")]
public class UsersController : ControllerBase
{
    private readonly AgendaproDbContext _db;

    public UsersController(AgendaproDbContext db) => _db = db;

    private int CurrentUserId =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");

    // ── GET /api/users ────────────────────────────────────
    /// <summary>Lista todos los usuarios activos (excepto el autenticado)</summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<UserDto>), 200)]
    public async Task<IActionResult> GetAll()
    {
        var myId = CurrentUserId;
        var list = await _db.Users
            .Where(u => u.IsActive == true && u.Id != myId)
            .OrderBy(u => u.Name)
            .Select(u => new UserDto { Id = u.Id, Name = u.Name, Email = u.Email })
            .ToListAsync();

        return Ok(list);
    }

    // ── GET /api/users/me ─────────────────────────────────
    /// <summary>Perfil del usuario autenticado</summary>
    [HttpGet("me")]
    [ProducesResponseType(typeof(UserDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetMe()
    {
        var user = await _db.Users.FindAsync(CurrentUserId);
        if (user == null) return NotFound(new { message = "Usuario no encontrado" });

        return Ok(new UserDto { Id = user.Id, Name = user.Name, Email = user.Email });
    }

    // ── PUT /api/users/me ─────────────────────────────────
    /// <summary>Actualiza el nombre del usuario autenticado</summary>
    [HttpPut("me")]
    [ProducesResponseType(typeof(UserDto), 200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> UpdateMe([FromBody] UpdateProfileRequest req)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var user = await _db.Users.FindAsync(CurrentUserId);
        if (user == null) return NotFound(new { message = "Usuario no encontrado" });

        user.Name = req.Name.Trim();
        await _db.SaveChangesAsync();

        return Ok(new UserDto { Id = user.Id, Name = user.Name, Email = user.Email });
    }

    // ── GET /api/users/{id} ───────────────────────────────
    /// <summary>Perfil público de un usuario por ID</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(UserDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetById(int id)
    {
        var user = await _db.Users
            .Where(u => u.Id == id && u.IsActive == true)
            .Select(u => new UserDto { Id = u.Id, Name = u.Name, Email = u.Email })
            .FirstOrDefaultAsync();

        if (user == null)
            return NotFound(new { message = $"Usuario {id} no encontrado" });

        return Ok(user);
    }
}
