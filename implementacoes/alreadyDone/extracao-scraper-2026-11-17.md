# PROJ-002 — Extração do scraper

Concluído em **2026-11-17**, conforme o [TODO geral](../../TODO.md).

## Resultado

- Projeto independente em `/home/jao/reposJao/JuridicoAI.Scrapper`, com CLI na
  raiz, README, origem da extração e `.gitignore`. Publicado, a pedido do usuário,
  no repositório privado [joaoMonaro/JuridicoAI.Scrapper](https://github.com/joaoMonaro/JuridicoAI.Scrapper),
  branch `main`, commit `67cdd2d`.
- Código anterior de ingestão, normalização, classificação, persistência MongoDB,
  configuração e endpoints preservado em `arquivo/legado-integracao-api` no novo
  projeto, excluído da compilação. Não constitui uma API executável independente.
- Capturas de sessão e resultado local preservados em `arquivo-local`, ignorado
  pelo Git. Nenhum `.env` real ou user-secret foi copiado para o projeto extraído.
- Removidos da API as quatro rotas `/api/jurisprudencias`, os componentes
  exclusivos, a referência ao scraper e o pacote MongoDB. MongoDB e Playwright
  não aparecem nas dependências diretas ou transitivas da API.
- Solução, Compose, exemplo de ambiente, exemplos HTTP e documentação atual
  ajustados. Removidos os artefatos antigos de compilação `bin/obj` da raiz,
  pertencentes a um projeto anterior. Documentos históricos gerais preservados.
- Nenhum banco, volume existente ou arquivo dos fluxos jurídicos foi apagado.

## Validação

- `dotnet build JuridicoAI.sln --nologo`: zero avisos e zero erros.
- `dotnet test JuridicoAI.sln --no-build --nologo`, com
  `JURIDICOAI_REVIEW_TEST_POSTGRES` apontando para PostgreSQL 17 descartável:
  **291 aprovados, zero falhas, 1 ignorado**, total 292. O teste ignorado exige
  runtime OCR; OCR permanece fora do MVP.
- Quatro casos de regressão HTTP verificam retorno 404 nas rotas removidas,
  incluindo consulta bruta, ingestão e reprocessamento.
- Aplicação real iniciada contra banco descartável: inicialização/migrations
  concluídas, quatro rotas removidas retornaram 404 e `/casos` sem autenticação
  retornou 401.
- `docker compose config --quiet`: válido, com somente PostgreSQL.
- CLI extraído: build sem avisos/erros e `--help` com saída zero.
- Nenhuma chamada à IA ou pesquisa no TJSP foi necessária para esta validação.

O PostgreSQL usado nos testes foi criado exclusivamente para esta tarefa, com
dados temporários. A extração não homologa qualidade jurídica, interface ou
implantação do MVP.
