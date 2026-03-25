// ============================================================
// EventService.cs  —  agenda2
// CAMBIOS respecto a versión anterior:
//   - Event ahora tiene Date (inicio) y EndDate (fin)
//   - isOngoing: now >= Date && now <= EndDate
//   - overlap exclusivos: rangos que se intersectan
//   - filter por fecha: cubre eventos que ocurren en ese momento/día
//   - ToDto: mapea EndDate
// ============================================================
using agenda2.Data;
using agenda2.DTOs;
using agenda2.Models;
using Microsoft.EntityFrameworkCore;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace agenda2.Services;

public interface IEventService
{
    Task<List<EventDto>> GetMyEventsAsync(int userId);
    Task<EventDto> GetByIdAsync(int eventId, int userId);
    Task<EventDto> CreateAsync(CreateEventRequest req, int userId);
    Task<EventDto> UpdateAsync(int eventId, UpdateEventRequest req, int userId);
    Task DeleteAsync(int eventId, int userId);
    Task<List<EventDto>> GetFilteredAsync(int userId, DateTime? date, string? search);
    Task<DashboardResponse> GetDashboardAsync(int userId);
    Task<List<EventDto>> GetPublicEventsAsync(int userId);
    Task SubscribeAsync(int eventId, int userId);
    Task SendToUsersAsync(int eventId, List<int> targetIds, int requesterId);
}

public class EventService : IEventService
{
    private readonly AgendaproDbContext _db;

    public EventService(AgendaproDbContext db) => _db = db;

    // ─────────────────────────────────────────────────────
    // GET MY EVENTS
    // ─────────────────────────────────────────────────────
    public async Task<List<EventDto>> GetMyEventsAsync(int userId)
    {
        var rows = await _db.Userevents
            .Where(ue => ue.UserId == userId && !ue.Event.IsDeleted)
            .Include(ue => ue.Event).ThenInclude(e => e.Owner)
            .Include(ue => ue.Event).ThenInclude(e => e.Eventparticipants)
            .OrderBy(ue => ue.Event.Date)
            .ToListAsync();

        return rows.Select(ue => ToDto(ue.Event, ue.IsOwner)).ToList();
    }

    // ─────────────────────────────────────────────────────
    // GET BY ID
    // ─────────────────────────────────────────────────────
    public async Task<EventDto> GetByIdAsync(int eventId, int userId)
    {
        var ue = await _db.Userevents
            .Where(x => x.EventId == eventId && x.UserId == userId)
            .Include(x => x.Event).ThenInclude(e => e.Owner)
            .Include(x => x.Event).ThenInclude(e => e.Eventparticipants)
            .FirstOrDefaultAsync()
            ?? throw new KeyNotFoundException("Evento no encontrado en tu agenda.");

        return ToDto(ue.Event, ue.IsOwner);
    }

