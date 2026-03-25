// ============================================================
// DTOs.cs  —  agenda2
// ============================================================
using System.ComponentModel.DataAnnotations;

namespace agenda2.DTOs;

// ── AUTH ─────────────────────────────────────────────────

public class LoginRequest
{
    [Required(ErrorMessage = "El email es requerido")]
    [EmailAddress(ErrorMessage = "Email inválido")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "La contraseña es requerida")]
    public string Password { get; set; } = string.Empty;
}

public class RegisterRequest
{
    [Required]
    [MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [MaxLength(200)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MinLength(6)]
    public string Password { get; set; } = string.Empty;
}

public class AuthResponse
{
    public string Token { get; set; } = string.Empty;
    public UserDto User { get; set; } = new();
}

// ── USERS ─────────────────────────────────────────────────

public class UserDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

public class UpdateProfileRequest
{
    [Required]
    [MaxLength(150)]
    public string Name { get; set; } = string.Empty;
}

// ── EVENTS ────────────────────────────────────────────────

public class CreateEventRequest
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Required(ErrorMessage = "La fecha de inicio es requerida")]
    public DateTime Date { get; set; }

    [Required(ErrorMessage = "La fecha de fin es requerida")]
    public DateTime EndDate { get; set; }

    [MaxLength(300)]
    public string? Location { get; set; }

    [Required]
    public string Type { get; set; } = "Shared";

    public List<string> Participants { get; set; } = new();
}

public class UpdateEventRequest
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Required(ErrorMessage = "La fecha de inicio es requerida")]
    public DateTime Date { get; set; }

    [Required(ErrorMessage = "La fecha de fin es requerida")]
    public DateTime EndDate { get; set; }

    [MaxLength(300)]
    public string? Location { get; set; }

    [Required]
    public string Type { get; set; } = "Shared";

    public List<string> Participants { get; set; } = new();
}

public class EventDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime Date { get; set; }   // inicio
    public DateTime EndDate { get; set; }   // fin
    public string? Location { get; set; }
    public string Type { get; set; } = string.Empty;
    public int OwnerId { get; set; }
    public string OwnerName { get; set; } = string.Empty;
    public List<string> Participants { get; set; } = new();
    public bool IsOwner { get; set; }
    public DateTime CreatedAt { get; set; }
}

// ── DASHBOARD ─────────────────────────────────────────────

public class DashboardResponse
{
    public DashboardStats Stats { get; set; } = new();
    public List<EventDto> Ongoing { get; set; } = new();
    public List<EventDto> Upcoming { get; set; } = new();
}

public class DashboardStats
{
    public int Total { get; set; }
    public int Ongoing { get; set; }
    public int Upcoming { get; set; }
    public int Exclusive { get; set; }
}

public class SendEventRequest
{
    [Required]
    public List<int> UserIds { get; set; } = new();
}

public class ApiResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;

    public ApiResponse(bool success, string message)
    {
        Success = success;
        Message = message;
    }
}
