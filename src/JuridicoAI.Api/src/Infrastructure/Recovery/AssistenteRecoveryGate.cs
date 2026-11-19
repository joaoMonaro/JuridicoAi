using JuridicoAI.Api.Application.Abstractions;
using JuridicoAI.Api.Domain.Entities;
using JuridicoAI.Api.Infrastructure.Database;
using JuridicoAI.Api.Infrastructure.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
namespace JuridicoAI.Api.Infrastructure.Recovery;

public sealed class AssistenteRecoveryStatus { public bool VerifiedAtStartup { get; set; } }
public sealed class AssistenteRecoveryGate(JuridicoAiDbContext db, AssistenteRecoveryStatus status, IOptions<DeletionProtectionSettings> options)
{
    public async Task<bool> IsOpenAsync(CancellationToken ct) => status.VerifiedAtStartup && !options.Value.MaintenanceMode &&
        !await db.Set<AssistenteRestoreState>().AsNoTracking().AnyAsync(s => s.InProgress, ct);
}
// An IHostedService (not BackgroundService): finishes before HTTP/ordinary workers start.
public sealed class AssistenteRecoveryStartup(IServiceScopeFactory scopes, AssistenteRecoveryStatus status, IAssistenteDeletionJournal journal,
    IOptions<DeletionProtectionSettings> options, ILogger<AssistenteRecoveryStartup> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken ct)
    {
        status.VerifiedAtStartup = false;
        if (!journal.Configured) { status.VerifiedAtStartup = !options.Value.MaintenanceMode; return; }
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            status.VerifiedAtStartup = await scope.ServiceProvider.GetRequiredService<AssistenteDeletionRecovery>().IsSafeForTrafficAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception) { /* Keep closed; never log IDs, paths, credentials or journal contents. */ }
        if (!status.VerifiedAtStartup) logger.LogError("AI_RESTORE_REQUIRED; tráfego e workers comuns bloqueados até conferir as exclusões protegidas.");
    }
    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
public sealed class AssistenteRecoveryMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, AssistenteRecoveryGate gate)
    {
        if (context.Request.Path == "/health/live") { await next(context); return; }
        bool open;
        try { open = await gate.IsOpenAsync(context.RequestAborted); }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested) { return; }
        catch (Exception) { open = false; }
        if (!open)
        {
            context.Response.StatusCode = 503; context.Response.Headers.CacheControl = "no-store";
            await context.Response.WriteAsJsonAsync(new { code = "AI_RESTORE_REQUIRED", message = "Ambiente temporariamente em manutenção." }, context.RequestAborted);
            return;
        }
        await next(context);
    }
}
