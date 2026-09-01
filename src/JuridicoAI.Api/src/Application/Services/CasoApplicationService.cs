using JuridicoAI.Api.Application.Abstractions;
using JuridicoAI.Api.Application.DTOs;
using JuridicoAI.Api.Application.Exceptions;
using JuridicoAI.Api.Domain.Entities;
using JuridicoAI.Api.Infrastructure.Settings;
using Microsoft.Extensions.Options;

namespace JuridicoAI.Api.Application.Services;

public sealed class CasoApplicationService(
    ICasoRepository repository,
    IFileStorage fileStorage,
    IAuthContext auth,
    IOptions<DocumentStorageSettings> storageOptions,
    ILogger<CasoApplicationService> logger)
{
    private readonly DocumentStorageSettings _storageSettings = storageOptions.Value;

    public Task<IReadOnlyList<Caso>> ListarCasosAsync(
        CasoStatus? status,
        CancellationToken cancellationToken) =>
        repository.ListarCasosAsync(auth.OrganizationId, status, cancellationToken);

    public async Task<Caso> ObterCasoAsync(Guid id, CancellationToken cancellationToken) =>
        await repository.ObterCasoAsync(id, auth.OrganizationId, cancellationToken)
        ?? throw NaoEncontrado("Caso", id);

    public async Task<Caso> CriarCasoAsync(CriarCasoRequest request, CancellationToken cancellationToken)
    {
        ValidarTextoObrigatorio(request.Titulo, "titulo", 200);
        ValidarTamanho(request.Descricao, "descricao", 4000);

        var agora = DateTime.UtcNow;
        return await repository.CriarCasoAsync(new Caso
        {
            Id = Guid.NewGuid(),
            OrganizationId = auth.OrganizationId,
            CreatedById = auth.UserId,
            UpdatedById = auth.UserId,
            Titulo = request.Titulo.Trim(),
            Descricao = NormalizarOpcional(request.Descricao),
            Status = CasoStatus.Ativo,
            DataCriacao = agora,
            DataAtualizacao = agora
        }, cancellationToken);
    }

    public async Task<Caso> AtualizarCasoAsync(
        Guid id,
        AtualizarCasoRequest request,
        CancellationToken cancellationToken)
    {
        ValidarTextoObrigatorio(request.Titulo, "titulo", 200);
        ValidarTamanho(request.Descricao, "descricao", 4000);
        ValidarEnum(request.Status, "status");
        var existente = await ObterCasoAsync(id, cancellationToken);
        existente.Titulo = request.Titulo.Trim();
        existente.Descricao = NormalizarOpcional(request.Descricao);
        existente.Status = request.Status!.Value;
        existente.UpdatedById = auth.UserId;
        existente.DataAtualizacao = DateTime.UtcNow;
        if (!await repository.AtualizarCasoAsync(existente, cancellationToken))
            throw NaoEncontrado("Caso", id);
        return existente;
    }

    public async Task ArquivarCasoAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!await repository.ArquivarCasoAsync(id, auth.OrganizationId, auth.UserId, DateTime.UtcNow, cancellationToken))
            throw NaoEncontrado("Caso", id);
    }

    public async Task<EnvolvidoCaso> CriarEnvolvidoAsync(
        Guid casoId,
        CriarEnvolvidoCasoRequest request,
        CancellationToken cancellationToken)
    {
        await GarantirCasoAsync(casoId, cancellationToken);
        ValidarTextoObrigatorio(request.Nome, "nome", 200);
        ValidarEnum(request.Tipo, "tipo");
        ValidarTamanho(request.Documento, "documento", 50);
        ValidarDadosAdicionais(request.DadosAdicionais);
        var agora = DateTime.UtcNow;
        return await repository.CriarEnvolvidoAsync(new EnvolvidoCaso
        {
            Id = Guid.NewGuid(),
            CasoId = casoId,
            Nome = request.Nome.Trim(),
            Tipo = request.Tipo!.Value,
            Documento = NormalizarOpcional(request.Documento),
            DadosAdicionais = request.DadosAdicionais,
            DataCriacao = agora,
            DataAtualizacao = agora
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<EnvolvidoCaso>> ListarEnvolvidosAsync(
        Guid casoId, CancellationToken cancellationToken)
    {
        await GarantirCasoAsync(casoId, cancellationToken);
        return await repository.ListarEnvolvidosAsync(casoId, auth.OrganizationId, cancellationToken);
    }

    public async Task<EnvolvidoCaso> ObterEnvolvidoAsync(
        Guid casoId, Guid id, CancellationToken cancellationToken)
    {
        await GarantirCasoAsync(casoId, cancellationToken);
        return await repository.ObterEnvolvidoAsync(casoId, id, auth.OrganizationId, cancellationToken)
            ?? throw NaoEncontrado("Envolvido", id);
    }

    public async Task<EnvolvidoCaso> AtualizarEnvolvidoAsync(
        Guid casoId, Guid id, AtualizarEnvolvidoCasoRequest request, CancellationToken cancellationToken)
    {
        ValidarTextoObrigatorio(request.Nome, "nome", 200);
        ValidarEnum(request.Tipo, "tipo");
        ValidarTamanho(request.Documento, "documento", 50);
        ValidarDadosAdicionais(request.DadosAdicionais);
        var existente = await ObterEnvolvidoAsync(casoId, id, cancellationToken);
        existente.Nome = request.Nome.Trim();
        existente.Tipo = request.Tipo!.Value;
        existente.Documento = NormalizarOpcional(request.Documento);
        existente.DadosAdicionais = request.DadosAdicionais;
        existente.DataAtualizacao = DateTime.UtcNow;
        if (!await repository.AtualizarEnvolvidoAsync(existente, cancellationToken))
            throw NaoEncontrado("Envolvido", id);
        return existente;
    }

    public async Task RemoverEnvolvidoAsync(Guid casoId, Guid id, CancellationToken cancellationToken)
    {
        await GarantirCasoAsync(casoId, cancellationToken);
        if (!await repository.RemoverEnvolvidoAsync(casoId, id, auth.OrganizationId, cancellationToken))
            throw NaoEncontrado("Envolvido", id);
    }

    public async Task<ContextoCaso> CriarContextoAsync(
        Guid casoId, CriarContextoCasoRequest request, CancellationToken cancellationToken)
    {
        await GarantirCasoAsync(casoId, cancellationToken);
        ValidarEnum(request.Tipo, "tipo");
        ValidarTextoObrigatorio(request.Conteudo, "conteudo", 50_000);
        ValidarTamanho(request.Titulo, "titulo", 200);
        var agora = DateTime.UtcNow;
        return await repository.CriarContextoAsync(new ContextoCaso
        {
            Id = Guid.NewGuid(),
            CasoId = casoId,
            Tipo = request.Tipo!.Value,
            Titulo = NormalizarOpcional(request.Titulo),
            Conteudo = request.Conteudo.Trim(),
            DataCriacao = agora,
            DataAtualizacao = agora
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<ContextoCaso>> ListarContextosAsync(
        Guid casoId, CancellationToken cancellationToken)
    {
        await GarantirCasoAsync(casoId, cancellationToken);
        return await repository.ListarContextosAsync(casoId, auth.OrganizationId, cancellationToken);
    }

    public async Task<ContextoCaso> ObterContextoAsync(
        Guid casoId, Guid id, CancellationToken cancellationToken)
    {
        await GarantirCasoAsync(casoId, cancellationToken);
        return await repository.ObterContextoAsync(casoId, id, auth.OrganizationId, cancellationToken)
            ?? throw NaoEncontrado("Contexto", id);
    }

    public async Task<ContextoCaso> AtualizarContextoAsync(
        Guid casoId, Guid id, AtualizarContextoCasoRequest request, CancellationToken cancellationToken)
    {
        ValidarEnum(request.Tipo, "tipo");
        ValidarTextoObrigatorio(request.Conteudo, "conteudo", 50_000);
        ValidarTamanho(request.Titulo, "titulo", 200);
        var existente = await ObterContextoAsync(casoId, id, cancellationToken);
        existente.Tipo = request.Tipo!.Value;
        existente.Titulo = NormalizarOpcional(request.Titulo);
        existente.Conteudo = request.Conteudo.Trim();
        existente.DataAtualizacao = DateTime.UtcNow;
        if (!await repository.AtualizarContextoAsync(existente, cancellationToken))
            throw NaoEncontrado("Contexto", id);
        return existente;
    }

    public async Task RemoverContextoAsync(Guid casoId, Guid id, CancellationToken cancellationToken)
    {
        await GarantirCasoAsync(casoId, cancellationToken);
        if (!await repository.RemoverContextoAsync(casoId, id, auth.OrganizationId, cancellationToken))
            throw NaoEncontrado("Contexto", id);
    }

    public async Task<DocumentoCaso> CriarDocumentoAsync(
        Guid casoId,
        string nomeOriginal,
        string mimeType,
        long tamanhoBytes,
        string? tipo,
        Stream conteudo,
        CancellationToken cancellationToken)
    {
        await GarantirCasoAsync(casoId, cancellationToken);
        ValidarArquivo(nomeOriginal, mimeType, tamanhoBytes);
        ValidarTamanho(tipo, "tipo", 100);

        var id = Guid.NewGuid();
        var nomeSeguro = Path.GetFileName(nomeOriginal);
        var extensao = Path.GetExtension(nomeSeguro).ToLowerInvariant();
        var nomeArmazenado = $"{id:N}{extensao}";
        var storageId = $"casos/{casoId:N}/documentos/{nomeArmazenado}";
        var documento = new DocumentoCaso
        {
            Id = id,
            CasoId = casoId,
            NomeOriginal = nomeSeguro,
            NomeArmazenado = nomeArmazenado,
            Tipo = NormalizarOpcional(tipo) ?? "Outro",
            MimeType = mimeType.ToLowerInvariant(),
            TamanhoBytes = tamanhoBytes,
            StorageId = storageId,
            DataUpload = DateTime.UtcNow
        };

        await fileStorage.SaveAsync(storageId, conteudo, cancellationToken);
        try
        {
            return await repository.CriarDocumentoAsync(documento, cancellationToken);
        }
        catch
        {
            try { await fileStorage.DeleteAsync(storageId, CancellationToken.None); }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Nao foi possivel compensar o arquivo {StorageId}", storageId);
            }
            throw;
        }
    }

    public async Task<IReadOnlyList<DocumentoCaso>> ListarDocumentosAsync(
        Guid casoId, CancellationToken cancellationToken)
    {
        await GarantirCasoAsync(casoId, cancellationToken);
        return await repository.ListarDocumentosAsync(casoId, auth.OrganizationId, cancellationToken);
    }

    public async Task<DocumentoCaso> ObterDocumentoAsync(
        Guid casoId, Guid id, CancellationToken cancellationToken)
    {
        await GarantirCasoAsync(casoId, cancellationToken);
        return await repository.ObterDocumentoAsync(casoId, id, auth.OrganizationId, cancellationToken)
            ?? throw NaoEncontrado("Documento", id);
    }

    public async Task<DocumentoArquivo> ObterArquivoAsync(
        Guid casoId, Guid id, CancellationToken cancellationToken)
    {
        var documento = await ObterDocumentoAsync(casoId, id, cancellationToken);
        var conteudo = await fileStorage.GetAsync(documento.StorageId, cancellationToken);
        return new DocumentoArquivo(documento, conteudo);
    }

    public async Task RemoverDocumentoAsync(Guid casoId, Guid id, CancellationToken cancellationToken)
    {
        var documento = await ObterDocumentoAsync(casoId, id, cancellationToken);
        if (!await repository.RemoverDocumentoAsync(casoId, id, auth.OrganizationId, cancellationToken))
            throw NaoEncontrado("Documento", id);
        try
        {
            await fileStorage.DeleteAsync(documento.StorageId, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Metadados removidos, mas falhou a remocao do arquivo {StorageId}", documento.StorageId);
        }
    }

    public async Task<CasoContextoCompletoDto> ObterContextoCompletoAsync(
        Guid casoId, CancellationToken cancellationToken)
    {
        var caso = await ObterCasoAsync(casoId, cancellationToken);
        var envolvidosTask = repository.ListarEnvolvidosAsync(casoId, auth.OrganizationId, cancellationToken);
        var contextosTask = repository.ListarContextosAsync(casoId, auth.OrganizationId, cancellationToken);
        var documentosTask = repository.ListarDocumentosAsync(casoId, auth.OrganizationId, cancellationToken);
        await Task.WhenAll(envolvidosTask, contextosTask, documentosTask);
        return new CasoContextoCompletoDto(
            caso,
            await envolvidosTask,
            await contextosTask,
            await documentosTask);
    }

    private async Task GarantirCasoAsync(Guid casoId, CancellationToken cancellationToken)
    {
        if (await repository.ObterCasoAsync(casoId, auth.OrganizationId, cancellationToken) is null)
            throw NaoEncontrado("Caso", casoId);
    }

    private void ValidarArquivo(string nomeOriginal, string mimeType, long tamanhoBytes)
    {
        if (string.IsNullOrWhiteSpace(nomeOriginal) || tamanhoBytes <= 0)
            throw new RequisicaoInvalidaException("Um arquivo nao vazio e obrigatorio.");
        if (tamanhoBytes > _storageSettings.MaxFileSizeBytes)
            throw new ArquivoMuitoGrandeException(
                $"O arquivo excede o limite de {_storageSettings.MaxFileSizeBytes} bytes.");
        var extensao = Path.GetExtension(Path.GetFileName(nomeOriginal));
        if (!_storageSettings.AllowedExtensions.Contains(extensao, StringComparer.OrdinalIgnoreCase)
            || !_storageSettings.AllowedMimeTypes.Contains(mimeType, StringComparer.OrdinalIgnoreCase))
            throw new TipoArquivoNaoSuportadoException("Extensao ou tipo MIME nao permitido.");
    }

    private static void ValidarDadosAdicionais(IReadOnlyDictionary<string, string?>? dados)
    {
        if (dados is null) return;
        if (dados.Count > 50 || dados.Any(item => string.IsNullOrWhiteSpace(item.Key) || item.Key.Length > 100
                                                  || item.Value?.Length > 2000))
            throw new RequisicaoInvalidaException("Dados adicionais invalidos.");
    }

    private static void ValidarTextoObrigatorio(string? valor, string campo, int tamanhoMaximo)
    {
        if (string.IsNullOrWhiteSpace(valor))
            throw new RequisicaoInvalidaException($"O campo {campo} e obrigatorio.");
        ValidarTamanho(valor, campo, tamanhoMaximo);
    }

    private static void ValidarTamanho(string? valor, string campo, int tamanhoMaximo)
    {
        if (valor?.Length > tamanhoMaximo)
            throw new RequisicaoInvalidaException($"O campo {campo} deve ter no maximo {tamanhoMaximo} caracteres.");
    }

    private static void ValidarEnum<T>(T? valor, string campo) where T : struct, Enum
    {
        if (!valor.HasValue || !Enum.IsDefined(valor.Value))
            throw new RequisicaoInvalidaException($"O campo {campo} possui um valor invalido.");
    }

    private static string? NormalizarOpcional(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    private static RecursoNaoEncontradoException NaoEncontrado(string recurso, Guid id) =>
        new($"{recurso} '{id}' nao encontrado.");
}
