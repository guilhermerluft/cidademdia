import { readFile, writeFile } from 'node:fs/promises';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const WEB_ROOT = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const DIST = resolve(WEB_ROOT, 'dist');
const PRODUCTION_ORIGIN = 'https://cidademdia.com.br';
const ORGANIZATION_ID = `${PRODUCTION_ORIGIN}/#organization`;
const WEBSITE_ID = `${PRODUCTION_ORIGIN}/#website`;
const DEFAULT_DESCRIPTION =
  'Registre ocorrências urbanas gratuitamente, acompanhe demandas e conecte cidadãos a órgãos e agentes públicos pelo Cidademdia.';
const ORGANIZATION_DESCRIPTION =
  'Cidademdia é uma plataforma digital de participação cidadã que aproxima cidadãos, órgãos, gestores e agentes públicos para registrar e acompanhar ocorrências urbanas com mais transparência.';

const pages = [
  {
    path: '/',
    file: 'index.html',
    title: 'Cidademdia | Ocorrências urbanas e participação cidadã',
    description: DEFAULT_DESCRIPTION,
    pageType: 'WebPage',
    breadcrumbLabel: 'Início',
    eyebrow: 'Participação cidadã e ocorrências urbanas',
    heading: 'Cidademdia: participação cidadã e ocorrências urbanas',
    summary:
      'Cidademdia é uma plataforma digital para registrar ocorrências urbanas, acompanhar demandas e aproximar cidadãos, órgãos, gestores e agentes públicos com mais transparência.',
  },
  {
    path: '/como-funciona',
    file: 'como-funciona.html',
    title: 'Como funciona o Cidademdia | Participação cidadã',
    description:
      'Veja em vídeo e passo a passo como registrar uma ocorrência no Cidademdia, compartilhar a demanda e acompanhar sua evolução.',
    pageType: 'WebPage',
    breadcrumbLabel: 'Como funciona',
    eyebrow: 'Passo a passo',
    heading: 'Como funciona o Cidademdia',
    summary:
      'Entenda como registrar uma ocorrência, informar local e evidências, direcionar a demanda e acompanhar sua evolução pelo Cidademdia.',
  },
  {
    path: '/ocorrencias',
    file: 'ocorrencias.html',
    title: 'Ocorrências públicas | Cidademdia',
    description:
      'Acompanhe ocorrências urbanas publicadas no Cidademdia, veja detalhes das demandas e apoie situações que também impactam você.',
    pageType: 'CollectionPage',
    breadcrumbLabel: 'Ocorrências',
    eyebrow: 'Demandas públicas',
    heading: 'Ocorrências urbanas públicas no Cidademdia',
    summary:
      'Consulte ocorrências urbanas publicadas, acompanhe detalhes das demandas e encontre situações que impactam a sua região.',
  },
  {
    path: '/representantes',
    file: 'representantes.html',
    title: 'Órgãos e agentes públicos | Cidademdia',
    description:
      'Consulte órgãos e agentes públicos cadastrados no Cidademdia e encontre quem pode acompanhar as demandas da sua cidade.',
    pageType: 'CollectionPage',
    breadcrumbLabel: 'Órgãos e agentes públicos',
    eyebrow: 'Gestão pública',
    heading: 'Órgãos e agentes públicos no Cidademdia',
    summary:
      'Encontre contas institucionais e agentes públicos cadastrados para conhecer quem pode acompanhar demandas relacionadas à sua cidade.',
  },
  {
    path: '/planos',
    file: 'planos.html',
    title: 'Planos para órgãos e gestores públicos | Cidademdia',
    description:
      'Conheça os planos do Cidademdia para órgãos, gestores e contas Master ampliarem atendimento, comunicação e acompanhamento de ocorrências.',
    pageType: 'WebPage',
    breadcrumbLabel: 'Planos',
    eyebrow: 'Planos institucionais',
    heading: 'Planos do Cidademdia para órgãos e gestores públicos',
    summary:
      'Conheça opções para contas Master organizarem atendimento, publicações, equipe e acompanhamento de ocorrências no Cidademdia.',
  },
  {
    path: '/sobre',
    file: 'sobre.html',
    title: 'Sobre o Cidademdia | Participação cidadã e gestão pública',
    description:
      'Conheça o Cidademdia, plataforma de participação cidadã para registrar ocorrências urbanas, acompanhar demandas e aproximar cidadãos de quem pode resolver.',
    pageType: 'AboutPage',
    breadcrumbLabel: 'Sobre',
    organizationAsMainEntity: true,
    eyebrow: 'Sobre a plataforma',
    heading: 'Sobre o Cidademdia',
    summary:
      'Cidademdia é uma plataforma de participação cidadã que aproxima cidadãos, órgãos, gestores e agentes públicos para registrar e acompanhar ocorrências urbanas.',
  },
];

