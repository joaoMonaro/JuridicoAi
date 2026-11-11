# Plano de implementação e evidências necessárias

> Planejamento técnico, 2026-11-11. Somente itens marcados têm evidência de execução.

Atualização de implementação: [recorte de E1/E4 integrado ao editor](../evidencias/e1-revisao-persistida-2026-11-11.md).
Inclui persistência de conversas/propostas, mensagens associadas a propostas,
commit transacional e reconciliação com autosave. As etapas abaixo permanecem
abertas onde exigem chat completo, fila, documentos ou validação integral.

Etapa seguinte: [histórico completo e contexto básico](../evidencias/e1-e3-historico-2026-11-11.md).
Persistência e recuperação abrangem perguntas/respostas, com política de
continuidade e Peça atual no input. A [conversa livre](../evidencias/e3-conversa-livre-2026-11-11.md)
adiciona intenção automática e ajuste de propostas. Resumos, busca e worker
continuam pendentes; a geração ainda é ligada à requisição.

## Decisões técnicas consolidadas

| Tema | Direção para implementar |
|---|---|
| Aplicação | Monólito modular ASP.NET Core existente |
| Persistência | PostgreSQL 17 e JSONB; fila durável no mesmo banco |
| Processamento | BackgroundService com leases, fence e checkpoints |
| Arquivos | IFileStorage com namespace privado por conversa e volume persistente |
| Autorização histórica | Esquema pessoal separado, sem Membership como requisito de leitura própria |
| IA | Responses com memória da aplicação, ferramentas fechadas e nenhuma gravação pelo modelo |
| Documentos | OpenXml existente, PdfPig candidato, Poppler e Tesseract locais |
| Recuperação | PostgreSQL full-text + pesquisa exata + cobertura integral quando solicitada |
| Editor | TipTap 3 atual, previews via plugin/decorations; aplicação por splices versionados |
| Ancoragem | Aliases de snapshot na v1, sem migração de IDs persistentes |
| Commit | Uma transação para Peça, decisões e recibo; reconciliação idempotente |

As escolhas são propostas de engenharia. Compatibilidade e qualidade dependem
das verificações abaixo; não há aprovação automática de fornecedores ou custos.
Não criar um segundo backend, outro banco vetorial ou serviço cloud nesta etapa.

## Etapas com dependências

### E0 — contratos e provas de compatibilidade

- [ ] Congelar fixtures de schema do editor com texto, marks, listas, links,
  headings, emoji e blocos desconhecidos existentes.
- [x] Validar splice + diff + undo/redo local no TipTap instalado, com exemplo
  fictício em `/dev/revisao-peca`; integração transacional permanece na E4.
- [ ] Medir leitura/OCR com corpus fictício e calibrar rejeição/aceitação.
- [ ] Validar modelo configurado com ferramentas e saída estruturada, com execução
  real autorizada e orçamento explícito; não usar dados jurídicos reais em logs.
- [ ] Registrar versões exatas e condições de redistribuição das dependências.

E0 valida as decisões de engenharia, sem exigir que o usuário escolha APIs ou
algoritmos. Se mudar experiência, custo contratado ou tratamento dos dados,
trazer a consequência concreta para decisão de produto.

### E1 — persistência, acesso e fila

- [x] Migrations aditivas e repositórios do recorte de revisão, índices e FKs compostas.
- [x] Conversa por usuário/Peça, mensagens gerais paginadas e idempotência no banco.
- [ ] Sessão pessoal de histórico, UI correspondente e matriz de autorização.
- [ ] Worker de leases/checkpoints/eventos, exclusão autorizada e proteção de resultados tardios.

### E2 — documentos e contexto

- [x] Recorte de upload privado, qualidade por trecho, extração nativa PDF/DOCX/TXT,
  fonte do Caso automática, progresso no frontend e worker documental com fence.
- [x] Pipeline OCR real, métricas e corpus sintético inicial; calibração não aprovada,
  portanto OCR desligado no rollout. Ver [evidências E2](../evidencias/e2-documentos-2026-11-11.md).
- [x] Busca full-text/exata filtrada e manifesto documental; fonte revogada ou
  reprocessada invalida publicação, aplicação e reutilização de histórico derivado.
- [ ] Upload privado, extração/OCR por página e indicadores de qualidade.
- [ ] Extração dos documentos já cadastrados no Caso, com versão e autorização.
- [ ] Busca filtrada antes de ranking, memória com proveniência e invalidação.
- [ ] Limites de arquivo, páginas, custo e recuperação após falha.

### E3 — conversa com IA e geração

