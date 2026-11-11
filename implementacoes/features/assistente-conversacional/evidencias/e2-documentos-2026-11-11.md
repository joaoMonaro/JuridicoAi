# Anexos privados, extração e contexto documental

Data: 2026-11-11. Implementação local no backend e em `../JuridicoAi-frontend`.
Recorte funcional da E2: documentos nativos, pipeline de OCR com gate separado e
worker documental durável. Não declara toda a E2 nem o assistente completo entregues.
Sem deploy, commit ou push desta etapa. Configuração e banco do usuário preservados.

## Fluxo implementado

- Upload de PDF, DOCX, TXT, PNG e JPEG no chat, com idempotência por
  clientUploadId, assinatura de formato, contagem de bytes e cotas por conversa.
- Originais privados usam namespace próprio de armazenamento por organização e
  conversa, reutilizando IFileStorage. Nem arquivos nem derivados passam ao Caso.
- O painel lista anexos próprios e documentos atuais autorizados do Caso, mostra
  processamento, trechos utilizáveis/excluídos e problemas por página/localizador.
  Baixar e reprocessar exigem autorização; não há URLs públicas de arquivo.
- Anexos aguardam associação ao pedido. Arquivos enviados mas não associados
  permanecem disponíveis para seleção; não são excluídos por TTL. Depois da
  associação, passam a integrar o contexto elegível da própria conversa.
- Documentos atuais do Caso são enfileirados automaticamente. A fila é gravada
  antes da reserva da mensagem, inclusive no primeiro envio sem listar anexos.
- Pedidos que dependem de arquivos ainda em processamento retornam um estado
  explícito de espera; não chamam o provedor nem afirmam leitura concluída.
- Somente trechos `Confiavel` entram no contexto e no índice. Resultados parciais
  acrescentam alertas no servidor, independentemente de o modelo lembrá-los.
- Fontes têm hash, versão de extração/política, tentativa identificada por token
  e manifesto persistido na mensagem. Reprocessamento/remoção de fonte revoga
  sua reutilização; histórico legível e conteúdo aprovado não são apagados.
  Propostas dependentes de fonte inelegível também são omitidas do novo contexto
  quando referenciadas, evitando reintroduzir conteúdo revogado pelo rascunho.
- Publicação e aprovação revalidam e bloqueiam as fontes em transações curtas.
  Uma fonte alterada/reprocessada impede usar uma proposta antiga. A IA continua
  preparando propostas, sem aplicar alterações diretamente na Peça.

## Extração e qualidade

| Formato | Caminho efetivo |
|---|---|
| TXT | UTF-8 estrito, BOM opcional; codificação inválida sinalizada |
| DOCX | OpenXml 3.5.1; parágrafos e tabelas simples, cabeçalhos/rodapés, imagens internas pelo pipeline de OCR |
| PDF nativo | Poppler `pdfinfo`, `pdfimages -list`, `pdftotext -bbox-layout`; texto e localização por página |
| PDF misto/digitalizado | Identificação de regiões gráficas; renderização com `pdftoppm` e OCR quando habilitado |
| PNG/JPEG | SkiaSharp valida formato/dimensões e normaliza orientação EXIF antes do OCR |

Foi reutilizado Poppler também para extração nativa, evitando adicionar PdfPig
antes de sua avaliação. Coordenadas do bbox permitem rejeitar conservadoramente
colunas paralelas/associações incertas. PDF não renderizável/protegido retorna falha.
DOCX não recebe paginação inventada: usa parágrafo/tabela/linha. Tabelas com células
mescladas, campos dinâmicos e notas não cobertas são sinalizados. ZIP tem limites;
macros, relações externas e campos dinâmicos não são executados.

Trechos preservam unidades de sentido; uma palavra duvidosa não é apagada para
transformar o restante em fonte confiável. Unidades longas são segmentadas sem
atravessar regiões reprovadas; unidades não segmentáveis são reprovadas.
Texto nativo não recebe confiança de OCR fictícia. Veracidade dos documentos
continua distinta da qualidade de extração.

Processos locais usam argumentos separados, sem shell, stdout/stderr limitados,
timeout e encerramento da árvore. Temporários têm diretório privado e são
removidos ao concluir/cancelar; originais não são apagados pelo processamento.

### Resultado do OCR

