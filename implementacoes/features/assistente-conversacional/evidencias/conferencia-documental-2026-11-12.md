# Conferência documental do assistente conversacional

Conferência em **2026-11-12**, após a rodada real reduzida e a mudança do TODO para
a pasta da feature. Escopo: documentação do assistente no backend, índices do
projeto, especificações de referência e registros correspondentes do frontend.

## Referências conferidas

- Baseline backend `4547485`, frontend `50aa439` e alterações locais da sessão.
- Controllers de conversa/workspace, documentos, análise, geração e histórico
  pessoal, além de DTOs e autenticação pessoal.
- Settings de IA/documentos, configurações EF, entidades/jobs e worker de geração.
- Evidências anteriores dos testes de backend/frontend e artefatos locais das
  duas execuções que concluíram a avaliação reduzida.

## Divergências encontradas e corrigidas

| Documento/frente | Divergência | Correção |
|---|---|---|
| Referência principal do assistente | Memória, worker geral e geração por seções tratados como pendentes | Estado implementado, flags, acompanhamento do job e leitura histórica pessoal descritos |
| API, arquitetura, autenticação e configuração | Descrição centrada no endpoint legado e na sessão organizacional | Coexistência v1/v2, sessão pessoal, flags novas e links para contratos efetivos |
| Especificações 01–04 | Projeção inicial de tabelas, interfaces, rotas e limites confundida com contrato atual | Desenho original identificado; diferenças concretas e fonte de integração na especificação 07 |
| Catálogo de exemplos | `MensagemAceita` 202 e journal `Evento` associados indevidamente ao fluxo durável entregue | Distinção entre exemplos/projeções e SSE de enfileiramento com polling |
| Índices e plano anterior de minutas | Nova geração ainda descrita como não implementada/E0 inicial | Plano antigo identificado como histórico e andamento direcionado à feature atual |
| Frontend | Limitações de recortes antigos pareciam pendências gerais; README omitia geração durável/histórico pessoal | Página de estado atual e referências em cada registro de etapa, preservando contagens/evidências originais |
| TODO e melhorias | Precisavam permanecer distinguíveis das pendências de entrega | TODO localizado na feature, P01–P09 separados de M01/M02, links de entrada atualizados |

A especificação [07 — contratos implementados](../especificacoes/07-contratos-implementados.md)
explicita, entre outras diferenças: capacidades em `/assistente/capacidades`,
SSE 200 de envio, acompanhamento em `/geracoes`, exclusão nas rotas pessoais,
mensagens retornadas como array, ausência do journal genérico inicialmente
proposto, lease de 180 s e interrupção sem retry pago automático de uso incerto.

## Estado que deve ser preservado

- Fluxo principal implementado atrás de flags falsas por padrão; sem liberação
  de produção ou homologação integral do piloto.
- Avaliação real reduzida concluída: quatro cenários/seis interações, 18 chamadas,
  custo calculado de US$ 0,04549805; cenários extensos e revisão jurídica abertos.
- Investigação de latência M01 aberta; correção do setup de isolamento M02 concluída.
- Controle financeiro de produção, monitoramento, proteção de restauração,
  rollout, compatibilidade ampliada e implantação continuam em P03–P08.
- Exclusão comprovada apenas no ambiente ativo; sem expurgo de backups comprovado.
- OCR continua desativado e em espera. Chave da avaliação não mantida configurada.

## Limites desta conferência

Não foram executadas novas chamadas pagas, migrations, testes de aplicação,
builds ou deploys. Os números de testes indicados são os resultados históricos
registrados; esta revisão não reaprova a suíte E2E inteira ou a qualidade jurídica.
Foram conferidos os destinos e as âncoras dos links locais dos documentos alterados
e o diff sem erros de whitespace. Documentos de evidência preservam data e limites
de cada etapa. Projeções originais não foram convertidas em migrations ou APIs novas.

Andamento canônico: [estado atual](../estado-atual.md) e [TODO](../TODO.md).
