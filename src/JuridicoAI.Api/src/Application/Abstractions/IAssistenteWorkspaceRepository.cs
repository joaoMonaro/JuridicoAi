using JuridicoAI.Api.Domain.Entities;

namespace JuridicoAI.Api.Application.Abstractions;

public interface IAssistenteWorkspaceRepository
{
    Task<T> EmTransacaoAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct);
    Task<AssistenteConversa> ObterOuCriarAsync(Guid organizationId, Guid userId, Guid pecaId, string titulo, CancellationToken ct);
    Task<AssistenteConversa?> ObterConversaAsync(Guid id, Guid organizationId, Guid userId, bool bloquear, CancellationToken ct);
    Task<AssistentePropostaPersistida?> ObterPropostaAsync(Guid id, Guid organizationId, Guid userId, CancellationToken ct);
    Task<AssistentePropostaPersistida?> ObterPorPedidoAsync(Guid conversaId, Guid requestId, CancellationToken ct);
    Task<IReadOnlyList<AssistentePropostaPersistida>> ListarPropostasAsync(Guid conversaId, CancellationToken ct);
    Task<IReadOnlyList<AssistenteMensagemPersistida>> ListarMensagensAsync(Guid conversaId, long? antes, int limite, CancellationToken ct);
    Task<AssistenteMensagemPersistida?> ObterPedidoMensagemAsync(Guid conversaId, Guid requestId, CancellationToken ct);
    Task<AssistenteMensagemPersistida?> ObterMensagemAsync(Guid conversaId, Guid id, CancellationToken ct);
    Task<AssistenteMensagemPersistida?> ObterRespostaAsync(Guid conversaId, Guid mensagemId, CancellationToken ct);
    Task<AssistenteMensagemPersistida?> ObterMensagemAtivaAsync(Guid conversaId, CancellationToken ct);
    Task<IReadOnlyList<AssistenteMensagemPersistida>> ListarContextoAsync(Guid conversaId, CancellationToken ct);
    Task<AssistenteAplicacaoRecibo?> ObterReciboAsync(Guid conversaId, Guid requestId, CancellationToken ct);
    void Adicionar(AssistentePropostaPersistida proposta);
    void Adicionar(AssistenteMensagemPersistida mensagem);
    void Adicionar(AssistenteAplicacaoRecibo recibo);
    void Atualizar(AssistentePropostaPersistida proposta);
    void Atualizar(AssistenteMensagemPersistida mensagem);
}
