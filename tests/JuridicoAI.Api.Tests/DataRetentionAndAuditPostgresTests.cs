using System.Text.Json;
using JuridicoAI.Api.Application.Abstractions;
using JuridicoAI.Api.Domain.Entities;
using JuridicoAI.Api.Infrastructure.Documents;
using JuridicoAI.Api.Infrastructure.Observability;
using JuridicoAI.Api.Infrastructure.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace JuridicoAI.Api.Tests;

public sealed partial class AssistenteWorkspacePostgresTests
{
    [ReviewPostgresFact]
    public async Task RetencaoLgpd_ExpurgaSoftDeletesSessoesEAuditoriaAntigosPreservandoAtivosEConversasVinculadas()
    {
        await using var db = await Database.Create();
        await using var ctx = db.Context();

        var now = DateTime.UtcNow;
        var oldDate = now.AddDays(-45);
        var recentDate = now.AddDays(-5);
        var veryOldAudit = now.AddDays(-400);
        var casoId = (await ctx.Pecas.AsNoTracking().SingleAsync()).CasoId;

        // 1. Peça soft-deleted antiga SEM conversa -> deve ser expurgada
        var oldPecaSemConversa = new Peca
        {
            Id = Guid.NewGuid(), OrganizationId = db.OrgId, CasoId = casoId,
            CreatedById = db.UserId, UpdatedById = db.UserId, Titulo = "Antiga sem conversa",
            ConteudoJson = JsonDocument.Parse("{}"), PageConfig = JsonDocument.Parse("{}"), Styles = JsonDocument.Parse("{}"),
            DataCriacao = oldDate, DataAtualizacao = oldDate, DataExclusao = oldDate
        };

        // 2. Peça soft-deleted antiga COM conversa vinculada -> preservada (FK Restrict)
        var oldPecaComConversa = new Peca
        {
            Id = Guid.NewGuid(), OrganizationId = db.OrgId, CasoId = casoId,
            CreatedById = db.UserId, UpdatedById = db.UserId, Titulo = "Antiga com conversa",
            ConteudoJson = JsonDocument.Parse("{}"), PageConfig = JsonDocument.Parse("{}"), Styles = JsonDocument.Parse("{}"),
            DataCriacao = oldDate, DataAtualizacao = oldDate, DataExclusao = oldDate
        };
        var linkedConversation = new AssistenteConversa
        {
            Id = Guid.NewGuid(), OrganizationId = db.OrgId, UserId = db.UserId, PecaId = oldPecaComConversa.Id,
            Titulo = "Conversa vinculada", CriadaEm = oldDate, AtualizadaEm = oldDate
        };

        // 3. Peça soft-deleted recente (< 30d) -> preservada
        var recentDeletedPeca = new Peca
        {
            Id = Guid.NewGuid(), OrganizationId = db.OrgId, CasoId = casoId,
            CreatedById = db.UserId, UpdatedById = db.UserId, Titulo = "Recente excluída",
            ConteudoJson = JsonDocument.Parse("{}"), PageConfig = JsonDocument.Parse("{}"), Styles = JsonDocument.Parse("{}"),
            DataCriacao = recentDate, DataAtualizacao = recentDate, DataExclusao = recentDate
        };

        // 4. Template soft-deleted antigo não referenciado -> expurgado
        var oldTemplate = new TemplatePeca
        {
            Id = Guid.NewGuid(), OrganizationId = db.OrgId, CreatedById = db.UserId, UpdatedById = db.UserId,
            Nome = "Template antigo", ConteudoJson = JsonDocument.Parse("{}"), PageConfig = JsonDocument.Parse("{}"), Styles = JsonDocument.Parse("{}"),
            DataCriacao = oldDate, DataAtualizacao = oldDate, DataExclusao = oldDate
        };

        // 5. Sessões antigas vs ativa
        var expiredSession = new Session
        {
            Id = Guid.NewGuid(), UserId = db.UserId, OrganizationId = db.OrgId,
            RefreshTokenHash = "expired-hash", RevocationTokenHash = "expired-rev",
            ExpiraEm = oldDate, DataCriacao = oldDate, DataAtualizacao = oldDate
        };
        var activeSession = new Session
        {
            Id = Guid.NewGuid(), UserId = db.UserId, OrganizationId = db.OrgId,
            RefreshTokenHash = "active-hash", RevocationTokenHash = "active-rev",
            ExpiraEm = now.AddDays(10), DataCriacao = now, DataAtualizacao = now
        };
        var expiredPersonalSession = new PersonalHistorySession
        {
            Id = Guid.NewGuid(), UserId = db.UserId,
            RefreshTokenHash = "p-expired-hash", RevocationTokenHash = "p-expired-rev",
            ExpiraEm = oldDate, CriadaEm = oldDate, AtualizadaEm = oldDate
        };

        // 6. Auditoria antiga (> 365d) vs recente
        var oldAudit = new DataAccessAuditLog
        {
            Id = Guid.NewGuid(), OrganizationId = db.OrgId, UserId = db.UserId,
            Action = "ViewEnvolvido", ResourceType = "EnvolvidoCaso", ResourceId = Guid.NewGuid(),
            OccurredAt = veryOldAudit
        };
        var recentAudit = new DataAccessAuditLog
        {
            Id = Guid.NewGuid(), OrganizationId = db.OrgId, UserId = db.UserId,
            Action = "DownloadDocumentoCaso", ResourceType = "DocumentoCaso", ResourceId = Guid.NewGuid(),
            OccurredAt = recentDate
        };

        ctx.AddRange(oldPecaSemConversa, oldPecaComConversa, linkedConversation, recentDeletedPeca,
            oldTemplate, expiredSession, activeSession, expiredPersonalSession, oldAudit, recentAudit);
        await ctx.SaveChangesAsync();

        var processor = new DataRetentionProcessor(ctx, Options.Create(new DataRetentionSettings
        {
            SoftDeleteRetentionDays = 30,
            ExpiredSessionRetentionDays = 30,
            AuditLogRetentionDays = 365
        }));

        var summary = await processor.RunOnceAsync(default);

        Assert.Equal(1, summary.PecasRemoved);
        Assert.Equal(1, summary.TemplatesRemoved);
        Assert.Equal(1, summary.SessionsRemoved);
        Assert.Equal(1, summary.PersonalSessionsRemoved);
        Assert.Equal(1, summary.AuditLogsRemoved);

        Assert.False(await ctx.Pecas.AnyAsync(p => p.Id == oldPecaSemConversa.Id));
        Assert.True(await ctx.Pecas.AnyAsync(p => p.Id == oldPecaComConversa.Id));
        Assert.True(await ctx.Pecas.AnyAsync(p => p.Id == recentDeletedPeca.Id));
        Assert.False(await ctx.TemplatesPeca.AnyAsync(t => t.Id == oldTemplate.Id));
        Assert.True(await ctx.Sessions.AnyAsync(s => s.Id == activeSession.Id));
        Assert.False(await ctx.Sessions.AnyAsync(s => s.Id == expiredSession.Id));
        Assert.Empty(await ctx.Set<PersonalHistorySession>().ToArrayAsync());
        Assert.Single(await ctx.DataAccessAuditLogs.ToArrayAsync());
        Assert.Equal(recentAudit.Id, (await ctx.DataAccessAuditLogs.SingleAsync()).Id);
    }

    [ReviewPostgresFact]
    public async Task AuditoriaAcesso_PersisteMetadadosSemPii()
    {
        await using var db = await Database.Create();
        await using var ctx = db.Context();

        var correlationId = Guid.NewGuid();
        var resourceId = Guid.NewGuid();
        var audit = new DataAccessAuditService(ctx, new TestAuthContext(db.OrgId, db.UserId), new FixedCorrelation(correlationId));

        await audit.RecordAsync("DownloadDocumentoCaso", "DocumentoCaso", resourceId, default);

        var saved = await ctx.DataAccessAuditLogs.AsNoTracking().SingleAsync();
        Assert.Equal(db.OrgId, saved.OrganizationId);
        Assert.Equal(db.UserId, saved.UserId);
        Assert.Equal("DownloadDocumentoCaso", saved.Action);
        Assert.Equal("DocumentoCaso", saved.ResourceType);
        Assert.Equal(resourceId, saved.ResourceId);
        Assert.Equal(correlationId.ToString("D"), saved.CorrelationId);
    }

    private sealed class FixedCorrelation(Guid id) : ICorrelationContext
    {
        public Guid CorrelationId => id;
    }
}
