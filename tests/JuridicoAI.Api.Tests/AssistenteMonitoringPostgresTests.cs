using JuridicoAI.Api.Application.Services;
using JuridicoAI.Api.Domain.Entities;
using JuridicoAI.Api.Infrastructure.Observability;
using JuridicoAI.Api.Infrastructure.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace JuridicoAI.Api.Tests;

public sealed partial class AssistenteWorkspacePostgresTests
{
    [ReviewPostgresFact]
    public async Task Monitoramento_ConsultaRealAgregaFilasExclusoesEReservasSemAlterarDados()
    {
        await using var db = await Database.Create();
        var model = new GenerationModel { AnswerOnly = true };
        var storage = new DocumentMemoryStorage();
        await QueueGeneration(db, storage, model);
        await using var context = db.Context();
        var now = DateTime.UtcNow;
        var old = now.AddHours(-1);
        await context.Set<AssistenteGeracao>().ExecuteUpdateAsync(s => s.SetProperty(j => j.CriadaEm, old)
            .SetProperty(j => j.AtualizadaEm, now).SetProperty(j => j.LeaseAte, now.AddMinutes(-1)).SetProperty(j => j.PrazoEm, now.AddMinutes(-1)));
        context.Add(new AssistenteExclusao { Id = Guid.NewGuid(), UserId = db.UserId, ConversaId = Guid.NewGuid(), AutorizadaEm = old, DisponivelEm = old });
        var account = new AiMonthlyAccount { Id = Guid.NewGuid(), OrganizationId = db.OrgId, PeriodYear = now.Year,
            PeriodMonth = now.Month, LimitUsd = 5, ConsumedUsd = 3.5m, ReservedUsd = 1, CreatedAt = old, UpdatedAt = now };
        context.Add(account);
        context.Add(new AiUsageReservation { Id = Guid.NewGuid(), AccountId = account.Id, OrganizationId = db.OrgId, UserId = db.UserId,
            SolicitacaoId = Guid.NewGuid(), ClientRequestId = Guid.NewGuid(), CorrelationId = Guid.NewGuid(), AttemptCount = 1,
            PeriodYear = now.Year, PeriodMonth = now.Month, Status = "Indeterminate", Provider = "OpenAI", Model = "fixture",
            PricingVersion = "fixture-v1", ReservedUsd = 1, CreatedAt = old, UpdatedAt = now });
        await context.SaveChangesAsync();
        var collector = new AssistenteMonitoringCollector(context, Options.Create(new AssistenteMonitoringSettings()), new MonitoringClock(new(now)));
        var snapshot = await collector.CollectAsync(default);
        double Read(string metric, string dimension = "", string category = "") => snapshot.Points.Single(p => p.Metric == metric && p.Dimension == dimension && p.Category == category).Value;
        Assert.Equal(1, Read("ai_jobs", "NaFila", "generation"));
        Assert.Equal(1, Read("ai_jobs_stalled", "NaFila", "generation")); // A recent claim is not progress.
        Assert.Equal(1, Read("ai_jobs_deadline_exceeded", "NaFila", "generation"));
        Assert.Equal(1, Read("ai_leases_expired", "NaFila", "generation"));
        Assert.Equal(1, Read("ai_deletions_pending"));
        Assert.Equal(1, Read("ai_deletions_overdue"));
        Assert.Equal(1, Read("ai_reservations_overdue", "Indeterminate"));
        Assert.Equal(0, Read("ai_reservations", "Reserved"));
        Assert.Equal(3.5, Read("ai_monthly_consumed_usd"));
        Assert.Equal(1, Read("ai_monthly_reserved_usd"));
        Assert.Equal(1, Read("ai_budget_organizations", "90"));
        Assert.Equal(0, Read("ai_budget_organizations", "100"));
        Assert.Equal(1, Read("ai_interactions_24h", "active"));
        Assert.Equal("Indeterminate", (await context.AiUsageReservations.AsNoTracking().SingleAsync()).Status);
        Assert.Equal(old, (await context.Set<AssistenteGeracao>().AsNoTracking().SingleAsync()).CriadaEm, TimeSpan.FromMilliseconds(1));
        Assert.Equal(0, model.Calls);
        Assert.All(snapshot.Points, p => Assert.DoesNotContain(db.OrgId.ToString(), p.ToString()));

        // Resolution publishes zeros, including each previously populated state.
        await context.Set<AssistenteGeracao>().ExecuteUpdateAsync(s => s.SetProperty(j => j.Estado, "Concluida"));
        await context.Set<AssistenteExclusao>().ExecuteUpdateAsync(s => s.SetProperty(j => j.Estado, "RemovidaDoAmbienteAtivo"));
        await context.AiUsageReservations.ExecuteUpdateAsync(s => s.SetProperty(j => j.Status, "Released"));
        snapshot = await collector.CollectAsync(default);
        Assert.Equal(0, Read("ai_jobs_stalled", "NaFila", "generation"));
        Assert.Equal(0, Read("ai_reservations_overdue", "Indeterminate"));
        Assert.Equal(0, Read("ai_deletions_pending"));
    }

