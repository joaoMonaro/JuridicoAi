using JuridicoAI.Api.Application.Abstractions;
using JuridicoAI.Api.Domain.Entities;
using JuridicoAI.Api.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace JuridicoAI.Api.Infrastructure.Repositories;

public sealed class AssistenteWorkspaceRepository(JuridicoAiDbContext context) : IAssistenteWorkspaceRepository
{
    public async Task<T> EmTransacaoAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(ct);
        try
        {
            var result = await action(ct);
            await context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return result;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            context.ChangeTracker.Clear();
            throw;
        }
    }

    public async Task<AssistenteConversa> ObterOuCriarAsync(Guid organizationId, Guid userId, Guid pecaId, string titulo, CancellationToken ct)
    {
        var id = Guid.NewGuid();
        var agora = DateTime.UtcNow;
        // ON CONFLICT handles two tabs opening the same user's conversation.
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO ai_conversations (id, organization_id, user_id, peca_id, titulo, proxima_sequencia, criada_em, atualizada_em)
            VALUES ({id}, {organizationId}, {userId}, {pecaId}, {titulo}, 1, {agora}, {agora})
            ON CONFLICT (organization_id, user_id, peca_id) DO NOTHING
            """, ct);
        return await context.Set<AssistenteConversa>().AsNoTracking().SingleOrDefaultAsync(
            x => x.OrganizationId == organizationId && x.UserId == userId && x.PecaId == pecaId, ct) ?? throw new JuridicoAI.Api.Application.Exceptions.AssistenteIaException(409, "AI_CONVERSATION_DELETING", "A conversa está em exclusão. Aguarde a conclusão antes de iniciar outra.");
    }

    public async Task<AssistenteConversa?> ObterConversaAsync(Guid id, Guid organizationId, Guid userId, bool bloquear, CancellationToken ct)
    {
        var item = await (bloquear
            ? context.Set<AssistenteConversa>().FromSqlInterpolated($"SELECT * FROM ai_conversations WHERE id = {id} AND organization_id = {organizationId} AND user_id = {userId} FOR UPDATE")
            : context.Set<AssistenteConversa>().Where(x => x.Id == id && x.OrganizationId == organizationId && x.UserId == userId))
        .AsNoTracking().SingleOrDefaultAsync(ct);
        if (bloquear && item is not null)
        {
            var tracked = context.Set<AssistenteConversa>().Local.SingleOrDefault(x => x.Id == item.Id);
            if (tracked is not null) context.Entry(tracked).State = EntityState.Detached;
            context.Attach(item);
        }
        return item;
    }

    public Task<AssistentePropostaPersistida?> ObterPropostaAsync(Guid id, Guid organizationId, Guid userId, CancellationToken ct) =>
        context.Set<AssistentePropostaPersistida>().AsNoTracking().Where(x => x.Id == id && x.OrganizationId == organizationId &&
            context.Set<AssistenteConversa>().Any(c => c.Id == x.ConversaId && c.OrganizationId == organizationId && c.UserId == userId))
            .SingleOrDefaultAsync(ct);

    public Task<AssistentePropostaPersistida?> ObterPorPedidoAsync(Guid conversaId, Guid requestId, CancellationToken ct) =>
        context.Set<AssistentePropostaPersistida>().AsNoTracking().SingleOrDefaultAsync(x => x.ConversaId == conversaId && x.ClientRequestId == requestId, ct);

    public async Task<IReadOnlyList<AssistentePropostaPersistida>> ListarPropostasAsync(Guid conversaId, CancellationToken ct) =>
        await context.Set<AssistentePropostaPersistida>().AsNoTracking().Where(x => x.ConversaId == conversaId)
            .OrderByDescending(x => x.CriadaEm).ThenByDescending(x => x.Id).Take(30).ToListAsync(ct);

    public async Task<IReadOnlyList<AssistenteMensagemPersistida>> ListarMensagensAsync(Guid conversaId, long? antes, int limite, CancellationToken ct) =>
        await context.Set<AssistenteMensagemPersistida>().AsNoTracking().Where(x => x.ConversaId == conversaId && (!antes.HasValue || x.Sequencia < antes.Value))
            .OrderByDescending(x => x.Sequencia).Take(limite).ToListAsync(ct);

    public Task<AssistenteAplicacaoRecibo?> ObterReciboAsync(Guid conversaId, Guid requestId, CancellationToken ct) =>
        context.Set<AssistenteAplicacaoRecibo>().AsNoTracking().SingleOrDefaultAsync(x => x.ConversaId == conversaId && x.RequestId == requestId, ct);

    public Task<AssistenteMensagemPersistida?> ObterPedidoMensagemAsync(Guid conversaId, Guid requestId, CancellationToken ct) =>
        context.AssistenteMensagens.AsNoTracking().SingleOrDefaultAsync(x => x.ConversaId == conversaId && x.ClientRequestId == requestId, ct);
    public Task<AssistenteMensagemPersistida?> ObterMensagemAsync(Guid conversaId, Guid id, CancellationToken ct) =>
        context.AssistenteMensagens.AsNoTracking().SingleOrDefaultAsync(x => x.ConversaId == conversaId && x.Id == id, ct);
    public Task<AssistenteMensagemPersistida?> ObterRespostaAsync(Guid conversaId, Guid mensagemId, CancellationToken ct) =>
        context.AssistenteMensagens.AsNoTracking().SingleOrDefaultAsync(x => x.ConversaId == conversaId && x.RespostaAId == mensagemId, ct);
    public Task<AssistenteMensagemPersistida?> ObterMensagemAtivaAsync(Guid conversaId, CancellationToken ct) =>
        context.AssistenteMensagens.AsNoTracking().SingleOrDefaultAsync(x => x.ConversaId == conversaId && (x.Estado == "Gerando" || x.Estado == "AguardandoAnalise" || x.Estado == "AguardandoGeracao"), ct);
    public async Task<IReadOnlyList<AssistenteMensagemPersistida>> ListarContextoAsync(Guid conversaId, CancellationToken ct) =>
        await context.AssistenteMensagens.AsNoTracking().Where(x => x.ConversaId == conversaId).OrderBy(x => x.Sequencia).Take(1001).ToArrayAsync(ct);

    public void Adicionar(AssistentePropostaPersistida proposta) => context.Add(proposta);
    public void Adicionar(AssistenteMensagemPersistida mensagem) => context.Add(mensagem);
    public void Adicionar(AssistenteAplicacaoRecibo recibo) => context.Add(recibo);
    public void Atualizar(AssistentePropostaPersistida proposta)
    {
        var tracked = context.Set<AssistentePropostaPersistida>().Local.SingleOrDefault(x => x.Id == proposta.Id);
        if (tracked is not null) context.Entry(tracked).State = EntityState.Detached;
        context.Update(proposta);
    }
    public void Atualizar(AssistenteMensagemPersistida mensagem)
    {
        var tracked = context.AssistenteMensagens.Local.SingleOrDefault(x => x.Id == mensagem.Id);
        if (tracked is not null) context.Entry(tracked).State = EntityState.Detached;
        context.Update(mensagem);
    }
}