- [x] Persistência/recuperação de perguntas e respostas e contexto cronológico
  básico, com fonte atual da Peça e rejeições, dentro da janela disponível.
- [ ] Adaptador ampliado, ferramentas, orçamento e prompts versionados.
- [x] Conversa sem modos técnicos, intenção e grupos na mesma saída estruturada,
  propostas validadas e abertura direta da comparação atrás de flag.
- [x] Ajuste de proposta referenciada substitui pendências atomicamente,
  preservando grupos aprovados; verificado com provedor simulado.
- [ ] Continuidade real, correções de fatos, ajustes em proposta referenciada.
- [ ] Geração extensa com checkpoints e distinção de resultado incompleto.
- [ ] Ledger por chamada e somatório por execução, sem conteúdo jurídico.

### E4 — edição visual e aplicação

- [ ] Compilador de propostas, hashes, grupos e diff determinístico.
- [ ] ReviewExtension/toolbar, acessibilidade e paginação de revisão.
- [ ] ensurePersisted e coordenação de commit externo sem PATCH duplicado.
- [ ] Serviço transacional, recibos, reconciliação e aplicação parcial.
- [ ] Undo/redo, preservação de marks e exportação sem previews.

### E5 — integração e ativação

- [ ] Jornada completa com conteúdo fictício e múltiplos usuários.
- [ ] Capacidades/flag de rollout v2 por organização. Gate separado das flags de operação.
- [ ] UI ativa v1 ou v2 por sessão, sem aplicar simultaneamente proposta legada
  sem registro e proposta nova no mesmo editor.
- [ ] Atualizar aviso quando necessário para refletir conversa persistida e
  anexos privados; não reutilizar consentimento de conteúdo diferente por conveniência.
- [ ] Plano de rollback preserva dados v2; desliga novas gerações/aplicações,
  não apaga chats, não faz downgrade destrutivo de banco nem reabre acesso.

## Matriz mínima de validação

| Cenário | Evidência exigida |
|---|---|
| Dois usuários, mesma Peça | Sem acesso ao chat/anexo/índice/recibo alheio; Peça aprovada é compartilhada |
| Sem Membership | Login pessoal lê/exclui histórico próprio; token não acessa Peça, Caso, geração ou aplicação |
| OCR ruim/parcial/misto | Trechos reprovados ausentes do contexto; falha e cobertura visíveis |
| Correção de fatos | Novo pedido usa correção, mantendo origem; informação antiga não ressurge do resumo |
| Conversa longa | Histórico íntegro, resumo versionado e recuperação relevante |
| Todos os documentos | Registro de cobertura ou declaração explícita de processamento incompleto |
| Aprovação | Resultado idêntico ao preview e nenhuma segunda confirmação |
| Duas aplicações simultâneas | Um commit ou mesmo recibo; sem duplicação nem gravação parcial |
| Rede cai após commit | Recibo resolve resultado; não sobrescreve versão mais recente |
| Autosave em andamento | Drena revisões; bloqueia alterações no commit; não dispara PATCH duplicado |
| Aprovação parcial | Restante preserva preview ou é desatualizado explicitamente |
| Desfazer/refazer | Um lote por evento de undo; persistência e exportação corretas |
| Exclusão durante execução | Cancelamento e fence impedem recriação de derivados |
| Restauração de backup | Exclusões autorizadas reaplicadas antes de abrir a aplicação |
| Budget/timeout/restart | Uso agregado, checkpoint consistente e nenhum rascunho truncado aplicável |

Backend: testes unitários para compilação e política; integração com PostgreSQL
real para locks, transações, constraints e falhas. InMemory não comprova esses
comportamentos. Frontend: Vitest/RTL para hooks/estado e Playwright para diff,
foco, paginação, rede, undo e autosave. Avaliações jurídicas e prompt injection
ganham casos de ferramentas, memória e anexos; baseline antiga não é transferida.

## Situação ao concluir o detalhamento, antes da implementação

Conferidos código backend/frontend e fontes oficiais; artefatos de contrato e
exemplos validados estruturalmente. Não foram executados testes de aplicação,
OCR, benchmarks, chamadas ao modelo, migrations ou alterações no frontend.
Datas, quantidades e limites são propostas de piloto, não resultados medidos.
Não há prazo fechado para a ampliação nem custos externos aprovados.

## Atualização da implementação

A prova local da E0 do editor foi implementada e verificada. Resultados e falhas
anteriores de regressão estão no
[registro E0 do editor](../evidencias/e0-editor-2026-11-11.md). A validação de OCR,
modelo e a integração transacional permanecem pendentes.
