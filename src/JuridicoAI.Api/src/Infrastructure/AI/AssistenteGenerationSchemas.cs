using System.Text.Json;
using System.Text.Json.Nodes;
namespace JuridicoAI.Api.Infrastructure.AI;
internal static class AssistenteGenerationSchemas
{
    public static JsonElement Plan { get; } = JsonDocument.Parse("""
    [{"type":"function","name":"planejar_minuta","strict":true,"description":"Planeja uma minuta integral por seções. Não altera a Peça; o servidor redigirá e revisará todas as partes antes de criar uma proposta única.","parameters":{"type":"object","properties":{"titulo":{"type":"string"},"substituirPropostaReferenciada":{"type":"boolean"},"secoes":{"type":"array","items":{"type":"object","properties":{"titulo":{"type":"string"},"objetivo":{"type":"string"}},"required":["titulo","objetivo"],"additionalProperties":false}}},"required":["titulo","substituirPropostaReferenciada","secoes"],"additionalProperties":false}}]
    """).RootElement.Clone();
    public static JsonElement Section { get; } = BuildSection();
    private static JsonElement BuildSection()
    {
        var definitions = JsonNode.Parse(AssistenteAutomaticOutputSchema.Criar(true, true).GetRawText())!["$defs"]!.DeepClone();
        var parameters = JsonNode.Parse("""
        {"type":"object","properties":{"secaoId":{"type":"string"},"concluida":{"type":"boolean"},"sumario":{"type":"string"},"blocos":{"type":"array","items":{"$ref":"#/$defs/Node"}}},"required":["secaoId","concluida","sumario","blocos"],"additionalProperties":false}
        """)!;
        parameters["$defs"] = definitions;
        return JsonSerializer.SerializeToElement(new[] { new { type = "function", name = "registrar_secao", strict = true,
            description = "Registra exclusivamente a seção solicitada, completa e em blocos TipTap. Não aplica a Peça. Nunca trunque a seção nem invente fatos ou citações.", parameters } });
    }
    public static JsonElement Review { get; } = JsonDocument.Parse("""
    [{"type":"function","name":"revisar_minuta","strict":true,"description":"Registra a revisão de completude e coerência do lote integral fornecido. Pendências impedem a proposta final. Não comprova mérito jurídico nem leitura de fontes ausentes.","parameters":{"type":"object","properties":{"loteSha256":{"type":"string"},"secoes":{"type":"array","items":{"type":"string"}},"completa":{"type":"boolean"},"pendencias":{"type":"array","items":{"type":"string"}}},"required":["loteSha256","secoes","completa","pendencias"],"additionalProperties":false}}]
    """).RootElement.Clone();
}