    [ReviewPostgresFact]
    public async Task Monitoramento_AvancoRealDistingueTrabalhoAntigoDeTrabalhoParado()
    {
        await using var db = await Database.Create();
        await QueueGeneration(db, new DocumentMemoryStorage(), new GenerationModel { AnswerOnly = true });
        await using var context = db.Context();
        var now = DateTime.UtcNow;
        await context.Set<AssistenteGeracao>().ExecuteUpdateAsync(s => s.SetProperty(j => j.CriadaEm, now.AddHours(-1))
            .SetProperty(j => j.UltimoProgressoEm, now).SetProperty(j => j.PrazoEm, now.AddMinutes(10)));
        var snapshot = await new AssistenteMonitoringCollector(context, Options.Create(new AssistenteMonitoringSettings()), new MonitoringClock(new(now))).CollectAsync(default);
        Assert.Equal(0, snapshot.Points.Single(p => p.Metric == "ai_jobs_stalled" && p.Category == "generation" && p.Dimension == "NaFila").Value);
        Assert.InRange(snapshot.Points.Single(p => p.Metric == "ai_job_oldest_age_seconds" && p.Category == "generation" && p.Dimension == "NaFila").Value, 3599, 3601);
    }

    [ReviewPostgresFact]
    public async Task Monitoramento_ReagendamentoPorCotaNaoAtualizaProgressoMasCheckpointAtualiza()
    {
        await using var db = await Database.Create();
        var storage = new DocumentMemoryStorage(); var model = new GenerationModel { AnswerOnly = true };
        await QueueGeneration(db, storage, model);
        var gate = new LocalCapacityGate();
        DateTime? progress;
        await using (var context = db.Context())
        {
            Assert.True(await GenerationServices(db, context, storage, model, gate).Processor.ProcessNextAsync(default));
            progress = (await context.Set<AssistenteGeracao>().AsNoTracking().SingleAsync()).UltimoProgressoEm;
            Assert.NotNull(progress); // Saving the initial context is a real checkpoint.
            await context.Set<AssistenteGeracao>().ExecuteUpdateAsync(s => s.SetProperty(j => j.LeaseAte, DateTime.UtcNow.AddSeconds(-1)));
        }
        await using (var context = db.Context())
        {
            Assert.True(await GenerationServices(db, context, storage, model, gate).Processor.ProcessNextAsync(default));
            Assert.Equal(progress, (await context.Set<AssistenteGeracao>().AsNoTracking().SingleAsync()).UltimoProgressoEm);
        }
        gate.Open = true;
        await using (var context = db.Context())
            await context.Set<AssistenteGeracao>().ExecuteUpdateAsync(s => s.SetProperty(j => j.LeaseAte, DateTime.UtcNow.AddSeconds(-1)));
        await DrainGeneration(db, storage, model);
        await using var verify = db.Context();
        Assert.NotNull((await verify.Set<AssistenteGeracao>().SingleAsync()).UltimoProgressoEm);
    }
}
