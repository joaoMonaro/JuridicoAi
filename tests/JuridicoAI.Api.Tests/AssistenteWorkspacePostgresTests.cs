using System.Text.Json;
using JuridicoAI.Api.Application.Abstractions;
using JuridicoAI.Api.Application.DTOs;
using JuridicoAI.Api.Application.Exceptions;
using JuridicoAI.Api.Application.Services;
using JuridicoAI.Api.Domain.Entities;
using JuridicoAI.Api.Domain.Enums;
using JuridicoAI.Api.Infrastructure.Database;
using JuridicoAI.Api.Infrastructure.Repositories;
using JuridicoAI.Api.Infrastructure.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;
using Npgsql;

namespace JuridicoAI.Api.Tests;

public sealed class ReviewPostgresFactAttribute : FactAttribute
{
    public ReviewPostgresFactAttribute()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("JURIDICOAI_REVIEW_TEST_POSTGRES")))
            Skip = "Defina JURIDICOAI_REVIEW_TEST_POSTGRES para executar com PostgreSQL descartável.";
    }
}

public sealed class AssistenteWorkspacePostgresTests
{
    [ReviewPostgresFact]
    public async Task Livre_PublicaPropostaComRespostaSemAplicarOuDuplicarMensagens()
    {
        await using var db = await Database.Create();
        await using var context = db.Context();
        var workspace = db.Service(context);
        var conversation = await workspace.ObterOuCriarAsync(db.PecaId, default);
        var request = ChatRequest("Melhore o primeiro parágrafo.") with { InteracaoAutomatica = true };
        var start = await db.Chat(context).IniciarAsync(conversation.Id, request, default);
        var result = await db.Chat(context).ConcluirLivreAsync(start, AutomaticResult(start.Pedido.Id,
            [new("Melhorar", [new(0, 1, [AssistentePatchCompilerTests.Paragraph("Primeiro melhorado")])])]), default);
        Assert.NotNull(result.Proposta);
        Assert.Equal(1, (await context.Pecas.AsNoTracking().SingleAsync()).Versao);
        Assert.Empty(await context.AssistenteRecibos.ToArrayAsync());
        var messages = await workspace.MensagensAsync(conversation.Id, null, 50, default);
        Assert.Equal(2, messages.Count);
        Assert.All(messages, message => Assert.Equal(result.Proposta.Id, message.PropostaId));
        Assert.Equal(result.Proposta.Id, messages[1].Resultado!.Proposta!.Id);
        Assert.False((await db.Chat(context).IniciarAsync(conversation.Id, request, default)).Novo);
        await workspace.AplicarAsync(result.Proposta.Id, Approve(result.Proposta, result.Proposta.Grupos[0].Id), default);
        Assert.Equal(2, (await context.Pecas.AsNoTracking().SingleAsync()).Versao);
        Assert.Equal(2, await context.AssistenteMensagens.CountAsync());
    }

    [ReviewPostgresFact]
    public async Task Livre_AjusteSubstituiPendenciasEPreservaTrechoJaAprovado()
    {
        await using var db = await Database.Create();
        await using var context = db.Context();
        var workspace = db.Service(context);
        var conversation = await workspace.ObterOuCriarAsync(db.PecaId, default);
        var first = await db.Chat(context).IniciarAsync(conversation.Id, ChatRequest("Melhore o documento") with { InteracaoAutomatica = true }, default);
        var original = (await db.Chat(context).ConcluirLivreAsync(first, AutomaticResult(first.Pedido.Id, Request().Grupos), default)).Proposta!;
        var partial = await workspace.AplicarAsync(original.Id, Approve(original, original.Grupos[0].Id), default);
        var next = await db.Chat(context).IniciarAsync(conversation.Id,
            ChatRequest("Encurte a alteração ainda pendente") with { InteracaoAutomatica = true, VersaoPeca = 2, PropostaReferenciadaId = original.Id }, default);
        using var payload = JsonDocument.Parse(next.Contexto.Split('\n')[2]);
        Assert.Equal("Aplicado", payload.RootElement.GetProperty("propostaReferenciada").GetProperty("grupos")[0].GetProperty("estado").GetString());
        var revised = (await db.Chat(context).ConcluirLivreAsync(next, AutomaticResult(next.Pedido.Id,
            [new("Encurtar", [new(1, 1, [AssistentePatchCompilerTests.Paragraph("Segundo curto")])])], substitute: true), default)).Proposta!;
        var old = await workspace.PropostaAsync(original.Id, default);
        Assert.Equal("Substituida", old.Estado);
        Assert.Equal(revised.Id, old.SubstituidaPorId);
        Assert.Equal("Aplicado", old.Grupos[0].Estado);
        var conflict = await Assert.ThrowsAsync<AssistenteIaException>(() => workspace.AplicarAsync(original.Id, Approve(partial.PropostaRestante, original.Grupos[1].Id), default));
        Assert.Equal("AI_PROPOSAL_REVISION_CONFLICT", conflict.Code);
        var final = await workspace.AplicarAsync(revised.Id, Approve(revised, revised.Grupos[0].Id), default);
        Assert.Equal(3, final.VersaoResultante);
        Assert.Equal("Primeiro revisado", final.ConteudoJson.GetProperty("content")[0].GetProperty("content")[0].GetProperty("text").GetString());
        Assert.Equal("Segundo curto", final.ConteudoJson.GetProperty("content")[1].GetProperty("content")[0].GetProperty("text").GetString());
        Assert.Equal(4, await context.AssistenteMensagens.CountAsync());
    }

