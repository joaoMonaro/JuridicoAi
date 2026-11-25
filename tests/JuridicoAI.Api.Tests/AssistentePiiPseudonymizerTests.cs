using System.Text.Json;
using JuridicoAI.Api.Application.Abstractions;
using JuridicoAI.Api.Application.DTOs;
using JuridicoAI.Api.Application.Services;
using JuridicoAI.Api.Infrastructure.AI;
using JuridicoAI.Api.Infrastructure.Settings;
using Microsoft.Extensions.Options;

namespace JuridicoAI.Api.Tests;

public sealed class AssistentePiiPseudonymizerTests
{
    [Fact]
    public void PseudonymizeERehydrate_ProtegeDadosEstruturadosEPreservaProcessoCnj()
    {
        var session = new AssistentePiiPseudonymizationSession();
        var original = """
            Processo nº 0001234-56.2025.8.26.0100.
            Autor: João da Silva, CPF 529.982.247-25, RG nº 12.345.678-9,
            residente no CEP 01310-100, e-mail joao.silva@exemplo.com.br, telefone (11) 98765-4321.
            Ré: Empresa Exemplo LTDA, CNPJ 11.222.333/0001-81, também citada com CPF 529.982.247-25.
            Valor da causa: R$ 15.000,00 em 25/11/2026. CPF inválido: 111.111.111-11.
            """;

        var masked = session.Pseudonymize(original);

        Assert.True(session.HasReplacements);
        Assert.DoesNotContain("529.982.247-25", masked);
        Assert.DoesNotContain("11.222.333/0001-81", masked);
        Assert.DoesNotContain("12.345.678-9", masked);
        Assert.DoesNotContain("01310-100", masked);
        Assert.DoesNotContain("joao.silva@exemplo.com.br", masked);
        Assert.DoesNotContain("(11) 98765-4321", masked);

        // Consistência referencial: mesmo CPF recebe o mesmo token
        Assert.Equal(2, masked.Split("[CPF_1]").Length - 1);
        Assert.Contains("[CNPJ_1]", masked);
        Assert.Contains("[RG_1]", masked);
        Assert.Contains("[CEP_1]", masked);
        Assert.Contains("[EMAIL_1]", masked);
        Assert.Contains("[TEL_1]", masked);

        // Não mascara número CNJ, valor monetário, data ou CPF inválido
        Assert.Contains("0001234-56.2025.8.26.0100", masked);
        Assert.Contains("R$ 15.000,00", masked);
        Assert.Contains("25/11/2026", masked);
        Assert.Contains("111.111.111-11", masked);

        // Reidratação devolve exatamente o texto original
        Assert.Equal(original, session.Rehydrate(masked));
    }

    [Fact]
    public void Pseudonymize_DetectaCpfECnpjSemMascaraComRotulo()
    {
        var session = new AssistentePiiPseudonymizationSession();
        var original = "CPF: 52998224725 e CNPJ: 11222333000181.";

        var masked = session.Pseudonymize(original);

        Assert.Equal("CPF: [CPF_1] e CNPJ: [CNPJ_1].", masked);
        Assert.Equal(original, session.Rehydrate(masked));
    }

    [Fact]
    public void RehydrateJson_RestauraPlaceholdersEmArvoreTipTapComCaracteresEspeciais()
    {
        var session = new AssistentePiiPseudonymizationSession();
        _ = session.Pseudonymize("CPF 529.982.247-25 e e-mail a@b.com.br");

        using var doc = JsonDocument.Parse("""
            {
              "type": "paragraph",
              "content": [
                { "type": "text", "text": "Titular do \"CPF\" [CPF_1]\nContato: [EMAIL_1]" }
              ]
            }
            """);

        var rehydrated = session.RehydrateJson(doc.RootElement);
        var text = rehydrated.GetProperty("content")[0].GetProperty("text").GetString();

        Assert.Equal("Titular do \"CPF\" 529.982.247-25\nContato: a@b.com.br", text);
    }

