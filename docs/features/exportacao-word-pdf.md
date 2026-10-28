# Exportação de peças para Word e PDF

**Status:** implementação disponível; aceite visual no Microsoft Word pendente.

Backend, interface e conversão local foram implementados e validados em
2026-10-28. A abertura no Microsoft Word precisa de conferência em um ambiente
com esse aplicativo e permanece explícita nos critérios de aceite.
Veja as [evidências da validação](evidencias/exportacao-word-pdf-2026-10-28.md)
e a [configuração operacional](../operacao/exportacao-word-pdf.md).

## 1. Objetivo

Permitir que o usuário baixe uma peça em **Word editável (`.docx`)** ou
**PDF com texto selecionável**, usando o conteúdo e a apresentação atuais da
peça: margens, fonte, espaçamentos, cabeçalho e rodapé.

A entrega tem quatro frentes: geração de DOCX, conversão para PDF, download
pela interface e validação dos arquivos. A importação fica para outra feature.

## 2. Ponto de partida antes desta implementação

- A peça já armazena `conteudoJson`, `pageConfig`, `styles`, `headerJson` e
  `footerJson`, além de `versao` para concorrência.
- A apresentação do modelo é copiada para a peça ao criar ou trocar seu modelo.
  Editar o modelo posteriormente não altera a peça existente.
- O editor tem autosave e exibe um documento contínuo. Sua estimativa de páginas
  não é uma paginação final de impressão.
- Cabeçalho com imagem/logotipo e rodapé textual configurável já existem.
- Ainda não existem endpoints ou ações de exportação de peças.

Referência do comportamento atual: [Peças e templates](../backend/pecas.md).

## 3. Escopo funcional

### 3.1. Exportar Word

- Gerar um arquivo `.docx` válido, com texto e estrutura editáveis no Word.
- Usar elementos próprios de documento para parágrafos, títulos, listas,
  cabeçalho e rodapé; não transformar a peça em uma imagem.
- Preservar o conteúdo suportado descrito na seção 4.
- Incorporar a imagem de cabeçalho ou logotipo quando configurada na peça.
- Permitir exportação tanto em rascunho quanto finalizada, sem mudar o status.

### 3.2. Exportar PDF

- Gerar um PDF a partir do mesmo DOCX produzido pelo exportador.
- Manter texto pesquisável, selecionável e copiável, incluindo acentos.
- Preservar as páginas e a apresentação produzidas pelo conversor configurado.
- Usar uma apresentação de impressão clara, independente de tema escuro, zoom,
  modo foco e tamanho da janela do editor.

### 3.3. Oferecer o download

- Adicionar uma ação **Exportar** na área de edição da peça, com as opções
  **Word (.docx)** e **PDF (.pdf)**.
- Exibir progresso indeterminado enquanto salva ou prepara o arquivo e impedir
  solicitações repetidas pela mesma ação durante esse período.
- Baixar o arquivo com nome derivado do título da peça e extensão correta;
  remover caracteres inválidos e usar `peca` quando não houver nome utilizável.
- Mostrar erros em português, com possibilidade de tentar novamente.
- Não exigir uma nova tela, configuração de exportação ou confirmação para baixar.

### 3.4. Usar a versão correta

- Antes de solicitar a exportação, aguardar o salvamento das alterações pendentes,
  inclusive uma troca de modelo. Acionar o autosave sem aguardar sua conclusão
  não atende a esse requisito.
- O pedido deve informar a versão efetivamente salva. Se houver erro de gravação
  ou conflito, não baixar silenciosamente uma versão anterior.
- A API deve capturar conteúdo e apresentação juntos, na versão solicitada, e
  gerar o arquivo a partir dessa cópia consistente.
- Alterações feitas depois dessa captura pertencem a uma exportação posterior.
- Exportar não altera conteúdo, apresentação, status nem versão da peça. Somente
  o salvamento prévio de alterações pode incrementar a versão.

## 4. Conteúdo e apresentação suportados

### 4.1. Corpo da peça

| Elemento atual | Comportamento esperado no arquivo |
|---|---|
| Texto, parágrafos e linhas vazias | Preservar ordem, caracteres e separações |
| Quebras de linha | Preservar sem substituir por espaços |
| Títulos e subtítulos | Preservar hierarquia e apresentação equivalente ao editor claro |
| Negrito, itálico, sublinhado e tachado | Preservar, inclusive combinações |
| Alinhamento de parágrafo | Preservar esquerda, centro, direita e justificado |
| Listas com marcadores e numeradas | Gerar listas próprias do Word, incluindo níveis existentes |
| Citações | Preservar conteúdo e distinção visual por recuo |
| Links | Preservar texto e destino como hiperlink |
| Código em linha/bloco e linha horizontal | Mapear os nós já aceitos pelo StarterKit, sem perda do conteúdo |

Não ampliar as ferramentas do editor para esta feature. Nós ou marcas que o
exportador não suporte devem produzir erro identificável; não omitir trechos nem
descartar formatação desconhecida silenciosamente.

### 4.2. Página e tipografia

