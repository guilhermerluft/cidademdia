import { useEffect } from 'react';
import { useLocation } from 'react-router-dom';

const PRODUCTION_ORIGIN = 'https://cidademdia.com.br';
const PRODUCTION_HOSTS = new Set(['cidademdia.com.br', 'www.cidademdia.com.br']);
const ORGANIZATION_ID = `${PRODUCTION_ORIGIN}/#organization`;
const WEBSITE_ID = `${PRODUCTION_ORIGIN}/#website`;

interface SeoPageConfig {
  title: string;
  description: string;
  pageType?: 'WebPage' | 'CollectionPage' | 'AboutPage';
  breadcrumbLabel?: string;
  organizationAsMainEntity?: boolean;
}

const DEFAULT_TITLE = 'Cidademdia | Ocorrências urbanas e participação cidadã';
const DEFAULT_DESCRIPTION =
  'Registre ocorrências urbanas gratuitamente, acompanhe demandas e conecte cidadãos a órgãos e agentes públicos pelo Cidademdia.';
const ORGANIZATION_DESCRIPTION =
  'Cidademdia é uma plataforma digital de participação cidadã que aproxima cidadãos, órgãos, gestores e agentes públicos para registrar e acompanhar ocorrências urbanas com mais transparência.';

const PUBLIC_SEO: Record<string, SeoPageConfig> = {
  '/': {
    title: DEFAULT_TITLE,
    description: DEFAULT_DESCRIPTION,
    breadcrumbLabel: 'Início',
  },
  '/ocorrencias': {
    title: 'Ocorrências públicas | Cidademdia',
    description:
      'Acompanhe ocorrências urbanas publicadas no Cidademdia, veja detalhes das demandas e apoie situações que também impactam você.',
    pageType: 'CollectionPage',
    breadcrumbLabel: 'Ocorrências',
  },
  '/representantes': {
    title: 'Órgãos e agentes públicos | Cidademdia',
    description:
      'Consulte órgãos e agentes públicos cadastrados no Cidademdia e encontre quem pode acompanhar as demandas da sua cidade.',
    pageType: 'CollectionPage',
    breadcrumbLabel: 'Órgãos e agentes públicos',
  },
  '/planos': {
    title: 'Planos para órgãos e gestores públicos | Cidademdia',
    description:
      'Conheça os planos do Cidademdia para órgãos, gestores e contas Master ampliarem atendimento, comunicação e acompanhamento de ocorrências.',
    breadcrumbLabel: 'Planos',
  },
  '/como-funciona': {
    title: 'Como funciona o Cidademdia | Participação cidadã',
    description:
      'Veja em vídeo e passo a passo como registrar uma ocorrência no Cidademdia, compartilhar a demanda e acompanhar sua evolução.',
    breadcrumbLabel: 'Como funciona',
  },
  '/sobre': {
    title: 'Sobre o Cidademdia | Participação cidadã e gestão pública',
    description:
      'Conheça o Cidademdia, plataforma de participação cidadã para registrar ocorrências urbanas, acompanhar demandas e aproximar cidadãos de quem pode resolver.',
    pageType: 'AboutPage',
    breadcrumbLabel: 'Sobre',
    organizationAsMainEntity: true,
  },
};

function canonicalUrlForPath(pathname: string) {
  return pathname === '/' ? `${PRODUCTION_ORIGIN}/` : `${PRODUCTION_ORIGIN}${pathname}`;
}

function upsertMeta(attribute: 'name' | 'property', key: string, content: string) {
  let element = document.head.querySelector<HTMLMetaElement>(`meta[${attribute}="${key}"]`);

  if (!element) {
    element = document.createElement('meta');
    element.setAttribute(attribute, key);
    document.head.appendChild(element);
  }

  element.setAttribute('content', content);
}

function upsertCanonical(href: string) {
  let element = document.head.querySelector<HTMLLinkElement>('link[rel="canonical"]');

  if (!element) {
    element = document.createElement('link');
    element.setAttribute('rel', 'canonical');
    document.head.appendChild(element);
  }

  element.setAttribute('href', href);
}

