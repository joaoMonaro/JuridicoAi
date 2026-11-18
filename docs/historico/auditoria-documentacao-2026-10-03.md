# Auditoria da documentação — 2026-10-03

> **Tipo:** registro histórico de auditoria
>
> **Estado:** concluída para o conteúdo existente nesta data
>
> **Referência atual:** [`docs/README.md`](../README.md)

## Objetivo e critério

A auditoria comparou a documentação versionada com a estrutura do repositório,
rotas da API, configurações do assistente e entregas registradas nos commits
recentes. Cada arquivo foi classificado como documentação permanente,
documentação viva de implementação ou registro histórico.

O critério de organização adotado é:

```text
docs/             comportamento atual e operação estável
implementacoes/   especificações, decisões, pendências e evidências em evolução
avaliacoes/       datasets, rubricas, baselines e artefatos de avaliação
docs/historico/   visão e auditorias antigas, sem valor normativo atual
```

## Alterações realizadas

| Área | Constatação | Correção |
|---|---|---|
| Assistente de IA | documentos de naturezas diferentes estavam todos na raiz da feature | separados em `especificacoes`, `decisoes`, `planejamento` e `evidencias` |
| Índices | links apontavam para a estrutura plana anterior | índices e referências relativas atualizados |
| Escopo do projeto | o índice dizia que não havia cadastro público nem Swagger | descrição atualizada para refletir `/auth/register` e Swagger em desenvolvimento |
| Assistente implementado | fatos estáveis estavam somente na documentação da feature | criado `docs/backend/assistente-ia.md` |
| API | resumo não listava transparência e vários grupos de Caso | índice de endpoints atualizado |
| Configuração | configurações de autenticação e IA não apareciam no guia principal | guia ampliado e ligado à página de User Secrets |
| Peças | o editor ainda era descrito como futuro | texto corrigido para refletir editor e autosave existentes no frontend |
| Produção | troubleshooting ainda recomendava “adicionar autenticação” | orientação substituída pelos riscos realmente pendentes |
| Postman | a coleção se apresentava como completa, mas não cobre SSE do assistente | escopo explicitado; contrato SSE permanece em documento próprio |
| Visão inicial | texto conceitual aparecia junto da documentação corrente | movido para `docs/historico` e marcado como não normativo |
| Revisão do scraper | auditoria antiga estava dentro dos documentos canônicos do scraper | movida para `docs/historico`; links canônicos preservados |

## Situação dos documentos antigos

| Documento ou grupo | Classificação após auditoria |
|---|---|
| `docs/arquitetura.md` | permanente; atualizado |
| `docs/configuracao-e-execucao.md` | permanente; atualizado |
| `docs/user-secrets.md` | permanente e atual |
| `docs/backend/*.md` | permanentes; inconsistências pontuais corrigidas |
| `docs/scraper/{visao-geral,cli,integracao-tjsp}.md` | permanentes; scraper fora do fluxo do MVP, mas ainda existente |
| `src/JuridicoAI.Scrapper/docs/fluxo-scraper-tjsp.md` | legado técnico preservado; não canônico |
| `implementacoes/features/assistente-ia/*` | documentação viva reorganizada por finalidade |
| `avaliacoes/*` | evidência executável; mantida fora de `docs` e `implementacoes` |

## Pendências documentais conhecidas

- a coleção Postman não representa o stream SSE do assistente; para esse fluxo,
  o contrato versionado e os testes automatizados são a referência;
- o checklist manual de teclado físico, leitor de tela e plataformas reais
  continua pendente;
- políticas jurídicas e comerciais definitivas de LGPD, suboperadores,
  transferência internacional e retenção precisam de validação especializada;
- orçamento financeiro, observabilidade completa e rollout nominal ainda devem
  ser promovidos para `docs/` quando forem efetivamente entregues;
- a futura criação de um harness de desenvolvimento da IA deve reaproveitar
  `avaliacoes/` e não duplicar datasets ou baselines dentro da documentação.

Esta auditoria é um retrato da data indicada. Mudanças futuras devem atualizar
a documentação permanente no mesmo commit e manter pendências na documentação
viva da feature.
