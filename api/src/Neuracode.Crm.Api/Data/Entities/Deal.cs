using System;
using System.Collections.Generic;

namespace Neuracode.Crm.Api.Data.Entities;

public partial class Deal
{
    public string Id { get; set; } = null!;

    public string Title { get; set; } = null!;

    public int Value { get; set; }

    public string StageId { get; set; } = null!;

    public string ContactId { get; set; } = null!;

    public int? ExpectedClose { get; set; }

    public int Probability { get; set; }

    public string? Notes { get; set; }

    public int CreatedAt { get; set; }

    public int UpdatedAt { get; set; }

    public virtual ICollection<Activity> Activities { get; set; } = new List<Activity>();

    public virtual Contact Contact { get; set; } = null!;

    public virtual PipelineStage Stage { get; set; } = null!;
}
