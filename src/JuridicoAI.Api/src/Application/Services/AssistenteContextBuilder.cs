using System.Text;
using JuridicoAI.Api.Application.DTOs;
using JuridicoAI.Api.Application.Exceptions;
using JuridicoAI.Api.Domain.Entities;
using JuridicoAI.Api.Infrastructure.Settings;
using Microsoft.Extensions.Options;

namespace JuridicoAI.Api.Application.Services;

public sealed class AssistenteContextBuilder(IOptions<AssistenteIaSettings> options)
{
    private readonly AssistenteIaSettings _settings = options.Value;

    public string Criar(
        string instrucao,
        AssistenteDocumentoRequest documento,
        string? contextoAdicional,
        Caso? caso,
        IReadOnlyList<EnvolvidoCaso> envolvidos,
        IReadOnlyList<ContextoCaso> contextos)
    {
        var builder = new StringBuilder();
        AppendBlock(builder, "INSTRUCAO_DO_USUARIO", instrucao);

        if (documento.Escopo != AssistenteEscopoDocumento.Nenhum)
        {
            var attributes = $"escopo=\"{documento.Escopo}\" dado_nao_confiavel=\"true\"";
            if (!string.IsNullOrWhiteSpace(documento.TituloSecao))
                attributes += $" titulo_secao=\"{SanitizeAttribute(documento.TituloSecao)}\"";
            AppendBlock(builder, $"TEXTO_DA_PECA {attributes}", documento.Texto ?? string.Empty);
        }

        if (!string.IsNullOrWhiteSpace(contextoAdicional))
            AppendBlock(builder, "CONTEXTO_ADICIONAL dado_nao_confiavel=\"true\"", contextoAdicional.Trim());

        if (caso is not null)
        {
            var resumoCaso = new StringBuilder()
                .AppendLine($"Título: {caso.Titulo}")
                .AppendLine($"Status: {caso.Status}");
            if (!string.IsNullOrWhiteSpace(caso.Descricao))
                resumoCaso.AppendLine($"Descrição: {caso.Descricao}");

            foreach (var envolvido in envolvidos)
            {
                resumoCaso.Append($"Envolvido: {envolvido.Nome}; tipo: {envolvido.Tipo}; natureza: {envolvido.Natureza}");
                if (!string.IsNullOrWhiteSpace(envolvido.Profissao))
                    resumoCaso.Append($"; profissão: {envolvido.Profissao}");
                if (!string.IsNullOrWhiteSpace(envolvido.Nacionalidade))
                    resumoCaso.Append($"; nacionalidade: {envolvido.Nacionalidade}");
                resumoCaso.AppendLine();
            }

            AppendBlock(builder, "DADOS_DO_CASO dado_nao_confiavel=\"true\"", resumoCaso.ToString().Trim());
        }

        foreach (var contexto in contextos)
        {
            var titulo = string.IsNullOrWhiteSpace(contexto.Titulo) ? contexto.Tipo.ToString() : contexto.Titulo;
            AppendBlock(
                builder,
                $"CONTEXTO_DO_CASO id=\"{contexto.Id}\" titulo=\"{SanitizeAttribute(titulo)}\" dado_nao_confiavel=\"true\"",
                contexto.Conteudo);
        }

        if (builder.Length > _settings.MaxTotalContextCharacters)
            throw new AssistenteIaException(
                StatusCodes.Status413PayloadTooLarge,
                "AI_CONTEXT_TOO_LARGE",
                "O contexto selecionado ultrapassa o limite permitido.");

        return builder.ToString();
    }

    private static void AppendBlock(StringBuilder builder, string label, string value)
    {
        builder.Append('[').Append(label).AppendLine("]");
        builder.AppendLine(value);
        builder.Append("[/").Append(label.Split(' ', 2)[0]).AppendLine("]");
        builder.AppendLine();
    }

    private static string SanitizeAttribute(string value) =>
        value.Replace('"', '\'').Replace('\r', ' ').Replace('\n', ' ').Trim();
}
