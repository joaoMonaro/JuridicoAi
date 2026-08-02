# Geração de petições com OpenAI

## Entrada e saída

Endpoint:

```http
POST /peticoes/gerar
Content-Type: application/json

{
  "fatoSimples": "Descrição objetiva do caso e dos pedidos pretendidos."
}
```

A resposta bem-sucedida é o texto retornado pela OpenAI, serializado como uma
string JSON pelo ASP.NET Core.

## Montagem do contexto

Antes de chamar o modelo, `PeticaoApplicationService`:

1. consulta até 30 ementas no MongoDB;
2. ordena por confiança de classificação decrescente;
3. prefixa cada ementa com `- `;
4. usa `[nenhuma ementa fornecida]` quando a collection está vazia;
5. combina as ementas com `fatoSimples` em um prompt jurídico.

O prompt solicita uma peça de Direito do Consumidor e orienta o modelo a:

- usar somente fatos fornecidos pelo usuário;
- usar somente as ementas recebidas;
- não inventar datas, valores, provas ou dados das partes;
- usar marcadores quando faltar informação;
- criar tutela de urgência e valor da causa somente quando houver suporte nos
  dados.

## Integração OpenAI

O gateway envia:

```http
POST https://api.openai.com/v1/responses
Authorization: Bearer {OpenAI:ApiKey}
Content-Type: application/json

{
  "model": "valor de OpenAI:Model",
  "input": "prompt montado pelo serviço"
}
```

Ele lê `output_text` da resposta. Se esse campo não puder ser obtido, devolve o
corpo bruto recebido.

Configurações:

| Chave | Obrigatória | Padrão |
|---|---|---|
| `OpenAI:ApiKey` | sim para esta funcionalidade | nenhum |
| `OpenAI:Model` | não | `gpt-4.1` |

## Tratamento de erros

- `fatoSimples` ausente ou vazio retorna HTTP 400;
- chave ausente provoca erro de configuração ao resolver o gateway;
- resposta não bem-sucedida da OpenAI provoca `HttpRequestException`;
- não há tratamento específico no controller para indisponibilidade, rate
  limit ou timeout da OpenAI;
- a mensagem da exceção atual inclui o corpo de erro devolvido pelo provedor.

## Cuidados de uso

O resultado é conteúdo gerado por modelo e precisa de revisão profissional
antes de qualquer uso jurídico. Também devem ser observados:

- sigilo e base legal para enviar fatos do caso a um serviço externo;
- minimização ou anonimização de dados pessoais;
- adequação da peça ao juízo e ao procedimento;
- conferência integral das ementas e dos pedidos;
- custo e limite de contexto, pois as ementas não são truncadas pelo backend.

O projeto não implementa atualmente auditoria do prompt, moderação, cache,
retentativa, timeout próprio, seleção semântica de precedentes ou validação
posterior da peça.