    [ReviewPostgresFact]
    public async Task Livre_FalhaNaPublicacaoRevertePropostaEVinculos()
    {
        await using var db = await Database.Create();
        await using var context = db.Context();
        var conversation = await db.Service(context).ObterOuCriarAsync(db.PecaId, default);
        var start = await db.Chat(context).IniciarAsync(conversation.Id, ChatRequest("Melhore") with { InteracaoAutomatica = true }, default);
        await using (var failing = db.Context(new FailAnswer()))
            await Assert.ThrowsAsync<InvalidOperationException>(() => db.Chat(failing).ConcluirLivreAsync(start,
                AutomaticResult(start.Pedido.Id, Request().Grupos), default));
        await using var verify = db.Context();
        Assert.Empty(await verify.AssistentePropostas.ToArrayAsync());
        Assert.All(await verify.AssistenteMensagens.ToArrayAsync(), message => Assert.Null(message.PropostaId));
        Assert.Null((await verify.AssistenteMensagens.SingleAsync(m => m.RespostaAId != null)).Resultado);
        Assert.Equal(1, (await verify.Pecas.SingleAsync()).Versao);
    }

    [ReviewPostgresFact]
    public async Task Livre_RevalidaPermissoesEImpedeGeracaoIntegralPorColecaoDeSplices()
    {
        await using var db = await Database.Create();
        await using var context = db.Context();
        var conversation = await db.Service(context).ObterOuCriarAsync(db.PecaId, default);
        var start = await db.Chat(context).IniciarAsync(conversation.Id, ChatRequest("Melhore") with { InteracaoAutomatica = true }, default);
        db.Access.Full = false;
        var denied = await Assert.ThrowsAsync<AssistenteIaException>(() => db.Chat(context).ConcluirLivreAsync(start, AutomaticResult(start.Pedido.Id, Request().Grupos), default));
        Assert.Equal(403, denied.StatusCode);
        db.Access.Edit = false;
        denied = await Assert.ThrowsAsync<AssistenteIaException>(() => db.Chat(context).ConcluirLivreAsync(start,
            AutomaticResult(start.Pedido.Id, [Request().Grupos[0]]), default));
        Assert.Equal(403, denied.StatusCode);
        Assert.Empty(await context.AssistentePropostas.ToArrayAsync());
        Assert.Equal(1, (await context.Pecas.SingleAsync()).Versao);
    }

    private static AssistenteSolicitacaoConcluidaDto AutomaticResult(Guid message, IReadOnlyList<AssistenteGrupoRequest> groups, bool substitute = false) =>
        ChatResult(message, "Preparei alterações para revisão.") with {
            Sugestao = ChatResult(message, "Preparei alterações para revisão.").Sugestao with { OperacaoSugerida = AssistenteOperacao.ProporAlteracoes },
            RascunhoAutomatico = new(AssistenteIntencao.EditarPeca, groups, substitute) };

