import type { AnchorHTMLAttributes } from 'react';

export interface BrandProps extends AnchorHTMLAttributes<HTMLAnchorElement> {
  compact?: boolean;
}

export function Brand({ className, compact = false, href = '/', ...props }: BrandProps) {
  return (
    <a
      className={['ced-brand', compact ? 'ced-brand--compact' : '', className ?? ''].filter(Boolean).join(' ')}
      href={href}
      aria-label="CidadeEmDia"
      {...props}
    >
      <span className="ced-brand__mark" aria-hidden="true">
        <span />
        <span />
        <span />
        <span />
      </span>
      <span className="ced-brand__wordmark">
        <strong>CIDADE</strong><em>MDIA</em>
      </span>
    </a>
  );
}
