import { useEffect } from 'react';
import { listPlacementPosts } from '../posts/postService';

export const HERO_BANNER_UPDATED_EVENT = 'cidademdia:hero-banner-updated';

const STYLE_ID = 'cidademdia-dynamic-hero-banner';
const REFRESH_INTERVAL_MS = 10 * 60 * 1000;

function applyHeroBanner(url?: string | null) {
  document.getElementById(STYLE_ID)?.remove();
  if (!url) return;

  const style = document.createElement('style');
  style.id = STYLE_ID;
  style.textContent = `.public-home .public-home__hero { background-image: url(${JSON.stringify(url)}) !important; }`;
  document.head.appendChild(style);
}

async function refreshHeroBanner() {
  try {
    const page = await listPlacementPosts('hero', undefined, 5, 'platform');
    const banner = page.items.find((post) =>
      post.masterUserId == null
      && post.status === 'published'
      && post.type === 'image');
    const media = banner?.media.find((item) =>
      item.status === 'ready'
      && item.readUrl
      && item.contentType.startsWith('image/'));

    applyHeroBanner(media?.readUrl ?? null);
  } catch {
    applyHeroBanner(null);
  }
}

export function HeroBannerBootstrap() {
  useEffect(() => {
    const handleRefresh = () => void refreshHeroBanner();

    void refreshHeroBanner();
    window.addEventListener(HERO_BANNER_UPDATED_EVENT, handleRefresh);
    const interval = window.setInterval(handleRefresh, REFRESH_INTERVAL_MS);

    return () => {
      window.removeEventListener(HERO_BANNER_UPDATED_EVENT, handleRefresh);
      window.clearInterval(interval);
      document.getElementById(STYLE_ID)?.remove();
    };
  }, []);

  return null;
}