    [ReviewPostgresFact]
    public async Task Chat_ReabreRespostaCompletaEUsaHistoricoPrivadoNoPedidoSeguinte()
    {
        await using var db = await Database.Create();
        await using var context = db.Context();
        var conversation = await db.Service(context).ObterOuCriarAsync(db.PecaId, default);
        var chat = db.Chat(context);
        var request = ChatRequest("A contratante é Ana. O valor é R$ 500.");
        var first = await chat.IniciarAsync(conversation.Id, request, default);
        Assert.Contains("Primeiro", first.Contexto);
        Assert.Equal(2, await context.AssistenteMensagens.CountAsync());
        await chat.ConcluirAsync(first, ChatResult(first.Pedido.Id, "Entendido, Ana."), default);
        await using var reopened = db.Context();
        var messages = await db.Service(reopened).MensagensAsync(conversation.Id, null, 50, default);
        Assert.Equal(request.Instrucao, messages[0].Texto);
        Assert.Equal("Entendido, Ana.", messages[1].Resultado!.Sugestao.Texto);
        Assert.Equal("Concluida", messages[1].Estado);
        var second = await db.Chat(reopened).IniciarAsync(conversation.Id, ChatRequest("Corrigindo: a contratante é Beatriz."), default);
        Assert.Contains("Ana", second.Contexto);
        await db.Chat(reopened).ConcluirAsync(second, ChatResult(second.Pedido.Id, "Correção recebida."), default);
        var third = await db.Chat(reopened).IniciarAsync(conversation.Id, ChatRequest("Qual o nome e valor informados?"), default);
        Assert.True(third.Contexto.IndexOf("Ana", StringComparison.Ordinal) < third.Contexto.IndexOf("Beatriz", StringComparison.Ordinal));
        Assert.Contains("500", third.Contexto);
        Assert.Equal(1, (await context.Pecas.AsNoTracking().SingleAsync()).Versao);
        var missing = await Assert.ThrowsAsync<AssistenteIaException>(() => db.Chat(reopened, Guid.NewGuid()).IniciarAsync(conversation.Id, ChatRequest("Outro"), default));
        Assert.Equal(404, missing.StatusCode);
    }

    [ReviewPostgresFact]
    public async Task Chat_RepetirPedidoNaoDuplicaMensagem_EConflitoNaoGeraOutroPedido()
    {
        await using var db = await Database.Create();
        await using var context = db.Context();
        var conversation = await db.Service(context).ObterOuCriarAsync(db.PecaId, default);
        var request = ChatRequest("Pedido único");
        async Task<AssistenteChatInicio> Start()
        { await using var scope = db.Context(); return await db.Chat(scope).IniciarAsync(conversation.Id, request, default); }
        var attempts = await Task.WhenAll(Start(), Start());
        Assert.Single(attempts.Where(x => x.Novo));
        Assert.Equal(attempts[0].Pedido.Id, attempts[1].Pedido.Id);
        Assert.Equal(2, await context.AssistenteMensagens.CountAsync());
        var chat = db.Chat(context);
        var conflict = await Assert.ThrowsAsync<AssistenteIaException>(() => chat.IniciarAsync(conversation.Id, request with { Instrucao = "Outro conteúdo" }, default));
        Assert.Equal("AI_IDEMPOTENCY_MISMATCH", conflict.Code);
        conflict = await Assert.ThrowsAsync<AssistenteIaException>(() => chat.IniciarAsync(conversation.Id, ChatRequest("Segundo"), default));
        Assert.Equal("AI_CONVERSATION_BUSY", conflict.Code);
        await chat.ConcluirAsync(attempts[0], ChatResult(attempts[0].Pedido.Id, "Resposta única"), default);
        var recovered = await chat.IniciarAsync(conversation.Id, request, default);
        Assert.False(recovered.Novo);
        Assert.Equal("Resposta única", AssistenteChatService.Read<AssistenteSolicitacaoConcluidaDto>(recovered.Resposta.Resultado)!.Sugestao.Texto);
    }

    [ReviewPostgresFact]
    public async Task Chat_CancelamentoEInterrupcaoPreservamPedidoEBloqueiamResultadoTardio()
    {
        await using var db = await Database.Create();
        await using var context = db.Context();
        var conversation = await db.Service(context).ObterOuCriarAsync(db.PecaId, default);
        var chat = db.Chat(context);
        var cancelled = await chat.IniciarAsync(conversation.Id, ChatRequest("Meu texto preservado"), default);
        await chat.FalharAsync(cancelled, "AI_CANCELLED", true);
        Assert.Equal("Cancelada", (await db.Service(context).MensagensAsync(conversation.Id, null, 50, default))[1].Estado);
        var interrupted = await chat.IniciarAsync(conversation.Id, ChatRequest("Pedido interrompido"), default);
        await context.AssistenteMensagens.Where(x => x.Id == interrupted.Resposta.Id).ExecuteUpdateAsync(update => update.SetProperty(x => x.ExpiraEm, DateTime.UtcNow.AddMinutes(-1)));
        var next = await chat.IniciarAsync(conversation.Id, ChatRequest("Novo pedido"), default);
        var late = await Assert.ThrowsAsync<AssistenteIaException>(() => chat.ConcluirAsync(interrupted, ChatResult(interrupted.Pedido.Id, "Não publicar"), default));
        Assert.Equal("AI_REQUEST_INTERRUPTED", late.Code);
        Assert.Equal("Gerando", next.Resposta.Estado);
        var messages = await db.Service(context).MensagensAsync(conversation.Id, null, 50, default);
        Assert.Equal("Interrompida", messages[3].Estado);
        Assert.All(messages, m => Assert.DoesNotContain("Não publicar", m.Texto));
    }

