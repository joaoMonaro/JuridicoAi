using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace JuridicoAI.Api.Domain.Entities;

public sealed class JurisprudenciaRaw
{
    [BsonId, BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonElement("fonte")] public string Fonte { get; init; } = string.Empty;
    [BsonElement("numeroProcesso")] public string NumeroProcesso { get; init; } = string.Empty;
    [BsonElement("relator")] public string? Relator { get; init; }
    [BsonElement("orgaoJulgador")] public string? OrgaoJulgador { get; init; }
    [BsonElement("dataJulgamento")] public string? DataJulgamento { get; init; }
    [BsonElement("dataPublicacao")] public string? DataPublicacao { get; init; }
    [BsonElement("ementaBruta")] public string EmentaBruta { get; init; } = string.Empty;
    [BsonElement("link")] public string Link { get; init; } = string.Empty;
    [BsonElement("htmlBruto")] public string? HtmlBruto { get; init; }
    [BsonElement("termoBuscado")] public string TermoBuscado { get; init; } = string.Empty;
    [BsonElement("dataColeta")] public DateTime DataColeta { get; init; } = DateTime.UtcNow;
    [BsonElement("status")] public string Status { get; set; } = "coletado";
    [BsonElement("erroProcessamento")] public string? ErroProcessamento { get; set; }
}
