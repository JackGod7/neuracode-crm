using System.Globalization;
using System.Text;
using Neuracode.Crm.Api.Data;
using Neuracode.Crm.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Neuracode.Crm.Api.Endpoints;

public static class ExportEndpoints
{

    public static IEndpointRouteBuilder MapExportEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/export", Export).WithTags("export");
        return app;
    }

    static async Task<IResult> Export(AppDbContext db, string? type = "contacts")
    {
        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");

        if (type == "contacts" || type is null)
        {
            var all = await db.Contacts.OrderByDescending(c => c.CreatedAt).ToListAsync();

            var headers = new[] { "Nombre", "Email", "Telefono", "Empresa", "Fuente", "Temperatura", "Score", "Notas", "Fecha de creacion" };
            var rows = all.Select(c => new[]
            {
                c.Name, c.Email ?? "", c.Phone ?? "", c.Company ?? "",
                LeadSource.GetLabel(c.Source),
                c.Temperature == "hot" ? "Caliente" : c.Temperature == "warm" ? "Tibio" : "Frio",
                c.Score.ToString(),
                c.Notes ?? "",
                FormatDate(c.CreatedAt)
            });

            return CsvResult(BuildCsv(headers, rows), $"contactos-{today}.csv");
        }

        if (type == "deals")
        {
            var all = await db.Deals
                .OrderBy(d => d.Stage != null ? d.Stage.Order : 0)
                .Select(d => new
                {
                    d.Title,
                    d.Value,
                    d.Probability,
                    d.Notes,
                    d.ExpectedClose,
                    d.CreatedAt,
                    ContactName = d.Contact != null ? d.Contact.Name : null,
                    StageName = d.Stage != null ? d.Stage.Name : null
                })
                .ToListAsync();

            var headers = new[] { "Titulo", "Valor", "Contacto", "Etapa", "Probabilidad", "Cierre Estimado", "Notas", "Fecha de creacion" };
            var rows = all.Select(d => new[]
            {
                d.Title,
                FormatCurrency(d.Value),
                d.ContactName ?? "",
                d.StageName ?? "",
                $"{d.Probability}%",
                d.ExpectedClose.HasValue ? FormatDate(d.ExpectedClose.Value) : "-",
                d.Notes ?? "",
                FormatDate(d.CreatedAt)
            });

            return CsvResult(BuildCsv(headers, rows), $"deals-{today}.csv");
        }

        return Results.BadRequest("Tipo invalido. Use ?type=contacts o ?type=deals");
    }

    static IResult CsvResult(string csv, string filename)
    {
        var bom = Encoding.UTF8.GetPreamble();
        var content = Encoding.UTF8.GetBytes(csv);
        var bytes = bom.Concat(content).ToArray();
        return Results.File(bytes, "text/csv; charset=utf-8", filename);
    }

    static string BuildCsv(string[] headers, IEnumerable<string[]> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", headers.Select(EscapeCsv)));
        foreach (var row in rows)
            sb.AppendLine(string.Join(",", row.Select(EscapeCsv)));
        return sb.ToString().TrimEnd();
    }

    static string EscapeCsv(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }

    static string FormatCurrency(int cents)
    {
        var amount = cents / 100m;
        return amount.ToString("C", new CultureInfo("es-MX"));
    }

    static string FormatDate(int unixSeconds)
    {
        if (unixSeconds == 0) return "-";
        var dt = DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
        return dt.ToString("d MMM yyyy", new CultureInfo("es-MX"));
    }
}
