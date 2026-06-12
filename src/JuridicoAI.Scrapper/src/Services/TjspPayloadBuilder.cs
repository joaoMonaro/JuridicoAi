namespace JuridicoAI.Scrapper.Services;

public static class TjspPayloadBuilder
{
    public static IReadOnlyList<KeyValuePair<string, string>> CriarPesquisaLivre(
        string termo,
        string recaptchaToken,
        string uuidCaptcha,
        bool pesquisarComSinonimos,
        string tipoDecisao)
    {
        var sinonimos = pesquisarComSinonimos ? "S" : "N";

        return
        [
            new("conversationId", string.Empty),
            new("dados.buscaInteiroTeor", termo),
            new("dados.pesquisarComSinonimos", sinonimos),
            new("dados.pesquisarComSinonimos", sinonimos),
            new("dados.buscaEmenta", string.Empty),
            new("dados.nuProcOrigem", string.Empty),
            new("dados.nuRegistro", string.Empty),
            new("agenteSelectedEntitiesList", string.Empty),
            new("contadoragente", "0"),
            new("contadorMaioragente", "0"),
            new("codigoCr", string.Empty),
            new("codigoTr", string.Empty),
            new("nmAgente", string.Empty),
            new("juizProlatorSelectedEntitiesList", string.Empty),
            new("contadorjuizProlator", "0"),
            new("contadorMaiorjuizProlator", "0"),
            new("codigoJuizCr", string.Empty),
            new("codigoJuizTr", string.Empty),
            new("nmJuiz", string.Empty),
            new("classesTreeSelection.values", string.Empty),
            new("classesTreeSelection.text", string.Empty),
            new("assuntosTreeSelection.values", string.Empty),
            new("assuntosTreeSelection.text", string.Empty),
            new("comarcaSelectedEntitiesList", string.Empty),
            new("contadorcomarca", "0"),
            new("contadorMaiorcomarca", "0"),
            new("cdComarca", string.Empty),
            new("nmComarca", string.Empty),
            new("secoesTreeSelection.values", string.Empty),
            new("secoesTreeSelection.text", string.Empty),
            new("dados.dtJulgamentoInicio", string.Empty),
            new("dados.dtJulgamentoFim", string.Empty),
            new("dados.dtPublicacaoInicio", string.Empty),
            new("dados.dtPublicacaoFim", string.Empty),
            new("dados.origensSelecionadas", "T"),
            new("tipoDecisaoSelecionados", tipoDecisao),
            new("dados.ordenarPor", "dtPublicacao"),
            new("recaptcha_response_token", recaptchaToken),
            new("uuidCaptcha", uuidCaptcha)
        ];
    }
}
