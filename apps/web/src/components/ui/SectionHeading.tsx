import type { ReactNode } from 'react';

export interface SectionHeadingProps {
  title: string;
  subtitle?: string;
  action?: ReactNode;
  className?: string;
}

export function SectionHeading({ title, subtitle, action, className }: SectionHeadingProps) {
  return (
    <div className={['ced-section-heading', className ?? ''].filter(Boolean).join(' ')}>
      <div className="ced-section-heading__copy">
        <h2 className="ced-section-heading__title">{title}</h2>
        {subtitle ? <p className="ced-section-heading__subtitle">{subtitle}</p> : null}
      </div>
      {action}
    </div>
  );
}