function canonicalUrl(pathname) {
  return pathname === '/' ? `${PRODUCTION_ORIGIN}/` : `${PRODUCTION_ORIGIN}${pathname}`;
}

function organizationNode() {
  return {
    '@type': 'Organization',
    '@id': ORGANIZATION_ID,
    name: 'Cidademdia',
    alternateName: 'Cidade Em Dia',
    url: `${PRODUCTION_ORIGIN}/`,
    description: ORGANIZATION_DESCRIPTION,
    logo: {
      '@type': 'ImageObject',
      '@id': `${PRODUCTION_ORIGIN}/#logo`,
      url: `${PRODUCTION_ORIGIN}/favicon.svg`,
      contentUrl: `${PRODUCTION_ORIGIN}/favicon.svg`,
      caption: 'Cidademdia',
    },
    email: 'atendimento@cidademdia.com.br',
    areaServed: {
      '@type': 'Country',
      name: 'Brasil',
    },
    contactPoint: [
      {
        '@type': 'ContactPoint',
        contactType: 'atendimento',
        email: 'atendimento@cidademdia.com.br',
        availableLanguage: 'pt-BR',
      },
      {
        '@type': 'ContactPoint',
        contactType: 'ouvidoria',
        email: 'ouvidoria@cidademdia.com.br',
        availableLanguage: 'pt-BR',
      },
      {
        '@type': 'ContactPoint',
        contactType: 'comercial',
        email: 'comercial@cidademdia.com.br',
        availableLanguage: 'pt-BR',
      },
    ],
  };
}

function structuredData(page) {
  const url = canonicalUrl(page.path);
  const pageNode = {
    '@type': page.pageType,
    '@id': `${url}#webpage`,
    url,
    name: page.title,
    description: page.description,
    inLanguage: 'pt-BR',
    isPartOf: { '@id': WEBSITE_ID },
    publisher: { '@id': ORGANIZATION_ID },
  };

  if (page.organizationAsMainEntity) {
    pageNode.mainEntity = { '@id': ORGANIZATION_ID };
  }

  const graph = [
    organizationNode(),
    {
      '@type': 'WebSite',
      '@id': WEBSITE_ID,
      url: `${PRODUCTION_ORIGIN}/`,
      name: 'Cidademdia',
      alternateName: 'Cidade Em Dia',
      description: DEFAULT_DESCRIPTION,
      inLanguage: 'pt-BR',
      publisher: { '@id': ORGANIZATION_ID },
    },
    pageNode,
  ];

  if (page.path !== '/') {
    const breadcrumbId = `${url}#breadcrumb`;
    pageNode.breadcrumb = { '@id': breadcrumbId };
    graph.push({
      '@type': 'BreadcrumbList',
      '@id': breadcrumbId,
      itemListElement: [
        {
          '@type': 'ListItem',
          position: 1,
          name: 'Cidademdia',
          item: `${PRODUCTION_ORIGIN}/`,
        },
        {
          '@type': 'ListItem',
          position: 2,
          name: page.breadcrumbLabel,
          item: url,
        },
      ],
    });
  }

  return JSON.stringify({ '@context': 'https://schema.org', '@graph': graph }, null, 2);
}

function escapeHtml(value) {
  return value
    .replaceAll('&', '&amp;')
    .replaceAll('<', '&lt;')
    .replaceAll('>', '&gt;')
    .replaceAll('"', '&quot;')
    .replaceAll("'", '&#039;');
}

function replaceRequired(html, pattern, replacement, label) {
  if (!pattern.test(html)) {
    throw new Error(`Não foi possível localizar ${label} no HTML base.`);
  }
  return html.replace(pattern, replacement);
}

