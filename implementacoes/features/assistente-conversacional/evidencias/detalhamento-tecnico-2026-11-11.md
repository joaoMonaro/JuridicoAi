# Conferência do detalhamento técnico

Data: 2026-11-11. Escopo: documentação e exemplos, sem implementação de produto.

## Código consultado

- Backend: base `1abfdae523ea5172de40f73deb0d660343f41ed9`.
- Frontend: `../JuridicoAi-frontend`, base
  `7700c511ff7f8ccd7e91893074e90a77a5a95769`, working tree limpo na consulta.
- Conferidos chat, TipTap/StarterKit/TextAlign, aplicação atual por seleção,
  autosave/prepareExport, paginação, cliente HTTP e workspace.
- Conferidos contratos, entidades/configurações EF, autorização JWT/Membership,
  repositório de Peças, flags, limites e exportador de corpo TipTap no backend.

## Verificação executada

- JSON Schema 2020-12 conferido com `jsonschema.Draft202012Validator`.
- Nove exemplos fictícios validados contra seus tipos, com formatos UUID/data.
- Hashes dos originais e previews conferidos usando serialização canônica descrita.
- Splice do exemplo aprovado reproduziu exatamente o documento e hash do recibo.
- Segundo grupo permaneceu idêntico e pendente no exemplo de aplicação parcial.
- Quatro entradas negativas rejeitadas: modo legado inesperado, exclusão não
  confirmada, hash malformado e link com javascript.
- Links locais e espaços dos documentos conferidos; `git diff --check` sem erros.

Essas verificações validam os artefatos de contrato e sua consistência interna.
Não comprovam a implementação de diff, transações, permissões ou OCR. Diagramas
Mermaid foram revisados textualmente; não foi executado renderizador nesta sessão.

## Não executado

Testes backend/frontend, migrations, chamadas ao modelo, instalação de dependências,
OCR, calibração de confiança, benchmarks, validação jurídica e deploy. Prazos de
retenção de backups e limites operacionais são propostas, não configuração aplicada.

As referências oficiais consultadas estão junto das decisões nas especificações.
A próxima etapa prevista é E0 no plano de entrega, incluindo provas de compatibilidade
e medições, antes de tratar os componentes como disponíveis no produto.
