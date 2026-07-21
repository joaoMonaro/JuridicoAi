using MongoDB.Bson.Serialization.Attributes;

namespace JuridicoAI.Api.Domain.Entities;

public sealed class JurisprudenciaClassificacao
{
    [BsonElement("area")] public string Area { get; init; } = "direito_consumidor";
    [BsonElement("tema")] public string Tema { get; init; } = "indefinido";
    [BsonElement("subtema")] public string Subtema { get; init; } = "indefinido";
    [BsonElement("tipoAcao")] public string TipoAcao { get; init; } = "indefinido";
    [BsonElement("resultado")] public string Resultado { get; init; } = "indefinido";
    [BsonElement("danoMoral")] public bool DanoMoral { get; init; }
    [BsonElement("valorDanoMoral")] public decimal? ValorDanoMoral { get; init; }
    [BsonElement("exclusaoNegativacao")] public bool ExclusaoNegativacao { get; init; }
    [BsonElement("palavrasChave")] public IReadOnlyList<string> PalavrasChave { get; init; } = [];
    [BsonElement("confianca")] public double Confianca { get; init; }
    [BsonElement("statusClassificacao")] public string StatusClassificacao { get; init; } = "classificado";
    [BsonElement("versaoClassificador")] public string VersaoClassificador { get; init; } = "regras-v1";
}
