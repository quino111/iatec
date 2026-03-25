// ============================================================
// EventsController.cs  —  agenda2
// Todos los endpoints que el frontend (app.js) consume:
//
//   GET  /api/events/my
//   GET  /api/events/dashboard
//   GET  /api/events/filter?date=&search=
//   GET  /api/events/public
//   GET  /api/events/{id}
//   POST /api/events
//   PUT  /api/events/{id}
//   DEL  /api/events/{id}
//   POST /api/events/{id}/subscribe
//   POST /api/events/{id}/send
// ============================================================
using System.Security.Claims;
using agenda2.DTOs;
using agenda2.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace agenda2.Controllers;

[ApiController]
[Route("api/events")]
[Authorize]
[Produces("application/json")]
public class EventsController : ControllerBase
{
    private readonly IEventService _events;

    public EventsController(IEventService events) => _events = events;

    /// <summary>ID del usuario del token JWT</summary>
    private int CurrentUserId =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");

    // ── GET /api/events/my ────────────────────────────────
    /// <summary>Todos los eventos de la agenda del usuario autenticado</summary>
    [HttpGet("my")]
    [ProducesResponseType(typeof(List<EventDto>), 200)]
    public async Task<IActionResult> GetMyEvents()
    {
        try
        {
            var list = await _events.GetMyEventsAsync(CurrentUserId);
            return Ok(list);
        }
        catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
    }

    // ── GET /api/events/dashboard ─────────────────────────
    /// <summary>Estadísticas + eventos en curso + próximos</summary>
    [HttpGet("dashboard")]
    [ProducesResponseType(typeof(DashboardResponse), 200)]
    public async Task<IActionResult> GetDashboard()
    {
        try
        {
            var data = await _events.GetDashboardAsync(CurrentUserId);
            return Ok(data);
        }
        catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
    }

    // ── GET /api/events/filter ────────────────────────────
    /// <summary>
    /// Filtra eventos por fecha y/o texto.
    /// Sin hora: todos los eventos del día.
    /// Con hora: ventana de ±30 minutos.
    /// </summary>
    [HttpGet("filter")]
    [ProducesResponseType(typeof(List<EventDto>), 200)]
    public async Task<IActionResult> Filter(
        [FromQuery] DateTime? date,
        [FromQuery] string? search)
    {
        try
        {
            var list = await _events.GetFilteredAsync(CurrentUserId, date, search);
            return Ok(list);
        }
        catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
    }

    // ── GET /api/events/public ────────────────────────────
    /// <summary>Eventos compartidos de otros usuarios disponibles para suscribirse</summary>
    [HttpGet("public")]
    [ProducesResponseType(typeof(List<EventDto>), 200)]
    public async Task<IActionResult> GetPublicEvents()
    {
        try
        {
            var list = await _events.GetPublicEventsAsync(CurrentUserId);
            return Ok(list);
        }
        catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
    }

    // ── GET /api/events/{id} ──────────────────────────────
    /// <summary>Evento por ID (solo si está en la agenda del usuario)</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(EventDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetById(int id)
    {
        try
        {
            var ev = await _events.GetByIdAsync(id, CurrentUserId);
            return Ok(ev);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
    }

    // ── POST /api/events ──────────────────────────────────
    /// <summary>
    /// Crea un nuevo evento.
    /// Regla: eventos Exclusivos no pueden coincidir en el mismo día.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(EventDto), 201)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> Create([FromBody] CreateEventRequest req)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        try
        {
            var ev = await _events.CreateAsync(req, CurrentUserId);
            return CreatedAtAction(nameof(GetById), new { id = ev.Id }, ev);
        }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
        catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
    }

    // ── PUT /api/events/{id} ──────────────────────────────
    /// <summary>Actualiza un evento. Solo el propietario puede editar.</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(EventDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateEventRequest req)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        try
        {
            var ev = await _events.UpdateAsync(id, req, CurrentUserId);
            return Ok(ev);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
        catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
    }

    // ── DELETE /api/events/{id} ───────────────────────────
    /// <summary>Elimina (soft delete) un evento. Solo el propietario.</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            await _events.DeleteAsync(id, CurrentUserId);
            return Ok(new ApiResponse(true, "Evento eliminado exitosamente"));
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
    }

    // ── POST /api/events/{id}/subscribe ───────────────────
    /// <summary>Agrega un evento compartido a la agenda del usuario actual</summary>
    [HttpPost("{id:int}/subscribe")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Subscribe(int id)
    {
        try
        {
            await _events.SubscribeAsync(id, CurrentUserId);
            return Ok(new ApiResponse(true, "Evento agregado a tu agenda"));
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
        catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
    }

    // ── POST /api/events/{id}/send ────────────────────────
    /// <summary>Envía un evento compartido a las agendas de otros usuarios</summary>
    [HttpPost("{id:int}/send")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> SendToUsers(int id, [FromBody] SendEventRequest req)
    {
        if (req?.UserIds == null || !req.UserIds.Any())
            return BadRequest(new { message = "Selecciona al menos un usuario" });

        try
        {
            await _events.SendToUsersAsync(id, req.UserIds, CurrentUserId);
            return Ok(new ApiResponse(true, $"Evento enviado a {req.UserIds.Count} usuario(s)"));
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
        catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
    }
}
