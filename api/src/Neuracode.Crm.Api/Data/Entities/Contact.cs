using System;
using System.Collections.Generic;

namespace Neuracode.Crm.Api.Data.Entities;

public partial class Contact
{
    public string Id { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string? Email { get; set; }

    public string? Phone { get; set; }

    public string? Company { get; set; }

    public string Source { get; set; } = null!;

    public string Temperature { get; set; } = null!;

    public int Score { get; set; }

    public string? Notes { get; set; }

    public string? WaId { get; set; }

    public bool OptedOut { get; set; }

    public bool BotHandling { get; set; }

    public int CreatedAt { get; set; }

    public int UpdatedAt { get; set; }

    public virtual ICollection<Activity> Activities { get; set; } = new List<Activity>();

    public virtual ICollection<Deal> Deals { get; set; } = new List<Deal>();
}
