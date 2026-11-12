using System.Net;
using System.Text;
using System.Text.Json;
using JuridicoAI.Api.Application.Abstractions;
using JuridicoAI.Api.Application.DTOs;
using JuridicoAI.Api.Application.Exceptions;
using JuridicoAI.Api.Application.Services;
using JuridicoAI.Api.Domain.Entities;
using JuridicoAI.Api.Infrastructure.AI;
using JuridicoAI.Api.Infrastructure.Database;
using JuridicoAI.Api.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace JuridicoAI.Api.Tests;

public sealed partial class AssistenteWorkspacePostgresTests
{
    [ReviewPostgresFact]
    public async Task Memoria_MaisDeMilMensagensCompactaBuscaOriginalEReabreSemApagarHistorico()
    {
        await using var database = await Database.Create(); await using var context = database.Context();
        var conversation = await database.Service(context).ObterOuCriarAsync(database.PecaId, default);
        await SeedHistory(database, context, conversation.Id, 502);
        var chat = MemoryChat(database, context);
        var request = Free("Qual o valor pago por Helena?");
        var start = await chat.IniciarAsync(conversation.Id, request, default);
        Assert.True(start.Historico!.Pendente); Assert.True(start.Historico.Estado.HistoricoAdicionalDisponivel);
        Assert.True(start.Contexto.Length < 40_000); Assert.DoesNotContain("Helena", start.Contexto);
        var tools = chat.CriarFerramentasDocumentais(start, (_, _) => Task.CompletedTask)!;
        Assert.False(tools.HasDocumentTools); Assert.Equal("atualizar_memoria", tools.RequiredTool!());
        var events = new List<AssistenteProviderCall>(); tools.RecordCall = call => { events.Add(call); return Task.CompletedTask; };
        tools.InitialInputTokens = 100;
        var providerCalls = 0;
        var provider = new OpenAiAssistenteIaProvider(new HttpClient(new MemoryHandler(async message =>
        {
            if (message.RequestUri!.AbsolutePath.EndsWith("input_tokens")) return new(HttpStatusCode.OK) { Content = new StringContent("{\"input_tokens\":100}") };
            using var body = JsonDocument.Parse(await message.Content!.ReadAsStringAsync());
            providerCalls++;
            if (providerCalls == 1)
            {
                Assert.Equal("atualizar_memoria", body.RootElement.GetProperty("tool_choice").GetProperty("name").GetString());
                return MemoryStream(null, [new { type = "function_call", name = "atualizar_memoria", call_id = "mem_1", arguments = Summary(1, "A contratante é Ana; valor de R$ 500,00.") }]);
            }
            Assert.Equal("auto", body.RootElement.GetProperty("tool_choice").GetString());
            if (providerCalls == 2) return MemoryStream(null, [new { type = "function_call", name = "consultar_historico", call_id = "hist_1", arguments = Search("Helena") }]);
            Assert.Contains("876,54", body.RootElement.ToString());
            return MemoryStream("""{"titulo":"Valor","texto":"Helena pagou R$ 876,54, conforme a mensagem 777 do usuário.","resumo":"","intencao":"Responder","alertas":[],"grupos":[],"substituirPropostaReferenciada":false}""", []);
        })) { BaseAddress = new("https://synthetic.test/v1/") }, Options.Create(database.Settings));
        var result = await provider.GerarAsync(new("Política" + tools.AdditionalInstructions, start.Contexto, AssistenteModo.Perguntar,
            [AssistenteOperacao.ResponderNoPainel], 1000, true, false, tools), default);
        Assert.Equal(3, providerCalls); Assert.Equal(3, events.Count); Assert.Equal(30, result.EntradaTokens);
        await chat.ConcluirLivreAsync(start, ChatResult(start.Pedido.Id, result.Texto) with { RascunhoAutomatico = new(AssistenteIntencao.Responder, [], false) }, default);
        Assert.Equal(1006, await context.AssistenteMensagens.CountAsync());
        Assert.Single(await context.Set<AssistenteMemoria>().ToArrayAsync());
        Assert.False((await chat.IniciarAsync(conversation.Id, request, default)).Novo);
        Assert.Equal(1006, await context.AssistenteMensagens.CountAsync());
        await using var reopened = database.Context();
        var next = await MemoryChat(database, reopened).IniciarAsync(conversation.Id, Free("Continue."), default);
        Assert.Equal(1, next.Historico!.Estado.VersaoMemoria); Assert.Contains("500,00", next.Contexto);
        var history = await database.Service(reopened).MensagensAsync(conversation.Id, null, 50, default);
        Assert.Contains(history, m => m.ContextoHistorico?.VersaoMemoria == 1);
        Assert.Equal(1, (await reopened.Pecas.SingleAsync()).Versao);
    }

