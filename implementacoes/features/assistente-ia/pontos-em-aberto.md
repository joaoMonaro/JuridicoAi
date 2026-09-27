# Pontos em aberto — Assistente de IA

> **Estado:** decisões pendentes
>
> **Última atualização:** 2026-09-26
>
> **Documento principal:** [Assistente de IA](README.md)

Este documento reúne decisões que ainda afetam produto, segurança, custo ou
arquitetura. Um item só é encerrado quando existe uma decisão registrada, um
responsável e, quando aplicável, evidência de teste ou homologação.

## Resumo e prioridade

| ID | Tema | Prioridade | Bloqueia produção |
|---|---|---:|:---:|
| `PA-01` | Modelo inicial e fallback | Alta | Sim |
| `PA-02` | Limites, rate limit e orçamento | Alta | Sim |
| `PA-03` | Retenção, exclusão e LGPD | Crítica | Sim |
| `PA-04` | Dados enviados e mascaramento | Crítica | Sim |
| `PA-05` | Aviso e consentimento do usuário | Alta | Sim |
| `PA-06` | Histórico de conversas | Média | Não |
| `PA-07` | Âncora expirada e destino alternativo | Média | Não |
| `PA-08` | Aplicação com autosave em erro | Alta | Sim |
| `PA-09` | Avaliação de qualidade jurídica | Crítica | Sim |
| `PA-10` | Legislação, jurisprudência e fontes | Crítica | Sim |
| `PA-11` | Segurança, isolamento e logs | Crítica | Sim |
| `PA-12` | Testes e acessibilidade do frontend | Alta | Sim |
| `PA-13` | Feature flag e rollout | Alta | Sim |
| `PA-14` | Observabilidade e métricas | Alta | Sim |
| `PA-15` | Tamanho do bundle do frontend | Baixa | Não |

## Ordem recomendada de decisão

1. Resolver `PA-03`, `PA-04` e `PA-05`, definindo a fronteira de dados.
2. Criar a avaliação de `PA-09` e a política de fontes de `PA-10`.
3. Selecionar modelo e limites em `PA-01` e `PA-02` usando essa avaliação.
4. Fechar segurança, autosave e testes em `PA-08`, `PA-11` e `PA-12`.
5. Preparar operação e lançamento com `PA-13` e `PA-14`.
6. Tratar melhorias não bloqueantes em `PA-06`, `PA-07` e `PA-15`.

## Decisões de IA e custo

### PA-01 — Modelo inicial e fallback

Definir:

- modelo principal configurado por ambiente;
- requisitos mínimos de saída estruturada, contexto e latência;
- se haverá fallback e em quais erros ele será permitido;
- como uma troca de modelo será indicada e auditada.

Direção atual: não fixar o modelo no código. Comparar candidatos usando o mesmo
conjunto de casos jurídicos e não fazer fallback silencioso para um modelo com
política ou qualidade diferente.

Critério de fechamento: modelo aprovado em avaliação, custo estimado por fluxo,
configuração documentada e comportamento de indisponibilidade testado.

### PA-02 — Limites, rate limit e orçamento

Definir limites de:

- caracteres e tokens de entrada e saída;
- requisições por usuário e por Organization;
- gerações simultâneas;
- gasto diário ou mensal por Organization;
- comportamento ao atingir cada limite.

Direção atual: manter limite por usuário, adicionar partição e orçamento por
Organization e recusar antes de chamar o provider sempre que possível.

Critério de fechamento: limites configuráveis, resposta compreensível ao
usuário, métricas de consumo e testes de concorrência e excesso.

## Privacidade e dados

### PA-03 — Retenção, exclusão e LGPD

Definir:

- se instruções, contextos e respostas serão persistidos pelo JuridicoAI;
- prazo de retenção e processo de exclusão;
- base legal, papéis de controlador/operador e fornecedores envolvidos;
- configuração de retenção e treinamento no provider;
- tratamento de backups e logs técnicos.

Direção atual: manter `store: false` no provider e não persistir conteúdo de
solicitações na primeira entrega. Registrar somente metadados técnicos sem
permitir reconstrução do conteúdo jurídico.

Critério de fechamento: política aprovada, termos refletidos na interface e
configurações de produção verificadas.

### PA-04 — Dados enviados e mascaramento

Definir exatamente quais campos entram quando “Incluir dados do caso” estiver
ativo, incluindo tratamento de nomes, CPF/CNPJ, endereços, contatos, dados de
saúde e informações de menores.

Direção atual: nenhum dado do Caso é selecionado por padrão; enviar somente o
necessário para a tarefa e mascarar identificadores quando seu valor literal não
for indispensável.

Critério de fechamento: matriz campo × finalidade, política de mascaramento,
testes de payload e revisão dos textos exibidos antes do envio.

### PA-05 — Aviso e consentimento do usuário

Definir:

- aviso apresentado no primeiro uso;
- resumo visível das fontes em cada solicitação;
- acesso às políticas de privacidade e do provider;
- necessidade de aceite por usuário ou por administrador da Organization.

