# Validação da exportação Word e PDF em 2026-10-28

## Resultado

Implementação de DOCX e PDF disponível no backend e no editor. A validação local
passou em estrutura OOXML, testes automatizados, renderização e downloads reais.
Abertura e edição no Microsoft Word permanecem pendentes: esse aplicativo não
está disponível neste ambiente. Não foram marcadas como executadas.

## Ambiente

- API .NET 10, Open XML SDK 3.5.1 e SkiaSharp 3.119.4.
- LibreOffice 26.2.6.3, distribuição oficial Linux extraída no diretório do usuário.
- Fontes de substituição conferidas: Liberation Serif, Liberation Sans e Noto Serif.
- Chrome com Playwright; Vite e API em portas de teste separadas das portas de uso.
- Banco PostgreSQL exclusivo para validação, com usuários, caso, modelos e peça fictícios.

## Verificações automatizadas

- Backend: **152 testes aprovados**. Incluem validação OOXML, preservação de
  conteúdo e estrutura editável, listas nativas, imagem, rodapé, legado,
  limites geométricos, nó/marca não suportado e PNG corrompido.
- Serviço: versão obrigatória, isolamento de Organization, peça excluída,
  formato inválido e geração sem gravações ou consulta ao modelo original.
- Conversor: executável ausente, processo com falha, timeout, sucesso,
  concorrência limitada, cancelamento e remoção dos temporários.
- Frontend: **87 testes aprovados**. Incluem espera por requisição de salvamento
  já em andamento, drenagem de edições posteriores, erro/conflito, versão
  confirmada, download após salvar e cancelamento ao sair do editor.
- Lint, TypeScript e build de produção aprovados. O build mantém o aviso já
  existente de tamanho do bundle; não houve falha de compilação.

## Fluxo com API, banco e navegador reais

1. Criar modelos fictícios com marca em imagem e com cabeçalho textual.
2. Criar uma peça de várias páginas a partir do modelo com imagem.
3. Editar o último parágrafo e baixar Word antes do autosave agendado.
4. Confirmar que a versão exportada inclui a última edição.
5. Trocar o modelo e baixar PDF imediatamente após a troca.
6. Confirmar a nova apresentação e a preservação do corpo da peça.
7. Conferir nome com acentos, MIME, `Content-Disposition` e `Cache-Control`.
8. Exportar após excluir o modelo de origem: usa a apresentação copiada na peça.
9. Exportar peça finalizada sem alterar status ou versão pela exportação.
10. Usar tema escuro e zoom de 75% sem levar essas preferências ao arquivo.
11. Confirmar respostas 401, 404 para outra Organization, 409 e 400.
12. Provocar conflito entre sessões: a interface bloqueia o download da versão antiga.

## Conferência dos documentos

- DOCX com cabeçalho textual: **5 páginas** na conversão local.
- DOCX com marca em imagem: **6 páginas** na conversão local.
- Caso de limites: logotipo quadrado de 50 mm, afastamento superior de 30 mm,
  intervalo até o corpo de 20 mm e rodapé de 14 linhas, 16 pt, afastado 30 mm
  da borda inferior; a área do corpo foi reservada sem sobreposição.
- Arquivos renderizados com `render_docx.py` e páginas conferidas visualmente.
- Texto selecionável e acentos conferidos no PDF; todos os trechos do corpo das
  duas fixtures foram localizados após desconsiderar cabeçalhos/rodapés repetidos
  na extração de texto. Continuidade entre páginas preservada.
- Coordenadas de texto conferidas em todas as páginas das fixtures principais:
  corpo abaixo do cabeçalho e acima do rodapé. Marca presente em todas as páginas
  da fixture com imagem.
- Arquivos baixados pela interface também foram abertos/renderizados e tiveram
  a última edição e o último parágrafo conferidos.

Os arquivos de conferência são intermediários locais em `/tmp/juridicoai-export-qa`,
não anexos nem peças reais. As fixtures podem ser regeneradas pelo comando
registrado na [documentação operacional](../../operacao/exportacao-word-pdf.md).

## Pendência de aceite

Abrir os DOCX no Microsoft Word, conferir aviso de reparação, aparência e edição,
e registrar versão do aplicativo/fontes usadas. A validação OOXML e a abertura
pelo LibreOffice não substituem esse passo. Diferenças de repaginação entre
aplicativos são previstas; conteúdo perdido, cortes e sobreposições não são.
