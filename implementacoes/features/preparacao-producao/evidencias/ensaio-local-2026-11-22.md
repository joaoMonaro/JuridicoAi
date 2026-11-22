# Ensaio local do pacote e da persistência — 2026-11-22

## Resultado e alcance

O recorte local do MVP foi executado com frontend compilado, API Release em
`Production`, PostgreSQL 17 e IA determinística em serviços separados. Banco,
documentos e certificados usam volumes exclusivos. O Compose de desenvolvimento
e os dados existentes não foram usados pelo ensaio.

Isso comprova instalação e integração nesse ambiente local, incluindo recuperação
dos dados. Publicação em nuvem e aceite jurídico do advogado continuam pendentes.
O simulador não mede qualidade jurídica, latência ou custo de um modelo real.

## Entrega

- [Roteiro e comandos](../../../../ops/rehearsal/README.md).
- Dockerfiles da API, frontend e simulador; Compose exclusivo, sem bind mounts
  do código ou dependências de runtime do host.
- Migração explícita por `migrate-database`, que encerra antes de iniciar o
  servidor. Provisionamento pela CLI existente, cadastro público fechado e
  runtime com `Postgres:RunMigrations=false`.
- Proxy Nginx com HTTPS temporário e dois nomes `.test`; frontend usa a API real,
  CORS explícito e refresh por cookie `Secure`/`HttpOnly`, limitado a `/auth`.
- API sem root, filesystem da imagem somente leitura, temporários em `/tmp`
  e documentos em volume persistente. Somente o proxy publica porta em loopback.
  API, PostgreSQL e simulador ficam exclusivamente na rede interna sem internet.
- Backup consistente de PostgreSQL e documentos com API/web interrompidos;
  manifesto SHA-256 e restauração em projeto e volumes novos.

Git, Docker, Python e OpenSSL são ferramentas do operador. Node/Playwright são
clientes de teste do navegador. .NET, Nginx, Node de build, fontes, Poppler e
LibreOffice usados pelos serviços vêm das imagens, sem aproveitar instalações
ou `node_modules`, `bin` e `obj` do host. O build precisa de internet para baixar
imagens e pacotes; o runtime da API não tem saída para a internet.

## Falha encontrada e corrigida

A exportação PDF retornou 503 no container sem servidor gráfico. O conversor
forçava `SAL_USE_VCLPLUGIN=gen`, que tentou abrir X11 mesmo com `--headless`.
A mudança para `svp` permitiu renderizar sem desktop ou Xvfb. O ensaio repetiu
exportações reais pela API e pelo navegador, inclusive depois da restauração.

Os ajustes do próprio ensaio também foram verificados: certificados transferidos
para volume Docker, rede de entrada compatível com Docker Desktop, comparação
do texto do PDF tolerando quebras de linha e consulta do cookie no caminho
`/auth/refresh`. O cenário sintético edita apenas o primeiro de dois parágrafos,
respeitando a proteção existente contra substituição integral não autorizada.

## Verificações

| Verificação | Resultado |
|---|---|
| Build em contextos copiados, sem artefatos locais | Aprovado |
| Banco vazio, migrations e provisionamento CLI | Aprovado |
| HTTPS, cookie seguro, HTTP direto recusado, cadastro fechado e acesso anônimo recusado | Aprovado |
| Isolamento de rede e API sem root | Aprovado |
| Caso criado sem descrição e Peça persistida | Aprovado |
| Upload e extração nativa real de TXT, DOCX e PDF com texto | Aprovado |
| Chat com IA sintética, proposta sem aplicação automática e aprovação explícita | Aprovado |
| Repetição da aprovação sem duplicar a alteração | Aprovado |
| Frontend compilado, refresh real, edição, save e recarga | Aprovado |
| Histórico e anexos recuperados pelo navegador | Aprovado |
| Word/PDF com corpo, cabeçalho e rodapé; downloads reais | Aprovado |
| Reinício dos serviços e recriação de API/web | Aprovado |
| Backup e restauração em PostgreSQL/storage novos | Aprovado |
| Texto/versão/histórico iguais e SHA-256 dos três anexos preservados | Aprovado |
| Limite financeiro zero impede chamada/novo consumo | Aprovado |
| Exclusão autorizada remove conversa/anexo do ambiente ativo, preservando a outra Peça | Aprovado |
| Configuração insegura recusa startup; mesma imagem válida recupera serviço/dados | Aprovado |

