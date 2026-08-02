namespace JuridicoAI.Api.Domain.Entities;

public sealed record ScraperResult(string TermoBuscado, IReadOnlyList<JurisprudenciaRaw> Resultados);