    [ReviewPostgresFact]
    public async Task Memoria_CorrecaoRecentePrevaleceEProximaVersaoReferenciaOriginalCorrigido()
    {
        await using var database = await Database.Create(); await using var context = database.Context();
        var conversation = await database.Service(context).ObterOuCriarAsync(database.PecaId, default);
        await SeedHistory(database, context, conversation.Id, 5);
        var chat = MemoryChat(database, context);
        var first = await chat.IniciarAsync(conversation.Id, Free("Corrigindo: a contratante é Beatriz e o valor correto é R$ 750,00."), default);
        var tools = chat.CriarFerramentasDocumentais(first, (_, _) => Task.CompletedTask)!;
        await tools.ExecuteAsync("atualizar_memoria", Summary(1, "A contratante é Ana; valor de R$ 500,00."), default);
        await chat.ConcluirAsync(first, ChatResult(first.Pedido.Id, "Correção recebida."), default);
        var second = await chat.IniciarAsync(conversation.Id, Free("Elabore a minuta."), default);
        Assert.Contains("Beatriz", second.Contexto); Assert.Contains("750,00", second.Contexto);
        Assert.Contains("Ana", second.Contexto); // Original claim is preserved; policy gives the later correction precedence.
        await chat.ConcluirAsync(second, ChatResult(second.Pedido.Id, "Rascunho ainda não aprovado."), default);
        await SeedHistory(database, context, conversation.Id, 4, append: true);
        var third = await chat.IniciarAsync(conversation.Id, Free("Continue com o valor corrigido."), default);
        var correction = third.Historico!.Lote.Single(e => e.Sequencia == first.Pedido.Sequencia);
        await chat.CriarFerramentasDocumentais(third, (_, _) => Task.CompletedTask)!.ExecuteAsync("atualizar_memoria",
            Summary(correction.Sequencia, "Corrigindo: a contratante é Beatriz e o valor correto é R$ 750,00.", "Correcao"), default);
        var versions = await context.Set<AssistenteMemoria>().AsNoTracking().OrderBy(m => m.Versao).ToArrayAsync();
        Assert.Equal(2, versions.Length); Assert.Contains("Ana", versions[0].Registros.RootElement.ToString());
        Assert.Contains("Beatriz", versions[1].Registros.RootElement.ToString());
        Assert.Equal(first.Pedido.Id.ToString(), versions[1].Registros.RootElement[0].GetProperty("origens")[0].GetProperty("mensagemId").GetString());
    }

    [ReviewPostgresFact]
    public async Task Memoria_RecusaCitacaoInventadaSequenciaFuturaEAssistenteComoFato()
    {
        await using var database = await Database.Create(); await using var context = database.Context();
        var conversation = await database.Service(context).ObterOuCriarAsync(database.PecaId, default);
        await SeedHistory(database, context, conversation.Id, 5);
        var chat = MemoryChat(database, context); var start = await chat.IniciarAsync(conversation.Id, Free("Continue."), default);
        var tools = chat.CriarFerramentasDocumentais(start, (_, _) => Task.CompletedTask)!;
        foreach (var args in new[] { Summary(1, "Citação inexistente"), Summary(start.Pedido.Sequencia, "Continue."), Summary(2, "Rascunho número 0.") })
            Assert.Equal("AI_HISTORY_TOOL_INVALID", (await Assert.ThrowsAsync<AssistenteIaException>(() => tools.ExecuteAsync("atualizar_memoria", args, default))).Code);
        Assert.Empty(await context.Set<AssistenteMemoria>().ToArrayAsync());
        await tools.ExecuteAsync("atualizar_memoria", Summary(2, "Rascunho número 0.", "Rascunho"), default);
        Assert.False(start.Historico!.Pendente);
        Assert.Equal("AI_HISTORY_TOOL_INVALID", (await Assert.ThrowsAsync<AssistenteIaException>(() => tools.ExecuteAsync("atualizar_memoria", Summary(1, "A contratante é Ana; valor de R$ 500,00."), default))).Code);
    }