    [ReviewPostgresFact]
    public async Task Chat_PropostaVinculadaNaoDuplicaParDeMensagens()
    {
        await using var db = await Database.Create();
        await using var context = db.Context();
        var service = db.Service(context);
        var conversation = await service.ObterOuCriarAsync(db.PecaId, default);
        var first = await db.Chat(context).IniciarAsync(conversation.Id, ChatRequest("Melhore o texto."), default);
        await db.Chat(context).ConcluirAsync(first, ChatResult(first.Pedido.Id, "Proposta de revisão."), default);
        var proposal = await service.RegistrarAsync(conversation.Id, Request() with { MensagemUsuarioId = first.Pedido.Id }, default);
        var messages = await service.MensagensAsync(conversation.Id, null, 50, default);
        Assert.Equal(2, messages.Count);
        Assert.All(messages, m => Assert.Equal(proposal.Id, m.PropostaId));
    }

    [ReviewPostgresFact]
    public async Task Chat_VersaoAtualEFontesElegiveisPrevalecemSemApagarHistorico()
    {
        await using var db = await Database.Create();
        await using var context = db.Context();
        var conversation = await db.Service(context).ObterOuCriarAsync(db.PecaId, default);
        var chat = db.Chat(context);
        var first = await chat.IniciarAsync(conversation.Id, ChatRequest("Informação privada do usuário"), default);
        await chat.ConcluirAsync(first, ChatResult(first.Pedido.Id, "Rascunho na versão antiga"), default);
        await context.Pecas.ExecuteUpdateAsync(update => update.SetProperty(x => x.Versao, 2));
        var second = await chat.IniciarAsync(conversation.Id, ChatRequest("Continuar") with { VersaoPeca = 2 }, default);
        Assert.DoesNotContain("Rascunho na versão antiga", second.Contexto);
        using var payload = JsonDocument.Parse(second.Contexto.Split('\n')[2]);
        Assert.Equal("Informação privada do usuário", payload.RootElement.GetProperty("historicoCronologico")[0].GetProperty("texto").GetString());
        Assert.Contains("conteudoOmitidoPorDependenciaDeFonte", second.Contexto);
        var oldMessages = await db.Service(context).MensagensAsync(conversation.Id, null, 50, default);
        Assert.Equal("Rascunho na versão antiga", oldMessages[1].Resultado!.Sugestao.Texto);
    }

    [ReviewPostgresFact]
    public async Task Chat_LimiteNaoTruncaNemApagaHistoricoEFlagNaoApagaMensagens()
    {
        await using var db = await Database.Create();
        await using var context = db.Context();
        var conversation = await db.Service(context).ObterOuCriarAsync(db.PecaId, default);
        var first = await db.Chat(context).IniciarAsync(conversation.Id, ChatRequest("Conteúdo preservado"), default);
        await db.Chat(context).ConcluirAsync(first, ChatResult(first.Pedido.Id, "Resposta preservada"), default);
        db.Settings.MaxTotalContextCharacters = 10;
        var ex = await Assert.ThrowsAsync<AssistenteIaException>(() => db.Chat(context).IniciarAsync(conversation.Id, ChatRequest("Continuação"), default));
        Assert.Equal("AI_HISTORY_CONTEXT_TOO_LARGE", ex.Code);
        Assert.Equal(2, await context.AssistenteMensagens.CountAsync());
        db.Settings.ConversationV2Enabled = false;
        ex = await Assert.ThrowsAsync<AssistenteIaException>(() => db.Chat(context).IniciarAsync(conversation.Id, ChatRequest("Continuação"), default));
        Assert.Equal("AI_CONVERSATION_DISABLED", ex.Code);
        Assert.Equal(2, (await db.Service(context).MensagensAsync(conversation.Id, null, 50, default)).Count);
    }

