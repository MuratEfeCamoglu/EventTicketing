import { cx } from './cx';

export function Spinner({ size = 'md', className }: { size?: 'sm' | 'md' | 'lg'; className?: string }) {
  const dimension = { sm: 'size-4 border-2', md: 'size-6 border-2', lg: 'size-9 border-[3px]' }[size];
  return <span role="status" aria-label="Yükleniyor" className={cx('inline-block animate-spin rounded-full border-current border-t-transparent', dimension, className)} />;
}

/** Centered spinner for a page or section that is loading. */
export function LoadingBlock({ label = 'Yükleniyor…' }: { label?: string }) {
  return (
    <div className="flex flex-col items-center justify-center gap-3 py-20 text-muted">
      <Spinner size="lg" className="text-brand" />
      <span className="text-sm">{label}</span>
    </div>
  );
}