Tesseract 5.5.0 com Leptonica 1.86.0, idiomas por+eng. Os modelos são registrados
por SHA-256 na versão da política da extração. Poppler local: 26.01.0.
Referências: [TSV do Tesseract](https://tesseract-ocr.github.io/tessdoc/Command-Line-Usage.html)
e [bbox-layout do Poppler](https://manpages.debian.org/trixie/poppler-utils/pdftotext.1.en.html).

A [medição inicial](documentos/calibracao-ocr-inicial-2026-11-11.json) executou
texto impresso limpo, desfocado e girado, com anotações fictícias. A política
proposta (média ponderada ≥97, palavras ≥90 e campos materiais ≥98) aceitou
**zero unidades**. Não houve falso aceite nessa amostra porque nada foi aceito;
isso não comprova qualidade. A cobertura útil é insuficiente e o corpus pequeno.
**OCR não aprovado para rollout externo.** Os limiares não foram reduzidos para
aparentar uma leitura completa. Ampliar o corpus, comparar estratégias e calibrar
erro/cobertura continua pendente da E0/E2.

O pipeline real de OCR está testado, mas desligado por padrão. Sem ele, imagem
e região digitalizada ficam marcadas como não lidas; o texto nativo aproveitável
do mesmo arquivo pode ser utilizado com alerta explícito de leitura parcial.

## Persistência e execução

Migration `20261111191036_AddAssistantDocuments`, após as três migrations da
conversa/revisão. Tabelas `ai_documents` e `ai_document_chunks`, manifesto opcional
em `ai_messages`, FKs compostas restritivas e CHECK de origem privada/compartilhada.
Documentos do Caso são referenciados sem FK que impeça sua remoção normal;
elegibilidade sempre exige que o registro atual ainda exista e tenha o storageId
esperado no mesmo Caso/organização.

O registro documental também é o trabalho durável. `FOR UPDATE SKIP LOCKED`
reserva um arquivo, com lease e fence. Progresso e trechos são persistidos em
transações curtas. Após expiração, outra instância pode reiniciar a extração,
substituindo os checkpoints anteriores; worker antigo não publica depois de
perder o lease. Até três tentativas de recuperação; reprocessamento manual é
explícito. A retomada refaz o arquivo, não apenas as páginas restantes.
Não há lock aberto durante OCR ou chamada ao modelo. A geração da resposta
continua request-bound; este worker não implementa execução durável da IA.

## API e contexto efetivos

Prefixo `/assistente/v2/conversas/{conversaId}`:

| Rota | Comportamento |
|---|---|
| `POST /anexos` | Multipart arquivo + clientUploadId; 202 com estado inicial |
| `GET /anexos` | Lista fontes privadas e atuais do Caso, progresso e problemas |
| `GET /anexos/{id}` | Estado e qualidade da fonte autorizada |
| `GET /anexos/{id}/arquivo` | Download autenticado, attachment e no-store |
| `POST /anexos/{id}/reprocessar` | Novo processamento autorizado; invalida revisão anterior |
| `GET /fontes/busca?consulta=&limite=12` | Busca full-text português e texto exato, filtrando acesso/qualidade antes do ranking/LIMIT |
| `POST /interacoes` | Acrescenta anexoIds opcionais, mantendo pedido natural e SSE 200 |

A montagem atual inclui integralmente os trechos elegíveis das fontes associadas
e do Caso, com origem e localização; até 60.000 caracteres de texto documental,
mais o limite global de contexto da conversa (80.000 por padrão). Excesso retorna
erro explícito e preserva os arquivos, sem truncar silenciosamente. A busca já
existe no backend, mas ainda não há loop de ferramentas que a use seletivamente
para superar a janela. Recuperação extensa com manifesto de cobertura e memória
versionada permanece pendente; não declarar “todos lidos” após top-k.

## Ativação e operação

As flags anteriores de revisão/histórico/conversa livre precisam estar ativas.
Depois da migration, habilitar `AssistenteDocumentos__Enabled=true` e instalar
Poppler no host de execução. A capacidade `documentos` informa os limites e a
disponibilidade efetiva de OCR. As novas finalidades exigem aviso com sufixo
`-documents-v1`; aceite antigo não é reaproveitado.

OCR permanece `OcrEnabled=false`. Ativação futura exige calibração identificada,
`OcrCalibrationVersion`, `TesseractPath` e `TessdataPath` absoluto com por/eng.
Usar modelos com os hashes medidos e fornecer as bibliotecas nativas necessárias.
Nenhum pacote foi instalado globalmente no computador do usuário: o Tesseract
do teste foi extraído para `/tmp`; sem serviços externos ou chamadas reais de IA.

Defaults: 20 MiB por arquivo, 100 páginas PDF, 25 MP por imagem, 50 anexos e
500 MiB por chat, ZIP DOCX até 100 MiB/10.000 entradas, texto extraído até
2.000.000 caracteres; timeout de processo 60s, lease 240s. Um worker por instância;
instâncias podem trabalhar simultaneamente com SKIP LOCKED. Listagem limita
250 fontes e informa excesso. Valores continuam configuráveis dentro dos tetos.

Desligar a flag preserva arquivos, mensagens e extrações. Downgrade é bloqueado
se apagaria documentos ou proveniência. Área pessoal após perda de Membership e
exclusão manual autorizada continuam em etapa própria; não há expurgo automático.

## Verificação

- 217 testes backend com PostgreSQL descartável, Poppler e Tesseract locais,
  sem skips. Incluem isolamento, idempotência, origem/qualidade, recuperação de
  lease, fence perdido, cotas, fonte removida/reprocessada, publicação/aprovação
  e primeiro envio com documentos do Caso sem listagem anterior.
- 114 testes frontend; 20 E2Es focados em conversa/revisão/documentos.
- Build e lint do frontend e build backend passaram; permanece aviso anterior
  de bundle maior que 500 kB. EF não aponta divergência do modelo/migration.
- Navegador com frontend/API/PostgreSQL reais: fonte do Caso automática, upload
  privado, leitura parcial, exclusão do trecho duvidoso do input capturado,
  busca, proposta, aprovação e reload. Provedor simulado, sem avaliação semântica
  ou jurídica com modelo real. Captura conferida em
  `../JuridicoAi-frontend/test-results/assistant-documents-api.png` (ignorada).
- Testes e servidores usaram portas livres escolhidas dinamicamente; a porta
  5173 foi preservada. PostgreSQL descartável e dados sintéticos somente.

Não declarar a suíte legada completa de Playwright aprovada; falhas anteriores
permanecem registradas na E0. Esta entrega não substitui calibração de OCR,
avaliação com o modelo real, memória extensa, geração em etapas ou exclusão.
