# Configuração da exportação Word e PDF

## Dependências

O backend gera DOCX com Open XML SDK. A conversão de DOCX para PDF usa uma
instalação local do LibreOffice Writer, executada sem interface. SkiaSharp mede
texto para reservar espaço de cabeçalho/rodapé e verifica a imagem incorporada.
No Linux, seu pacote nativo também precisa das bibliotecas de fontes do sistema.

Em Ubuntu/Debian, uma instalação convencional pode ser preparada com:

```bash
sudo apt-get install libreoffice-writer fontconfig libfontconfig1 fonts-liberation fonts-noto-core
```

O comando requer acesso administrativo do operador. Não é executado pela API.
Também é possível usar uma distribuição oficial do LibreOffice extraída em um
diretório do usuário e configurar o caminho absoluto para seu `program/soffice`.
Não apontar para um executável de uma instalação interativa em uso: cada
conversão da aplicação cria seu próprio perfil temporário.

## Configuração da API

A seção `PecaExport` aceita:

| Chave | Padrão | Uso |
|---|---|---|
| `LibreOfficePath` | `soffice` | Executável no PATH ou caminho absoluto, sem argumentos adicionais |
| `TempPath` | `<temporário do sistema>/juridicoai-export` | Diretório de trabalho gravável pelo usuário da API |
| `TimeoutSeconds` | `60` | Prazo da conversão; limitado a 1–300 segundos |
| `MaxConcurrentConversions` | `2` | Conversões simultâneas por processo da API; limitado a 1–8 |

Exemplo para uma instalação local:

```bash
dotnet user-secrets set 'PecaExport:LibreOfficePath' '/caminho/libreoffice/program/soffice' --project src/JuridicoAI.Api
```

Em operação, as mesmas chaves podem ser informadas por variáveis de ambiente,
por exemplo `PecaExport__LibreOfficePath` e `PecaExport__TempPath`.
Reiniciar a API depois de alterar essas configurações.

O limite de concorrência não é uma fila: se todas as vagas estiverem ocupadas,
a API responde 503 e o usuário pode tentar novamente. O limite é local a cada
instância, não compartilhado entre réplicas. Não envolve novas tabelas ou jobs.

A conversão usa um diretório privado e perfil do LibreOffice distintos para cada
pedido. Sucesso, falha, cancelamento e timeout encerram o processo e removem os
arquivos temporários. Uma falha de remoção gera aviso sem registrar conteúdo.
O diretório raiz deve ter permissões de escrita e espaço disponível.

## Fontes e correspondência visual

O DOCX mantém o nome da fonte configurada na peça. O renderizador de PDF usa
as fontes instaladas no servidor e o mecanismo de substituição do sistema.
No ambiente Linux de validação desta entrega, `fc-match` resolveu:

| Fonte solicitada | Substituição no ambiente validado |
|---|---|
| Times New Roman | Liberation Serif |
| Arial | Liberation Sans |
| Georgia | Noto Serif |

As substituições podem alterar quebras de página em relação ao Microsoft Word.
Preparar as mesmas famílias e regras de fontes em todos os ambientes de PDF;
se forem usadas outras famílias ou fontes originais, repetir a conferência
visual. Não assumir que a fonte instalada no computador do usuário está
instalada no servidor.

O exportador mapeia os estilos do editor claro: corpo com fonte/tamanho e
entrelinha da peça, espaçamento entre parágrafos, títulos hierárquicos, listas
nativas, citação recuada, links e marcas de texto. O espaçamento simulado da
paginação da tela, tema escuro e zoom não entram nos arquivos.
Cabeçalho e rodapé se repetem nos arquivos, enquanto o editor continua exibindo
um documento contínuo. A área reservada pode crescer para comportar a identidade
visual; configurações sem espaço útil para o corpo retornam 400.

## Verificação

Conferir o executável antes de iniciar a aplicação:

```bash
/caminho/libreoffice/program/soffice --headless --version
```

Para produzir as peças fictícias usadas nos testes estruturais:

```bash
JURIDICOAI_EXPORT_QA_DIR=/tmp/juridicoai-export-qa dotnet test tests/JuridicoAI.Api.Tests --filter FullyQualifiedName~PecaDocxGeneratorTests
```

Essa variável é usada somente pelos testes e grava fixtures DOCX e JSON no
diretório indicado. Não modifica peças reais. Abrir os arquivos no Word e
LibreOffice, converter em PDF e conferir todas as páginas, principalmente
cabeçalho, rodapé, fim de página e continuidade do texto.

Na interface, abrir uma peça, editar seu conteúdo e usar **Exportar → Word
(.docx)** ou **Exportar → PDF (.pdf)** antes de o autosave automático terminar.
O arquivo deve incluir a edição depois de sua gravação confirmada.

## Diagnóstico

| Resultado | Verificação |
|---|---|
| 400 | Documento/configurações inválidos, nó ou marca não suportado, imagem corrompida ou falta de área útil |
| 409 | A versão mudou; resolver o conflito/recarregar antes de exportar |
| 503, conversor indisponível | Conferir caminho do executável, dependências, acesso ao temporário e vagas disponíveis |
| 504 | A conversão excedeu o prazo; conferir tamanho/complexidade e recursos do ambiente |
| Fonte ou paginação diferente | Conferir `fc-match`, fontes instaladas e versão do LibreOffice |

Sem LibreOffice, o download Word continua disponível; a exportação PDF informa
a indisponibilidade. Os downloads exigem autenticação e a Organization da peça.
O conteúdo não é enviado para um serviço externo, não passa por IA e não é
armazenado como uma nova versão ou anexo no sistema.
