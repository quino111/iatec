using System;
using System.Collections.Generic;

namespace agenda2.Models;

public partial class Userevent
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public int EventId { get; set; }

    public bool IsOwner { get; set; }

    public DateTime AddedAt { get; set; }

    public virtual Event Event { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
