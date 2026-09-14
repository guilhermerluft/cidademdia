import { useEffect } from 'react';
import { useLocation } from 'react-router-dom';

const PRODUCTION_ORIGIN = 'https://cidademdia.com.br';
const PRODUCTION_HOSTS = new Set(['cidademdia.com.br', 'www.cidademdia.com.br']);

interface SeoPageConfig {
  title: string;
  description: string;
}

const DEFAULT_TITLE = 'Cidademdia | Ocorrências urbanas e participação cidadã';
const DEFAULT_DESCRIPTION =
  'Registre ocorrências urbanas gratuitamente, acompanhe demandas e conecte cidadãos a órgãos e agentes públicos pelo Cidademdia.';

const PUBLIC_SEO: Record<string, SeoPageConfig> = {
  '/': {
    title: DEFAULT_TITLE,
    description: DEFAULT_DESCRIPTION,
  },
  '/ocorrencias': {
    title: 'Ocorrências públicas | Cidademdia',
    description:
      'Acompanhe ocorrências urbanas publicadas no Cidademdia, veja detalhes das demandas e apoie situações que também impactam você.',
  },
  '/representantes': {
    title: 'Órgãos e agentes públicos | Cidademdia',
    description:
      'Consulte órgãos e agentes públicos cadastrados no Cidademdia e encontre quem pode acompanhar as demandas da sua cidade.',
  },
  '/planos': {
    title: 'Planos para órgãos e gestores públicos | Cidademdia',
    description:
      'Conheça os planos do Cidademdia para órgãos, gestores e contas Master ampliarem atendimento, comunicação e acompanhamento de ocorrências.',
  },
  '/como-funciona': {
    title: 'Como funciona | Cidademdia',
    description:
      'Veja em vídeo e passo a passo como registrar uma ocorrência no Cidademdia, compartilhar a demanda e acompanhar sua evolução.',
  },
};

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

function updateStructuredData(pathname: string, config?: SeoPageConfig) {
  const current = document.getElementById('seo-structured-data');

  if (!config) {
    current?.remove();
    return;
  }

  const canonicalUrl = `${PRODUCTION_ORIGIN}${pathname === '/' ? '' : pathname}`;
  const graph: Array<Record<string, unknown>> = [
    {
      '@type': 'Organization',
      '@id': `${PRODUCTION_ORIGIN}/#organization`,
      name: 'Cidademdia',
      url: `${PRODUCTION_ORIGIN}/`,
    },
    {
      '@type': 'WebSite',
      '@id': `${PRODUCTION_ORIGIN}/#website`,
      url: `${PRODUCTION_ORIGIN}/`,
      name: 'Cidademdia',
      alternateName: 'Cidade Em Dia',
      description: DEFAULT_DESCRIPTION,
      inLanguage: 'pt-BR',
      publisher: {
        '@id': `${PRODUCTION_ORIGIN}/#organization`,
      },
    },
    {
      '@type': 'WebPage',
      '@id': `${canonicalUrl}#webpage`,
      url: canonicalUrl,
      name: config.title,
      description: config.description,
      inLanguage: 'pt-BR',
      isPartOf: {
        '@id': `${PRODUCTION_ORIGIN}/#website`,
      },
    },
  ];

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
    const canonicalUrl = `${PRODUCTION_ORIGIN}${canonicalPath === '/' ? '' : canonicalPath}`;
    const robots = isIndexable
      ? 'index, follow, max-image-preview:large, max-snippet:-1, max-video-preview:-1'
      : 'noindex, nofollow';

    document.title = title;
    upsertMeta('name', 'description', description);
    upsertMeta('name', 'robots', robots);
    upsertMeta('name', 'googlebot', robots);
    upsertCanonical(canonicalUrl);

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
