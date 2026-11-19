# MVP — redação de peças jurídicas

Atualizado em **2026-11-19**. Primeiro usuário: **um advogado empresarial**.

## Objetivo

O advogado fornece fatos e documentos pelo chatbot, produz uma peça com apoio
da IA, revisa o texto e exporta um arquivo utilizável no seu trabalho.

## O que será entregue

- Acesso autenticado aos próprios trabalhos.
- Início e retomada de uma peça sem preencher cadastros de dados jurídicos.
- Entrada de fatos, orientações e correções pelo chat, com anexos PDF com texto,
  DOCX e TXT. Falhas e limitações de leitura devem ser informadas.
- Geração da peça e ajustes pela conversa, considerando os dados fornecidos e
  o texto atual. Informações ausentes devem ser sinalizadas.
- Visualização das alterações propostas, com aprovação ou rejeição antes de
  modificar a peça.
- Edição manual do texto e configurações de layout já existentes: modelo de
  apresentação, margens, fonte, espaçamento, cabeçalho e rodapé.
- Salvamento e retomada da peça, conversa e anexos, mantendo a exclusão do
  histórico já disponível.
- Exportação da versão salva para Word editável e PDF.

## Fora do MVP

- Cadastros manuais de empresas, partes, endereços, fatos e contextos; cadastro
  separado de documentos do Caso. Esses dados entram pelo chatbot.
- Criação automática de cadastros estruturados pela IA.
- Scraper, pesquisa e processamento de jurisprudências.
- OCR e leitura de imagens ou documentos digitalizados.
- Novos usos ou funcionalidades além da redação e finalização das peças.

## Quando estará pronto

A validação com o advogado será feita com o MVP online, em acesso restrito ao piloto.

- [ ] O advogado conclui o fluxo de fornecer informações até exportar a peça,
  usando um trabalho representativo da sua rotina, e considera o resultado útil.
- [ ] A peça respeita os fatos fornecidos e permite identificar e corrigir
  lacunas; nenhuma alteração é aplicada sem aprovação.
- [ ] Edição, aprovação, salvamento e retomada funcionam sem perder texto ou
  duplicar alterações, inclusive após recarregar a página ou uma falha de conexão.
- [ ] Word e PDF contêm o texto salvo e o layout escolhido, conferidos pelo advogado.
- [ ] O ambiente está configurado para esse usuário, com acesso aos dados
  protegido e tempo de resposta e consumo verificados para o uso inicial.

## Regra de encerramento

Até cumprir os critérios acima, o trabalho se limita a testar, corrigir falhas,
simplificar a jornada e preparar a operação desse escopo. Novas funcionalidades
ficam para depois do lançamento. Cumpridos os critérios, o MVP está concluído.

As tarefas de execução ficam no [TODO geral](TODO.md) e no
[TODO do assistente](implementacoes/features/assistente-conversacional/TODO.md).
