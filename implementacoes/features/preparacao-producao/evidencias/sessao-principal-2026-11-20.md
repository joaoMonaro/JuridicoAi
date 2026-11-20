# Sessão principal — refresh/logout e isolamento

Data: **2026-11-20**. Verificação técnica local, com dados fictícios e PostgreSQL
17 descartável. Nenhum banco existente ou credencial real foi utilizado.

## Problema e implementação

O logout organizacional buscava a sessão pelo refresh atual. Uma rotação que
salvasse o novo hash antes dessa busca fazia o logout perder a referência à
sessão. Uma resposta atrasada de refresh podia entregar novas credenciais de
uma sessão que não tinha sido revogada.

- Cookie com `segredoDeRevogacao.parteRotativa`, mantendo o contrato de cookie
  HttpOnly e ausência do refresh no JSON.
- `Session.RevocationTokenHash` persistido como SHA-256, com índice único.
- Login e registro geram duas partes aleatórias; cada renovação preserva a
  primeira e troca a segunda.
- Refresh aceita somente o token completo atual, com credencial estável
  correspondente; UPDATE condicional por sessão, hash atual, expiração e ausência
  de revogação. Duas rotações concorrentes permitem somente um vencedor.
- Logout faz UPDATE diretamente pelo hash estável e não depende do token atual.
  Cookies anteriores encerram somente a mesma sessão; logout é idempotente.
- Validação JWT organizacional extraída sem mudança de regras para
  `OrganizationAuthentication`, utilizada pela API e pelos testes HTTP reais.
- Validação de formato/tamanho antes das consultas de refresh/logout.

## Migration e compatibilidade

`20261120155327_AddMainSessionRevocationSecret` adiciona a coluna inicialmente
nullable, copia `refresh_token_hash` nas sessões anteriores, torna a coluna
obrigatória e cria o índice único. Tokens legados continuam renovando e passam
ao formato composto na primeira rotação. O cookie legado preserva capacidade
de logout após essa rotação, sem poder renovar novamente.

O teste migra até a versão anterior, insere duas sessões legadas, aplica a nova
migration e confirma a preservação, rotação e revogação independente. Também
verifica a recusa do downgrade quando ele perderia a credencial estável de
sessões ativas com tokens compostos.

## Verificação focada

Filtro `AuthApplicationServiceTests|AuthPrincipal_`: **25 testes aprovados,
zero falhas e zero ignorados**. Build sem avisos/erros.

Os testes novos usam controllers, repositórios PostgreSQL, cookies e JWT reais
com chave fictícia. Um interceptor de comandos pausa deterministicamente a
rotação antes ou depois do UPDATE; não depende de sleeps ou disputa aleatória.

Comprovado:

1. Refresh salva antes do logout; sua resposta chega depois, com cookie novo,
   mas tanto o JWT anterior quanto o novo e os refreshes retornam 401.
2. Logout ocorre entre leitura e UPDATE do refresh; a rotação retorna 401.
3. Dois refreshes leem o mesmo token antes dos UPDATEs; um vence e outro retorna
   401. Logout com o cookie anterior revoga as credenciais do vencedor.
4. Uma segunda sessão do mesmo usuário continua válida após logout da primeira.
5. Usuário/organização inativos, vínculo removido ou sessão expirada recusam
   access token e refresh.
6. JWT de outra organização não lê/lista/exclui Peças nem exporta PDF alheio;
   a listagem de Casos permanece no tenant correto.
7. Colega da mesma organização pode ler a Peça compartilhada, mas não conversa,
   listagem ou download de anexos privados do outro titular. Outra organização
   também recebe 404. O próprio titular acessa o anexo e exporta Word; após
   logout, o acesso ao anexo retorna 401. Outras contas continuam autenticadas.

Armazenamento de arquivos em memória, sem worker de extração ou geração. IA
simulada; nenhum provedor de IA é registrado no servidor HTTP desses testes.

## Operação

Regressão completa: **401 testes aprovados, zero falhas e 1 teste de OCR local
ignorado**, já fora do MVP. Comando:

```bash
dotnet test tests/JuridicoAI.Api.Tests/JuridicoAI.Api.Tests.csproj --no-build --no-restore --verbosity minimal
```

`JURIDICOAI_REVIEW_TEST_POSTGRES` apontou exclusivamente para o contêiner
descartável desta entrega; `AssistenteIa__Enabled=false`, `AssistenteIa__ApiKey`
e `OPENAI_API_KEY` vazios. Nenhuma chamada de IA real.

Aplicar a migration com a instância antiga parada/drenada, antes de iniciar a
nova versão. Os tokens antigos são compatíveis; executáveis antigos não devem
continuar inserindo sessões sem a coluna obrigatória. Essa orientação integra
o deploy já previsto, sem nova funcionalidade ou configuração de produto.

A aplicação real e a validação com o advogado continuam no checklist existente
de publicação e aceite do MVP.
