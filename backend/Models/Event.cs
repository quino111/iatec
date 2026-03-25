using System;
using System.Collections.Generic;

namespace agenda2.Models;

public partial class Event
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    /// <summary>Fecha/hora de inicio del evento</summary>
    public DateTime Date { get; set; }

    /// <summary>Fecha/hora de fin del evento</summary>
    public DateTime EndDate { get; set; }

    public string? Location { get; set; }

    /// <summary>false = Exclusivo | true = Compartido</summary>
    public bool Type { get; set; }

    public int OwnerId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public bool IsDeleted { get; set; }

    public virtual ICollection<Eventparticipant> Eventparticipants { get; set; } = new List<Eventparticipant>();

    public virtual User Owner { get; set; } = null!;

    public virtual ICollection<Userevent> Userevents { get; set; } = new List<Userevent>();
}
