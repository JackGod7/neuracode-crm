using System;
using System.Collections.Generic;

namespace Neuracode.Crm.Api.Data.Entities;

public partial class Activity
{
    public string Id { get; set; } = null!;

    public string Type { get; set; } = null!;

    public string Description { get; set; } = null!;

    public string ContactId { get; set; } = null!;

    public string? DealId { get; set; }

    public string? Wamid { get; set; }

    public int? ScheduledAt { get; set; }

    public int? CompletedAt { get; set; }

    public int CreatedAt { get; set; }

    public virtual Contact Contact { get; set; } = null!;

    public virtual Deal? Deal { get; set; }
}
