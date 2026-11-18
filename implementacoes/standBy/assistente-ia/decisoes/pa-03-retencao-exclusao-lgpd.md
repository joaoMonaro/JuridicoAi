# Tópico 3 — Retenção, exclusão e LGPD

> **Organização em 2026-10-23:** planejamento em espera (`standBy`).
> Consulte o [índice de pendências](../../README.md) para a prioridade atual.

> **Estado das decisões técnicas:** aprovado
>
> **Estado da implementação:** parcial; aviso versionado e `store=false`
> implementados; histórico, retenção, exclusão e pseudonimização adicional pendentes
>
> **Estado jurídico/contratual:** validação obrigatória antes da comercialização
>
> **Data da decisão:** 2026-09-27
>
> **Última conferência documental:** 2026-10-18
>
> **Pontos técnicos encerrados:** `PA-03`, `PA-04`, `PA-05` e `PA-06`
>
> **Pendência jurídica criada:** `PA-17`

Este documento define a política técnica do MVP para retenção, exclusão,
histórico de IA, logs, backups, minimização de dados e transparência. Ele não
substitui parecer jurídico, definição de base legal, contratos, política de
privacidade ou análise concreta das operações de tratamento.

## 1. Princípios aprovados

- limitar o tratamento ao mínimo necessário para a finalidade informada;
- não reutilizar conteúdo jurídico do cliente para finalidade própria do SaaS;
- manter o histórico funcional no JuridicoAI, não no provider de IA;
- separar conteúdo funcional de logs e métricas técnicas;
- permitir exclusão independente do histórico de IA;
- oferecer recuperação temporária do Caso antes da eliminação definitiva;
- preservar a eficácia da exclusão mesmo após restauração de backup;
- aplicar pseudonimização quando a identidade literal não for necessária;
- tratar dados sensíveis necessários com proteção reforçada, não com remoção
  automática que prejudique a finalidade jurídica;
- informar o usuário antes do primeiro uso da IA sem presumir que esse aceite
  seja a base legal de todos os tratamentos.