    [ReviewPostgresFact]
    public async Task Memoria_MudancaDaPecaInvalidaRascunhoDerivadoPreservandoDeclaracaoDoUsuario()
    {
        await using var database = await Database.Create(); await using var context = database.Context();
        var conversation = await database.Service(context).ObterOuCriarAsync(database.PecaId, default);
        await SeedHistory(database, context, conversation.Id, 5);
        var chat = MemoryChat(database, context); var start = await chat.IniciarAsync(conversation.Id, Free("Continue."), default);
        var summary = JsonSerializer.Serialize(new { registros = new object[] {
            new { categoria = "Rascunho", texto = "Rascunho do assistente", origens = new[] { new { sequencia = 2, citacao = "Rascunho número 0." } } },
            new { categoria = "FatoInformado", texto = "Ana informou R$ 500,00", origens = new[] { new { sequencia = 1, citacao = "A contratante é Ana; valor de R$ 500,00." } } } } });
        await chat.CriarFerramentasDocumentais(start, (_, _) => Task.CompletedTask)!.ExecuteAsync("atualizar_memoria", summary, default);
        await chat.ConcluirAsync(start, ChatResult(start.Pedido.Id, "Entendido."), default);
        await context.Pecas.ExecuteUpdateAsync(s => s.SetProperty(p => p.Versao, 2));
        var next = await chat.IniciarAsync(conversation.Id, Free("Continue.") with { VersaoPeca = 2 }, default);
        Assert.Equal(1, next.Historico!.Estado.RegistrosInvalidados);
        Assert.DoesNotContain("Rascunho do assistente", next.Contexto); Assert.Contains("Ana informou", next.Contexto);
        Assert.Contains("Rascunho número 0.", (await context.AssistenteMensagens.SingleAsync(m => m.Sequencia == 2)).Texto);
    }

    [ReviewPostgresFact]
    public async Task Memoria_ReprocessarFonteInterrompeReutilizacaoAntesDaProximaChamada()
    {
        await using var database = await Database.Create(); await using var context = database.Context();
        var storage = new DocumentMemoryStorage(); var docs = RetrievalDocuments(database, context, storage);
        var conversation = await database.Service(context).ObterOuCriarAsync(database.PecaId, default);
        var source = await Upload(docs, conversation.Id, "fonte.txt", "Beatriz pagou R$ 876,54.");
        await using (var processing = database.Context()) await Processor(processing, storage).ProcessNextAsync(default);
        var legacy = RetrievalChat(database, context, docs);
        var first = await legacy.IniciarAsync(conversation.Id, new EnviarInteracaoLivreRequest(Guid.NewGuid(), "Leia.", 1, AnexoIds: [source.Id]).Solicitar(), default);
        await legacy.ConcluirAsync(first, ChatResult(first.Pedido.Id, "Beatriz pagou R$ 876,54."), default);
        await SeedHistory(database, context, conversation.Id, 5, append: true);
        var chat = MemoryChat(database, context, docs); var start = await chat.IniciarAsync(conversation.Id, Free("Continue."), default);
        var tools = chat.CriarFerramentasDocumentais(start, (_, _) => Task.CompletedTask)!;
        await tools.ExecuteAsync("atualizar_memoria", Summary(first.Resposta.Sequencia, "Beatriz pagou R$ 876,54.", "Rascunho"), default);
        await docs.RetryAsync(conversation.Id, source.Id, default);
        Assert.Equal("AI_HISTORY_SOURCE_CHANGED", (await Assert.ThrowsAsync<AssistenteIaException>(() => tools.BeforeCall!(default))).Code);
        await chat.FalharAsync(start, "AI_HISTORY_SOURCE_CHANGED", false);
        await using (var processing = database.Context()) await Processor(processing, storage).ProcessNextAsync(default);
        var next = await chat.IniciarAsync(conversation.Id, Free("E agora?"), default);
        Assert.Equal(1, next.Historico!.Estado.RegistrosInvalidados);
        Assert.DoesNotContain("Beatriz pagou", next.Historico.Dados.GetProperty("memoria").ToString());
    }

