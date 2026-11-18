# IA, documentos, memória e limites

> Desenho original de 2026-11-11, com situação reconciliada em 2026-11-12.
> As interfaces/ferramentas e a tabela de defaults abaixo preservam a proposta
> inicial e não equivalem aos nomes/valores atuais do código. Consultar
> [contratos e limites implementados](07-contratos-implementados.md).

Leitura nativa OpenXml/Poppler, recuperação por ferramentas, memória versionada,
análise documental durável e geração geral/minutas por seções estão implementadas.
Pipeline Tesseract existe, mas OCR está **desativado e em espera**. Orçamento
financeiro distribuído de produção continua pendente.

A [avaliação real reduzida](../evidencias/avaliacao-reduzida-gpt-6.1-terra-2026-11-12.md)
com GPT-6.1 Terra executou quatro cenários/seis interações e passou nos checks
com custo calculado de US$ 0,04549805. Memória extensa, análise integral documental
e homologação jurídica seguem abertas. O modelo do produto continua configurável
sem valor default; a avaliação não configurou automaticamente o ambiente.

A geração usa ferramentas como `planejar_minuta`, `registrar_secao` e
`revisar_minuta`, além das ferramentas documentais e de histórico, conforme as
[evidências da geração](../evidencias/e2-e3-geracao-duravel-2026-11-12.md).
`consultar_peca` e `preparar_alteracoes` na tabela do desenho original não são
nomes de ferramentas atualmente anunciadas ao modelo.

## Desenho original e decisões de referência

## Decisões de implementação

- Manter OpenAI Responses pelo adaptador existente, ampliando a interface para
  mensagens, itens de ferramentas, eventos e resultados tipados.
