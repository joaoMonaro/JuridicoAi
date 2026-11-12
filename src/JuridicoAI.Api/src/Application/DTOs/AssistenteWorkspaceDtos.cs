using System.Text.Json;
using System.Text.Json.Serialization;

namespace JuridicoAI.Api.Application.DTOs;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AssistenteSpliceRequest(int StartIndex, int DeleteCount, IReadOnlyList<JsonElement> NovosBlocos);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AssistenteGrupoRequest(string Explicacao, IReadOnlyList<AssistenteSpliceRequest> Operacoes);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RegistrarPropostaRequest(Guid ClientRequestId, int VersaoPecaEsperada,
    string Instrucao, string TextoResposta, IReadOnlyList<AssistenteGrupoRequest> Grupos, Guid? MensagemUsuarioId = null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AprovarGrupoRequest(Guid Id, string PreviewSha256);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AplicarGruposRequest(Guid RequestId, int RevisaoProposta, int VersaoDecisoes,
    int VersaoPecaEsperada, IReadOnlyList<AprovarGrupoRequest> Grupos);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RejeitarGruposRequest(int RevisaoProposta, int VersaoDecisoes, IReadOnlyList<Guid> Grupos);

public sealed record AssistenteConversaDto(Guid Id, Guid PecaId, string Titulo, DateTime CriadaEm);
public sealed record AssistenteMensagemDto(Guid Id, long Sequencia, string Autor, string Texto, Guid? PropostaId, DateTime CriadaEm,
    Guid? ClientRequestId = null, Guid? RespostaAId = null, string Estado = "Concluida", string? CodigoErro = null,
    CriarSolicitacaoAssistenteRequest? Solicitacao = null, AssistenteSolicitacaoConcluidaDto? Resultado = null,
    AssistenteCoberturaDocumentalDto? CoberturaDocumental = null,
    AssistenteContextoHistoricoDto? ContextoHistorico = null, Guid? AnaliseDocumentalId = null);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record EnviarMensagemChatRequest(Guid ClientRequestId, AssistenteModo Modo, string? Instrucao,
    int VersaoPeca, AssistenteOperacao? OperacaoPreferida, AssistenteContextoRequest? Contexto)
{
    public CriarSolicitacaoAssistenteRequest Solicitar() => new(ClientRequestId, Modo, Instrucao, VersaoPeca, OperacaoPreferida, Contexto);
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record EnviarInteracaoLivreRequest(Guid ClientRequestId, string Texto, int VersaoPecaEsperada,
    AssistenteSelecaoRequest? Selecao = null, Guid? PropostaReferenciadaId = null,
    string? ContextoAdicional = null, bool IncluirDadosCaso = false, IReadOnlyList<Guid>? ContextoCasoIds = null, IReadOnlyList<Guid>? AnexoIds = null)
{
    public CriarSolicitacaoAssistenteRequest Solicitar() => new(ClientRequestId, AssistenteModo.Perguntar, Texto,
        VersaoPecaEsperada, AssistenteOperacao.ResponderNoPainel,
        new(new(Selecao is null ? AssistenteEscopoDocumento.Nenhum : AssistenteEscopoDocumento.Selecao,
            Selecao?.TextoOriginal, Selecao: Selecao), ContextoAdicional, IncluirDadosCaso, ContextoCasoIds),
        InteracaoAutomatica: true, PropostaReferenciadaId, AnexoIds);
}
public sealed record AssistenteOperacaoPersistida(string Tipo, int StartIndex, int DeleteCount,
    string ExpectedBlocksSha256, IReadOnlyList<JsonElement> NovosBlocos);
public sealed record AssistenteGrupoPersistido(Guid Id, string Estado, string Explicacao,
    string PreviewSha256, IReadOnlyList<string> AlvosOriginais, IReadOnlyList<JsonElement> Original,
    IReadOnlyList<JsonElement> Proposto, IReadOnlyList<AssistenteOperacaoPersistida> Operacoes);
public sealed record AssistentePropostaDto(string SchemaVersion, Guid Id, Guid ConversaId, Guid SnapshotId,
    int Revisao, int VersaoDecisoes, int VersaoPecaAplicavel, string Estado,
    JsonElement DocumentoBase, IReadOnlyList<AssistenteGrupoPersistido> Grupos, Guid? SubstituidaPorId = null);
public sealed record AssistenteReciboDto(string SchemaVersion, Guid RequestId, Guid PropostaId,
    int VersaoAnterior, int VersaoResultante, int VersaoPecaAtual, bool ResultadoAindaAtual,
    IReadOnlyList<Guid> GruposAplicados, JsonElement ConteudoJson, string DocumentSha256,
    DateTime DataAtualizacao, AssistentePropostaDto PropostaRestante);

public sealed record AssistenteContextoHistoricoDto(int? VersaoMemoria, long AteSequencia, int MensagensRecentes,
    int RegistrosInvalidados, bool HistoricoAdicionalDisponivel);