    [ReviewPostgresFact]
    public async Task Memoria_CancelamentoPreservaCheckpointEMensagensMasImpedeNovasFerramentas()
    {
        await using var database = await Database.Create(); await using var context = database.Context();
        var conversation = await database.Service(context).ObterOuCriarAsync(database.PecaId, default);
        await SeedHistory(database, context, conversation.Id, 5);
        var chat = MemoryChat(database, context); var start = await chat.IniciarAsync(conversation.Id, Free("Continue."), default);
        var tools = chat.CriarFerramentasDocumentais(start, (_, _) => Task.CompletedTask)!;
        await tools.ExecuteAsync("atualizar_memoria", Summary(1, "A contratante é Ana; valor de R$ 500,00."), default);
        await chat.FalharAsync(start, "AI_CANCELLED", true);
        Assert.Equal("AI_HISTORY_SOURCE_CHANGED", (await Assert.ThrowsAsync<AssistenteIaException>(() => tools.ExecuteAsync("consultar_historico", Search("Ana"), default))).Code);
        var response = await context.AssistenteMensagens.AsNoTracking().SingleAsync(m => m.Id == start.Resposta.Id);
        Assert.Equal("Cancelada", response.Estado); Assert.Equal(1, AssistenteChatService.Dto(response).ContextoHistorico!.VersaoMemoria);
        Assert.Equal(12, await context.AssistenteMensagens.CountAsync());
    }

    [ReviewPostgresFact]
    public async Task Memoria_HistoricoCongeladoNaoVazaOutroUsuarioENaoInterpretaPercentualComoCuringa()
    {
        await using var database = await Database.Create(); await using var context = database.Context();
        var conversation = await database.Service(context).ObterOuCriarAsync(database.PecaId, default);
        await SeedHistory(database, context, conversation.Id, 5);
        var chat = MemoryChat(database, context); var start = await chat.IniciarAsync(conversation.Id, Free("Continue."), default);
        var tools = chat.CriarFerramentasDocumentais(start, (_, _) => Task.CompletedTask)!;
        await tools.ExecuteAsync("atualizar_memoria", Summary(1, "A contratante é Ana; valor de R$ 500,00."), default);
        await SeedHistory(database, context, conversation.Id, 1, append: true, special: "Futuro confidencial.");
        var output = await tools.ExecuteAsync("consultar_historico", Search("Futuro"), default);
        Assert.Empty(JsonDocument.Parse(output).RootElement.GetProperty("mensagens").EnumerateArray());
        var wildcard = await tools.ExecuteAsync("consultar_historico", Search("%"), default);
        Assert.Empty(JsonDocument.Parse(wildcard).RootElement.GetProperty("mensagens").EnumerateArray());
        var denied = await Assert.ThrowsAsync<AssistenteIaException>(() => MemoryChat(database, context, user: Guid.NewGuid()).IniciarAsync(conversation.Id, Free("Ana"), default));
        Assert.Equal(404, denied.StatusCode);
    }

