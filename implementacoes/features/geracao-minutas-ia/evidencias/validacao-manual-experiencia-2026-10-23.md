# Validação manual da experiência de IA na Peça — 2026-10-23

> **Estado:** em andamento, conduzida com o usuário
>
> **Última observação:** 2026-10-24
>
> **Método:** uso da interface pelo usuário e relato dos resultados na conversa
>
> **Ambiente e versões em execução:** ainda não confirmados

## Escopo

Avaliar a utilização do assistente dentro de uma Peça, do ponto de vista do
usuário. Esta sessão não encerra os gates de regressão, acessibilidade ou
produção. Não houve execução automatizada nem observação direta da interface.

## VM-01 — Solicitar uma minuta com fatos fictícios

**Pedido:** gerar uma minuta de petição inicial sobre cobranças posteriores ao
cancelamento de telefonia. O prompt fornece partes fictícias, datas, valores,
documentos disponíveis e objetivos; pede cinco seções, campos `[PREENCHER]`,
ausência de fatos inventados e observações de revisão separadas da minuta.

**Orientação fornecida:** abrir uma Peça vazia e selecionar `Redigir`. A ação
efetivamente usada, o estado inicial da Peça e o payload não foram confirmados.

**Esperado:** apresentar a minuta para revisão, com os avisos separados, sem
alterar automaticamente o documento.

**Resultado relatado:** o usuário esclareceu que recebeu somente este bloco,
sem a minuta:

> Verifique antes de usar
>
> • Não foram informados comarca, Estado, juízo competente, endereços nem qualificações completas das partes.
>
> • A fundamentação legal aplicável deverá ser conferida com fontes jurídicas adequadas antes do protocolo.

**Avaliação:** o resultado relatado não atende ao objetivo de geração da minuta.
Os avisos são pertinentes, mas não comprovam entrega do conteúdo solicitado.

**Investigação pendente:** confirmar a ação exibida no resumo do pedido e
inspecionar a resposta completa/interface para distinguir ausência de geração
de problema de apresentação. O código local permite texto vazio em `Revisar`,
mas o rejeita nos demais modos; isso é uma hipótese de investigação, não
comprovação do modo usado ou da causa desta ocorrência.

**Próximo passo:** verificar se o resumo da mensagem enviada mostra `Perguntar`,
`Redigir`, `Reescrever` ou `Revisar`, antes de repetir o pedido.

## VM-02 — Repetir com qualificações e endereçamento fictícios

**Pedido:** nova versão do prompt, com Estado, comarca, juízo indicado para o
exercício, qualificações, endereços, representação e identificadores
intencionalmente inválidos. Mantém os fatos anteriores e pede expressamente
que os avisos acompanhem a minuta sem substituir sua elaboração.

O prompt foi salvo, a pedido do usuário, em
`/home/jao/Desktop/Testes-JuridicoAI/prompt-geracao-peca-ficticia.txt`.
Uma [cópia para reprodução](prompts/prompt-geracao-peca-ficticia.txt) acompanha
esta evidência no repositório.

**Avisos relatados pelo usuário:**

> Verifique antes de usar
>
> • Valor da causa, situação atual da cobrança de outubro e eventual tutela de urgência não foram definidos.
>
> • Eventual fundamentação legislativa deve ser revisada com base em fontes jurídicas adequadas, pois não foram fornecidas referências normativas.

**Estado:** presença da minuta ainda não confirmada nesta segunda tentativa.
O relato confirma a apresentação dos novos avisos, mas não permite concluir
se a geração foi entregue. A ação efetivamente usada também segue sem confirmação.

## Esclarecimento posterior — operação de resposta no painel

O usuário informou que usou o prompt do Desktop diretamente no chat e observou
uma operação `ResponderNoPainel`, sem aplicação de conteúdo na Peça.

No código inspecionado, o pedido livre começa em `Perguntar`, que permite
somente `ResponderNoPainel`. Isso é compatível com o caminho relatado, mas o
payload não foi capturado para confirmar o modo exato daquela execução.

Essa operação apresenta conteúdo no chat e não oferece aplicação no documento.
Ela não explica sozinha a ausência relatada da minuta: o backend local rejeita
texto vazio nos modos diferentes de `Revisar`. O diagnóstico dessa ausência
permanece pendente, sem atribuição comprovada de bloqueio ao prompt interno.

## Encaminhamento de produto

O proprietário considerou a criação de minutas por pedido livre um uso
essencial. Foi registrada a [nova implementação](../README.md), ainda a
implementar, para receber o pedido, apresentar a proposta e permitir aplicação
explícita. A sessão permanece uma evidência de problema e definição de objetivo,
sem aprovação funcional do novo fluxo.

## VM-03 — Minuta apresentada pelo usuário em 2026-10-24

**Resultado observado no relato:** o usuário colou na conversa uma minuta com
endereçamento, qualificações, título da ação, fatos, fundamentação, pedidos,
provas e fechamento. Isso comprova a obtenção de texto de minuta nesta
tentativa, sem comprovar a operação enviada, a aplicação ou a persistência.

**Aderência ao cenário fictício:**

- preserva o cancelamento de 10/08/2026, as cobranças de setembro e outubro de
  R$ 149,90 e o pagamento somente da cobrança de setembro;
- mantém a tentativa de atendimento de 05/10/2026 e o protocolo TESTE-001;
- pede restituição simples de R$ 149,90 e não formula pedido de dano moral;
- não contém números de artigos, jurisprudência ou processos externos;
- usa `[PREENCHER]` para valor da causa e data do fechamento.

**Ajuste de redação identificado:** o texto afirma que os documentos “instruem
a presente demanda”. O prompt apenas informa que os documentos estão
disponíveis. Disponibilidade não comprova anexação; a minuta deve distinguir
documentos disponíveis dos efetivamente anexados.

**Contexto da interface:** foi esclarecido na conversa que o botão visível
`Novo trecho` muda o modo interno para `Redigir`, preenche um pedido padrão e
mostra o chip `Redigir trecho`. O usuário pode substituir o texto preenchido.
Não houve confirmação explícita da ação usada nesta tentativa; não se atribui
o resultado a uma mudança de prompt ou de modo sem essa confirmação.

**Pendências do teste:** confirmar a operação/botão de aplicação, se o texto
permaneceu como proposta até confirmação, o destino, autosave, undo/redo e
persistência após recarregar. O pedido livre com intenção automática e o
contrato próprio de geração integral continuam sem homologação.

Esta conferência verifica a aderência textual ao cenário de teste, sem concluir
adequação jurídica, processual ou autorização de uso da minuta.