- Usar papel A4 e as quatro margens armazenadas na peça.
- Respeitar a fonte, tamanho, entrelinha e alinhamento geral de `styles`.
- Aplicar o alinhamento explícito do parágrafo quando existir; nos demais casos,
  usar o alinhamento geral da peça.
- Definir um mapeamento documentado dos espaçamentos e estilos do editor para o
  DOCX. Margens artificiais usadas para simular páginas na tela não são conteúdo
  e não devem ser copiadas.
- Preparar as fontes do ambiente de conversão. Eventuais substituições precisam
  estar documentadas e passar pela validação visual; a fonte não deve depender
  de estar instalada na máquina do desenvolvedor.

### 4.3. Cabeçalho

- Exportar o texto e a imagem/logotipo efetivamente armazenados na peça.
- Preservar proporção, largura limitada à área útil, altura máxima, alinhamento,
  distância do topo e espaço até o corpo.
- Incorporar PNG/JPEG no arquivo, sem depender de URLs externas.
- Repetir o mesmo cabeçalho em todas as páginas do documento exportado.
- Manter compatibilidade com cabeçalhos textuais antigos, sem `presentation`.

### 4.4. Rodapé

- Preservar todas as linhas, inclusive linhas vazias, e a fonte do modelo.
- Respeitar tamanho, cor, alinhamento, linha divisória e distância da borda inferior.
- Repetir o mesmo rodapé em todas as páginas do documento exportado.
- Manter compatibilidade com rodapés antigos, sem `presentation`.

### 4.5. Paginação dos arquivos

- Reservar a altura necessária para cabeçalho e rodapé, ampliando a área reservada
  quando eles não couberem nas margens originais. Não permitir sobreposição com
  o corpo nem corte da imagem ou das linhas do rodapé.
- Rejeitar configurações sem espaço útil para o corpo com uma mensagem clara.
- Evitar títulos isolados no final da página e aplicar controle de linhas órfãs
  e viúvas. Parágrafos longos podem continuar na página seguinte.
- Não garantir que Word e PDF terão quebras idênticas em todos os aplicativos:
  o PDF é a saída fixa do ambiente de conversão; o DOCX pode ser repaginado ao abrir.
- O editor continua apresentando um documento contínuo nesta entrega. Não usar sua
  estimativa de páginas como critério para o total de páginas exportadas.

## 5. Direção técnica

### 5.1. Pipeline

1. Autorizar o acesso à peça pela Organization autenticada.
2. Verificar a versão e capturar os JSONs necessários em uma leitura consistente.
3. Converter o conteúdo e a apresentação para DOCX usando Open XML SDK no backend.
4. Para Word, devolver esse DOCX. Para PDF, convertê-lo com LibreOffice em modo
   sem interface e devolver o resultado.
5. Remover os arquivos temporários da solicitação, inclusive em falhas e cancelamentos.

Essa é a abordagem prevista para a primeira versão. Validar um documento de
várias páginas logo no primeiro incremento, antes de completar a interface.
O Open XML SDK produz a estrutura do DOCX; a renderização/paginação do PDF cabe
ao conversor, não ao SDK.

### 5.2. Contrato implementado da API

Endpoint autenticado:

```http
POST /pecas/{pecaId}/exportar
Content-Type: application/json

{
  "formato": "docx",
  "versao": 7
}
```

| Situação | Resposta |
|---|---|
| `formato: "docx"` | Arquivo com MIME `application/vnd.openxmlformats-officedocument.wordprocessingml.document` |
| `formato: "pdf"` | Arquivo com MIME `application/pdf` |
| Sucesso | HTTP 200, corpo binário e `Content-Disposition: attachment` |
| Formato, versão ou documento inválido | HTTP 400, explicando o problema |
| Peça excluída, inexistente ou de outra Organization | HTTP 404, sem revelar dados |
| Versão diferente da solicitada | HTTP 409; não exportar outra versão automaticamente |
| Conversor indisponível ou sem capacidade | HTTP 503, permitindo nova tentativa |
| Tempo de conversão esgotado | HTTP 504, sem devolver arquivo parcial |

Erros devem seguir o padrão `application/problem+json` da API. Quando necessário
ao download entre origens, expor `Content-Disposition` pela configuração de CORS.
As respostas de arquivo devem impedir cache compartilhado do conteúdo da peça.

### 5.3. Execução e armazenamento

- Gerar sob demanda, sem novas tabelas, migrações, histórico ou armazenamento
  permanente dos arquivos exportados.
- Usar diretório e perfil de conversão isolados por execução, impedindo mistura
  de arquivos entre usuários e conflitos entre instâncias do LibreOffice.
- Definir tempo máximo de execução e limite de conversões simultâneas. Calibrar
  esses valores com documentos representativos e informar falha quando excedidos.
- Encerrar o processo e limpar temporários quando houver timeout ou cancelamento.
- Não enviar o conteúdo para serviços externos ou usar IA na exportação.
- Não registrar conteúdo da peça, imagens ou arquivos gerados nos logs.
- Documentar instalação/configuração do conversor, fontes e diretório temporário
  para desenvolvimento e operação. Não criar uma nova plataforma de jobs nesta versão.