function upsertAlternateLanguage(href: string) {
  let element = document.head.querySelector<HTMLLinkElement>('link[rel="alternate"][hreflang="pt-BR"]');

  if (!element) {
    element = document.createElement('link');
    element.setAttribute('rel', 'alternate');
    element.setAttribute('hreflang', 'pt-BR');
    document.head.appendChild(element);
  }

  element.setAttribute('href', href);
}

function organizationNode(): Record<string, unknown> {
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

function updateStructuredData(pathname: string, config?: SeoPageConfig) {
  const current = document.getElementById('seo-structured-data');

  if (!config) {
    current?.remove();
    return;
  }

  const canonicalUrl = canonicalUrlForPath(pathname);
  const webpageId = `${canonicalUrl}#webpage`;
  const pageNode: Record<string, unknown> = {
    '@type': config.pageType ?? 'WebPage',
    '@id': webpageId,
    url: canonicalUrl,
    name: config.title,
    description: config.description,
    inLanguage: 'pt-BR',
    isPartOf: {
      '@id': WEBSITE_ID,
    },
    publisher: {
      '@id': ORGANIZATION_ID,
    },
  };

  if (config.organizationAsMainEntity) {
    pageNode.mainEntity = { '@id': ORGANIZATION_ID };
  }

  const graph: Array<Record<string, unknown>> = [
    organizationNode(),
    {
      '@type': 'WebSite',
      '@id': WEBSITE_ID,
      url: `${PRODUCTION_ORIGIN}/`,
      name: 'Cidademdia',
      alternateName: 'Cidade Em Dia',
      description: DEFAULT_DESCRIPTION,
      inLanguage: 'pt-BR',
      publisher: {
        '@id': ORGANIZATION_ID,
      },
    },
    pageNode,
  ];

  if (pathname !== '/') {
    const breadcrumbId = `${canonicalUrl}#breadcrumb`;
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
          name: config.breadcrumbLabel ?? config.title,
          item: canonicalUrl,
        },
      ],
    });
  }

  const element = current ?? document.createElement('script');
  element.id = 'seo-structured-data';
  element.setAttribute('type', 'application/ld+json');
  element.textContent = JSON.stringify({ '@context': 'https://schema.org', '@graph': graph });

  if (!current) {
    document.head.appendChild(element);
  }
}

export function SeoMetadata() {
  const location = useLocation();

  useEffect(() => {
    const config = PUBLIC_SEO[location.pathname];
    const isProductionHost = PRODUCTION_HOSTS.has(window.location.hostname);
    const isIndexable = Boolean(config) && isProductionHost;
    const title = config?.title ?? 'Cidademdia';
    const description = config?.description ?? DEFAULT_DESCRIPTION;
    const canonicalPath = config ? location.pathname : '/';
    const canonicalUrl = canonicalUrlForPath(canonicalPath);
    const robots = isIndexable
      ? 'index, follow, max-image-preview:large, max-snippet:-1, max-video-preview:-1'
      : 'noindex, nofollow';

    document.title = title;
    upsertMeta('name', 'description', description);
    upsertMeta('name', 'robots', robots);
    upsertMeta('name', 'googlebot', robots);
    upsertCanonical(canonicalUrl);
    upsertAlternateLanguage(canonicalUrl);

    upsertMeta('property', 'og:type', 'website');
    upsertMeta('property', 'og:locale', 'pt_BR');
    upsertMeta('property', 'og:site_name', 'Cidademdia');
    upsertMeta('property', 'og:title', title);
    upsertMeta('property', 'og:description', description);
    upsertMeta('property', 'og:url', canonicalUrl);

    upsertMeta('name', 'twitter:card', 'summary');
    upsertMeta('name', 'twitter:title', title);
    upsertMeta('name', 'twitter:description', description);

    updateStructuredData(location.pathname, config);
  }, [location.pathname]);

  return null;
}
