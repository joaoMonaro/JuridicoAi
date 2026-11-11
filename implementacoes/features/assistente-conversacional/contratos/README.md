# Contratos de fronteira e exemplos

Schema: [assistente-v2.schema.json](assistente-v2.schema.json), JSON Schema 2020-12.
Cada tipo de fronteira está em `$defs`; validar o tipo esperado, não o documento
raiz que funciona apenas como catálogo. Exemplos fictícios em `exemplos/`.

Na integração persistida, `Proposta.documentoBase` contém o documento da versão
aplicável para reabrir a comparação sem depender da memória do navegador. Depois
de uma aplicação parcial, corresponde ao documento resultante dessa aplicação.
Não é autorização para substituir a Peça atual: versão e conteúdo continuam
sendo conferidos no servidor. O endpoint transitório de registro de sugestões
legadas usa DTO próprio e não implementa `EnviarMensagem`/execução durável.

O endpoint implementado de conversa livre é `POST /conversas/{id}/interacoes`,
com `EnviarInteracaoLivreRequest` sem modos, ainda em SSE 200. Seu contrato e
limites estão no [registro de implementação](../evidencias/e3-conversa-livre-2026-11-11.md).
Não equivale ao comando futuro `EnviarMensagem` com execução durável.
`Proposta.substituidaPorId` é opcional/nullable e identifica a sucessora de uma
proposta substituída; isso não reverte os grupos já aplicados na Peça.

| Tipo | Uso |
|---|---|
| EnviarMensagem | POST mensagem, sem modo técnico |
| MensagemAceita | HTTP 202 com execução persistida |
| Proposta | GET de revisão e grupos aprováveis |
| AplicarGrupos | Aprovação vinculada ao que foi exibido |
| Recibo | Commit confirmado e estado remanescente |
| Evento | Envelope SSE e principais payloads |
| ExcluirConversa | Comando destrutivo autorizado |
| Problema | Erro de fronteira sem conteúdo jurídico |

O schema valida forma, não todas as regras de domínio. A implementação deve
validar também: filhos permitidos por tipo TipTap, links com attrs apropriados,
limite total de profundidade/nós, from<to, IDs de grupos sem duplicação mesmo com
hashes distintos, pertença dos anexos, operações sem overlap, hashes, versões,
conteúdo integral e permissões. Não publicar schema como garantia de correção
jurídica nem enviá-lo inteiro como schema de ferramenta ao provider.

`previewSha256` vincula exatamente `{grupoId, alvosOriginais, original, proposto}`
serializado em json-c14n-v1; não inclui explicação textual nem metadados de decisão.
Alvos são aliases qualificados pelo snapshot inicial. Posições compiladas podem
mudar apenas pelo remapeamento comprovado de uma aplicação parcial da própria
proposta; por isso não entram nesse hash. A versão da Peça, a revisão e os hashes
do original continuam obrigatórios no commit. Alteração externa não usa esse remapeamento.
`expectedBlocksSha256` é o hash do array de blocos removidos, inclusive `[]` na
inserção. `documentSha256` é o hash do doc inteiro. Hash é evidência de igualdade,
não assinatura de autorização. Recibo só pode vir do servidor autenticado.

Seleção from/to usa posições ProseMirror, conferidas contra o snapshot; esses
números não são offsets de bytes nem IDs persistentes. Compilação da proposta
usa aliases resolvidos no backend.

Payloads adicionais de eventos a implementar:

- mensagem.aceita: mensagemId, execucaoId.
- anexo.progresso: anexoId, estado, paginasTotal nullable, paginasConcluidas,
  trechosConfiaveis, trechosExcluidos, problemas com localizador e código.
- execucao.progresso: estado, etapa, concluido, total nullable.
- resposta.concluida: mensagemId, texto canônico, referencias com origem/localizador.
- proposta.atualizada: propostaId, revisao, versaoDecisoes.
- execucao.falhou: code, message, retryable, resultadoIncompletoDisponivel.
- execucao.cancelada: execucaoId e resultadoIncompletoDisponivel.

O envelope é estável; campos desses payloads devem virar schemas específicos na
implementação antes de gerar clientes. Os exemplos já formalizados cobrem o
caminho crítico mensagem → proposta → aplicação → recibo → evento. Não representam
um OpenAPI completo de todas as rotas administrativas.