Critério de fechamento: texto aprovado, versão do aceite auditável quando
necessário e interface homologada.

## Produto e edição

### PA-06 — Histórico de conversas

Escolher entre:

- solicitações independentes mantidas somente enquanto a Peça está aberta;
- histórico local temporário;
- conversas persistentes por Peça.

Persistência exige autorização, modelo próprio, retenção, exclusão e isolamento
por Organization. Ela não deve ser adicionada ao `conteudoJson` da Peça.

Direção atual: manter estado apenas na tela até existir necessidade validada.

### PA-07 — Âncora expirada e destino alternativo

Hoje, qualquer mudança após o envio invalida a aplicação no destino original.
Definir se o usuário poderá escolher explicitamente “Inserir no cursor atual”
ou gerar novamente a proposta.

Direção atual: manter o bloqueio seguro. Um destino alternativo só deve ser
oferecido como uma nova ação explícita, nunca como fallback automático.

### PA-08 — Aplicação com autosave em erro

Conflitos já bloqueiam aplicação. Ainda é necessário decidir se os estados
`ERRO` e `NAO_SALVO` permitem edição local com aviso ou exigem persistência
restabelecida antes da aplicação.

Direção recomendada: permitir edição local, informar claramente que ainda não
foi salva e preservar undo. Antes do lançamento, validar risco de perda ao
recarregar ou trocar de Peça.

Critério de fechamento: comportamento aprovado e coberto por testes de erro,
recuperação, navegação e conflito 409.

## Qualidade jurídica

### PA-09 — Avaliação de qualidade jurídica

Criar um conjunto versionado de exemplos fictícios e anonimizados cobrindo:

- perguntas sem seleção;
- redação e reescrita;
- lacunas, contradições e clareza;
- recusa em inventar fatos;
- manutenção do sentido jurídico;
- alertas para informação ausente ou não verificada.

Definir rubrica, avaliadores, nota mínima, casos impeditivos e comparação entre
modelos/prompts. A aprovação deve incluir profissionais jurídicos.

### PA-10 — Legislação, jurisprudência e fontes

Definir se o assistente:

- apenas usa fontes fornecidas no contexto;
- pode mencionar referências não verificadas acompanhadas de alerta;
- será integrado futuramente a uma pesquisa jurídica confiável.

Direção atual: não pesquisar externamente e nunca apresentar processo, julgado
ou citação como confirmado sem fonte fornecida. Referências duvidosas devem
aparecer separadamente como item de verificação.

Critério de fechamento: política no prompt e no produto, exemplos de teste e
mensagem clara ao usuário.

## Segurança e validação

### PA-11 — Segurança, isolamento e logs

Ainda precisam de evidência específica:

- testes de prompt injection em Peça e contexto do Caso;
- tentativa de acesso cruzado entre Organizations;
- inspeção de logs e telemetria para conteúdo, CPF/CNPJ e tokens;
- cancelamento, timeout, payload excessivo e respostas inválidas;
- validação das configurações de produção.

Critério de fechamento: suíte automatizada, revisão dos logs gerados e ausência
de dados sensíveis nos cenários de falha.

### PA-12 — Testes e acessibilidade do frontend

Adicionar infraestrutura de testes e cobrir:

- parser SSE fragmentado, UTF-8, falha e EOF incompleto;
- captura de seleção, seção e documento;
- validade de âncoras e transação única do TipTap;
- cancelar, rejeitar, tentar novamente, aplicar e desfazer;
- conflito e erro de autosave;
- navegação por teclado, foco, leitor de tela e contraste;
- atalho em Windows, Linux e macOS.

Critério de fechamento: testes críticos automatizados e auditoria manual de
acessibilidade registrada.

## Lançamento e operação

### PA-13 — Feature flag e rollout

Definir ativação por ambiente e por Organization, grupo piloto, responsáveis
pela liberação e procedimento de desligamento. A flag global atual não oferece
rollout seletivo.

Critério de fechamento: ativação gradual testada, rollback documentado e
organizações sem acesso incapazes de gerar solicitações.

### PA-14 — Observabilidade e métricas

Definir métricas sem conteúdo sensível:

- volume, sucesso, falha e cancelamento;
- latência até início e conclusão;
- tokens e custo lógico;
- aplicação, rejeição e nova tentativa, somente por eventos explícitos;
- alertas de indisponibilidade, erro e orçamento.

Critério de fechamento: dashboards, alertas, responsáveis e política de acesso
aos dados operacionais.

### PA-15 — Tamanho do bundle do frontend

O build atual gera um chunk JavaScript principal superior a 500 kB. Avaliar
lazy loading do workspace ou do painel de IA e separação de dependências antes
que novas funções ampliem esse custo.

Este item não bloqueia o piloto, mas deve possuir medição antes/depois quando
for implementado.

## Registro de resolução

Ao fechar um item, registrar no [documento principal](README.md):

```text
Data | ID | decisão | responsável | evidência/link
```

Depois, remover o item deste documento ou marcá-lo como resolvido com link para
a decisão consolidada. Não encerrar um ponto apenas porque existe código parcial.

