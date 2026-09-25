using System.ComponentModel.DataAnnotations;
using JuridicoAI.Api.Domain.Entities;
using JuridicoAI.Api.Domain.Enums;
using Microsoft.AspNetCore.Http;

namespace JuridicoAI.Api.Application.DTOs;

public sealed record CriarCasoRequest(
    [param: Required, StringLength(200, MinimumLength = 1)] string Titulo,
    [param: StringLength(4000)] string? Descricao);

public sealed record AtualizarCasoRequest(
    [param: Required, StringLength(200, MinimumLength = 1)] string Titulo,
    [param: StringLength(4000)] string? Descricao,
    [param: Required] CasoStatusEnum? Status);

public sealed record CriarEnvolvidoCasoRequest(
    [param: Required, StringLength(200, MinimumLength = 1)] string Nome,
    [param: Required] TipoEnvolvidoCasoEnum? Tipo,
    [param: Required] NaturezaPessoaEnum? Natureza,
    string? NomeFantasia = null,
    string? CpfCnpj = null,
    string? Rg = null,
    string? RgOrgaoEmissor = null,
    string? RgUf = null,
    string? Nacionalidade = null,
    EstadoCivilEnum? EstadoCivil = null,
    string? Profissao = null,
    DateOnly? DataNascimento = null,
    string? InscricaoEstadual = null,
    string? Email = null,
    string? Telefone = null);

public sealed record AtualizarEnvolvidoCasoRequest(
    [param: Required, StringLength(200, MinimumLength = 1)] string Nome,
    [param: Required] TipoEnvolvidoCasoEnum? Tipo,
    [param: Required] NaturezaPessoaEnum? Natureza,
    string? NomeFantasia = null,
    string? CpfCnpj = null,
    string? Rg = null,
    string? RgOrgaoEmissor = null,
    string? RgUf = null,
    string? Nacionalidade = null,
    EstadoCivilEnum? EstadoCivil = null,
    string? Profissao = null,
    DateOnly? DataNascimento = null,
    string? InscricaoEstadual = null,
    string? Email = null,
    string? Telefone = null);

public sealed record EnvolvidoResumoDto(
    Guid Id,
    Guid CasoId,
    string Nome,
    TipoEnvolvidoCasoEnum Tipo,
    NaturezaPessoaEnum Natureza,
    string? NomeFantasia,
    string? DocumentoMascarado,
    DateTime DataAtualizacao);

public sealed record EnvolvidoDetalheDto(
    Guid Id,
    Guid CasoId,
    string Nome,
    TipoEnvolvidoCasoEnum Tipo,
    NaturezaPessoaEnum Natureza,
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
    string? Telefone,
    DateTime DataCriacao,
    DateTime DataAtualizacao,
    IReadOnlyList<EnderecoEnvolvidoDto> Enderecos);

public sealed record CriarEnderecoEnvolvidoRequest(
    [param: Required] TipoEnderecoEnum? Tipo,
    bool Principal,
    string? Cep,
    [param: Required, StringLength(200, MinimumLength = 1)] string Logradouro,
    string? Numero,
    string? Complemento,
    string? Bairro,
    [param: Required, StringLength(100, MinimumLength = 1)] string Cidade,
    string? Uf,
    string? EstadoProvincia,
    string? Pais = "BR");

public sealed record AtualizarEnderecoEnvolvidoRequest(
    [param: Required] TipoEnderecoEnum? Tipo,
    bool Principal,
    string? Cep,
    [param: Required, StringLength(200, MinimumLength = 1)] string Logradouro,
    string? Numero,
    string? Complemento,
    string? Bairro,
    [param: Required, StringLength(100, MinimumLength = 1)] string Cidade,
    string? Uf,
    string? EstadoProvincia,
    string? Pais = "BR");

public sealed record EnderecoEnvolvidoDto(
    Guid Id,
    Guid EnvolvidoCasoId,
    TipoEnderecoEnum Tipo,
    bool Principal,
    string? Cep,
    string Logradouro,
    string? Numero,
    string? Complemento,
    string? Bairro,
    string Cidade,
    string? Uf,
    string? EstadoProvincia,
    string Pais,
    DateTime DataCriacao,
    DateTime DataAtualizacao);

public sealed record CriarContextoCasoRequest(
    [param: Required] TipoContextoCasoEnum? Tipo,
    [param: StringLength(200)] string? Titulo,
    [param: Required, StringLength(50_000, MinimumLength = 1)] string Conteudo);

public sealed record AtualizarContextoCasoRequest(
    [param: Required] TipoContextoCasoEnum? Tipo,
    [param: StringLength(200)] string? Titulo,
    [param: Required, StringLength(50_000, MinimumLength = 1)] string Conteudo);

public sealed class CriarDocumentoRequest
{
    public IFormFile? Arquivo { get; init; }
    public string? Tipo { get; init; }
}

public sealed record CasoContextoCompletoDto(
    Caso Caso,
    IReadOnlyList<EnvolvidoDetalheDto> Envolvidos,
    IReadOnlyList<ContextoCaso> Contextos,
    IReadOnlyList<DocumentoCaso> Documentos);

public sealed record DocumentoArquivo(DocumentoCaso Documento, Stream Conteudo);
