using System;
using System.Collections.Generic;

namespace Neuracode.Crm.Api.Data.Entities;

public partial class PipelineStage
{
    public string Id { get; set; } = null!;

    public string Name { get; set; } = null!;

    public int Order { get; set; }

    public string Color { get; set; } = null!;

    public int IsWon { get; set; }

    public int IsLost { get; set; }

    public virtual ICollection<Deal> Deals { get; set; } = new List<Deal>();
}
