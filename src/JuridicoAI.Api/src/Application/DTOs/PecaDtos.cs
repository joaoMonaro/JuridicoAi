using System.Text.Json;
using JuridicoAI.Api.Domain.Entities;
using JuridicoAI.Api.Domain.Enums;

namespace JuridicoAI.Api.Application.DTOs;

public sealed record CriarPecaRequest(string Titulo, Guid? TemplateId = null);

public sealed record AtualizarPecaRequest(
    int Versao,
    string? Titulo = null,
    JsonElement? ConteudoJson = null,
    JsonElement? PageConfig = null,
    JsonElement? HeaderJson = null,
    JsonElement? FooterJson = null,
    JsonElement? Styles = null,
    PecaStatusEnum? Status = null,
    JsonElement TemplateId = default);

public sealed record PecaListaDto(
    Guid Id, string Titulo, PecaStatusEnum Status, int Versao, Guid? TemplateId, DateTime DataAtualizacao);

public sealed record PecaApresentacaoDto(Guid? TemplateId, JsonElement PageConfig, JsonElement Styles,
    JsonElement? HeaderJson, JsonElement? FooterJson);

public sealed record PecaAtualizadaDto(Guid Id, int Versao, DateTime DataAtualizacao,
    PecaApresentacaoDto? Apresentacao = null);

public sealed record CriarTemplatePecaRequest(
    string Nome,
    JsonElement? ConteudoJson = null,
    JsonElement? PageConfig = null,
    JsonElement? HeaderJson = null,
    JsonElement? FooterJson = null,
    JsonElement? Styles = null);

public sealed record AtualizarTemplatePecaRequest(
    string? Nome = null,
    JsonElement? ConteudoJson = null,
    JsonElement? PageConfig = null,
    JsonElement? HeaderJson = null,
    JsonElement? FooterJson = null,
    JsonElement? Styles = null);
