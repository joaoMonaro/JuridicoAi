using System.Net.Mail;
using JuridicoAI.Api.Application.Abstractions;
using JuridicoAI.Api.Application.DTOs;
using JuridicoAI.Api.Application.Exceptions;
using JuridicoAI.Api.Domain.Entities;
using JuridicoAI.Api.Domain.Enums;
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
        CasoStatusEnum? status,
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
            Status = CasoStatusEnum.Ativo,
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

    public async Task<EnvolvidoDetalheDto> CriarEnvolvidoAsync(
        Guid casoId,
        CriarEnvolvidoCasoRequest request,
        CancellationToken cancellationToken)
    {
        await GarantirCasoAsync(casoId, cancellationToken);
        var agora = DateTime.UtcNow;
        var envolvido = new EnvolvidoCaso
        {
            Id = Guid.NewGuid(),
            CasoId = casoId,
            DataCriacao = agora,
            DataAtualizacao = agora
        };
        AplicarIdentidade(envolvido, Identidade(request));
        await repository.CriarEnvolvidoAsync(envolvido, cancellationToken);
        return Detalhe(envolvido, []);
    }

    public async Task<IReadOnlyList<EnvolvidoResumoDto>> ListarEnvolvidosAsync(
        Guid casoId, CancellationToken cancellationToken)
    {
        await GarantirCasoAsync(casoId, cancellationToken);
        var envolvidos = await repository.ListarEnvolvidosAsync(casoId, auth.OrganizationId, cancellationToken);
        return envolvidos.Select(Resumo).ToList();
    }

    public async Task<EnvolvidoDetalheDto> ObterEnvolvidoAsync(
        Guid casoId, Guid id, CancellationToken cancellationToken)
    {
        var envolvido = await ObterEnvolvidoEntidadeAsync(casoId, id, cancellationToken);
        var enderecos = await repository.ListarEnderecosAsync(casoId, id, auth.OrganizationId, cancellationToken);
        return Detalhe(envolvido, enderecos);
    }

    public async Task<EnvolvidoDetalheDto> AtualizarEnvolvidoAsync(
        Guid casoId, Guid id, AtualizarEnvolvidoCasoRequest request, CancellationToken cancellationToken)
    {
        var existente = await ObterEnvolvidoEntidadeAsync(casoId, id, cancellationToken);
        AplicarIdentidade(existente, Identidade(request));
        existente.DataAtualizacao = DateTime.UtcNow;
        if (!await repository.AtualizarEnvolvidoAsync(existente, auth.OrganizationId, cancellationToken))
            throw NaoEncontrado("Envolvido", id);
        var enderecos = await repository.ListarEnderecosAsync(casoId, id, auth.OrganizationId, cancellationToken);
        return Detalhe(existente, enderecos);
    }

    public async Task RemoverEnvolvidoAsync(Guid casoId, Guid id, CancellationToken cancellationToken)
    {
        await GarantirCasoAsync(casoId, cancellationToken);
        if (!await repository.RemoverEnvolvidoAsync(casoId, id, auth.OrganizationId, cancellationToken))
            throw NaoEncontrado("Envolvido", id);
    }

    public async Task<EnderecoEnvolvidoDto> CriarEnderecoAsync(
        Guid casoId, Guid envolvidoId, CriarEnderecoEnvolvidoRequest request, CancellationToken cancellationToken)
    {
        await ObterEnvolvidoEntidadeAsync(casoId, envolvidoId, cancellationToken);
        var agora = DateTime.UtcNow;
        var endereco = new EnderecoEnvolvidoCaso
        {
            Id = Guid.NewGuid(),
            EnvolvidoCasoId = envolvidoId,
            DataCriacao = agora,
            DataAtualizacao = agora
        };
        AplicarEndereco(endereco, Endereco(request));
        return EnderecoDto(await repository.CriarEnderecoAsync(casoId, auth.OrganizationId, endereco, cancellationToken));
    }

    public async Task<IReadOnlyList<EnderecoEnvolvidoDto>> ListarEnderecosAsync(
        Guid casoId, Guid envolvidoId, CancellationToken cancellationToken)
    {
        await ObterEnvolvidoEntidadeAsync(casoId, envolvidoId, cancellationToken);
        var enderecos = await repository.ListarEnderecosAsync(casoId, envolvidoId, auth.OrganizationId, cancellationToken);
        return enderecos.Select(EnderecoDto).ToList();
    }

    public async Task<EnderecoEnvolvidoDto> ObterEnderecoAsync(
        Guid casoId, Guid envolvidoId, Guid id, CancellationToken cancellationToken)
    {
        await ObterEnvolvidoEntidadeAsync(casoId, envolvidoId, cancellationToken);
        var endereco = await repository.ObterEnderecoAsync(casoId, envolvidoId, id, auth.OrganizationId, cancellationToken)
            ?? throw NaoEncontrado("Endereco", id);
        return EnderecoDto(endereco);
    }

    public async Task<EnderecoEnvolvidoDto> AtualizarEnderecoAsync(
        Guid casoId,
        Guid envolvidoId,
        Guid id,
        AtualizarEnderecoEnvolvidoRequest request,
        CancellationToken cancellationToken)
    {
        await ObterEnvolvidoEntidadeAsync(casoId, envolvidoId, cancellationToken);
        var endereco = await repository.ObterEnderecoAsync(casoId, envolvidoId, id, auth.OrganizationId, cancellationToken)
            ?? throw NaoEncontrado("Endereco", id);
        AplicarEndereco(endereco, Endereco(request));
        endereco.DataAtualizacao = DateTime.UtcNow;
        if (!await repository.AtualizarEnderecoAsync(casoId, auth.OrganizationId, endereco, cancellationToken))
            throw NaoEncontrado("Endereco", id);
        return EnderecoDto(endereco);
    }

    public async Task RemoverEnderecoAsync(
        Guid casoId, Guid envolvidoId, Guid id, CancellationToken cancellationToken)
    {
        await ObterEnvolvidoEntidadeAsync(casoId, envolvidoId, cancellationToken);
        if (!await repository.RemoverEnderecoAsync(casoId, envolvidoId, id, auth.OrganizationId, cancellationToken))
            throw NaoEncontrado("Endereco", id);
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
        if (!await repository.AtualizarContextoAsync(existente, auth.OrganizationId, cancellationToken))
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
        var envolvidos = await repository.ListarEnvolvidosAsync(casoId, auth.OrganizationId, cancellationToken);
        var enderecos = await repository.ListarEnderecosCasoAsync(casoId, auth.OrganizationId, cancellationToken);
        var contextos = await repository.ListarContextosAsync(casoId, auth.OrganizationId, cancellationToken);
        var documentos = await repository.ListarDocumentosAsync(casoId, auth.OrganizationId, cancellationToken);
        var enderecosPorEnvolvido = enderecos.GroupBy(x => x.EnvolvidoCasoId)
            .ToDictionary(x => x.Key, x => (IReadOnlyList<EnderecoEnvolvidoCaso>)x.ToList());
        return new CasoContextoCompletoDto(
            caso,
            envolvidos.Select(x => Detalhe(x, enderecosPorEnvolvido.GetValueOrDefault(x.Id) ?? [])).ToList(),
            contextos,
            documentos);
    }

    private async Task<EnvolvidoCaso> ObterEnvolvidoEntidadeAsync(
        Guid casoId, Guid id, CancellationToken cancellationToken)
    {
        await GarantirCasoAsync(casoId, cancellationToken);
        return await repository.ObterEnvolvidoAsync(casoId, id, auth.OrganizationId, cancellationToken)
            ?? throw NaoEncontrado("Envolvido", id);
    }

    private static IdentidadeInput Identidade(CriarEnvolvidoCasoRequest request) => new(
        request.Nome, request.Tipo, request.Natureza, request.NomeFantasia, request.CpfCnpj, request.Rg,
        request.RgOrgaoEmissor, request.RgUf, request.Nacionalidade, request.EstadoCivil, request.Profissao,
        request.DataNascimento, request.InscricaoEstadual, request.Email, request.Telefone);

    private static IdentidadeInput Identidade(AtualizarEnvolvidoCasoRequest request) => new(
        request.Nome, request.Tipo, request.Natureza, request.NomeFantasia, request.CpfCnpj, request.Rg,
        request.RgOrgaoEmissor, request.RgUf, request.Nacionalidade, request.EstadoCivil, request.Profissao,
        request.DataNascimento, request.InscricaoEstadual, request.Email, request.Telefone);

    private static void AplicarIdentidade(EnvolvidoCaso destino, IdentidadeInput input)
    {
        ValidarTextoObrigatorio(input.Nome, "nome", 200);
        ValidarEnum(input.Tipo, "tipo");
        ValidarEnum(input.Natureza, "natureza");
        if (input.EstadoCivil is { } estadoCivil && !Enum.IsDefined(estadoCivil))
            throw new RequisicaoInvalidaException("O campo estadoCivil possui um valor invalido.");
        if (input.DataNascimento is { } nascimento && nascimento > DateOnly.FromDateTime(DateTime.UtcNow))
            throw new RequisicaoInvalidaException("O campo dataNascimento nao pode estar no futuro.");

        ValidarTamanho(input.NomeFantasia, "nomeFantasia", 200);
        ValidarTamanho(input.Rg, "rg", 30);
        ValidarTamanho(input.RgOrgaoEmissor, "rgOrgaoEmissor", 30);
        ValidarTamanho(input.Nacionalidade, "nacionalidade", 100);
        ValidarTamanho(input.Profissao, "profissao", 150);
        ValidarTamanho(input.InscricaoEstadual, "inscricaoEstadual", 30);

        destino.Nome = input.Nome.Trim();
        destino.Tipo = input.Tipo!.Value;
        destino.Natureza = input.Natureza!.Value;
        destino.NomeFantasia = NormalizarOpcional(input.NomeFantasia);
        destino.CpfCnpj = NormalizarCpfCnpj(input.CpfCnpj, destino.Natureza);
        destino.Rg = NormalizarOpcional(input.Rg);
        destino.RgOrgaoEmissor = NormalizarOpcional(input.RgOrgaoEmissor);
        destino.RgUf = NormalizarSigla(input.RgUf, "rgUf", obrigatoria: false);
        destino.Nacionalidade = NormalizarOpcional(input.Nacionalidade);
        destino.EstadoCivil = input.EstadoCivil;
        destino.Profissao = NormalizarOpcional(input.Profissao);
        destino.DataNascimento = input.DataNascimento;
        destino.InscricaoEstadual = NormalizarOpcional(input.InscricaoEstadual);
        destino.Email = NormalizarEmail(input.Email);
        destino.Telefone = NormalizarTelefone(input.Telefone);
    }

    private static EnderecoInput Endereco(CriarEnderecoEnvolvidoRequest request) => new(
        request.Tipo, request.Principal, request.Cep, request.Logradouro, request.Numero, request.Complemento,
        request.Bairro, request.Cidade, request.Uf, request.EstadoProvincia, request.Pais);

    private static EnderecoInput Endereco(AtualizarEnderecoEnvolvidoRequest request) => new(
        request.Tipo, request.Principal, request.Cep, request.Logradouro, request.Numero, request.Complemento,
        request.Bairro, request.Cidade, request.Uf, request.EstadoProvincia, request.Pais);

    private static void AplicarEndereco(EnderecoEnvolvidoCaso destino, EnderecoInput input)
    {
        ValidarEnum(input.Tipo, "tipo");
        ValidarTextoObrigatorio(input.Logradouro, "logradouro", 200);
        ValidarTextoObrigatorio(input.Cidade, "cidade", 100);
        ValidarTamanho(input.Numero, "numero", 30);
        ValidarTamanho(input.Complemento, "complemento", 100);
        ValidarTamanho(input.Bairro, "bairro", 100);
        ValidarTamanho(input.EstadoProvincia, "estadoProvincia", 100);

        var pais = NormalizarSigla(input.Pais ?? "BR", "pais", obrigatoria: true)!;
        var uf = NormalizarSigla(input.Uf, "uf", obrigatoria: false);
        var cep = NormalizarOpcional(input.Cep);
        if (pais == "BR" && cep is not null)
        {
            cep = SomenteDigitosFormatados(cep, "cep");
            if (cep.Length != 8)
                throw new RequisicaoInvalidaException("O campo cep deve conter oito digitos para enderecos brasileiros.");
        }
        else
        {
            ValidarTamanho(cep, "cep", 20);
        }

        destino.Tipo = input.Tipo!.Value;
        destino.Principal = input.Principal;
        destino.Cep = cep;
        destino.Logradouro = input.Logradouro.Trim();
        destino.Numero = NormalizarOpcional(input.Numero);
        destino.Complemento = NormalizarOpcional(input.Complemento);
        destino.Bairro = NormalizarOpcional(input.Bairro);
        destino.Cidade = input.Cidade.Trim();
        destino.Uf = uf;
        destino.EstadoProvincia = NormalizarOpcional(input.EstadoProvincia);
        destino.Pais = pais;
    }

    private static EnvolvidoResumoDto Resumo(EnvolvidoCaso envolvido) => new(
        envolvido.Id, envolvido.CasoId, envolvido.Nome, envolvido.Tipo, envolvido.Natureza,
        envolvido.NomeFantasia, MascararDocumento(envolvido.CpfCnpj), envolvido.DataAtualizacao);

    private static EnvolvidoDetalheDto Detalhe(
        EnvolvidoCaso envolvido, IReadOnlyList<EnderecoEnvolvidoCaso> enderecos) => new(
        envolvido.Id, envolvido.CasoId, envolvido.Nome, envolvido.Tipo, envolvido.Natureza,
        envolvido.NomeFantasia, envolvido.CpfCnpj, envolvido.Rg, envolvido.RgOrgaoEmissor, envolvido.RgUf,
        envolvido.Nacionalidade, envolvido.EstadoCivil, envolvido.Profissao, envolvido.DataNascimento,
        envolvido.InscricaoEstadual, envolvido.Email, envolvido.Telefone, envolvido.DataCriacao,
        envolvido.DataAtualizacao, enderecos.Select(EnderecoDto).ToList());

    private static EnderecoEnvolvidoDto EnderecoDto(EnderecoEnvolvidoCaso endereco) => new(
        endereco.Id, endereco.EnvolvidoCasoId, endereco.Tipo, endereco.Principal, endereco.Cep,
        endereco.Logradouro, endereco.Numero, endereco.Complemento, endereco.Bairro, endereco.Cidade,
        endereco.Uf, endereco.EstadoProvincia, endereco.Pais, endereco.DataCriacao, endereco.DataAtualizacao);

    private static string? NormalizarCpfCnpj(string? valor, NaturezaPessoaEnum natureza)
    {
        if (string.IsNullOrWhiteSpace(valor)) return null;
        var documento = SomenteDigitosFormatados(valor, "cpfCnpj");
        var valido = natureza switch
        {
            NaturezaPessoaEnum.PessoaFisica => documento.Length == 11 && CpfValido(documento),
            NaturezaPessoaEnum.PessoaJuridica or NaturezaPessoaEnum.OrgaoPublico =>
                documento.Length == 14 && CnpjValido(documento),
            _ => documento.Length == 11 ? CpfValido(documento) : documento.Length == 14 && CnpjValido(documento)
        };
        if (!valido)
            throw new RequisicaoInvalidaException("O campo cpfCnpj nao corresponde a um CPF ou CNPJ valido para a natureza informada.");
        return documento;
    }

    private static string SomenteDigitosFormatados(string valor, string campo)
    {
        if (valor.Any(c => !char.IsDigit(c) && !char.IsWhiteSpace(c) && c is not '.' and not '-' and not '/'))
            throw new RequisicaoInvalidaException($"O campo {campo} possui caracteres invalidos.");
        return new string(valor.Where(char.IsDigit).ToArray());
    }

    private static bool CpfValido(string cpf)
    {
        if (cpf.Length != 11 || cpf.All(c => c == cpf[0])) return false;
        var primeiro = CalcularDigito(cpf.AsSpan(0, 9), 10);
        var segundo = CalcularDigito(cpf.AsSpan(0, 10), 11);
        return cpf[9] - '0' == primeiro && cpf[10] - '0' == segundo;
    }

    private static bool CnpjValido(string cnpj)
    {
        if (cnpj.Length != 14 || cnpj.All(c => c == cnpj[0])) return false;
        int[] pesosPrimeiro = [5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];
        int[] pesosSegundo = [6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];
        var primeiro = CalcularDigito(cnpj.AsSpan(0, 12), pesosPrimeiro);
        var segundo = CalcularDigito(cnpj.AsSpan(0, 13), pesosSegundo);
        return cnpj[12] - '0' == primeiro && cnpj[13] - '0' == segundo;
    }

    private static int CalcularDigito(ReadOnlySpan<char> digitos, int pesoInicial)
    {
        var soma = 0;
        for (var i = 0; i < digitos.Length; i++) soma += (digitos[i] - '0') * (pesoInicial - i);
        var resto = soma % 11;
        return resto < 2 ? 0 : 11 - resto;
    }

    private static int CalcularDigito(ReadOnlySpan<char> digitos, IReadOnlyList<int> pesos)
    {
        var soma = 0;
        for (var i = 0; i < digitos.Length; i++) soma += (digitos[i] - '0') * pesos[i];
        var resto = soma % 11;
        return resto < 2 ? 0 : 11 - resto;
    }

    private static string? NormalizarEmail(string? valor)
    {
        var email = NormalizarOpcional(valor);
        if (email is null) return null;
        if (email.Length > 254 || !MailAddress.TryCreate(email, out var endereco) || endereco.Address != email)
            throw new RequisicaoInvalidaException("O campo email possui um valor invalido.");
        return email.ToLowerInvariant();
    }

    private static string? NormalizarTelefone(string? valor)
    {
        var telefone = NormalizarOpcional(valor);
        if (telefone is null) return null;
        if (telefone.Any(c => !char.IsDigit(c) && !char.IsWhiteSpace(c) && c is not '+' and not '-' and not '(' and not ')'))
            throw new RequisicaoInvalidaException("O campo telefone possui caracteres invalidos.");
        var digitos = new string(telefone.Where(char.IsDigit).ToArray());
        if (digitos.Length is < 8 or > 15)
            throw new RequisicaoInvalidaException("O campo telefone deve conter entre 8 e 15 digitos.");
        return telefone.StartsWith('+') ? $"+{digitos}" : digitos;
    }

    private static string? NormalizarSigla(string? valor, string campo, bool obrigatoria)
    {
        var sigla = NormalizarOpcional(valor);
        if (sigla is null)
        {
            if (obrigatoria) throw new RequisicaoInvalidaException($"O campo {campo} e obrigatorio.");
            return null;
        }
        if (sigla.Length != 2 || !sigla.All(char.IsLetter))
            throw new RequisicaoInvalidaException($"O campo {campo} deve conter duas letras.");
        return sigla.ToUpperInvariant();
    }

    private static string? MascararDocumento(string? documento) => documento?.Length switch
    {
        11 => $"***.***.***-{documento[^2..]}",
        14 => $"**.***.***/****-{documento[^2..]}",
        _ => null
    };

    private sealed record IdentidadeInput(
        string Nome,
        TipoEnvolvidoCasoEnum? Tipo,
        NaturezaPessoaEnum? Natureza,
        string? NomeFantasia,
        string? CpfCnpj,
        string? Rg,
        string? RgOrgaoEmissor,
        string? RgUf,
        string? Nacionalidade,
        EstadoCivilEnum? EstadoCivil,
        string? Profissao,
        DateOnly? DataNascimento,
        string? InscricaoEstadual,
        string? Email,
        string? Telefone);

    private sealed record EnderecoInput(
        TipoEnderecoEnum? Tipo,
        bool Principal,
        string? Cep,
        string Logradouro,
        string? Numero,
        string? Complemento,
        string? Bairro,
        string Cidade,
        string? Uf,
        string? EstadoProvincia,
        string? Pais);

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
