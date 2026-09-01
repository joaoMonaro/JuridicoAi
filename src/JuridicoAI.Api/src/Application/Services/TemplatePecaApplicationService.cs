using JuridicoAI.Api.Application.Abstractions;
using JuridicoAI.Api.Application.DTOs;
using JuridicoAI.Api.Application.Exceptions;
using JuridicoAI.Api.Domain.Entities;

namespace JuridicoAI.Api.Application.Services;

public sealed class TemplatePecaApplicationService(IPecaRepository repository, IAuthContext auth)
{
    public async Task<TemplatePeca> CriarAsync(CriarTemplatePecaRequest request, CancellationToken ct)
    {
        PecaApplicationService.ValidarNome(request.Nome, "nome");
        PecaApplicationService.ValidarDocumento(request.ConteudoJson, "conteudoJson", true);
        PecaApplicationService.ValidarPagina(request.PageConfig);
        PecaApplicationService.ValidarDocumento(request.HeaderJson, "headerJson", false);
        PecaApplicationService.ValidarDocumento(request.FooterJson, "footerJson", false);
        PecaApplicationService.ValidarObjeto(request.Styles, "styles");
        var agora = DateTime.UtcNow;
        return await repository.CriarTemplateAsync(new TemplatePeca
        {
            Id = Guid.NewGuid(),
            OrganizationId = auth.OrganizationId,
            CreatedById = auth.UserId,
            UpdatedById = auth.UserId,
            Nome = request.Nome.Trim(),
            ConteudoJson = request.ConteudoJson is { } conteudo ? PecaApplicationService.Documento(conteudo) : PecaDefaults.NovoConteudo(),
            PageConfig = request.PageConfig is { } pagina ? PecaApplicationService.Documento(pagina) : PecaDefaults.NovaPagina(),
            HeaderJson = request.HeaderJson is { } header ? PecaApplicationService.Documento(header) : null,
            FooterJson = request.FooterJson is { } footer ? PecaApplicationService.Documento(footer) : null,
            Styles = request.Styles is { } styles ? PecaApplicationService.Documento(styles) : PecaDefaults.NovosEstilos(),
            DataCriacao = agora,
            DataAtualizacao = agora
        }, ct);
    }

    public Task<IReadOnlyList<TemplatePeca>> ListarAsync(CancellationToken ct) => repository.ListarTemplatesAsync(auth.OrganizationId, ct);

    public async Task<TemplatePeca> ObterAsync(Guid id, CancellationToken ct) =>
        await repository.ObterTemplateAsync(id, auth.OrganizationId, ct)
        ?? throw new RecursoNaoEncontradoException($"Template de peca '{id}' nao encontrado.");

    public async Task<TemplatePeca> AtualizarAsync(Guid id, AtualizarTemplatePecaRequest request, CancellationToken ct)
    {
        if (request.Nome is not null) PecaApplicationService.ValidarNome(request.Nome, "nome");
        PecaApplicationService.ValidarDocumento(request.ConteudoJson, "conteudoJson", true);
        PecaApplicationService.ValidarPagina(request.PageConfig);
        PecaApplicationService.ValidarDocumento(request.HeaderJson, "headerJson", false);
        PecaApplicationService.ValidarDocumento(request.FooterJson, "footerJson", false);
        PecaApplicationService.ValidarObjeto(request.Styles, "styles");
        if (request.Nome is null && request.ConteudoJson is null && request.PageConfig is null &&
            request.HeaderJson is null && request.FooterJson is null && request.Styles is null)
            throw new RequisicaoInvalidaException("Informe ao menos um campo para atualizar.");
        var item = await ObterAsync(id, ct);
        if (request.Nome is not null) item.Nome = request.Nome.Trim();
        if (request.ConteudoJson is { } conteudo) item.ConteudoJson = PecaApplicationService.Documento(conteudo);
        if (request.PageConfig is { } pagina) item.PageConfig = PecaApplicationService.Documento(pagina);
        if (request.HeaderJson is { } header) item.HeaderJson = PecaApplicationService.Documento(header);
        if (request.FooterJson is { } footer) item.FooterJson = PecaApplicationService.Documento(footer);
        if (request.Styles is { } styles) item.Styles = PecaApplicationService.Documento(styles);
        item.DataAtualizacao = DateTime.UtcNow;
        item.UpdatedById = auth.UserId;
        if (!await repository.AtualizarTemplateAsync(item, ct))
            throw new RecursoNaoEncontradoException($"Template de peca '{id}' nao encontrado.");
        return item;
    }

    public async Task ExcluirAsync(Guid id, CancellationToken ct)
    {
        if (!await repository.ExcluirTemplateAsync(id, auth.OrganizationId, auth.UserId, DateTime.UtcNow, ct))
            throw new RecursoNaoEncontradoException($"Template de peca '{id}' nao encontrado.");
    }
}