    [ReviewPostgresFact]
    public async Task Memoria_ConversaCurtaEJanelaAmortizadaNaoExigemResumoACadaMensagem()
    {
        await using var database = await Database.Create(); await using var context = database.Context();
        var conversation = await database.Service(context).ObterOuCriarAsync(database.PecaId, default);
        await SeedHistory(database, context, conversation.Id, 2);
        var chat = MemoryChat(database, context); var start = await chat.IniciarAsync(conversation.Id, Free("Continue."), default);
        Assert.False(start.Historico!.Pendente); Assert.Equal(4, start.Historico.Estado.MensagensRecentes);
        Assert.Null(chat.CriarFerramentasDocumentais(start, (_, _) => Task.CompletedTask)!.RequiredTool!());
        await chat.ConcluirAsync(start, ChatResult(start.Pedido.Id, "Entendido."), default);
        var next = await chat.IniciarAsync(conversation.Id, Free("Continue."), default);
        Assert.False(next.Historico!.Pendente); Assert.Equal(6, next.Historico.Estado.MensagensRecentes);
        database.Settings.HistoryMemoryEnabled = false;
        Assert.Equal("AI_HISTORY_MEMORY_DISABLED", (await Assert.ThrowsAsync<AssistenteIaException>(() => chat.CriarFerramentasDocumentais(next, (_, _) => Task.CompletedTask)!.BeforeCall!(default))).Code);
    }

    [ReviewPostgresFact]
    public async Task Memoria_FalhaNaPersistenciaNaoSalvaVersaoNemConfirmaCheckpoint()
    {
        await using var database = await Database.Create(); await using var setup = database.Context();
        var conversation = await database.Service(setup).ObterOuCriarAsync(database.PecaId, default);
        await SeedHistory(database, setup, conversation.Id, 5);
        await using var failing = database.Context(new FailMemory());
        var chat = MemoryChat(database, failing); var start = await chat.IniciarAsync(conversation.Id, Free("Continue."), default);
        await Assert.ThrowsAsync<InvalidOperationException>(() => chat.CriarFerramentasDocumentais(start, (_, _) => Task.CompletedTask)!
            .ExecuteAsync("atualizar_memoria", Summary(1, "A contratante é Ana; valor de R$ 500,00."), default));
        Assert.True(start.Historico!.Pendente);
        await using var verify = database.Context();
        Assert.Empty(await verify.Set<AssistenteMemoria>().ToArrayAsync());
        var response = await verify.AssistenteMensagens.SingleAsync(m => m.Id == start.Resposta.Id);
        Assert.Null(AssistenteChatService.Dto(response).ContextoHistorico!.VersaoMemoria);
        Assert.Equal(12, await verify.AssistenteMensagens.CountAsync());
    }

    [ReviewPostgresFact]
    public async Task Memoria_ConsultaPorIntervaloDistinguePropostaRejeitadaDeAplicacao()
    {
        await using var database = await Database.Create(); await using var context = database.Context();
        var conversation = await database.Service(context).ObterOuCriarAsync(database.PecaId, default);
        var originalChat = database.Chat(context);
        var first = await originalChat.IniciarAsync(conversation.Id, Free("Melhore o texto."), default);
        var proposal = (await originalChat.ConcluirLivreAsync(first, AutomaticResult(first.Pedido.Id, Request().Grupos), default)).Proposta!;
        await database.Service(context).RejeitarAsync(proposal.Id, new RejeitarGruposRequest(proposal.Revisao,
            proposal.VersaoDecisoes, proposal.Grupos.Select(g => g.Id).ToArray()), default);
        await SeedHistory(database, context, conversation.Id, 5, append: true);
        var chat = MemoryChat(database, context); var start = await chat.IniciarAsync(conversation.Id, Free("O que foi decidido?"), default);
        var tools = chat.CriarFerramentasDocumentais(start, (_, _) => Task.CompletedTask)!;
        await tools.ExecuteAsync("atualizar_memoria", Summary(1, "Melhore o texto."), default);
        var output = await tools.ExecuteAsync("consultar_historico", Search(null, 0, 2), default);
        Assert.Contains("Rejeitado", output); Assert.Contains("decisoes", output);
        Assert.Equal(1, (await context.Pecas.AsNoTracking().SingleAsync()).Versao);
    }

