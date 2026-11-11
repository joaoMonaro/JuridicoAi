namespace JuridicoAI.Api.Application.DTOs;

public sealed record AssistenteDocumentoProblema(int? Pagina, string Localizador, string Codigo);
public sealed record AssistenteDocumentoDto(Guid Id, string Nome, string Origem, string Formato,
    long TamanhoBytes, string Estado, int? PaginasTotal, int PaginasConcluidas,
    int TrechosConfiaveis, int TrechosExcluidos, string? CodigoErro,
    IReadOnlyList<AssistenteDocumentoProblema> Problemas, bool Vinculado);
public sealed record AssistenteDocumentCapabilities(long ArquivoBytes, int PdfPaginas, int ImagemMegapixels,
    int AnexosPorConversa, long ConversaBytes, bool OcrHabilitado);
public sealed record AssistenteDocumentoContexto(string Texto, string Manifesto, IReadOnlyList<AssistenteAlertaDto> Alertas);
public sealed record AssistenteDocumentoBuscaResultado(Guid DocumentoId, string Nome, string Origem,
    Guid TrechoId, int? Pagina, string Localizador, string Texto);
