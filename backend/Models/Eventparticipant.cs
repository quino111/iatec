using System;
using System.Collections.Generic;

namespace agenda2.Models;

public partial class Eventparticipant
{
    public int Id { get; set; }

    public int EventId { get; set; }

    public string ParticipantName { get; set; } = null!;

    public virtual Event Event { get; set; } = null!;
}