    private sealed class FailMemory : Microsoft.EntityFrameworkCore.Diagnostics.SaveChangesInterceptor
    {
        public override ValueTask<Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<int>> SavingChangesAsync(
            Microsoft.EntityFrameworkCore.Diagnostics.DbContextEventData eventData,
            Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<int> result, CancellationToken ct = default)
        {
            if (eventData.Context!.ChangeTracker.Entries<AssistenteMemoria>().Any(e => e.State == EntityState.Added))
                throw new InvalidOperationException("Falha sintética ao persistir memória.");
            return base.SavingChangesAsync(eventData, result, ct);
        }
    }

    private static AssistenteChatService MemoryChat(Database database, JuridicoAiDbContext context, AssistenteDocumentService? docs = null, Guid? user = null)
    {
        database.Settings.HistoryMemoryEnabled = true; database.Settings.HistoryRecentMessages = 2; database.Settings.HistoryCompactionMessages = 8;
        var repository = new AssistenteWorkspaceRepository(context); var pieces = new PecaRepository(context);
        var auth = new TestAuthContext(database.OrgId, user ?? database.UserId); var settings = new Monitor(database.Settings);
        var history = new AssistenteHistoryService(context, repository, pieces, auth, database.Access, new Transparency(), settings, docs);
        return new(repository, pieces, auth, database.Access, new Transparency(), settings, docs, history);
    }
    private static CriarSolicitacaoAssistenteRequest Free(string text) => ChatRequest(text) with { InteracaoAutomatica = true };
    private static string Summary(long sequence, string quote, string category = "FatoInformado") => JsonSerializer.Serialize(new
    { registros = new[] { new { categoria = category, texto = quote, origens = new[] { new { sequencia = sequence, citacao = quote } } } } });
    private static string Search(string? query, long after = 0, long? through = null, int count = 12) => JsonSerializer.Serialize(new
    { consulta = query, aposSequencia = after, ateSequencia = through, limite = count });
    private static async Task SeedHistory(Database database, JuridicoAiDbContext context, Guid id, int turns, bool append = false, string? special = null)
    {
        var conversation = await context.AssistenteConversas.SingleAsync(c => c.Id == id);
        if (!append) Assert.Equal(1, conversation.ProximaSequencia);
        for (var i = 0; i < turns; i++)
        {
            var sequence = conversation.ProximaSequencia++;
            var text = special ?? (sequence == 1 ? "A contratante é Ana; valor de R$ 500,00." : sequence == 777 ? "Helena pagou R$ 876,54." : $"Informação histórica número {sequence}.");
            var user = new AssistenteMensagemPersistida { Id = Guid.NewGuid(), ConversaId = id, OrganizationId = database.OrgId, Sequencia = sequence,
                Autor = "Usuario", Texto = text, Solicitacao = AssistentePatchCompiler.Json(ChatRequest(text)), CriadaEm = DateTime.UtcNow };
            context.Add(user);
            context.Add(new AssistenteMensagemPersistida { Id = Guid.NewGuid(), ConversaId = id, OrganizationId = database.OrgId,
                Sequencia = conversation.ProximaSequencia++, Autor = "Assistente", Texto = $"Rascunho número {i}.", RespostaAId = user.Id, CriadaEm = DateTime.UtcNow });
        }
        await context.SaveChangesAsync(); context.ChangeTracker.Clear();
    }
    private static HttpResponseMessage MemoryStream(string? text, object[] output)
    {
        var delta = text is null ? "" : "data: " + JsonSerializer.Serialize(new { type = "response.output_text.delta", delta = text }) + "\n\n";
        var completed = JsonSerializer.Serialize(new { type = "response.completed", response = new { status = "completed", model = "fixture",
            output, usage = new { input_tokens = 10, output_tokens = 5, input_tokens_details = new { cached_tokens = 0 } } } });
        return new(HttpStatusCode.OK) { Content = new StringContent(delta + "data: " + completed + "\n\n", Encoding.UTF8, "text/event-stream") };
    }
    private sealed class MemoryHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> send) : HttpMessageHandler
    { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => send(request); }
}
