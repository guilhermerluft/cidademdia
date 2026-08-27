import type { HTMLAttributes, PropsWithChildren } from 'react';

export interface CardProps extends HTMLAttributes<HTMLElement> {
  elevated?: boolean;
  interactive?: boolean;
}

export function Card({
  className,
  elevated = false,
  interactive = false,
  children,
  ...props
}: PropsWithChildren<CardProps>) {
  const classes = [
    'ced-card',
    elevated ? 'ced-card--elevated' : '',
    interactive ? 'ced-card--interactive' : '',
    className ?? '',
  ]
    .filter(Boolean)
    .join(' ');

  return (
    <section className={classes} {...props}>
      {children}
    </section>
  );
}

export function CardHeader({ className, ...props }: PropsWithChildren<HTMLAttributes<HTMLDivElement>>) {
  return <div className={['ced-card__header', className ?? ''].filter(Boolean).join(' ')} {...props} />;
}

export function CardBody({ className, ...props }: PropsWithChildren<HTMLAttributes<HTMLDivElement>>) {
  return <div className={['ced-card__body', className ?? ''].filter(Boolean).join(' ')} {...props} />;
}

export function CardFooter({ className, ...props }: PropsWithChildren<HTMLAttributes<HTMLDivElement>>) {
  return <div className={['ced-card__footer', className ?? ''].filter(Boolean).join(' ')} {...props} />;
}

export function CardTitle({ className, ...props }: PropsWithChildren<HTMLAttributes<HTMLHeadingElement>>) {
  return <h2 className={['ced-card__title', className ?? ''].filter(Boolean).join(' ')} {...props} />;
}