    private static CriarSolicitacaoAssistenteRequest ChatRequest(string text) => new(Guid.NewGuid(), AssistenteModo.Perguntar, text, 1,
        AssistenteOperacao.ResponderNoPainel, new(new(AssistenteEscopoDocumento.Nenhum)));
    private static AssistenteSolicitacaoConcluidaDto ChatResult(Guid message, string text) => new(
        new(Guid.NewGuid(), AssistenteModo.Perguntar, AssistenteOperacao.ResponderNoPainel, "Resposta", text, "", [], [], new(null, null, null, 0, null), message),
        new(10, 10), "ficticio");

    [ReviewPostgresFact]
    public async Task Chat_RejeicaoDeRespostaPersisteEExcluiTextoDaMemoriaSemApagarHistorico()
    {
        await using var db = await Database.Create();
        await using var context = db.Context();
        var conversation = await db.Service(context).ObterOuCriarAsync(db.PecaId, default);
        var chat = db.Chat(context);
        var first = await chat.IniciarAsync(conversation.Id, ChatRequest("Analise"), default);
        await chat.ConcluirAsync(first, ChatResult(first.Pedido.Id, "Resposta que o titular rejeitou"), default);
        await chat.RejeitarRespostaAsync(conversation.Id, first.Pedido.Id, default);
        await chat.RejeitarRespostaAsync(conversation.Id, first.Pedido.Id, default);
        var messages = await db.Service(context).MensagensAsync(conversation.Id, null, 50, default);
        Assert.Equal("Rejeitada", messages[1].Estado);
        Assert.Equal("Resposta que o titular rejeitou", messages[1].Resultado!.Sugestao.Texto);
        var next = await chat.IniciarAsync(conversation.Id, ChatRequest("Reavalie"), default);
        Assert.DoesNotContain("Resposta que o titular rejeitou", next.Contexto);
        Assert.Equal(4, await context.AssistenteMensagens.CountAsync());
    }

    [ReviewPostgresFact]
    public async Task DocumentoVazioLegado_UsaMesmoParagrafoImplicitoDoEditor()
    {
        await using var db = await Database.Create();
        await using var context = db.Context();
        await context.Pecas.ExecuteUpdateAsync(update => update.SetProperty(x => x.ConteudoJson, JsonDocument.Parse("""{"type":"doc","content":[]}""")));
        var service = db.Service(context);
        var conversation = await service.ObterOuCriarAsync(db.PecaId, default);
        var request = Request() with { Grupos = [new("Inserir texto", [new(0, 1, [AssistentePatchCompilerTests.Paragraph("Novo texto")])])] };
        var proposal = await service.RegistrarAsync(conversation.Id, request, default);
        var receipt = await service.AplicarAsync(proposal.Id, Approve(proposal, proposal.Grupos[0].Id), default);
        Assert.Equal("Novo texto", receipt.ConteudoJson.GetProperty("content")[0].GetProperty("content")[0].GetProperty("text").GetString());
    }
    [ReviewPostgresFact]
    public async Task ConversaPersistida_ReabreSemAlterarPecaEIsolaTitulares()
    {
        await using var db = await Database.Create();
        await using var first = db.Context();
        var service = db.Service(first);
        var conversation = await service.ObterOuCriarAsync(db.PecaId, default);
        var proposal = await service.RegistrarAsync(conversation.Id, Request(), default);
        Assert.Equal(1, (await first.Pecas.SingleAsync()).Versao);
        await using var reopened = db.Context();
        var same = await db.Service(reopened).ObterOuCriarAsync(db.PecaId, default);
        Assert.Equal(conversation.Id, same.Id);
        Assert.Equal(2, (await db.Service(reopened).MensagensAsync(same.Id, null, 50, default)).Count);
        Assert.Single(await db.Service(reopened).PropostasAsync(same.Id, default));
        var other = db.Service(reopened, Guid.NewGuid());
        var ex = await Assert.ThrowsAsync<AssistenteIaException>(() => other.PropostaAsync(proposal.Id, default));
        Assert.Equal(404, ex.StatusCode);
        ex = await Assert.ThrowsAsync<AssistenteIaException>(() => other.MensagensAsync(same.Id, null, 50, default));
        Assert.Equal(404, ex.StatusCode);
    }

