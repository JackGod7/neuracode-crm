using System;
using System.Collections.Generic;

namespace Neuracode.Crm.Api.Data.Entities;

public partial class CrmSetting
{
    public string Key { get; set; } = null!;

    public string Value { get; set; } = null!;
}