## 6. Fora do escopo

- Importar Word, PDF ou executar OCR.
- Exportar o formato antigo `.doc`, RTF, ODT ou outros formatos.
- Adicionar tabelas, imagens no corpo, notas de rodapé ou ferramentas de edição.
- Reproduzir integralmente qualquer documento externo ou a petição PDF de referência.
- Reformular o editor para exibir páginas com as mesmas quebras dos arquivos.
- Cabeçalhos/rodapés diferentes por página, primeira página especial ou páginas pares/ímpares.
- Acrescentar numeração de páginas configurável.
- Assinatura digital, protocolo judicial, PDF/A, proteção por senha ou certificação de acessibilidade.
- Exportação em lote, envio por e-mail, links públicos e integração com armazenamento externo.
- Histórico de exportações, cache persistente ou painel de administração de conversões.

## 7. Etapas de implementação

| Etapa | Entrega verificável |
|---|---|
| 1. Gerar DOCX | Peça curta e peça de várias páginas, com estilos, cabeçalho e rodapé, abrem como documentos editáveis |
| 2. Converter PDF | Os mesmos exemplos geram PDFs legíveis e selecionáveis, sem cortes nem sobreposição |
| 3. Integrar download | Ações de exportação aguardam o autosave, usam a versão correta e tratam progresso/erros |
| 4. Validar e documentar | Conferência dos formatos, cenários de falha e instruções para executar no ambiente da aplicação |

As etapas são partes de uma única feature. A entrega só está concluída quando
Word e PDF puderem ser baixados pela interface e atenderem aos critérios abaixo.

## 8. Critérios de aceite

- [x] Exportar a mesma peça em DOCX e PDF pela interface.
- [ ] Abrir o DOCX no Microsoft Word, sem aviso de reparação, e editar o texto.
- [x] Abrir o DOCX também no LibreOffice, sem corrupção ou perda de conteúdo.
- [x] Pesquisar, selecionar e copiar texto acentuado no PDF.
- [x] Preservar todo o texto e os elementos suportados da seção 4.
- [x] Conferir margens, fonte, entrelinha, alinhamentos, listas e títulos.
- [x] Conferir cabeçalho textual, imagem de marca e logotipo; a imagem mantém proporção.
- [x] Conferir rodapé com múltiplas linhas, cor, tamanho, alinhamento, divisor e distância inferior.
- [x] Em documento de várias páginas, repetir cabeçalho e rodapé sem sobreposição ou cortes.
- [x] Exportar peças sem modelo e com configurações antigas de cabeçalho/rodapé.
- [x] Usar a apresentação da peça, mesmo que seu modelo tenha sido alterado ou excluído depois.
- [x] Incluir edição ou troca de modelo imediatamente anterior ao clique, após salvamento confirmado.
- [x] Bloquear exportação de versão desatualizada, em conflito ou com erro no salvamento.
- [x] Exportar rascunho e finalizada sem modificar o estado persistido pela exportação.
- [x] Impedir exportação de peça de outra Organization.
- [x] Tratar conversor ausente, falha, timeout e concorrência sem arquivo parcial nem temporários abandonados.
- [x] Confirmar que tema escuro e zoom não alteram a aparência do arquivo.

## 9. Validação de qualidade

Usar documentos fictícios com os recursos da versão, incluindo um caso curto,
um de várias páginas e um com cabeçalho e rodapé próximos dos limites permitidos.

Automatizar a verificação de estrutura DOCX, preservação de texto, autorização,
controle de versão, download e falhas do conversor. Renderizar os documentos
gerados e conferir visualmente as páginas para validar a apresentação. Extração
de texto, sozinha, não comprova qualidade de layout.

Registrar ambiente, fontes e versões do conversor usados na conferência.
Testes locais no LibreOffice não substituem a abertura no Microsoft Word exigida
no aceite. Diferenças aceitáveis de repaginação não incluem conteúdo ausente,
imagens deformadas, texto cortado ou sobreposição.

## 10. Custo e dependências

- A abordagem usa Open XML SDK e LibreOffice, sem contratação de API de conversão
  nem cobrança por arquivo dessas ferramentas.
- O custo recorrente é o consumo de CPU, memória e armazenamento temporário da
  infraestrutura. Dimensionar após medir os exemplos de validação.
- O esforço está concentrado no mapeamento dos estilos, paginação dos arquivos,
  integração com o salvamento e preparação do conversor.
- A estimativa inicial de 4–7 dias foi uma referência conservadora de planejamento,
  não um prazo fechado. Refinar após validar o primeiro DOCX e sua conversão para PDF.

Referências técnicas:

- [Open XML SDK e licença MIT](https://github.com/dotnet/Open-XML-SDK).
- [LibreOffice: uso sem taxas de licença](https://www.libreoffice.org/faq/).
- [LibreOffice: conversão para PDF por linha de comando](https://help.libreoffice.org/latest/en-US/text/shared/guide/pdf_params.html).
- [Microsoft: limitações da conversão de PDF em documento editável](https://support.microsoft.com/en-us/word/opening-pdfs-in-word), relevante para a futura importação, fora desta entrega.