    [ReviewPostgresFact]
    public async Task AplicacaoParcial_EAtomicaIdempotenteEReciboAntigoNaoSobrescreveVersaoNova()
    {
        await using var db = await Database.Create();
        await using var context = db.Context();
        var service = db.Service(context);
        var conversation = await service.ObterOuCriarAsync(db.PecaId, default);
        var proposal = await service.RegistrarAsync(conversation.Id, Request(), default);
        var command = Approve(proposal, proposal.Grupos[0].Id);
        var first = await service.AplicarAsync(proposal.Id, command, default);
        Assert.Equal(2, first.VersaoResultante);
        Assert.Equal("Aplicado", first.PropostaRestante.Grupos[0].Estado);
        Assert.Equal("Pendente", first.PropostaRestante.Grupos[1].Estado);
        Assert.Equal(1, await context.AssistenteRecibos.CountAsync());
        var second = await service.AplicarAsync(proposal.Id, Approve(first.PropostaRestante, proposal.Grupos[1].Id), default);
        Assert.Equal(3, second.VersaoResultante);
        var retry = await service.AplicarAsync(proposal.Id, command, default);
        Assert.False(retry.ResultadoAindaAtual);
        Assert.Equal(2, retry.VersaoResultante);
        Assert.Equal(3, retry.VersaoPecaAtual);
        Assert.Equal(2, await context.AssistenteRecibos.CountAsync());
        Assert.Equal(3, (await context.Pecas.AsNoTracking().SingleAsync()).Versao);
        var ex = await Assert.ThrowsAsync<AssistenteIaException>(() => service.AplicarAsync(proposal.Id, command with { VersaoPecaEsperada = 2 }, default));
        Assert.Equal("AI_IDEMPOTENCY_MISMATCH", ex.Code);
    }

    [ReviewPostgresFact]
    public async Task DuasAbas_MesmaAprovacaoGeraUmUnicoCommit()
    {
        await using var db = await Database.Create();
        var proposal = await db.Prepare();
        var command = Approve(proposal, proposal.Grupos[0].Id);
        async Task<AssistenteReciboDto> Apply()
        { await using var scope = db.Context(); return await db.Service(scope).AplicarAsync(proposal.Id, command, default); }
        var receipts = await Task.WhenAll(Apply(), Apply());
        Assert.All(receipts, receipt => Assert.Equal(2, receipt.VersaoResultante));
        await using var verify = db.Context();
        Assert.Equal(1, await verify.AssistenteRecibos.CountAsync());
        Assert.Equal(2, (await verify.Pecas.SingleAsync()).Versao);
    }

    [ReviewPostgresFact]
    public async Task FalhaAoGravarRecibo_ReverteUpdateJaExecutadoNaPeca()
    {
        await using var db = await Database.Create();
        var proposal = await db.Prepare();
        await using (var failing = db.Context(new FailReceipt()))
            await Assert.ThrowsAsync<InvalidOperationException>(() => db.Service(failing).AplicarAsync(proposal.Id, Approve(proposal, proposal.Grupos[0].Id), default));
        await using var verify = db.Context();
        Assert.Equal(1, (await verify.Pecas.SingleAsync()).Versao);
        Assert.Empty(await verify.AssistenteRecibos.ToListAsync());
        Assert.All((await db.Service(verify).PropostaAsync(proposal.Id, default)).Grupos, g => Assert.Equal("Pendente", g.Estado));
    }

    [ReviewPostgresFact]
    public async Task EdicaoConcorrenteBloqueiaAplicacao_ERejeicaoNaoAlteraPeca()
    {
        await using var db = await Database.Create();
        var proposal = await db.Prepare();
        await using var context = db.Context();
        await context.Pecas.ExecuteUpdateAsync(update => update.SetProperty(x => x.Versao, 2));
        var service = db.Service(context);
        var ex = await Assert.ThrowsAsync<AssistenteIaException>(() => service.AplicarAsync(proposal.Id, Approve(proposal, proposal.Grupos[0].Id), default));
        Assert.Equal("AI_PROPOSAL_STALE", ex.Code);
        await service.RejeitarAsync(proposal.Id, new(1, 1, [proposal.Grupos[0].Id]), default);
        Assert.Equal(2, (await context.Pecas.AsNoTracking().SingleAsync()).Versao);
        Assert.Empty(await context.AssistenteRecibos.ToListAsync());
    }

