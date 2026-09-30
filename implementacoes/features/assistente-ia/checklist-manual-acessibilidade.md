# Checklist manual — teclado e tecnologias assistivas

> **Estado:** pendente de execução manual
>
> **Relacionado:** [Tópico 12 — Testes e acessibilidade](topico-12-testes-acessibilidade.md)

Este checklist registra somente verificações que a suíte automatizada não
comprova integralmente. Os testes Playwright já cobrem atalhos simulados, foco
inicial, parte da ordem de tabulação, `Escape`, aplicação por teclado, undo e
redo.

Nenhum dado real de cliente deve ser utilizado durante esta auditoria.

## 1. Teclado físico e foco visível

Executar pelo menos uma vez em Windows ou Linux e uma vez em macOS:

- [ ] `Ctrl+Alt+A` abre o assistente em Windows/Linux sem conflito com o SO ou
      navegador;
- [ ] `Cmd+Alt+A` abre o assistente no macOS sem conflito com o SO ou navegador;
- [ ] o indicador de foco é claramente visível em botões, campos, selects,
      resumo expansível e links;
- [ ] a ordem completa de `Tab` e `Shift+Tab` acompanha a ordem visual e não
      salta ações relevantes;
- [ ] não existe armadilha de foco no painel lateral não modal;
- [ ] `Escape` cancela somente a geração ativa; um novo `Escape` fecha o painel;
- [ ] ao fechar o painel, o cursor retorna ao editor em posição coerente;
- [ ] é possível solicitar, revisar, editar, aplicar, rejeitar, copiar e gerar
      novamente sem usar mouse;
- [ ] undo e redo funcionam pelos atalhos nativos da plataforma após aplicar;
- [ ] zoom do navegador em 200% não oculta foco nem ações essenciais.

## 2. Leitor de tela

Executar o fluxo principal com pelo menos uma combinação disponível:

```text
Windows -> NVDA + Chrome ou Firefox
macOS   -> VoiceOver + Safari
Linux   -> Orca + Firefox
```

- [ ] o botão informa nome, estado aberto/fechado e atalho compreensível;
- [ ] o painel é anunciado como região “Assistente de IA”;
- [ ] labels dos campos e opções são anunciados corretamente;
- [ ] o estado de geração é anunciado uma vez, sem leitura de cada token;
- [ ] conclusão, cancelamento e erros são anunciados sem repetição excessiva;
- [ ] original e proposta podem ser distinguidos sem depender apenas de cor;
- [ ] alertas e motivo de bloqueio da aplicação são compreensíveis;
- [ ] o retorno ao editor após fechar ou aplicar é perceptível;
- [ ] a leitura continua utilizável com conteúdo jurídico extenso.

## 3. Revisão visual complementar

- [ ] foco, hover, erro e estado desabilitado possuem contraste perceptível;
- [ ] tema claro e tema escuro do documento não escondem cursor ou seleção;
- [ ] painel permanece utilizável em viewport estreita e com texto ampliado;
- [ ] movimento de foco não causa rolagem inesperada ou perda de contexto;
- [ ] nenhuma instrução depende exclusivamente de posição, ícone ou cor.

## 4. Registro da execução

Preencher uma linha por ambiente testado:

| Data | Pessoa | SO/navegador | Leitor de tela | Resultado | Evidência ou issue |
|---|---|---|---|---|---|
| Pendente | — | — | — | Não executado | — |

Qualquer falha crítica ou que impeça operação por teclado bloqueia o fechamento
do PA-12. Problemas menores devem ser registrados com severidade, responsável e
prazo.
