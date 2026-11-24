# Versionamento

Fluxo definido em **2026-11-18** para este repositório backend.

## Branches permanentes

| Branch | Finalidade | Entrada de mudanças |
|---|---|---|
| `main` | Referência das versões aprovadas para produção; permanece como branch padrão do GitHub | Pull request de `homologacao`, após validação, ou correção urgente de produção |
| `homologacao` | Integração e validação do próximo conjunto de mudanças | Pull requests de branches curtas de desenvolvimento |

A criação dessas branches organiza o código. A implantação dos ambientes é uma
etapa operacional separada, acompanhada nos TODOs do projeto.

## Organização inicial

- `homologacao` parte de `85f687e`, último commit de
  `feature/melhora-tratamento-de-dados` no momento da organização. Assim, recebe
  os 45 commits acumulados desde a `main`, preservando seu histórico.
- `main` permanece em `f470e4e` até uma promoção aprovada. Essa referência
  anterior não constitui homologação do código antigo nem declaração de deploy.
- As branches anteriores ficam preservadas para consulta. O desenvolvimento
  continua pelo novo fluxo, sem acrescentar trabalho à antiga branch de feature.
- A primeira promoção deve cumprir os critérios do [MVP](../MVP.md), incluindo
  as condições do [TODO geral](../TODO.md) e do
  [Preparação para produção e pendências](../implementacoes/producao-e-pendencias.md).

## Desenvolvimento

Criar uma branch a partir de `homologacao` atualizada, com nome ligado à tarefa:

```bash
git switch homologacao
git pull --ff-only origin homologacao
git switch -c feature/proj-003-jornada-sem-cadastros
```

Usar `feature/<tarefa>` para funcionalidades, `fix/<tarefa>` para correções e
`chore/<tarefa>` para manutenção. Cada branch deve ter um escopo revisável;
novas tarefas independentes começam em outra branch.

Publicar a branch e abrir um pull request com destino a `homologacao`,
descrevendo a mudança e as verificações realizadas. Atualizar a documentação
afetada junto com a implementação e executar os checks adequados à alteração.

## Promoção para produção

1. Validar o conjunto integrado em `homologacao`, registrando o commit avaliado
   e as evidências técnicas e de uso exigidas pelo escopo.
2. Abrir um pull request de `homologacao` para `main`, com o resumo da entrega
   e a validação. Novos commits exigem conferir novamente o conjunto a promover.
3. Integrar com **merge commit**, preservando a ancestralidade entre as branches
   permanentes; evitar squash ou rebase nessa promoção.
4. Identificar a versão aprovada com uma tag `vMAJOR.MINOR.PATCH` no commit da
   `main` usado na entrega, por exemplo `v0.1.0` para a primeira versão do MVP.
5. Sincronizar `main` de volta em `homologacao` por merge quando necessário,
   especialmente se houver correções feitas diretamente na linha de produção.

As regras de pull request descritas aqui são o fluxo de trabalho acordado.
Proteções obrigatórias e checks automáticos no GitHub dependem de configuração
própria; a `main` estava sem proteção na consulta inicial.

## Correções urgentes de produção

Criar `hotfix/<tarefa>` a partir de `main`, validar a correção e abrir pull
request para `main`. Depois da aprovação, promover uma versão de correção e
integrar `main` em `homologacao` por pull request, para preservar o ajuste no
próximo lançamento.

Preservar o histórico das branches permanentes, usando merges e commits de
reversão quando necessário.
