import type { HTMLAttributes, PropsWithChildren } from 'react';

type BadgeVariant =
  | 'neutral'
  | 'primary'
  | 'success'
  | 'warning'
  | 'danger'
  | 'info'
  | 'forwarded'
  | 'progress'
  | 'resolved'
  | 'cancelled';

export interface BadgeProps extends HTMLAttributes<HTMLSpanElement> {
  variant?: BadgeVariant;
}

export function Badge({
  variant = 'neutral',
  className,
  children,
  ...props
}: PropsWithChildren<BadgeProps>) {
  return (
    <span
      className={['ced-badge', `ced-badge--${variant}`, className ?? ''].filter(Boolean).join(' ')}
      {...props}
    >
      {children}
    </span>
  );
}
