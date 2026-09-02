using System.ComponentModel.DataAnnotations;
using JuridicoAI.Api.Domain.Entities;
using Microsoft.AspNetCore.Http;

namespace JuridicoAI.Api.Application.DTOs;

public sealed record CriarCasoRequest(
    [param: Required, StringLength(200, MinimumLength = 1)] string Titulo,
    [param: StringLength(4000)] string? Descricao);

public sealed record AtualizarCasoRequest(
    [param: Required, StringLength(200, MinimumLength = 1)] string Titulo,
    [param: StringLength(4000)] string? Descricao,
    [param: Required] CasoStatus? Status);

public sealed record CriarEnvolvidoCasoRequest(
    [param: Required, StringLength(200, MinimumLength = 1)] string Nome,
    [param: Required] TipoEnvolvidoCaso? Tipo,
    [param: StringLength(50)] string? Documento,
    IReadOnlyDictionary<string, string?>? DadosAdicionais);

public sealed record AtualizarEnvolvidoCasoRequest(
    [param: Required, StringLength(200, MinimumLength = 1)] string Nome,
    [param: Required] TipoEnvolvidoCaso? Tipo,
    [param: StringLength(50)] string? Documento,
    IReadOnlyDictionary<string, string?>? DadosAdicionais);

public sealed record CriarContextoCasoRequest(
    [param: Required] TipoContextoCaso? Tipo,
    [param: StringLength(200)] string? Titulo,
    [param: Required, StringLength(50_000, MinimumLength = 1)] string Conteudo);

public sealed record AtualizarContextoCasoRequest(
    [param: Required] TipoContextoCaso? Tipo,
    [param: StringLength(200)] string? Titulo,
    [param: Required, StringLength(50_000, MinimumLength = 1)] string Conteudo);

public sealed class CriarDocumentoRequest
{
    public IFormFile? Arquivo { get; init; }
    public string? Tipo { get; init; }
}

public sealed record CasoContextoCompletoDto(
    Caso Caso,
    IReadOnlyList<EnvolvidoCaso> Envolvidos,
    IReadOnlyList<ContextoCaso> Contextos,
    IReadOnlyList<DocumentoCaso> Documentos);

public sealed record DocumentoArquivo(DocumentoCaso Documento, Stream Conteudo);
