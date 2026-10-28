using JuridicoAI.Api.Application.Abstractions;
using JuridicoAI.Api.Application.Exceptions;

namespace JuridicoAI.Api.Application.Services;

public sealed record ExportarPecaRequest(string Formato, int Versao);
public sealed record PecaExportFile(byte[] Content, string ContentType, string FileName);

public sealed class PecaExportApplicationService(IPecaRepository repository, IAuthContext auth, PecaDocxGenerator generator, IPecaPdfConverter converter)
{
    public async Task<PecaExportFile> ExportarAsync(Guid id, ExportarPecaRequest request, CancellationToken ct)
    {
        if (request.Formato is not ("docx" or "pdf") || request.Versao < 1)
            throw new RequisicaoInvalidaException("Informe o formato docx ou pdf e uma versão válida da peça.");
        // Uma única consulta captura todos os JSONs, sem reler o modelo original.
        var peca = await repository.ObterPecaAsync(id, auth.OrganizationId, ct)
            ?? throw new RecursoNaoEncontradoException("Peça não encontrada.");
        if (peca.Versao != request.Versao) throw new ConflitoDeVersaoException("A peça foi alterada. Recarregue a versão mais recente antes de exportar.");
        var docx = generator.Generate(peca, ct);
        var content = request.Formato == "pdf" ? await converter.ConvertAsync(docx, ct) : docx;
        return new(content, request.Formato == "pdf" ? "application/pdf" : "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            FileName(peca.Titulo) + "." + request.Formato);
    }

    private static string FileName(string title)
    {
        var name = new string(title.Where(c => !char.IsControl(c) && !"<>:\"/\\|?*".Contains(c)).Take(120).ToArray()).Trim().Trim('.');
        return string.IsNullOrWhiteSpace(name) ? "peca" : name;
    }
}