    [ReviewPostgresFact]
    public async Task CriacaoIdempotente_RecusaOutroPayloadEPermissoesRevogadas()
    {
        await using var db = await Database.Create();
        await using var context = db.Context();
        var service = db.Service(context);
        var conversation = await service.ObterOuCriarAsync(db.PecaId, default);
        var request = Request();
        var proposal = await service.RegistrarAsync(conversation.Id, request, default);
        Assert.Equal(proposal.Id, (await service.RegistrarAsync(conversation.Id, request, default)).Id);
        var ex = await Assert.ThrowsAsync<AssistenteIaException>(() => service.RegistrarAsync(conversation.Id, request with { Instrucao = "Outro pedido" }, default));
        Assert.Equal("AI_IDEMPOTENCY_MISMATCH", ex.Code);
        Assert.Equal(2, await context.AssistenteMensagens.CountAsync());
        db.Settings.ReviewV2Enabled = false;
        ex = await Assert.ThrowsAsync<AssistenteIaException>(() => service.AplicarAsync(proposal.Id, Approve(proposal, proposal.Grupos[0].Id), default));
        Assert.Equal("AI_REVIEW_V2_DISABLED", ex.Code);
        Assert.Equal(proposal.Id, (await service.PropostaAsync(proposal.Id, default)).Id);
    }

    [ReviewPostgresFact]
    public async Task ExclusaoLogicaNaoApagaHistorico_EFkImpedeCascataFisica()
    {
        await using var db = await Database.Create();
        var proposal = await db.Prepare();
        await using var context = db.Context();
        await context.Pecas.ExecuteUpdateAsync(update => update.SetProperty(x => x.DataExclusao, DateTime.UtcNow));
        Assert.Equal(2, (await db.Service(context).MensagensAsync(proposal.ConversaId, null, 50, default)).Count);
        await Assert.ThrowsAsync<PostgresException>(() => context.Pecas.ExecuteDeleteAsync());
        Assert.Equal(1, await context.AssistenteConversas.CountAsync());
    }

    private static RegistrarPropostaRequest Request() => new(Guid.NewGuid(), 1, "Melhore o texto.", "Proposta de revisão.",
        [new("Primeiro", [new(0, 1, [AssistentePatchCompilerTests.Paragraph("Primeiro revisado")])]),
         new("Segundo", [new(1, 1, [AssistentePatchCompilerTests.Paragraph("Segundo revisado")])])]);
    private static AplicarGruposRequest Approve(AssistentePropostaDto p, Guid id) => new(Guid.NewGuid(), p.Revisao, p.VersaoDecisoes,
        p.VersaoPecaAplicavel, [new(id, p.Grupos.Single(x => x.Id == id).PreviewSha256)]);

