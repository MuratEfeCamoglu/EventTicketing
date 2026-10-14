import type { ButtonHTMLAttributes } from 'react';
import { cx } from './cx';
import { Spinner } from './Spinner';

type Variant = 'primary' | 'secondary' | 'danger' | 'ghost' | 'dangerGhost';
type Size = 'sm' | 'md';

const variants: Record<Variant, string> = {
  primary: 'bg-brand text-white hover:bg-brand-hover disabled:bg-brand/50',
  secondary: 'bg-surface text-ink border border-line-strong hover:bg-page disabled:text-faint',
  danger: 'bg-danger text-white hover:bg-danger/90 disabled:bg-danger/50',
  ghost: 'text-ink-soft hover:bg-page hover:text-ink disabled:text-faint',
  dangerGhost: 'text-danger hover:bg-danger-soft disabled:text-faint',
};

const sizes: Record<Size, string> = {
  sm: 'h-8 px-3 text-sm gap-1.5',
  md: 'h-10 px-4 text-sm gap-2',
};

export type ButtonProps = ButtonHTMLAttributes<HTMLButtonElement> & {
  variant?: Variant;
  size?: Size;
  loading?: boolean;
};

export function buttonClasses(variant: Variant = 'primary', size: Size = 'md', extra?: string) {
  return cx(
    'inline-flex items-center justify-center rounded-lg font-semibold transition-colors disabled:cursor-not-allowed',
    variants[variant],
    sizes[size],
    extra,
  );
}

export function Button({ variant = 'primary', size = 'md', loading = false, className, children, disabled, type = 'button', ...rest }: ButtonProps) {
  return (
    <button type={type} className={buttonClasses(variant, size, className)} disabled={disabled || loading} aria-busy={loading || undefined} {...rest}>
      {loading && <Spinner size="sm" />}
      {children}
    </button>
  );
}
