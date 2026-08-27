# CidadeEmDia — referência visual e design system

## Contexto

A nova aplicação será construída do zero. As telas e estilos do legado podem ser consultados apenas como referência visual quando houver um padrão útil, sem importar CSS, HTML, componentes, rotas ou estrutura antiga.

As referências aprovadas pelo cliente em 27/08/2026 definem a direção visual da home e dos dashboards das contas.

## Direção visual

- Azul vivo como cor principal para navegação, CTAs, ícones ativos e ações primárias.
- Azul-marinho para títulos e textos de maior hierarquia.
- Verde para sucesso, resoluções e destaques positivos.
- Amarelo/laranja para ênfase, andamento e destaques complementares.
- Fundo geral claro, superfícies brancas e bordas suaves.
- Cards arredondados com sombra discreta e pouca poluição visual.
- Badges semânticos para status.
- Navegação com comportamento próximo de aplicativo, especialmente em mobile.
- Botões com área de toque mínima de 44 px e foco visível.

Os valores CSS foram extraídos visualmente das referências e servem como base técnica. Caso o cliente forneça manual de marca ou valores oficiais posteriormente, os tokens devem ser ajustados sem alterar os componentes consumidores.

## Tokens

Os tokens ficam em `apps/web/src/styles/tokens.css` e cobrem:

- cores e estados semânticos;
- tipografia;
- espaçamento;
- border radius;
- sombras;
- focus ring;
- duração de transições;
- largura máxima e gutters.

Nenhum módulo deve hardcodar novamente cores que já existam como token, salvo casos realmente específicos de conteúdo.

## Componentes base

A fundação inicial fica em `apps/web/src/components/ui`:

- `Button`: primary, secondary, soft, ghost e danger;
- `Card`: base, elevated e interactive, com header/body/footer;
- `Badge`: estados neutro, primário, sucesso, warning, danger, info e status de ocorrência;
- `Brand`: wordmark visual reutilizável sem depender de asset legado;
- `SectionHeading`: cabeçalho reutilizável de seção.

## Responsividade

Faixas mínimas de validação:

- 320–375 px;
- 390–430 px;
- tablet;
- notebook;
- desktop.

No mobile, a navegação deve priorizar padrão app-like, cards em uma coluna quando necessário e CTAs do hero sem conflito com imagem ou conteúdo adjacente.

## Home

A home deve seguir a composição visual aprovada:

1. header/navegação;
2. hero com headline, copy, imagem e dois CTAs;
3. mídias do CidadeEmDia;
4. ocorrências recentes;
5. navegação inferior em mobile quando aplicável.

A home antiga não será reutilizada.

## Dashboards

Citizen, Master, Subaccount e Admin compartilham o mesmo shell visual, variando navegação e conteúdo conforme role/policies.

O shell deve fornecer:

- marca e navegação;
- cabeçalho/contexto da conta;
- cards de resumo;
- ações rápidas;
- áreas de listagem;
- estados vazios/loading/error consistentes;
- comportamento responsivo.

## Regra de reutilização do legado

Pode ser reproduzido conceitualmente:

- paleta aproximada;
- ritmo de espaçamento;
- arredondamento;
- hierarquia visual;
- padrão de cards, botões, badges e navegação.

Não pode ser reaproveitado diretamente:

- arquivos CSS;
- componentes React;
- HTML;
- estrutura de páginas;
- modelos/contratos antigos;
- dependências técnicas do frontend legado.