    // ─────────────────────────────────────────────────────
    // CREATE
    // ─────────────────────────────────────────────────────
    public async Task<EventDto> CreateAsync(CreateEventRequest req, int userId)
    {
        // Validar que EndDate > Date
        if (req.EndDate <= req.Date)
            throw new InvalidOperationException(
                "La fecha de fin debe ser posterior a la fecha de inicio.");

        var isExclusive = ParseType(req.Type);

        // Eventos exclusivos: verificar que no se superpongan con otro exclusivo
        if (isExclusive == false)
            await CheckExclusiveOverlapAsync(userId, req.Date, req.EndDate, excludeId: null);

        var ev = new Event
        {
            Name = req.Name.Trim(),
            Description = req.Description?.Trim(),
            //Date = req.Date.ToUniversalTime(),
            //EndDate = req.EndDate.ToUniversalTime(),
            Date = req.Date,
            EndDate = req.EndDate,
            Location = req.Location?.Trim(),
            Type = isExclusive,
            OwnerId = userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IsDeleted = false
        };

        _db.Events.Add(ev);
        await _db.SaveChangesAsync();

        AddParticipants(ev.Id, req.Participants);

        _db.Userevents.Add(new Userevent
        {
            UserId = userId,
            EventId = ev.Id,
            IsOwner = true,
            AddedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
        return await GetByIdAsync(ev.Id, userId);
    }

    // ─────────────────────────────────────────────────────
    // UPDATE
    // ─────────────────────────────────────────────────────
    public async Task<EventDto> UpdateAsync(int eventId, UpdateEventRequest req, int userId)
    {
        if (req.EndDate <= req.Date)
            throw new InvalidOperationException(
                "La fecha de fin debe ser posterior a la fecha de inicio.");

        var ev = await _db.Events
            .Include(e => e.Eventparticipants)
            .FirstOrDefaultAsync(e => e.Id == eventId && e.OwnerId == userId && !e.IsDeleted)
            ?? throw new KeyNotFoundException("Evento no encontrado o sin permiso.");

        var isExclusive = ParseType(req.Type);

        if (isExclusive == false)
            await CheckExclusiveOverlapAsync(userId, req.Date, req.EndDate, excludeId: eventId);

        ev.Name = req.Name.Trim();
        ev.Description = req.Description?.Trim();
        //ev.Date = req.Date.ToUniversalTime();
        //ev.EndDate = req.EndDate.ToUniversalTime();
        ev.Date = req.Date;
        ev.EndDate = req.EndDate;
        ev.Location = req.Location?.Trim();
        ev.Type = isExclusive;
        ev.UpdatedAt = DateTime.UtcNow;

        _db.Eventparticipants.RemoveRange(ev.Eventparticipants);
        AddParticipants(ev.Id, req.Participants);

        await _db.SaveChangesAsync();
        return await GetByIdAsync(ev.Id, userId);
    }

    // ─────────────────────────────────────────────────────
    // DELETE (soft)
    // ─────────────────────────────────────────────────────
    public async Task DeleteAsync(int eventId, int userId)
    {
        var ev = await _db.Events
            .FirstOrDefaultAsync(e => e.Id == eventId && e.OwnerId == userId && !e.IsDeleted)
            ?? throw new KeyNotFoundException("Evento no encontrado o sin permiso.");

        ev.IsDeleted = true;
        ev.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    // ─────────────────────────────────────────────────────
    // FILTER
    // El requisito dice: "al indicar una fecha (con o sin hora),
    // mostrar todos los eventos que OCURREN en ese día/momento".
    // Con inicio+fin real, un evento "ocurre" en un instante/día
    // si su rango [Date, EndDate] se solapa con ese instante/día.
    // ─────────────────────────────────────────────────────
    public async Task<List<EventDto>> GetFilteredAsync(int userId, DateTime? date, string? search)
    {
        var query = _db.Userevents
            .Where(ue => ue.UserId == userId && !ue.Event.IsDeleted)
            .Include(ue => ue.Event).ThenInclude(e => e.Owner)
            .Include(ue => ue.Event).ThenInclude(e => e.Eventparticipants)
            .AsQueryable();

        if (date.HasValue)
        {
            //var d = date.Value.ToUniversalTime();
            var d = date.Value;
            //var hasTime = d.TimeOfDay.TotalMinutes > 0;
            var hasTime = date.Value.TimeOfDay != TimeSpan.Zero;

            if (hasTime)
            {
                // Instante exacto: el evento debe estar activo en ese momento
                // Condición: Date <= d && EndDate >= d
                query = query.Where(ue =>
                    ue.Event.Date <= d &&
                    ue.Event.EndDate >= d && !ue.Event.IsDeleted);
            }
            else
            {
                // Día completo: el evento debe tocarse con ese día
                // Condición: Date < fin_dia && EndDate > inicio_dia
                var dayStart = d.Date;
                var dayEnd = dayStart.AddDays(1);
                query = query.Where(ue =>
                    ue.Event.Date < dayEnd &&
                    ue.Event.EndDate > dayStart && !ue.Event.IsDeleted);
            }
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var t = search.Trim().ToLower();
            query = query.Where(ue =>
                ue.Event.Name.ToLower().Contains(t) ||
                (ue.Event.Description != null && ue.Event.Description.ToLower().Contains(t)) ||
                (ue.Event.Location != null && ue.Event.Location.ToLower().Contains(t)) && !ue.Event.IsDeleted);
        }

        var rows = await query.OrderBy(ue => ue.Event.Date).ToListAsync();
        return rows.Select(ue => ToDto(ue.Event, ue.IsOwner)).ToList();
    }

    // ─────────────────────────────────────────────────────
    // DASHBOARD
    // "Eventos en curso": now >= Date && now <= EndDate
    // "Próximos": Date > now
    // ─────────────────────────────────────────────────────
    public async Task<DashboardResponse> GetDashboardAsync(int userId)
    {
        //var now = DateTime.UtcNow;
        var now = DateTime.Now;

        var all = await _db.Userevents
            .Where(ue => ue.UserId == userId && !ue.Event.IsDeleted)
            .Include(ue => ue.Event).ThenInclude(e => e.Owner)
            .Include(ue => ue.Event).ThenInclude(e => e.Eventparticipants)
            .ToListAsync();

        // En curso: el evento está activo ahora mismo
        var ongoing = all
            .Where(ue => ue.Event.Date <= now && ue.Event.EndDate >= now && !ue.Event.IsDeleted)
            .OrderBy(ue => ue.Event.Date)
            .Take(6)
            .Select(ue => ToDto(ue.Event, ue.IsOwner))
            .ToList();

        // Próximos: aún no han empezado
        var upcoming = all
            .Where(ue => ue.Event.Date > now && !ue.Event.IsDeleted)
            .OrderBy(ue => ue.Event.Date)
            .Take(8)
            .Select(ue => ToDto(ue.Event, ue.IsOwner))
            .ToList();

        var stats = new DashboardStats
        {
            Total = all.Count,
            Ongoing = ongoing.Count,
            Upcoming = upcoming.Count,
            Exclusive = all.Count(ue => ue.Event.Type == false)
        };

        return new DashboardResponse { Stats = stats, Ongoing = ongoing, Upcoming = upcoming };
    }

    // ─────────────────────────────────────────────────────
    // PUBLIC EVENTS
    // ─────────────────────────────────────────────────────
    public async Task<List<EventDto>> GetPublicEventsAsync(int userId)
    {
        var myIds = await _db.Userevents
            .Where(ue => ue.UserId == userId && !ue.Event.IsDeleted)
            .Select(ue => ue.EventId)
            .ToListAsync();

        var events = await _db.Events
            .Where(e => e.Type == true && !e.IsDeleted && !myIds.Contains(e.Id))
            .Include(e => e.Owner)
            .Include(e => e.Eventparticipants)
            .OrderBy(e => e.Date)
            .ToListAsync();

        return events.Select(e => ToDto(e, false)).ToList();
    }

    // ─────────────────────────────────────────────────────
    // SUBSCRIBE
    // ─────────────────────────────────────────────────────
    public async Task SubscribeAsync(int eventId, int userId)
    {
        var ev = await _db.Events.FindAsync(eventId)
            ?? throw new KeyNotFoundException("Evento no encontrado.");

        if (ev.Type == false)
            throw new InvalidOperationException("Solo puedes agregar eventos de tipo Compartido.");

        if (await _db.Userevents.AnyAsync(ue => ue.UserId == userId && ue.EventId == eventId))
            throw new InvalidOperationException("El evento ya está en tu agenda.");

        _db.Userevents.Add(new Userevent
        {
            UserId = userId,
            EventId = eventId,
            IsOwner = false,
            AddedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
    }

    // ─────────────────────────────────────────────────────
    // SEND TO USERS
    // ─────────────────────────────────────────────────────
    public async Task SendToUsersAsync(int eventId, List<int> targetIds, int requesterId)
    {
        var ev = await _db.Events
            .FirstOrDefaultAsync(e => e.Id == eventId && e.OwnerId == requesterId && !e.IsDeleted)
            ?? throw new KeyNotFoundException("Evento no encontrado o sin permiso.");

        if (ev.Type == false)
            throw new InvalidOperationException("Solo puedes enviar eventos de tipo Compartido.");

        if (!targetIds.Any())
            throw new ArgumentException("Selecciona al menos un usuario.");

        var alreadyHave = await _db.Userevents
            .Where(ue => ue.EventId == eventId && targetIds.Contains(ue.UserId) && !ue.Event.IsDeleted)
            .Select(ue => ue.UserId)
            .ToListAsync();

        var toAdd = targetIds.Distinct().Where(id => !alreadyHave.Contains(id)).ToList();

        if (!toAdd.Any())
            throw new InvalidOperationException("Todos los usuarios ya tienen este evento.");

        foreach (var uid in toAdd)
        {
            _db.Userevents.Add(new Userevent
            {
                UserId = uid,
                EventId = eventId,
                IsOwner = false,
                AddedAt = DateTime.UtcNow
            });
        }

        await _db.SaveChangesAsync();
    }

    // ─────────────────────────────────────────────────────
    // PRIVADOS
    // ─────────────────────────────────────────────────────

    /// <summary>
    /// Verifica que un evento EXCLUSIVO no se solape con otro exclusivo del usuario.
    /// Dos rangos se solapan si: inicioA &lt; finB && finA &gt; inicioB
    /// </summary>
    private async Task CheckExclusiveOverlapAsync(
        int userId, DateTime start, DateTime end, int? excludeId)
    {
        //var startUtc = start.ToUniversalTime();
        //var endUtc = end.ToUniversalTime();
        var startUtc = start;
        var endUtc = end;

        var conflict = await _db.Userevents
            .Where(ue =>
                ue.UserId == userId &&
                ue.Event.Type == false &&
                !ue.Event.IsDeleted &&
                ue.Event.Date < endUtc &&
                ue.Event.EndDate > startUtc &&
                (excludeId == null || ue.EventId != excludeId))
            .Select(ue => new
            {
                ue.Event.Name,
                ue.Event.Date,
                ue.Event.EndDate
            })
            .FirstOrDefaultAsync();
        //var conflict = await _db.Userevents
        //    .Where(ue =>
        //        ue.UserId == userId &&
        //        ue.Event.Type == false &&   // false = Exclusivo
        //        !ue.Event.IsDeleted &&
        //        ue.Event.Date < endUtc &&   // inicio del existente < fin del nuevo
        //        ue.Event.EndDate > startUtc &&   // fin del existente > inicio del nuevo
        //        (excludeId == null || ue.EventId != excludeId))
        //    .Select(ue => ue.Event.Name)
        //    .FirstOrDefaultAsync();

        if (conflict != null)
            throw new InvalidOperationException(
               $"Ya tienes un evento exclusivo \"{conflict.Name}\" " +
               $"de {conflict.Date:HH:mm} a {conflict.EndDate:HH:mm}. " +
               $"Ese horario ya está ocupado.");
        //$"El horario se superpone con el evento exclusivo \"{conflict}\". " +
        //"Los eventos exclusivos no pueden coincidir en el mismo rango de tiempo.");
    }

    private void AddParticipants(int eventId, List<string> participants)
    {
        foreach (var name in participants
                     .Where(p => !string.IsNullOrWhiteSpace(p))
                     .Select(p => p.Trim())
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            _db.Eventparticipants.Add(new Eventparticipant
            {
                EventId = eventId,
                ParticipantName = name
            });
        }
    }

    /// <summary>
    /// false = Exclusivo  |  true = Compartido
    /// </summary>
    private static bool ParseType(string? type) =>
        (type ?? "").ToLowerInvariant() switch
        {
            "shared" => true,
            "1" => true,
            _ => false
        };

    /// <summary>
    /// Mapea Event → EventDto.
    /// false = Exclusivo → "Exclusive"
    /// true  = Compartido → "Shared"
    /// </summary>
    private static EventDto ToDto(Event ev, bool isOwner) => new()
    {
        Id = ev.Id,
        Name = ev.Name,
        Description = ev.Description,
        Date = ev.Date,
        EndDate = ev.EndDate,
        Location = ev.Location,
        Type = ev.Type ? "Shared" : "Exclusive",
        OwnerId = ev.OwnerId,
        OwnerName = ev.Owner?.Name ?? string.Empty,
        Participants = ev.Eventparticipants?
                         .Select(p => p.ParticipantName)
                         .ToList() ?? new List<string>(),
        IsOwner = isOwner,
        CreatedAt = ev.CreatedAt
    };
}