function staticContent(page) {
  return `<!-- SEO_STATIC_CONTENT_START -->
      <main class="seo-static-content">
        <a class="seo-static-content__brand" href="/">Cidademdia</a>
        <span class="seo-static-content__eyebrow">${escapeHtml(page.eyebrow)}</span>
        <h1>${escapeHtml(page.heading)}</h1>
        <p>${escapeHtml(page.summary)}</p>
        <nav aria-label="Páginas públicas do Cidademdia">
          <a href="/como-funciona">Como funciona</a>
          <a href="/ocorrencias">Ocorrências</a>
          <a href="/representantes">Órgãos e agentes públicos</a>
          <a href="/planos">Planos</a>
          <a href="/sobre">Sobre</a>
        </nav>
        <footer>Atendimento: atendimento@cidademdia.com.br · Ouvidoria: ouvidoria@cidademdia.com.br · Comercial: comercial@cidademdia.com.br</footer>
      </main>
      <!-- SEO_STATIC_CONTENT_END -->`;
}

function renderPage(baseHtml, page) {
  const url = canonicalUrl(page.path);
  let html = baseHtml;

  html = replaceRequired(html, /<title>[\s\S]*?<\/title>/i, `<title>${escapeHtml(page.title)}</title>`, 'title');
  html = replaceRequired(
    html,
    /<meta\s+name="description"[\s\S]*?\/>/i,
    `<meta name="description" content="${escapeHtml(page.description)}" />`,
    'meta description',
  );
  html = replaceRequired(
    html,
    /<link\s+rel="canonical"[\s\S]*?\/>/i,
    `<link rel="canonical" href="${url}" />`,
    'canonical',
  );
  html = replaceRequired(
    html,
    /<link\s+rel="alternate"\s+hreflang="pt-BR"[\s\S]*?\/>/i,
    `<link rel="alternate" hreflang="pt-BR" href="${url}" />`,
    'hreflang pt-BR',
  );
  html = replaceRequired(
    html,
    /<meta\s+property="og:title"[\s\S]*?\/>/i,
    `<meta property="og:title" content="${escapeHtml(page.title)}" />`,
    'og:title',
  );
  html = replaceRequired(
    html,
    /<meta\s+property="og:description"[\s\S]*?\/>/i,
    `<meta property="og:description" content="${escapeHtml(page.description)}" />`,
    'og:description',
  );
  html = replaceRequired(
    html,
    /<meta\s+property="og:url"[\s\S]*?\/>/i,
    `<meta property="og:url" content="${url}" />`,
    'og:url',
  );
  html = replaceRequired(
    html,
    /<meta\s+name="twitter:title"[\s\S]*?\/>/i,
    `<meta name="twitter:title" content="${escapeHtml(page.title)}" />`,
    'twitter:title',
  );
  html = replaceRequired(
    html,
    /<meta\s+name="twitter:description"[\s\S]*?\/>/i,
    `<meta name="twitter:description" content="${escapeHtml(page.description)}" />`,
    'twitter:description',
  );
  html = replaceRequired(
    html,
    /<script id="seo-structured-data" type="application\/ld\+json">[\s\S]*?<\/script>/i,
    `<script id="seo-structured-data" type="application/ld+json">\n${structuredData(page)}\n    </script>`,
    'JSON-LD',
  );
  html = replaceRequired(
    html,
    /<!-- SEO_STATIC_CONTENT_START -->[\s\S]*?<!-- SEO_STATIC_CONTENT_END -->/i,
    staticContent(page),
    'conteúdo estático SEO',
  );

  return html;
}

const baseHtml = await readFile(resolve(DIST, 'index.html'), 'utf8');

for (const page of pages) {
  const rendered = renderPage(baseHtml, page);
  if (!rendered.includes(page.heading)) throw new Error(`H1 estático ausente em ${page.file}.`);
  if (!rendered.includes(`href="${canonicalUrl(page.path)}"`)) throw new Error(`Canonical ausente em ${page.file}.`);
  if (!rendered.includes('"name": "Cidademdia"')) throw new Error(`Entidade Cidademdia ausente em ${page.file}.`);
  if (rendered.includes('CidadeEmDia')) throw new Error(`Grafia antiga da marca encontrada em ${page.file}.`);
  await writeFile(resolve(DIST, page.file), rendered, 'utf8');
  console.log(`seo_static_page=OK ${page.path} -> ${page.file}`);
}

console.log('SEO STATIC RENDER: OK');
