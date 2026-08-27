# CidadeEmDia — referência visual e design system

## Contexto

A nova aplicação será construída do zero. As telas e estilos do legado podem ser consultados apenas como referência visual quando houver um padrão útil, sem importar CSS, HTML, componentes, rotas ou estrutura antiga.

As referências aprovadas pelo cliente em 27/08/2026 definem a direção visual da home e dos dashboards das contas. O logo oficial fornecido no mesmo dia passa a ser a referência principal para a identidade cromática.

## Direção visual

- Azul como base institucional e de confiança.
- Verde como cor de conexão, evolução e resolução.
- Amarelo como destaque energético da marca.
- A identidade deve transmitir a transição visual azul → verde → amarelo presente no logo, sem aplicar gradiente indiscriminadamente em todos os componentes.
- Azul-marinho para títulos e textos de maior hierarquia.
- Fundo geral claro, superfícies brancas e bordas suaves.
- Cards arredondados com sombra discreta e pouca poluição visual.
- Badges semânticos para status.
- Navegação com comportamento próximo de aplicativo, especialmente em mobile.
- Botões com área de toque mínima de 44 px e foco visível.

## Paleta oficial de trabalho

Os valores abaixo foram extraídos visualmente do logo fornecido pelo cliente e serão os tokens institucionais até eventual entrega de manual de marca oficial:

| Token | Hex | Uso principal |
| --- | --- | --- |
| Brand Blue | `#1962A9` | base institucional, navegação, links e ações |
| Brand Sky | `#4892CF` | transição e superfícies de apoio |
| Brand Teal | `#4F9290` | ponte entre azul e verde |
| Brand Green | `#69A53D` | resolução, conexão e destaque positivo |
| Brand Lime | `#A0C03E` | apoio entre verde e amarelo |
| Brand Yellow | `#E3DE56` | destaque, energia e assinatura visual |
| Navy | `#0D274E` | títulos e textos de alta hierarquia |

Gradiente institucional:

```css
linear-gradient(
  105deg,
  #1962A9 0%,
  #4892CF 26%,
  #4F9290 48%,
  #69A53D 68%,
  #E3DE56 100%
)
```

O gradiente completo deve ser priorizado em áreas de branding, hero, faixas, wordmark e detalhes decorativos. Para botões com texto branco, usar a variação azul → teal → verde, preservando contraste; o amarelo permanece como detalhe de marca e não como fundo principal sob texto branco.

## Tokens

Os tokens ficam em `apps/web/src/styles/tokens.css` e cobrem:

- cores institucionais e estados semânticos;
- gradiente institucional completo e gradiente de ação;
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
- `Brand`: wordmark reutilizável com gradiente institucional e marca quadrada multicolorida;
- `SectionHeading`: cabeçalho reutilizável de seção.

## Login e autenticação

A tela de autenticação deve funcionar como primeira validação prática da identidade:

- desktop com painel institucional azul → verde → amarelo e card de autenticação ao lado;
- mobile com painel institucional compacto acima do formulário;
- wordmark com gradiente institucional no card;
- CTA principal com gradiente de ação de alto contraste;
- formulários claros e discretos;
- mesma linguagem para login, cadastro e recuperação de senha.

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

- paleta e gradientes;
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
