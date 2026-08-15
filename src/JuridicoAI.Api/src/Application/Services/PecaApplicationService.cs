using System.Text.Json;
using JuridicoAI.Api.Application.Abstractions;
using JuridicoAI.Api.Application.DTOs;
using JuridicoAI.Api.Application.Exceptions;
using JuridicoAI.Api.Domain.Entities;

namespace JuridicoAI.Api.Application.Services;

public sealed class PecaApplicationService(IPecaRepository repository)
{
    public async Task<Peca> CriarAsync(Guid casoId, CriarPecaRequest request, CancellationToken ct)
    {
        ValidarNome(request.Titulo, "titulo");
        if (!await repository.CasoExisteAsync(casoId, ct))
            throw new RecursoNaoEncontradoException($"Caso '{casoId}' nao encontrado.");

        TemplatePeca? template = null;
        if (request.TemplateId is { } templateId)
            template = await repository.ObterTemplateAsync(templateId, ct)
                ?? throw new RecursoNaoEncontradoException($"Template de peca '{templateId}' nao encontrado.");

        var agora = DateTime.UtcNow;
        return await repository.CriarPecaAsync(new Peca
        {
            Id = Guid.NewGuid(),
            CasoId = casoId,
            Titulo = request.Titulo.Trim(),
            TemplateId = template?.Id,
            ConteudoJson = template is null ? PecaDefaults.NovoConteudo() : PecaDefaults.Copiar(template.ConteudoJson),
            PageConfig = template is null ? PecaDefaults.NovaPagina() : PecaDefaults.Copiar(template.PageConfig),
            HeaderJson = template?.HeaderJson is null ? null : PecaDefaults.Copiar(template.HeaderJson),
            FooterJson = template?.FooterJson is null ? null : PecaDefaults.Copiar(template.FooterJson),
            Styles = template is null ? PecaDefaults.NovosEstilos() : PecaDefaults.Copiar(template.Styles),
            Status = PecaStatus.Rascunho,
            Versao = 1,
            DataCriacao = agora,
            DataAtualizacao = agora
        }, ct);
    }

    public async Task<IReadOnlyList<PecaListaDto>> ListarAsync(Guid casoId, CancellationToken ct)
    {
        if (!await repository.CasoExisteAsync(casoId, ct))
            throw new RecursoNaoEncontradoException($"Caso '{casoId}' nao encontrado.");
        return await repository.ListarPecasAsync(casoId, ct);
    }

    public async Task<Peca> ObterAsync(Guid id, CancellationToken ct) =>
        await repository.ObterPecaAsync(id, ct)
        ?? throw new RecursoNaoEncontradoException($"Peca '{id}' nao encontrada.");

    public async Task<PecaAtualizadaDto> AtualizarAsync(Guid id, AtualizarPecaRequest request, CancellationToken ct)
    {
        if (request.Versao < 1) throw new RequisicaoInvalidaException("A versao deve ser maior que zero.");
        if (request.Titulo is not null) ValidarNome(request.Titulo, "titulo");
        if (request.Status is { } status && !Enum.IsDefined(status)) throw new RequisicaoInvalidaException("Status invalido.");
        ValidarDocumento(request.ConteudoJson, "conteudoJson", true);
        ValidarPagina(request.PageConfig);
        ValidarDocumento(request.HeaderJson, "headerJson", false);
        ValidarDocumento(request.FooterJson, "footerJson", false);
        ValidarObjeto(request.Styles, "styles");
        if (request.Titulo is null && request.ConteudoJson is null && request.PageConfig is null &&
            request.HeaderJson is null && request.FooterJson is null && request.Styles is null && request.Status is null)
            throw new RequisicaoInvalidaException("Informe ao menos um campo para atualizar.");

        var peca = await ObterAsync(id, ct);
        if (request.Titulo is not null) peca.Titulo = request.Titulo.Trim();
        if (request.ConteudoJson is { } conteudo) peca.ConteudoJson = Documento(conteudo);
        if (request.PageConfig is { } pagina) peca.PageConfig = Documento(pagina);
        if (request.HeaderJson is { } header) peca.HeaderJson = Documento(header);
        if (request.FooterJson is { } footer) peca.FooterJson = Documento(footer);
        if (request.Styles is { } styles) peca.Styles = Documento(styles);
        if (request.Status is { } novoStatus) peca.Status = novoStatus;
        peca.DataAtualizacao = DateTime.UtcNow;
        if (!await repository.AtualizarPecaAsync(peca, request.Versao, ct))
            throw new ConflitoDeVersaoException("A peca foi alterada por outra requisicao.");
        return new PecaAtualizadaDto(peca.Id, request.Versao + 1, peca.DataAtualizacao);
    }

    public async Task ExcluirAsync(Guid id, int versao, CancellationToken ct)
    {
        if (versao < 1) throw new RequisicaoInvalidaException("A versao deve ser maior que zero.");
        await ObterAsync(id, ct);
        if (!await repository.ExcluirPecaAsync(id, versao, DateTime.UtcNow, ct))
            throw new ConflitoDeVersaoException("A peca foi alterada por outra requisicao.");
    }

    internal static void ValidarNome(string? value, string campo)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new RequisicaoInvalidaException($"O campo {campo} e obrigatorio.");
        if (value.Trim().Length > 200) throw new RequisicaoInvalidaException($"O campo {campo} deve ter no maximo 200 caracteres.");
    }

    internal static JsonDocument Documento(JsonElement value) => JsonDocument.Parse(value.GetRawText());

    internal static void ValidarDocumento(JsonElement? value, string campo, bool exigirDoc)
    {
        if (value is null) return;
        if (value.Value.ValueKind != JsonValueKind.Object)
            throw new RequisicaoInvalidaException($"O campo {campo} deve ser um objeto JSON.");
        if (exigirDoc && (!value.Value.TryGetProperty("type", out var type) || type.GetString() != "doc"))
            throw new RequisicaoInvalidaException($"O campo {campo} deve representar um documento TipTap.");
    }

    internal static void ValidarObjeto(JsonElement? value, string campo) => ValidarDocumento(value, campo, false);

    internal static void ValidarPagina(JsonElement? value)
    {
        ValidarObjeto(value, "pageConfig");
        if (value is null) return;
        if (!value.Value.TryGetProperty("format", out var format) || format.GetString() != "A4")
            throw new RequisicaoInvalidaException("pageConfig.format deve ser A4.");
        foreach (var nome in new[] { "marginTop", "marginBottom", "marginLeft", "marginRight" })
            if (!value.Value.TryGetProperty(nome, out var margem) || !margem.TryGetDecimal(out var numero) || numero < 0)
                throw new RequisicaoInvalidaException($"pageConfig.{nome} deve ser um numero nao negativo em milimetros.");
    }
}
