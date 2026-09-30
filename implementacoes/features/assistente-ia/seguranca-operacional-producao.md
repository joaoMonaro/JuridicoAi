# Segurança operacional da IA em produção

> **Escopo:** critérios de homologação de logs, proxy, APM e hospedagem
>
> **Estado:** configuração da API revisada; infraestrutura real pendente
>
> **Ponto relacionado:** `PA-11`

Este roteiro existe porque o repositório ainda não contém proxy reverso, APM
nem definição da hospedagem de produção. A ausência desses componentes no
código não comprova que estarão seguros no ambiente implantado.

O `PA-11` só pode considerar essa etapa concluída depois que os itens abaixo
forem executados no ambiente do piloto e as evidências forem preservadas sem
copiar conteúdo jurídico ou segredos.

## 1. Proteções já presentes na API

- não há middleware de HTTP logging configurado pela aplicação;
- o logging automático do `HttpClient` do provider foi removido;
- erros do fluxo de IA registram códigos e identificadores técnicos, sem
  registrar o objeto de exceção do provider;
- `appsettings.Production.json` mantém logs automáticos do ASP.NET Core, HTTP e
  comandos SQL em níveis que não capturam o tráfego normal;
- o corpo JSON da rota de IA, uploads, contexto, tokens e timeout possuem
  limites configuráveis;
- respostas SSE desabilitam cache e solicitam que proxies não façam buffering;
- a correlação usa um UUID opaco em `X-Correlation-ID`.

Essas proteções não controlam agentes, sidecars, gateways, proxies ou APMs
adicionados pela infraestrutura.

## 2. Requisitos para proxy e gateway

- não registrar corpo de request ou response das rotas da API;
- não registrar `Authorization`, `Cookie`, `Set-Cookie` nem headers de API key;
- não registrar query strings que possam conter pesquisa ou conteúdo jurídico;
- preservar `X-Correlation-ID`, sem tratá-lo como credencial;
- manter buffering desabilitado para `text/event-stream`;
- adotar limite de corpo igual ou menor que o limite correspondente da API;
- configurar timeout do stream acima do deadline da API, hoje 90 segundos,
  acrescido de margem operacional;
- encerrar a conexão quando o cliente desconectar, permitindo o cancelamento
  no backend;
- aceitar tráfego externo somente por TLS;
- não confiar em headers encaminhados de qualquer origem. A lista de proxies e
  redes confiáveis deve ser explícita antes de habilitar forwarded headers.

O endereço público, as redes confiáveis e o produto de proxy ainda precisam ser
definidos. Por isso, nenhuma configuração específica de Nginx, Traefik ou
serviço de nuvem é presumida neste documento.

## 3. Requisitos para APM, tracing e captura de erros

- desabilitar captura de request body, response body e payload de SSE;
- desabilitar captura dos headers sensíveis listados acima;
- não coletar variáveis de ambiente, argumentos de processo ou configuração
  que possam conter `AssistenteIa:ApiKey`, JWT secret ou connection strings;
- não anexar prompt, instrução, Peça, documento ou resposta da IA a spans;
- não usar `OrganizationId`, `UserId`, `CasoId`, `PecaId` ou `CorrelationId`
  como label de métrica de alta cardinalidade;
- restringir acesso a logs e traces e definir retenção conforme o `PA-03`;
- validar a política de scrub/redaction do produto escolhido com marcadores
  sintéticos, sem usar dados reais de clientes.

## 4. Ensaio obrigatório antes do piloto

Usar valores sintéticos únicos, por exemplo:

```text
LEGAL-CONTENT-MARKER-<uuid>
CPF-MARKER-<uuid>
JWT-MARKER-<uuid>
APIKEY-MARKER-<uuid>
PROVIDER-ERROR-MARKER-<uuid>
```

Executar, pela mesma rota externa que será usada pelo frontend:

1. sucesso de geração;
2. autenticação ausente e inválida (`401`);
3. recurso de outra Organization (`404` ou `403`);
4. payload e contexto acima do limite (`413`);
5. frequência ou concorrência excedida (`429`);
6. timeout do provider;
7. erro controlado do provider;
8. saída inválida do provider;
9. cancelamento do stream pelo cliente;
10. upload acima do limite.

Depois, pesquisar cada marcador em:

- stdout/stderr e agregador de logs da aplicação;
- access e error logs do proxy/gateway;
- traces, spans, breadcrumbs e eventos do APM;
- captura de exceções e alertas;
- logs do balanceador, WAF e plataforma de hospedagem.

Resultado obrigatório:

```text
0 ocorrências de todos os marcadores proibidos
```

Os códigos, status, duração, contagem de tokens e `CorrelationId` podem aparecer
quando previstos na política. O conteúdo usado no teste não pode aparecer.

## 5. Evidência de homologação

Registrar, sem segredos:

```text
Data e ambiente
Versão/commit implantado
Produtos e versões de proxy, APM e hospedagem
Configurações de captura/redaction verificadas
Cenários executados
Resultado da busca por marcador
Responsável técnico
Decisão: aprovado ou reprovado
Pendências e prazo, se houver
```

Não anexar exportações brutas que contenham tokens, headers ou conteúdo dos
requests. A evidência deve registrar o resultado da inspeção, não reproduzir o
dado proibido.

## 6. Critério de conclusão

Esta etapa do `PA-11` estará concluída somente quando:

- proxy, gateway, APM e hospedagem do piloto estiverem definidos;
- todas as configurações acima tiverem sido revisadas no ambiente implantado;
- o ensaio externo retornar zero ocorrências;
- a evidência tiver responsável e aprovação técnica;
- qualquer alteração posterior de proxy/APM repetir o ensaio.
