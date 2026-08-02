using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace JuridicoAI.Api.Domain.Entities;

public sealed class Jurisprudencia
{
    [BsonId, BsonRepresentation(BsonType.ObjectId)] public string? Id { get; set; }
    [BsonElement("rawId"), BsonRepresentation(BsonType.ObjectId)] public string RawId { get; init; } = string.Empty;
    [BsonElement("fonte")] public string Fonte { get; init; } = string.Empty;
    [BsonElement("numeroProcesso")] public string NumeroProcesso { get; init; } = string.Empty;
    [BsonElement("relator")] public string? Relator { get; init; }
    [BsonElement("orgaoJulgador")] public string? OrgaoJulgador { get; init; }
    [BsonElement("dataJulgamento")] public DateTime? DataJulgamento { get; init; }
    [BsonElement("dataPublicacao")] public DateTime? DataPublicacao { get; init; }
    [BsonElement("ementa")] public string Ementa { get; init; } = string.Empty;
    [BsonElement("link")] public string Link { get; init; } = string.Empty;
    [BsonElement("classificacao")] public JurisprudenciaClassificacao Classificacao { get; set; } = new();
    [BsonElement("dataProcessamento")] public DateTime DataProcessamento { get; set; } = DateTime.UtcNow;
}
