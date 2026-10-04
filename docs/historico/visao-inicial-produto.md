# Visão inicial do produto

> **Tipo:** documento histórico
>
> **Estado:** não normativo
>
> **Última revisão:** 2026-10-03

Este texto preserva a concepção inicial do produto. Para o comportamento atual,
consulte a [arquitetura](../arquitetura.md) e a
[documentação do assistente](../backend/assistente-ia.md).

A proposta parte de uma comparação com os copilotos utilizados por programadores.

No desenvolvimento de software, ferramentas como Rider, Visual Studio e outros editores funcionam como ambientes centrais de trabalho. Embora existam diferenças entre eles, sua organização costuma seguir uma estrutura semelhante: um espaço para visualizar os arquivos do projeto, uma área central para produzir o conteúdo e um painel auxiliar para interações e ferramentas.

A ideia é aplicar esse mesmo princípio ao contexto jurídico.

Ao iniciar um projeto no Rider, o programador abre um ambiente dedicado ao desenvolvimento de uma aplicação. No copilot jurídico, a ação equivalente seria iniciar o trabalho em uma nova peça.

A partir desse momento, seria aberto um ambiente completo de produção jurídica, dividido em três áreas principais.

### Painel esquerdo: contexto e documentos

No Rider, o painel esquerdo é utilizado para visualizar os diretórios e arquivos do projeto.

No copilot jurídico, esse espaço seria utilizado para anexar, organizar e visualizar todo o conteúdo relacionado ao trabalho em andamento, como:

* documentos;
* contratos;
* comprovantes;
* decisões;
* petições anteriores;
* relatos do cliente;
* descrição da situação;
* observações do advogado;
* demais arquivos relevantes.

Esse painel representaria o conjunto de informações que fornece contexto para a elaboração da peça.

### Área central: produção da peça

A área central seria o principal ambiente de trabalho.

Nela, o advogado produziria, revisaria e organizaria o conteúdo jurídico que dará origem ao produto final.

Esse espaço poderia conter a estrutura da peça, suas seções, argumentos, fatos, fundamentos, pedidos e demais elementos necessários para a elaboração do documento.

Assim como o código é o principal conteúdo trabalhado em uma IDE, a peça jurídica seria o conteúdo central do copilot.

### Painel direito: chat com o copilot

No lado direito, haveria um chat para interação com a inteligência artificial.

Por meio desse painel, o advogado poderia enviar comandos, solicitar análises, pedir alterações no conteúdo, buscar fundamentos, consultar jurisprudências e utilizar os documentos anexados como contexto.

O chat funcionaria como um assistente integrado ao ambiente de produção, capaz de compreender tanto o conteúdo da peça quanto os materiais disponíveis no painel esquerdo.

De forma resumida, a interface seria organizada da seguinte maneira:

```text
┌─────────────────────┬──────────────────────────────┬─────────────────────┐
│ Contexto e arquivos │                              │ Chat com o copilot  │
│                     │                              │                     │
│ Documentos          │      Conteúdo da peça        │ Prompts             │
│ Relato do cliente   │                              │ Solicitações        │
│ Comprovantes        │      Produção e revisão      │ Análises            │
│ Observações         │                              │ Sugestões           │
│ Jurisprudências     │                              │                     │
└─────────────────────┴──────────────────────────────┴─────────────────────┘
```

A proposta, portanto, é criar para o advogado um ambiente de produção semelhante a uma IDE: um espaço único em que o contexto do caso, o conteúdo jurídico e a interação com a inteligência artificial estejam integrados.
