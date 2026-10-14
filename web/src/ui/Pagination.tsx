import { ChevronLeft, ChevronRight } from 'lucide-react';
import { Button } from './Button';

export function Pagination({ page, totalPages, onPage }: { page: number; totalPages: number; onPage: (page: number) => void }) {
  if (totalPages <= 1) return null;
  return (
    <nav className="mt-8 flex items-center justify-center gap-3" aria-label="Sayfalar">
      <Button variant="secondary" size="sm" disabled={page <= 1} onClick={() => onPage(page - 1)} aria-label="Önceki sayfa">
        <ChevronLeft className="size-4" />
      </Button>
      <span className="text-sm text-muted">
        Sayfa <strong className="text-ink">{page}</strong> / {totalPages}
      </span>
      <Button variant="secondary" size="sm" disabled={page >= totalPages} onClick={() => onPage(page + 1)} aria-label="Sonraki sayfa">
        <ChevronRight className="size-4" />
      </Button>
    </nav>
  );
}
