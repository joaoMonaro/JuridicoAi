# M01 — Diagnóstico e correção local da latência

Recorte implementado e verificado em **2026-11-14**, sobre a baseline `98db3ad`.
Referência: [M01 no TODO da feature](../TODO.md#melhorias-evidenciadas-na-avaliação-reduzida)
e a [avaliação real de 2026-11-12](avaliacao-reduzida-gpt-6.1-terra-2026-11-12.md).
**Nenhuma chamada paga ou nova credencial usada neste recorte.**

## Diagnóstico e alcance da conclusão

A consulta após aprovação levou 63,941 s na rodada real, com uma chamada de
geração. Os artefatos originais não separaram fila, contagem, provedor e publicação;
portanto não permitem atribuir retrospectivamente todo esse tempo a uma etapa.

O código revelou um mecanismo consistente com a demora: as duas minutas
anteriores usaram cinco etapas cada, compartilhando o limitador local de dez
operações por minuto do usuário. Ao atingir o limite, o worker agendava a próxima
aquisição em `agora + RetryAfter`. O FixedWindowRateLimiter do runtime instalado
(.NET 10.0.2) retorna uma janela inteira de 60 s, sem descontar o tempo já
transcorrido. Isso pode adiar o trabalho além da reposição efetiva da cota.
Fonte: [implementação oficial do runtime](https://github.com/dotnet/runtime/blob/v10.0.2/src/libraries/System.Threading.RateLimiting/src/System/Threading/RateLimiting/FixedWindowRateLimiter.cs#L191).

Também foi reproduzido um problema de admissão: negar a operação por limite da
organização ou por concorrência já consumia a cota por minuto do usuário. Repetir
a verificação com mais frequência sem corrigir isso poderia criar outra espera.

Esse mecanismo foi comprovado localmente. Ele é uma explicação provável para a
rodada antiga; confirmar a divisão exata e o ganho com GPT-6.1 Terra requer uma
nova medição real autorizada. Não há um novo benchmark do modelo neste registro.

## Alterações

- Worker reavalia limites **locais** em até 2 s, passando novamente por todas as
  validações. Não se trata de promessa de resposta em 2 s: espera legítima de
  cota continua existindo e o polling ocioso pode adicionar aproximadamente 1 s.
- Admissão local verifica cotas sem consumi-las e só desconta após conseguir as
  vagas de concorrência. Probes e aquisição são atômicos por instância; negações
  e cancelamento anterior à admissão não esgotam cotas. Limites não foram elevados.
- Chamada externa incerta continua interrompida, sem retry pago automático.
  O tratamento de espera por documentos permanece separado.
- Medições estruturadas: aceite, contexto, preparação, contagem de entrada,
  cabeçalhos do provedor, geração, publicação e processamento do worker. Logs de
  aquisição/reagendamento incluem IDs técnicos, estado e motivo da espera.
- Avaliador 1.1.0 exporta `clientTimingMs`, `latency.json` e duração/data/correlação
  no ledger. Apenas campos técnicos permitidos entram no extrato de latência;
  prompts, respostas, títulos, nomes de arquivos, credenciais e exceções não entram.

Os eventos informativos 6401–6403 exigem nível Information nas categorias de
controller, processor e provider. O avaliador os habilita apenas no processo
descartável, com console JSON e scopes. O produto não recebeu ativação de flags,
alteração de modelo, migration ou contrato HTTP novo.

Tempos de etapas aninhadas se sobrepõem e não devem ser somados. Ausência de
eventos significa medição indisponível, não latência zero. Os eventos registram
duração inclusive em falhas; não atestam conclusão bem-sucedida por si mesmos.

## Verificações

| Verificação | Resultado |
|---|---|
| Controle negativo do worker, com o reagendamento anterior | Novo teste falhou com próxima aquisição em aproximadamente **59,986 s** |
| Mesmo teste com a correção | Passou; rechecagens limitadas a 2 s, sem chamada/ledger antes da liberação e uma única geração ao liberar |
| Controles negativos do limitador anterior | Dois testes reproduziram consumo de cota em tentativas negadas |
| Admissão corrigida | Passou em negações repetidas, concorrência, cancelamento e contenção; não ultrapassou a cota |
| Suíte backend final, PostgreSQL 17 descartável | **287 aprovados, 1 OCR ignorado** por indisponibilidade do runtime local |
| Testes offline do avaliador | **24 aprovados**, incluindo extração seletiva e proteção de conteúdo |
| HTTP com API/PostgreSQL reais e transporte de IA em memória | Passou; **zero chamadas externas** |

Na prova HTTP final, o documento fictício foi previamente salvo para conferir
transporte/timings; ela não substitui os testes de aprovação. O transporte
simulado introduziu atrasos conhecidos de 120 ms na contagem e 180 ms nos
cabeçalhos, que apareceram separadamente nas medições:

| Medição sintética | Tempo observado |
|---|---:|
| Interação completa, incluindo consultas do avaliador | 2,337 s |
| Contagem de entrada | 143,396 ms |
| Cabeçalhos do provedor | 184,313 ms |
| Geração, incluindo cabeçalhos/leitura/validação | 192,326 ms |
| Publicação | 31,608 ms |

Esses números validam a instrumentação local. **Não são uma redução medida dos
63,941 s do modelo real** e não estabelecem SLA. Os processos e bancos
descartáveis foram encerrados.

## Reprodução e pendências

Prova HTTP sem chave e sem chamadas externas, com Docker/PostgreSQL 17 e .NET 10:

```bash
python3 tools/smoke-tests/assistant-latency-http.py
```

Testes PostgreSQL usam `JURIDICOAI_REVIEW_TEST_POSTGRES` apontando exclusivamente
para banco descartável; a fixture cria/remove bancos próprios. Os testes
relevantes estão em `AssistenteGenerationPostgresTests`, `AssistenteIaGovernanceTests`
e `AssistenteLatencyTests`. A suíte Python continua no comando documentado no
[README do avaliador](../../../../avaliacoes/assistente-conversacional/README.md).

Resta comparar a consulta original com o modelo real, usando a instrumentação
nova e orçamento definido. Não elevar cotas, mudar a unidade de admissão ou
reduzir contexto para aparentar melhoria. P03 continua sendo o próximo
desenvolvimento do fluxo; P04 ainda precisa de monitoramento/alertas operacionais
completos. A correção local de M01 não conclui essas entregas.

## Atualização posterior — teste real concluído

Após este recorte local, o responsável autorizou a comparação com chave temporária.
A [rodada real](m01-latencia-real-2026-11-14.md) concluiu o cenário de três
interações: consulta de 63,941 s para 22,697 s, custo total de US$ 0,03260130.
M01 foi encerrado no recorte de diagnóstico/correção/comparação; a política de
espera remanescente por cota é acompanhada separadamente em M03. Os números
sintéticos deste documento permanecem identificados como tal.
