using System.ComponentModel.DataAnnotations;
using JuridicoAI.Api.Domain.Entities;

namespace JuridicoAI.Api.Application.DTOs;

public sealed record CriarCasoRequest(
    [property: Required, StringLength(200, MinimumLength = 1)] string Titulo,
    [property: StringLength(4000)] string? Descricao);

public sealed record AtualizarCasoRequest(
    [property: Required, StringLength(200, MinimumLength = 1)] string Titulo,
    [property: StringLength(4000)] string? Descricao,
    [property: Required] CasoStatus? Status);

public sealed record CriarEnvolvidoCasoRequest(
    [property: Required, StringLength(200, MinimumLength = 1)] string Nome,
    [property: Required] TipoEnvolvidoCaso? Tipo,
    [property: StringLength(50)] string? Documento,
    IReadOnlyDictionary<string, string?>? DadosAdicionais);

public sealed record AtualizarEnvolvidoCasoRequest(
    [property: Required, StringLength(200, MinimumLength = 1)] string Nome,
    [property: Required] TipoEnvolvidoCaso? Tipo,
    [property: StringLength(50)] string? Documento,
    IReadOnlyDictionary<string, string?>? DadosAdicionais);

public sealed record CriarContextoCasoRequest(
    [property: Required] TipoContextoCaso? Tipo,
    [property: StringLength(200)] string? Titulo,
    [property: Required, StringLength(50_000, MinimumLength = 1)] string Conteudo);

public sealed record AtualizarContextoCasoRequest(
    [property: Required] TipoContextoCaso? Tipo,
    [property: StringLength(200)] string? Titulo,
    [property: Required, StringLength(50_000, MinimumLength = 1)] string Conteudo);

public sealed record CasoContextoCompletoDto(
    Caso Caso,
    IReadOnlyList<EnvolvidoCaso> Envolvidos,
    IReadOnlyList<ContextoCaso> Contextos,
    IReadOnlyList<DocumentoCaso> Documentos);

public sealed record DocumentoArquivo(DocumentoCaso Documento, Stream Conteudo);