    [Fact]
    public async Task Decorator_PseudonimizaEntradaToolCallsEReidrataRespostaEProposta()
    {
        var fake = new CapturingProvider();
        var options = new StaticOptionsMonitor(new AssistenteIaSettings { PiiPseudonymizationEnabled = true });
        var decorator = new PseudonymizingAssistenteIaProvider(fake, fake, options);

        string? receivedToolArgs = null;
        var tools = new AssistenteDocumentTools(
            JsonDocument.Parse("[]").RootElement.Clone(),
            (name, args, _) =>
            {
                receivedToolArgs = args;
                return Task.FromResult("Trecho do documento: CPF 529.982.247-25 e CNPJ 11.222.333/0001-81.");
            },
            () => new AssistenteCoberturaDocumentalDto("Relevantes", "Completa", [], false),
            maxRounds: 2, maxTotalTokens: 10000, maxContextCharacters: 20000);

        var request = new AssistenteIaProviderRequest(
            Instructions: "Instruções base.",
            Input: "Redija qualificação para CPF 529.982.247-25 e e-mail cliente@escritorio.com.br.",
            Modo: AssistenteModo.Perguntar,
            OperacoesPermitidas: [AssistenteOperacao.ResponderNoPainel, AssistenteOperacao.ProporAlteracoes],
            MaxOutputTokens: 1000,
            Conversacional: true,
            PodeGerarMinuta: true,
            FerramentasDocumentais: tools);

        var tokens = await decorator.ContarTokensEntradaAsync(request, default);
        Assert.True(tokens > 0);
        Assert.DoesNotContain("529.982.247-25", fake.LastCountedInput);

        var result = await decorator.GerarAsync(request, default);

        // Provedor nunca recebeu os dados reais na entrada nem no retorno da tool
        Assert.DoesNotContain("529.982.247-25", fake.LastGeneratedInput);
        Assert.DoesNotContain("cliente@escritorio.com.br", fake.LastGeneratedInput);
        Assert.DoesNotContain("11.222.333/0001-81", fake.LastToolOutputSeenByProvider);
        Assert.Contains("[CPF_1]", fake.LastToolOutputSeenByProvider);
        Assert.Contains("[CNPJ_1]", fake.LastToolOutputSeenByProvider);

        // A ferramenta local recebeu os argumentos reidratados para buscar no banco
        Assert.Contains("529.982.247-25", receivedToolArgs);

        // A resposta final e os blocos TipTap voltaram reidratados para o usuário
        Assert.Contains("529.982.247-25", result.Texto);
        Assert.Contains("11.222.333/0001-81", result.Texto);
        Assert.Contains("cliente@escritorio.com.br", result.Resumo);
        var blockText = result.RascunhoAutomatico!.Grupos[0].Operacoes[0].NovosBlocos[0]
            .GetProperty("content")[0].GetProperty("text").GetString();
        Assert.Equal("Qualificação: CPF 529.982.247-25 e CNPJ 11.222.333/0001-81.", blockText);
    }

    [Fact]
    public async Task Decorator_DesabilitadoDelegaSemAlterarPayload()
    {
        var fake = new CapturingProvider();
        var options = new StaticOptionsMonitor(new AssistenteIaSettings { PiiPseudonymizationEnabled = false });
        var decorator = new PseudonymizingAssistenteIaProvider(fake, fake, options);

        var request = new AssistenteIaProviderRequest(
            "Base", "CPF 529.982.247-25", AssistenteModo.Perguntar,
            [AssistenteOperacao.ResponderNoPainel], 500);

        await decorator.GerarAsync(request, default);
        Assert.Equal("CPF 529.982.247-25", fake.LastGeneratedInput);
    }

    private sealed class CapturingProvider : IAssistenteIaProvider, IAssistenteTokenCounter
    {
        public string LastCountedInput { get; private set; } = string.Empty;
        public string LastGeneratedInput { get; private set; } = string.Empty;
        public string LastToolOutputSeenByProvider { get; private set; } = string.Empty;

        public Task<int> ContarTokensEntradaAsync(AssistenteIaProviderRequest request, CancellationToken cancellationToken)
        {
            LastCountedInput = request.Input;
            return Task.FromResult(request.Input.Length);
        }

        public async Task<AssistenteIaProviderResult> GerarAsync(AssistenteIaProviderRequest request, CancellationToken cancellationToken)
        {
            LastGeneratedInput = request.Input;
            if (request.FerramentasDocumentais is not null)
            {
                LastToolOutputSeenByProvider = await request.FerramentasDocumentais.ExecuteAsync(
                    "buscar_documentos", "{\"query\":\"[CPF_1]\"}", cancellationToken);
            }

            using var block = JsonDocument.Parse("""
                {
                  "type": "paragraph",
                  "content": [{ "type": "text", "text": "Qualificação: CPF [CPF_1] e CNPJ [CNPJ_1]." }]
                }
                """);

            return new AssistenteIaProviderResult(
                Titulo: "Minuta [CPF_1]",
                Texto: "Dados encontrados: [CPF_1] e [CNPJ_1].",
                Resumo: "Contato [EMAIL_1]",
                OperacaoSugerida: AssistenteOperacao.ProporAlteracoes,
                Alertas: [new("INFO", "Confira [CPF_1]")],
                Achados: [],
                EntradaTokens: 100,
                EntradaTokensEmCache: 0,
                SaidaTokens: 50,
                Modelo: "test-model",
                RascunhoAutomatico: new AssistenteRascunhoAutomatico(
                    AssistenteIntencao.EditarPeca,
                    [new AssistenteGrupoRequest("Atualizar [CPF_1]", [new AssistenteSpliceRequest(0, 0, [block.RootElement.Clone()])])],
                    false));
        }
    }

    private sealed class StaticOptionsMonitor(AssistenteIaSettings current) : IOptionsMonitor<AssistenteIaSettings>
    {
        public AssistenteIaSettings CurrentValue => current;
        public AssistenteIaSettings Get(string? name) => current;
        public IDisposable? OnChange(Action<AssistenteIaSettings, string?> listener) => null;
    }
}