A conferência visual do PDF e da captura do navegador não encontrou cortes,
glifos ausentes ou sobreposição no documento fictício curto usado no ensaio.
Não é um aceite de layouts arbitrários ou documentos longos.

Regressão do backend com PostgreSQL descartável exclusivo: **401 aprovados,
0 falhas, 1 ignorado (OCR)**. O banco de testes foi criado para essa execução e
removido ao final, sem usar bancos de desenvolvimento. Também passaram as
verificações de sintaxe Python/JavaScript e `git diff --check`.

## Reprodução e evidências locais

Na raiz do backend, usando uma pasta vazia:

```bash
python3 ops/rehearsal/run.py all \
  --state /tmp/juridicoai-release-ensaio \
  --frontend ../JuridicoAi-frontend
```

Estado da execução final: `/tmp/juridicoai-release-20261122-mvp`, projeto
`juridicoai-rehearsal-6dcf5c87` e restauração com sufixo `-restored`.
O comando completo encerrou com código 0 e `result.json` com `status=passed`;
uma [cópia do resultado sem segredos](ensaio-local-2026-11-22.result.json) acompanha
este registro. Containers/redes do ensaio foram desligados ao final, preservando
volumes e backups. Os serviços de desenvolvimento existentes foram preservados.
Esse diretório é privado e temporário; não é destino de backup de produção.
Contém `result.json`, `images.json`, resultados por etapa, logs, capturas,
exports e backup. Contém também credenciais sintéticas e chave TLS; não publicar
o diretório nem os arquivos privados. A regressão completa está registrada em
`/tmp/juridicoai-release-20261122-complete/regression.log`.

Fontes verificadas:

- Backend: base `6702603cc853d42eb9ec38d951c567c3b2565143`, com alterações locais
  desta entrega; SHA-256 do snapshot da API
  `62849012a02c1c37d48c178750d699d69cbac8559df1a68fa8b3a2a75e029b56`.
- Frontend: `bf2059aba748822153693a8d85118ed464153367`, sem alterações locais;
  SHA-256 `0c7884b932b022e7282f4525ca2cd9252b4c2be8bc2e2c7007289de0427edff6`.
- Imagem API: `sha256:2e1616e0b1e96542bf92549c78068dfc9b04615ef15ca6a0d733c190a85823d8`.
- Imagem frontend: `sha256:4c3828bc8221e1c03935cf3287bad5f25f8223636dd04f856ee6d1fa32bdf922`.
- Os demais IDs/digests estão no manifesto local `images.json`; `result.json`
  registra também as fontes e checks aprovados. Nenhum commit de liberação foi
  criado por este ensaio.

## Limpeza antes da integração em homologação

Após o ensaio, foram removidos a classe `PostgresSettings` e seu registro sem
consumidor, a variável não utilizada `REHEARSAL_CONFIG`, o import `Path` de
`verify.py` e, no frontend, o cliente não importado da antiga rota `/peticoes/gerar`.
As chaves de conexão e as flags funcionais do assistente foram preservadas.

Essa limpeza passou em **59 testes focados do backend** (transporte de produção
e exportação), **162 testes do frontend**, build/lint do frontend e verificação
de sintaxe/imports dos scripts. Os resultados completos do ensaio acima
correspondem aos snapshots históricos identificados, anteriores a essa limpeza.
Nenhuma chamada de IA real foi feita na verificação da limpeza.

## Limites que permanecem

- Configurar domínio, certificado válido, proxies, segredos, banco e storage
  privados no provedor escolhido e repetir a conferência do ambiente publicado.
- Configurar modelo, tarifas reais e limite da organização; eventual teste pago
  depende de solicitação e orçamento. **Nenhuma chamada de IA real foi feita.**
- Homologar o trabalho representativo com o advogado e registrar o aceite do MVP.
- Definir responsável pelo piloto, destino independente/retenção dos backups
  e acompanhamento dos erros, consumo e jobs.
- A recuperação ensaia falha de configuração e volta ao pacote válido, sem
  downgrade destrutivo. Não comprova compatibilidade com migrations futuras ou
  drenagem de jobs em andamento. O usuário SQL é exclusivo do ensaio, mas não
  há separação de credenciais SQL de migrations e runtime neste recorte.
- OCR, CI/CD completo e reaplicação de exclusões após restaurar backup (P05)
  continuam fora deste trabalho. O teste de exclusão ocorre no ambiente ativo
  restaurado; não afirma reaplicação de exclusões feitas depois de um backup.