- Memória e arquivos na aplicação; não depender de Conversations ou File Search
  hospedados. Enviar histórico selecionado em input com store=false. A OpenAI
  documenta gestão manual do estado em
  [Conversation state](https://developers.openai.com/api/docs/guides/conversation-state).
- Ferramentas com strict=true, schemas fechados e campos opcionais como null;
  início com parallel_tool_calls=false. Execução e autorização das ferramentas
  permanecem no backend. Base: [Function calling](https://developers.openai.com/api/docs/guides/function-calling).
- store=false não equivale a uma garantia global de retenção zero em todos os
  mecanismos do provedor. Não alterar promessas do aviso sem verificar a configuração
  efetiva: [Data controls](https://developers.openai.com/api/docs/guides/your-data).
- Não trocar o modelo arbitrariamente. Manter `AssistenteIa:Model` configurável e
  exigir uma avaliação do modelo configurado com ferramentas, schema e geração
  extensa antes de habilitar v2. Não há fallback silencioso para outro modelo.

## Interfaces previstas

`IAssistenteConversationProvider`: recebe política, itens tipados de input,
schemas de ferramentas e limites; emite texto público incremental, chamadas
completas de ferramenta e resultado final com uso/modelo/status. Itens opacos
necessários ao replay do ciclo do provider permanecem associados à execução
privada; não expor raciocínio interno ao usuário. Chamada incompleta nunca se
converte em ferramenta executável com JSON parcial.

`IAssistenteToolDispatcher`: allowlist de ferramentas, autorização por recurso,
validação de schema/argumentos, duração e tamanho de resultado. Recebe a identidade
da execução, não um OrganizationId escolhido pelo modelo.

`IAssistenteContextAssembler`: seleciona fontes e gera manifesto de conteúdo;
`IDocumentTextExtractor`, `IDocumentOcrProvider`, `IDocumentQualityEvaluator` e
`IAssistenteBudgetService` completam o pipeline.

## Ferramentas do modelo

| Ferramenta | Argumentos relevantes | Retorno autorizado |
|---|---|---|
| `consultar_peca` | aliases opcionalmente selecionados | snapshot/aliases e conteúdo da Peça da execução |
| `listar_fontes` | filtro de tipo opcional | metadados, cobertura e IDs das fontes autorizadas |
| `buscar_fontes` | consulta, limite ≤ 12 | trechos elegíveis, fontes e localização |
| `ler_fonte` | sourceId, página/faixa | conteúdo elegível e cobertura, não arquivo arbitrário |
| `consultar_historico` | consulta ou intervalo de sequência | mensagens da própria conversa, com proveniência |
| `preparar_alteracoes` | aliases-base, grupos e conteúdo proposto | rascunho privado validado ou erro de compilação |

Gerar minuta extensa é um workflow do orquestrador, invocável a partir da intenção
natural. Não precisa de uma “ferramenta que chama a si própria” nem de agente com
acesso livre ao banco. Intenção pode ser sugerida pelo modelo, mas autorização
efetiva é calculada pelo servidor: AI_ASSISTANT para conversa, AI_EDIT para edição,
AI_REVIEW para revisão estruturada e AI_DOCUMENT_GENERATION para minuta integral.
Verificar também cobertura total do corpo para não contornar geração integral
por uma coleção de splices de edição. A consulta livre informativa não recebe
um modo técnico obrigatório.

Não disponibilizar ferramenta aplicar/aprovar, shell, SQL, navegador ou scraper.
Um pedido não autorizado deve produzir explicação útil sem proposta aplicável.

## Contexto e recuperação

Montagem por chamada, respeitando o orçamento efetivo:

1. Política versionada e catálogo de ferramentas autorizado.
2. Pedido atual, proposta referenciada e seleção opcional ligada ao snapshot.
3. Memória versionada: fatos com origem, correções, preferências privadas,
   decisões e pendências, distinguindo aprovado/rejeitado/informado pelo usuário.
4. Últimas mensagens e recuperação seletiva do histórico anterior.
5. Estrutura da Peça atual e aliases necessários. Documento grande usa mapa
   completo de seções e consultas a blocos; nunca chama leitura parcial de leitura total.
6. Dados atuais do Caso e trechos documentais relevantes elegíveis.

Busca inicial: PostgreSQL full-text em português mais busca exata por nomes,
valores e identificadores, sempre filtrando escopo e qualidade antes de LIMIT.
Quebrar por parágrafos/página, alvo de 800 tokens com sobreposição de até 100;
não atravessar uma região reprovada para juntar duas partes legíveis.

Para “considere todos os documentos”, listar o conjunto fechado de fontes e criar
checkpoints de cobertura, lendo cada parte elegível e produzindo resumos com
referências. Top-k lexical sozinho não atende essa instrução. Se exceder orçamento,
retornar Incompleta e indicar fontes não cobertas; não alegar leitura integral.
Busca semântica só entra se a avaliação mostrar falhas materiais da busca inicial.

Memória inclui throughSequence e sourceVersion/hash. Antes de reutilizar, revalidar
fontes compartilhadas e reprocessamentos; invalidar afirmações de extrações
substituídas/reprovadas. Mensagens antigas do assistente não promovem um trecho
de OCR reprovado a fonte confiável. Histórico integral permanece legível, mas a
montagem do contexto filtra conteúdo derivado sem fonte elegível para novos pedidos.
Declaração nova do usuário pode servir como informação do usuário, com essa origem.

## Extração escolhida para implementação inicial

| Entrada | Caminho |
|---|---|
| TXT UTF-8 (BOM opcional) | Leitura estrita; codificação inválida sinalizada, sem substituição silenciosa de caracteres |
| DOCX | DocumentFormat.OpenXml já presente; extrair parágrafos, tabelas como linhas/células e imagens incorporadas |
| PDF com texto | PdfPig como dependência candidata para extrair texto e posições por página |
| PDF escaneado ou misto | Renderizar páginas necessárias com Poppler/pdftoppm e OCR local |
| PNG/JPG/JPEG | Validar decodificação, dimensões e orientação; OCR local |

PdfPig expõe letras e posições para reconstruir ordem de leitura; PDF não garante
que a sequência interna corresponda à leitura visual. Conferir saída com corpus
multicoluna, conforme [documentação do PdfPig](https://github.com/UglyToad/PdfPig).
Poppler é o rasterizador proposto: [projeto oficial](https://poppler.freedesktop.org/).

OCR inicial: Tesseract via processo local, idiomas por+eng, modelos registrados
por hash, saída TSV com posição e confiança por palavra. Base documental:
[Tesseract CLI](https://tesseract-ocr.github.io/tessdoc/Command-Line-Usage.html).
Essa escolha evita contratar serviço externo nesta fase. Deve passar pela
calibração antes de ser considerada suporte entregue.

DOCX não possui paginação estável sem renderização: localizar por parágrafo/tabela
e índice, não inventar página. Relações externas, macros e campos dinâmicos não
são executados; imagens internas passam pelo mesmo OCR. Tabelas com associação
incerta de célula/valor ficam reprovadas, não convertidas em narrativa por suposição.
PDF protegido por senha ou conteúdo não renderizável retorna erro visível.

Processos usam ProcessStartInfo.ArgumentList sem shell, diretório privado,
timeout e encerramento de árvore como no conversor de PDF existente. Não expor
filesystem/network arbitrários ao parser. Expansão de DOCX (ZIP) tem limite de
tamanho e entradas. Rejeitar SVG, URLs e documentos remotos como upload de imagem.
Não adicionar .doc legado implicitamente ao assistente só porque o armazenamento
atual de documentos do Caso aceita essa extensão.

## Qualidade: política inicial a calibrar

Estados de trecho: Confiavel, Duvidoso, Ilegivel. Só Confiavel é indexado ou enviado
ao modelo. Armazenar bounding boxes, métricas e motivo; a interface expõe as regiões
afetadas sem pedir ao usuário para confiar numa classificação opaca.

Proposta conservadora para corpus inicial de OCR:

- Confiança média ponderada por caracteres ≥ 97, nenhuma palavra < 90 no trecho,
  e palavras que contenham datas/valores/identificadores ≥ 98.
- Ausência de U+FFFD, controles inesperados, corte de linha/célula e ambiguidade
  detectada na ordem de leitura. Divergência em campo material entre tentativas
  de OCR/extração marca o trecho como Duvidoso, não escolhe uma versão por média.
- Sem confiança retornada, nunca atribuir 100. Texto nativo tem critérios próprios:
  mapeamento de caracteres, ordem, cobertura e integridade, não score de OCR falso.
- Fragmento reprovado não vira um “buraco” removido dentro de frase elegível;
  reprovar a unidade de sentido dependente e mostrar a lacuna.
- Manuscrito, carimbo sobreposto ou fotografia degradada só é aceito se passar
  nos mesmos critérios e avaliação; a presença de texto reconhecido não basta.

Esses números são hipóteses de calibração, não probabilidade de correção nem
promessa de zero erro. Gate: corpus fictício anotado, comparação caractere/palavra,
zero erro em campos materiais nos trechos aceitos da amostra e inspeção de falsos
aceites, além da cobertura útil por categoria. Reportar rejeição excessiva; não
baixar limiar apenas para aparentar leitura completa. Sem gate aprovado, OCR fica
desabilitado no rollout externo, com funcionalidade explicitamente não entregue.

## Geração extensa e validação

Workflow: fixar snapshot/fontes → plano de seções → gerar partes com fatos e
fontes comuns → consolidar → conferir repetições, lacunas e preservação → compilar
proposta tipada → disponibilizar. Nenhuma parte é aplicada durante o workflow.

Falha ou budget interrompido preserva checkpoint marcado incompleto. Uma retomada
exige acesso e fontes ainda válidos; se a Peça mudou, gerar a comparação contra
nova base e reapresentar. Minuta completa exige corpo completo e flags adequadas.

Validar schema, operações permitidas, hashes, extensão e referências. A validação
determinística não comprova mérito jurídico. Avaliação semântica e revisão humana
continuam necessárias para fidelidade ao pedido e às fontes.

## Defaults inicialmente propostos — não usar como configuração efetiva

Valores da proposta inicial. Defaults efetivos, inclusive mensagem de 4.000
caracteres, HTTP de 512 KiB, saída de 6.000 tokens e prazo de geração de 1.800 s,
estão na especificação 07 e nas classes Settings. Não são tarifas ou SLAs aprovados.

| Configuração | Inicial |
|---|---|
| Texto por mensagem | 20.000 caracteres; JSON de comando até 1 MiB |
| Arquivo | 20 MiB; PDF até 100 páginas; PNG/JPEG até 25 megapixels |
| DOCX expandido | 100 MiB e até 10.000 entradas |
| Chat | até 50 anexos e 500 MiB de originais privados; atingir limite bloqueia novo upload, não apaga histórico |
| OCR | raster alvo 300 DPI, reduzido até 25 MP; timeout 60s por página, 2 processos por worker |
| Input por chamada | menor entre 60.000 tokens e janela declarada do modelo menos saída/overhead; contagem inclui ferramentas |
| Saída por chamada | 4.000 tokens em resposta; 6.000 em geração de seção |
| Geração | até 8 seções e 12 chamadas, incluindo consultas/resumos/correções; soma de input+output até 180.000 tokens por execução |
| Corpo final proposto | até 120.000 caracteres, 30.000 nós, profundidade 24 e listas até 9 níveis, compatível com limites do exportador |
| Chamada ao provider | 90s, como timeout atual, consumido dentro do prazo total |
| Execução em geração | até 10min; aguardando documentos até 30min, sem segurar slot de provider |
| Concorrência/rate | preservar 10/min e 2 ativas por usuário; 30/min e 6 ativas por organização; considerar fila/OCR separadamente |

Aplicar limites de corpo por endpoint: middleware atual só cobre solicitacoes
legadas; não limita as novas rotas. Multipart tem teto de 20 MiB + overhead de
campos, com leitura contada mesmo sem Content-Length. Tokens/tentativas são
reservados no banco antes da chamada e liquidados depois; preços não configurados
não podem ser tratados como custo zero.

O teto financeiro por organização é configuração operacional obrigatória para
piloto, sem valor em USD presumido. Até configurar tarifas válidas e orçamento,
v2 externo fica indisponível; desenvolvimento usa limite explícito de tokens.
Reserva pessimista deve incluir uso indeterminado após queda de conexão e ser
reconciliada sem permitir excedente por liberações antecipadas. O ledger soma
chamadas de OCR externo se houver evolução futura, resumo, leitura e geração.

Não elevar automaticamente limites para completar um pedido. Informar o limite,
as fontes/partes não cobertas e o rascunho preservado. Medições do corpus e do
piloto determinam ajustes, sem pedir ao usuário escolhas numéricas de engenharia.
