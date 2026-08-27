import type { ButtonHTMLAttributes } from 'react';

type ButtonVariant = 'primary' | 'secondary' | 'soft' | 'ghost' | 'danger';
type ButtonSize = 'sm' | 'md' | 'lg';

export interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: ButtonVariant;
  size?: ButtonSize;
  fullWidth?: boolean;
}

export function Button({
  className,
  variant = 'primary',
  size = 'md',
  fullWidth = false,
  type = 'button',
  ...props
}: ButtonProps) {
  const classes = [
    'ced-button',
    `ced-button--${variant}`,
    size !== 'md' ? `ced-button--${size}` : '',
    fullWidth ? 'ced-button--full' : '',
    className ?? '',
  ]
    .filter(Boolean)
    .join(' ');

  return <button className={classes} type={type} {...props} />;
}