    private sealed class FailReceipt : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken ct = default)
        {
            if (eventData.Context!.ChangeTracker.Entries<AssistenteAplicacaoRecibo>().Any(e => e.State == EntityState.Added))
                throw new InvalidOperationException("Falha sintética antes do recibo.");
            return base.SavingChangesAsync(eventData, result, ct);
        }
    }

    private sealed class FailAnswer : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken ct = default)
        {
            if (eventData.Context!.ChangeTracker.Entries<AssistenteMensagemPersistida>().Any(e => e.State == EntityState.Modified && e.Entity.Resultado is not null))
                throw new InvalidOperationException("Falha sintética na publicação da resposta.");
            return base.SavingChangesAsync(eventData, result, ct);
        }
    }

    private sealed class Monitor(AssistenteIaSettings settings) : IOptionsMonitor<AssistenteIaSettings>
    {
        public AssistenteIaSettings CurrentValue => settings;
        public AssistenteIaSettings Get(string? name) => settings;
        public IDisposable? OnChange(Action<AssistenteIaSettings, string?> listener) => null;
    }
    private sealed class Features : IAssistenteFeatureAccess
    {
        public bool Edit { get; set; } = true;
        public bool Full { get; set; } = true;
        public Task<AssistenteFeatureSnapshot> ObterAsync(Guid org, CancellationToken ct) => Task.FromResult(new AssistenteFeatureSnapshot(true,
            Full ? [OrganizationFeatureEnum.AI_ASSISTANT, OrganizationFeatureEnum.AI_EDIT, OrganizationFeatureEnum.AI_DOCUMENT_GENERATION] : [OrganizationFeatureEnum.AI_ASSISTANT, OrganizationFeatureEnum.AI_EDIT],
            Edit ? Enum.GetValues<AssistenteModo>() : [AssistenteModo.Perguntar]));
        public Task ExigirModoAsync(Guid org, AssistenteModo mode, CancellationToken ct) =>
            mode != AssistenteModo.Perguntar && !Edit ? Task.FromException(new AssistenteIaException(403, "AI_FEATURE_NOT_ENABLED", "Edição desabilitada.")) : Task.CompletedTask;
    }
    private sealed class Transparency : IAssistenteTransparencyService
    {
        public Task ExigirConfirmacaoAsync(CancellationToken ct) => Task.CompletedTask;
        public Task<AssistenteTransparenciaDto> ObterAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task<AssistenteTransparenciaDto> ConfirmarAsync(ConfirmarAssistenteTransparenciaRequest req, CancellationToken ct) => throw new NotSupportedException();
    }
    private sealed class Database(string connection, string name) : IAsyncDisposable
    {
        public Guid OrgId { get; } = Guid.NewGuid();
        public Guid UserId { get; } = Guid.NewGuid();
        public Guid PecaId { get; } = Guid.NewGuid();
        public AssistenteIaSettings Settings { get; } = new() { ReviewV2Enabled = true, ConversationV2Enabled = true, AutomaticConversationEnabled = true };
        public Features Access { get; } = new();
        public JuridicoAiDbContext Context(IInterceptor? interceptor = null)
        {
            var options = new DbContextOptionsBuilder<JuridicoAiDbContext>().UseNpgsql(connection);
            if (interceptor is not null) options.AddInterceptors(interceptor);
            return new(options.Options);
        }
        public AssistenteWorkspaceService Service(JuridicoAiDbContext context, Guid? user = null) =>
            new(new AssistenteWorkspaceRepository(context), new PecaRepository(context), new TestAuthContext(OrgId, user ?? UserId),
                Access, new Transparency(), new Monitor(Settings), new AssistentePatchCompiler());
        public AssistenteChatService Chat(JuridicoAiDbContext context, Guid? user = null) =>
            new(new AssistenteWorkspaceRepository(context), new PecaRepository(context), new TestAuthContext(OrgId, user ?? UserId),
                Access, new Transparency(), new Monitor(Settings));
        public async Task<AssistentePropostaDto> Prepare()
        {
            await using var ctx = Context();
            var svc = Service(ctx);
            var conversation = await svc.ObterOuCriarAsync(PecaId, default);
            return await svc.RegistrarAsync(conversation.Id, Request(), default);
        }
        public static async Task<Database> Create()
        {
            var admin = Environment.GetEnvironmentVariable("JURIDICOAI_REVIEW_TEST_POSTGRES")!;
            var name = "review_" + Guid.NewGuid().ToString("N");
            await using (var cn = new NpgsqlConnection(admin))
            { await cn.OpenAsync(); await using var cmd = new NpgsqlCommand($"CREATE DATABASE {name}", cn); await cmd.ExecuteNonQueryAsync(); }
            var db = new Database(new NpgsqlConnectionStringBuilder(admin) { Database = name, Pooling = false }.ConnectionString, name);
            await using var context = db.Context();
            await context.Database.MigrateAsync();
            var now = DateTime.UtcNow;
            var caso = new Caso { Id = Guid.NewGuid(), OrganizationId = db.OrgId, CreatedById = db.UserId, UpdatedById = db.UserId,
                Titulo = "Caso fictício", DataCriacao = now, DataAtualizacao = now };
            context.AddRange(new Organization { Id = db.OrgId, Nome = "Escritório teste", DataCriacao = now, DataAtualizacao = now },
                new User { Id = db.UserId, Nome = "Titular", Email = $"{db.UserId:N}@example.test", PasswordHash = "ficticio", DataCriacao = now, DataAtualizacao = now }, caso);
            context.Add(new Peca { Id = db.PecaId, CasoId = caso.Id, OrganizationId = db.OrgId, CreatedById = db.UserId, UpdatedById = db.UserId,
                Titulo = "Peça fictícia", ConteudoJson = JsonDocument.Parse("""{"type":"doc","content":[{"type":"paragraph","content":[{"type":"text","text":"Primeiro"}]},{"type":"paragraph","content":[{"type":"text","text":"Segundo"}]}]}"""),
                PageConfig = JsonDocument.Parse("{}"), Styles = JsonDocument.Parse("{}"), DataCriacao = now, DataAtualizacao = now });
            await context.SaveChangesAsync();
            return db;
        }
        public async ValueTask DisposeAsync()
        {
            await using var cn = new NpgsqlConnection(Environment.GetEnvironmentVariable("JURIDICOAI_REVIEW_TEST_POSTGRES"));
            await cn.OpenAsync();
            await using var cmd = new NpgsqlCommand($"DROP DATABASE {name} WITH (FORCE)", cn);
            await cmd.ExecuteNonQueryAsync();
        }
    }
}
