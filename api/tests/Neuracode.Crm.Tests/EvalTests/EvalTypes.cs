using System.Text.Json.Serialization;

namespace Neuracode.Crm.Tests.EvalTests;

public sealed record EvalTurn(
    [property: JsonPropertyName("messages")] string[] Messages,
    [property: JsonPropertyName("name")] string Name = "Cliente");

public sealed record EvalExpected(
    [property: JsonPropertyName("minNaturalidad")] int MinNaturalidad = 3,
    [property: JsonPropertyName("minPrecision")] int MinPrecision = 4,
    [property: JsonPropertyName("deflectionForbidden")] bool DeflectionForbidden = true);

public sealed record EvalScenario(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("turns")] EvalTurn[] Turns,
    [property: JsonPropertyName("expected")] EvalExpected Expected);

public sealed record ScenariosFile(
    [property: JsonPropertyName("scenarios")] EvalScenario[] Scenarios);

public sealed record JudgeVerdict(
    [property: JsonPropertyName("naturalidad")] int Naturalidad,
    [property: JsonPropertyName("precision")] int Precision,
    [property: JsonPropertyName("deflectado")] bool Deflectado,
    [property: JsonPropertyName("razon")] string Razon);
