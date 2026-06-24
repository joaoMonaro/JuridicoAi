using JuridicoAI.Api.Infrastructure.Gateways.OpenIA;
using JuridicoAI.Api.Infrastructure.Repositories;

namespace JuridicoAI.Api.Application.Services;

public class PeticaoApplicationService(
    OpenIAGateway openIAGateway,
    TjspSearchResultRepository tjspSearchResultRepository)
{
    public async Task<string> GerarAsync(string fatoSimples, CancellationToken cancellationToken = default)
    {
        var ementas = await tjspSearchResultRepository.ObterEmentasAsync(cancellationToken);
        var contextoEmentas = ementas.Count == 0
            ? "[nenhuma ementa fornecida]"
            : string.Join(Environment.NewLine, ementas.Select(ementa => $"- {ementa}"));

        return await openIAGateway.GerarTextoAsync(CriarPrompt(fatoSimples, contextoEmentas), cancellationToken);
    }

    private static string CriarPrompt(string fatoSimples, string contextoEmentas)
    {
        return $"""
            Atue como advogado especialista em Direito do Consumidor.

            Sua tarefa e elaborar uma peca juridica em favor da parte representada pelos fatos descritos na variavel `fatoSimples`, que contem o motivo da peca e a narrativa do caso.

            Use exclusivamente os dados fornecidos em `fatoSimples` para descrever os fatos, formular a tese e estruturar os pedidos. Nao crie, presuma ou acrescente fatos, datas, valores, documentos, nomes, numeros de processo, provas, contratos, protocolos, enderecos, qualificacoes das partes ou qualquer outro dado que nao esteja expressamente informado.

            Use exclusivamente a variavel `contextoEmentas` como fonte de jurisprudencia. E proibido citar, mencionar, resumir ou inventar jurisprudencia que nao esteja presente em `contextoEmentas`.

            Elabore uma peca juridica completa, tecnica, clara e persuasiva, adequada ao caso narrado em `fatoSimples`.

            A peca deve conter, quando possivel com base nos dados fornecidos:

            1. Enderecamento;
            2. Qualificacao das partes, sem inventar dados ausentes;
            3. Sintese dos fatos;
            4. Fundamentacao juridica em Direito do Consumidor;
            5. Aplicacao das ementas existentes em `contextoEmentas`;
            6. Demonstracao da falha na prestacao do servico, pratica abusiva, responsabilidade civil, dano material, dano moral, obrigacao de fazer/nao fazer ou outro fundamento cabivel ao caso;
            7. Pedido de tutela de urgencia, somente se houver elementos em `fatoSimples` que justifiquem;
            8. Pedido de inversao do onus da prova, se cabivel;
            9. Pedidos finais claros e organizados;
            10. Valor da causa, somente se houver dados suficientes; caso contrario, indicar `[valor da causa nao informado]`;
            11. Fechamento formal.

            Regras obrigatorias:

            - Baseie-se apenas em `fatoSimples` e `contextoEmentas`.
            - Nao utilize dados ficticios.
            - Nao invente jurisprudencia.
            - Nao complete lacunas com suposicoes.
            - Se uma informacao essencial nao estiver disponivel, use marcador como `[informacao nao fornecida]`.
            - Nao cite numeros de processos, datas de julgamento, relator, camara ou tribunal se tais dados nao constarem em `contextoEmentas`.
            - A peca deve defender a parte favorecida pelos fatos narrados em `fatoSimples`.
            - Use linguagem juridica formal, objetiva e convincente.

            Variavel `fatoSimples`:
            {fatoSimples}

            Variavel `contextoEmentas`:
            {contextoEmentas}
            """;
    }
}
