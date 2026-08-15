using System.Text.Json;
using JuridicoAI.Api.Domain.Entities;

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
    PecaStatus? Status = null);

public sealed record PecaListaDto(
    Guid Id, string Titulo, PecaStatus Status, int Versao, Guid? TemplateId, DateTime DataAtualizacao);

public sealed record PecaAtualizadaDto(Guid Id, int Versao, DateTime DataAtualizacao);

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
