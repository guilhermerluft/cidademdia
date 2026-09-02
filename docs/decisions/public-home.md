# Nova Home pública — CidadeEmDia

## Objetivo

A rota pública principal apresenta o CidadeEmDia antes da autenticação e conduz o cidadão para login ou cadastro sem duplicar o fluxo de identidade existente.

## Decisões

- A Home é implementada do zero em `apps/web/src/modules/home`.
- Usa os tokens e componentes do design system aprovado.
- Publicações usam o endpoint público real de placements, priorizando `horizontal` e usando `feed` como fallback.
- Não são inventados dados públicos de ocorrências: o domínio atual exige autenticação para listar ocorrências. A seção correspondente explica a proteção e conduz ao cadastro/login.
- Login, cadastro, recuperação de senha e convites permanecem no fluxo existente de `App.tsx`.
- Usuário já autenticado continua sendo direcionado ao dashboard.
- Mobile usa navegação inferior e reorganiza o hero para manter CTAs separados da composição visual.

## Responsividade

Breakpoints cobertos pela implementação:

- 320–390 px;
- 390–620 px;
- tablet até 820 px;
- notebook até 1024 px;
- desktop acima de 1024 px.

## Critérios de homologação

- `/` sem sessão abre a Home pública.
- CTA `Entrar` abre o formulário existente de login.
- CTA `Criar conta` abre o formulário existente de cadastro.
- `/` com sessão válida continua abrindo o dashboard.
- reset de senha e convite por token continuam tendo precedência sobre a Home.
- publicações públicas carregam sem autenticação e possuem estado vazio/erro seguro.
- nenhuma listagem privada de ocorrência é chamada pela Home.