O princípio de necessidade é compatível com a orientação da ANPD de limitar o
tratamento a dados pertinentes, proporcionais e não excessivos. Referências:
[LGPD](https://www.planalto.gov.br/ccivil_03/_ato2015-2018/2018/lei/l13709.htm)
e [orientações da ANPD sobre necessidade e transparência](https://www.gov.br/anpd/pt-br/centrais-de-conteudo/materiais-educativos-e-publicacoes/guia_tratamento_de_dados_pessoais_pelo_poder_publico___defeso_eleitoral.pdf/@@display-file/file).

## 2. Política de retenção do JuridicoAI

| Categoria | Exemplos | Retenção aprovada para o MVP |
|---|---|---|
| Dados do Caso | Caso, Peças, contextos e documentos | enquanto o Caso existir, incluindo a janela de recuperação |
| Histórico de IA | conversas, mensagens, respostas e sugestões | enquanto o Caso existir, com exclusão separada disponível |
| Logs técnicos | status, IDs técnicos, modelo, tokens, latência e erro | 90 dias |
| Backups | cópias de banco e arquivos | no máximo 30 dias |

Obrigação legal, ordem de preservação ou necessidade de defesa de direitos pode
exigir tratamento excepcional. Essa hipótese não será presumida pelo código:
precisa de política jurídica em `PA-17`, justificativa registrada, acesso
restrito e prazo definido.

### Logs técnicos

Logs e eventos de auditoria usam lista permitida de campos:

A lista completa para aplicação, proxy, APM e exceções está no
[Tópico 11 — Segurança, isolamento e logs](pa-11-seguranca-isolamento-logs.md).

```text
OrganizationId
UserId
RequestId
OperationType
Model
InputTokens
OutputTokens
DurationMs
Status
ErrorCode
CreatedAt
```

Mesmo esses identificadores têm acesso restrito e retenção de 90 dias. Não
podem entrar em logs:

```text
Prompt ou resposta
Conteúdo da Peça, Caso ou documento
Nome das partes
CPF, CNPJ, RG
Endereço, telefone ou e-mail pessoal
Dados bancários, de saúde ou outros dados sensíveis
Mapas de pseudonimização
Tokens e segredos de autenticação
```

Mensagens de exceção vindas de serviços externos devem ser normalizadas antes
do log para impedir que corpo de requisição ou resposta seja registrado.

## 3. Exclusão do Caso

O ciclo aprovado é:

```text
Ativo ou arquivado
    -> solicitação de exclusão
    -> soft delete imediato e inacessível ao uso normal
    -> janela de recuperação de 30 dias
    -> hard delete de banco e armazenamento ativo
    -> expiração dos backups remanescentes em até 30 dias
```

Após o hard delete, devem ser eliminados do armazenamento ativo:

- Caso, envolvidos, endereços e contextos;
- Peças e seus metadados;
- documentos e objetos binários;
- conversas, mensagens e sugestões da IA;
- referências e índices derivados que apontem para esses dados;
- metadados que não possuam justificativa específica de conservação.

Arquivar e excluir são operações diferentes. Atualmente,
`DELETE /casos/{id}` apenas muda o status para `Arquivado`; a implementação deve
separar os dois conceitos e migrar o contrato sem transformar arquivamentos
existentes em solicitações de exclusão.

### Recuperação

Durante os 30 dias, apenas perfis autorizados podem restaurar o Caso. A
restauração recupera o conjunto dependente de forma atômica. Depois de
`purgeAfter`, restauração deixa de ser possível.

### Backups e restauração

Backups serão criptografados, terão acesso restrito e expirarão em até 30 dias.
Dados já eliminados não podem voltar ao uso operacional por uma restauração.
Para isso, a implementação deve manter um registro de tombstones de exclusão em
local protegido e aplicá-lo obrigatoriamente no procedimento pós-restauração.

O uso de dados provenientes de backup só é permitido no procedimento legítimo
de recuperação. O backup não funciona como arquivo consultável nem como forma
de contornar pedido de exclusão.

## 4. Histórico da IA

O histórico funcional será persistido pelo JuridicoAI e isolado por
Organization e Caso:

```text
Caso
└── AiConversation
    ├── AiMessage (User)
    ├── AiMessage (Assistant)
    └── AiSuggestion
```

Cada registro carrega `OrganizationId` como fronteira de segurança, mesmo que o
Caso também o possua. O conteúdo deve ser criptografado em repouso conforme a
infraestrutura escolhida e nunca duplicado em logs.

Existirão duas ações independentes:

- `Limpar histórico da IA`: remove conversas, mensagens e sugestões, sem apagar
  Caso, Peças ou documentos;
- `Excluir Caso`: segue soft delete, recuperação e hard delete de todo o
  agregado, incluindo o histórico.

Limpar o histórico torna o conteúdo imediatamente inacessível e inicia sua
eliminação no armazenamento ativo. Metadados técnicos sem conteúdo podem
permanecer pelo prazo de 90 dias.

## 5. Minimização e pseudonimização

A preparação do contexto seguirá esta sequência:

```text
Dados autorizados do Caso
    -> identificação de PII e dados sensíveis
    -> avaliação de necessidade para a operação
    -> exclusão, minimização ou pseudonimização
    -> confirmação das fontes enviadas
    -> provider de IA
```

### Regra padrão

- dado desnecessário: não enviar;
- identificador desnecessário, mas cuja função seja relevante: substituir por
  marcador semântico, como `[PARTE_AUTORA]` ou `[CPF_AUTOR]`;
- dado pessoal ou sensível necessário: enviar somente o fragmento mínimo e
  registrar no snapshot da solicitação qual categoria foi autorizada;
- dúvida sobre necessidade: não revelar automaticamente; pedir decisão
  explícita do usuário.

Serão pseudonimizados por padrão, quando irrelevantes: CPF, RG, telefone, e-mail
pessoal, endereço completo, dados bancários e identificadores equivalentes. O
CNPJ também pode ser pseudonimizado quando a identidade específica da empresa
não for necessária, com atenção reforçada a empresário individual e MEI.

Condição de saúde ou outro dado sensível juridicamente relevante não deve ser
apagado de forma cega. O sistema envia apenas o necessário e sinaliza a presença
de categoria sensível antes da confirmação.

### Mapeamento de marcadores

O mapa entre valor real e marcador é efêmero, limitado à solicitação e mantido
em memória. Não é persistido nem registrado. A eventual recomposição aceita
somente marcadores conhecidos daquele pedido; o modelo não pode criar novos
marcadores capazes de acessar dados do Caso.

Detecção automática em texto livre é uma defesa adicional, não uma garantia.
O MVP combina campos estruturados conhecidos, padrões de identificadores e
confirmação visível do usuário.

## 6. Processamento pela OpenAI

O provider não será a fonte principal de histórico. O adapter mantém:

```json
{ "store": false }
```

O produto não usará `/v1/conversations` para guardar o histórico do Caso e não
referenciará respostas armazenadas como dependência para continuar uma conversa.

### Limite da garantia de `store=false`

`store=false` evita a persistência de estado da aplicação para a Response, mas
não equivale, sozinho, a Zero Data Retention. Segundo os
[controles de dados da OpenAI](https://developers.openai.com/api/docs/guides/your-data),
o uso da API não é usado para treinamento por padrão, porém logs de monitoramento
de abuso podem conter prompts e respostas e, no regime padrão, ser mantidos por
até 30 dias. Modified Abuse Monitoring e Zero Data Retention dependem de
elegibilidade, aprovação e configuração da Organization/projeto no provider.

Antes da comercialização, `PA-17` deve validar contrato, suboperadores,
localização do processamento, mecanismo de transferência internacional e se o
projeto terá ZDR ou MAM. Nenhuma interface pode prometer “retenção zero” apenas
porque o payload contém `store=false`.

Ferramentas, arquivos, background mode, cache estendido e novos endpoints não
podem ser habilitados sem nova avaliação de retenção e transferência.

## 7. Transparência antes do primeiro uso

> **Implementado em 2026-10-01:** aviso versionado, confirmação persistida por
> usuário e Organization, reconfirmação em nova versão e bloqueio da geração no
> backend. A URL da Política de Privacidade é configurável e deve ser definida
> antes da comercialização.

Antes da primeira solicitação, o backend exigirá confirmação da versão vigente
do aviso. Texto inicial aprovado para revisão jurídica:

> **Uso de inteligência artificial**
>
> Algumas informações do caso poderão ser processadas por provedores de
> inteligência artificial para gerar, revisar ou analisar conteúdo jurídico.
>
> A plataforma procura enviar apenas as informações necessárias à execução da
> solicitação e pode aplicar técnicas de minimização e pseudonimização de dados.
>
> As respostas geradas por inteligência artificial podem conter erros e devem
> ser revisadas pelo profissional responsável.

A ação será `Entendi e continuar`, acompanhada de acesso à política completa.
O registro técnico contém usuário, Organization, versão do aviso e data/hora,
sem copiar dados do Caso.

Essa confirmação comprova apresentação do aviso; não deve ser nomeada ou usada
automaticamente como consentimento nem como base legal geral. Mudança material
no texto ou na política incrementa a versão e exige nova confirmação.

## 8. Premissa de uso no MVP

O escritório determina a finalidade jurídica e o JuridicoAI processa os dados
para prestar o serviço contratado, sem reutilizar o conteúdo para finalidade
própria. Durante o MVP é vedado:

- usar Peças, documentos ou mensagens de clientes para treinar modelos próprios;
- formar base jurídica própria a partir do conteúdo dos clientes;
- usar conteúdo para publicidade, perfilamento ou venda de dados;
- criar avaliações internas com dados reais sem autorização e governança
  específicas;
- reutilizar conteúdo para finalidade não solicitada pela Organization.

Essa é uma restrição técnica e de produto. A classificação formal dos agentes
de tratamento permanece sujeita à análise prevista em `PA-17` e ao
[guia da ANPD sobre controlador, operador e encarregado](https://www.gov.br/anpd/pt-br/centrais-de-conteudo/materiais-educativos-e-publicacoes/guia-orientativo-para-definicoes-dos-agentes-de-tratamento-de-dados-pessoais-e-do-encarregado).

## 9. Plano técnico de implementação

### Etapa A — modelo de dados e histórico

- criar `AiConversation`, `AiMessage` e `AiSuggestion` com `OrganizationId`,
  `CasoId`, datas e índices de isolamento;
- separar conteúdo funcional do ledger técnico do Tópico 1;
- definir criptografia em repouso e autorização por função;
- criar endpoints de listar conversa e limpar histórico;
- alterar `suportaHistorico` somente depois de backend e frontend completos;
- testar isolamento, exclusão independente e ausência de conteúdo em logs.

### Etapa B — ciclo de exclusão do Caso

- adicionar `DeletionRequestedAt`, `PurgeAfter` e dados mínimos de auditoria;
- separar arquivamento da solicitação de exclusão no contrato HTTP;
- filtrar Casos excluídos e todos os dependentes do uso normal;
- implementar restauração autorizada dentro de 30 dias;
- criar worker idempotente de hard delete com ordem explícita para as FKs;
- apagar objetos de documentos e índices derivados;
- registrar tombstone sem conteúdo e integrá-lo ao procedimento de restore;
- testar falha parcial, repetição do job e isolamento entre Organizations.

### Etapa C — retenção operacional

- configurar TTL de 90 dias no coletor/armazenamento de logs;
- adotar eventos estruturados por allowlist e sanitização de exceções;
- configurar backups criptografados com expiração máxima de 30 dias;
- documentar e testar restore, aplicação de tombstones e acesso emergencial;
- monitorar falhas de purga sem registrar dados eliminados.

### Etapa D — preparação segura do contexto

- criar `IAssistenteDataMinimizer` antes de `AssistenteContextBuilder`;
- detectar campos estruturados e padrões iniciais de CPF, CNPJ, RG, telefone,
  e-mail, endereço e dados bancários;
- substituir valores por marcadores semânticos por solicitação;
- classificar categorias sensíveis e exigir confirmação quando incluídas;
- devolver ao frontend um resumo das categorias, nunca os valores mascarados;
- testar falsos positivos, texto malicioso, marcadores inventados e
  recomposição somente de marcadores conhecidos.

### Etapa E — transparência e política do provider

- [x] versionar o aviso de IA no backend;
- [x] persistir confirmação por usuário e Organization;
- [x] bloquear geração com `AI_TRANSPARENCY_ACK_REQUIRED` enquanto faltar confirmação;
- [x] expor versão, texto e URL configurável da política em endpoint próprio;
- [x] apresentar diálogo acessível no primeiro uso e reconfirmar após mudança de versão;
- manter teste de contrato que assegure `store=false`;
- criar checklist operacional para ZDR/MAM, região, contrato e suboperadores;
- revisar qualquer nova feature do provider antes de habilitá-la.

### Etapa F — validação jurídica e lançamento

- mapear finalidade, categoria de dados, titular, base legal e retenção de cada
  operação;
- confirmar papéis de controlador, operador, suboperador e encarregado;
- revisar aviso, Política de Privacidade, Termos, contrato e acordo de dados;
- validar mecanismo de transferência internacional conforme a
  [Resolução CD/ANPD nº 19/2024](https://www.gov.br/anpd/pt-br/assuntos/assuntos-internacionais/transferencia-internacional-de-dados);
- documentar atendimento ao titular, incidentes e lista de fornecedores;
- bloquear comercialização enquanto `PA-17` não estiver formalmente aprovado.

## 10. Critérios de aceite técnicos

- [ ] Logs de aplicação contêm somente campos permitidos e expiram em 90 dias.
- [ ] Histórico pode ser apagado sem apagar Caso, Peça ou documentos.
- [ ] Excluir Caso o torna inacessível imediatamente e permite restauração por 30 dias.
- [ ] Hard delete elimina banco, arquivos, histórico e índices derivados.
- [ ] Backups expiram em até 30 dias e restore reaplica tombstones.
- [ ] PII desnecessária é removida ou pseudonimizada antes do provider.
- [ ] Dados sensíveis não são enviados sem necessidade e confirmação visível.
- [ ] Mapas de pseudonimização não são persistidos nem registrados.
- [x] O provider recebe `store=false`, com teste de contrato; o produto não usa
      histórico persistido no provider.
- [ ] O sistema não promete ZDR sem configuração e contrato que a sustentem.
- [x] Primeiro uso exige confirmação versionada do aviso de transparência.
- [ ] Cancelar, falhar ou excluir não reutiliza conteúdo para outra finalidade.
- [ ] Testes comprovam isolamento por Organization em histórico e exclusão.
- [ ] `PA-17` está aprovado antes da comercialização.

## 11. Pontos jurídicos que permanecem pendentes

Permanecem em `PA-17`:

- base legal por operação, inclusive para dados pessoais sensíveis;
- papéis formais das partes e instruções do controlador;
- cláusulas sobre segurança, incidentes, titulares, retenção e encerramento;
- mecanismo de transferência internacional e regiões de processamento;
- política definitiva de privacidade, Termos de Uso e acordo de tratamento;
- lista e governança de fornecedores/suboperadores;
- procedimento para direitos dos titulares e resposta a incidentes;
- hipóteses excepcionais de conservação e legal hold.
