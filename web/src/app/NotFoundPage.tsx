import { Compass } from 'lucide-react';
import { Link } from 'react-router';
import { buttonClasses } from '../ui/Button';
import { EmptyState } from '../ui/Card';

export function NotFoundPage({ title = 'Sayfa bulunamadı', description = 'Aradığınız sayfa taşınmış ya da hiç var olmamış olabilir.' }: { title?: string; description?: string }) {
  return (
    <EmptyState
      icon={<Compass className="size-10" />}
      title={title}
      description={description}
      action={
        <Link to="/" className={buttonClasses('primary')}>
          Etkinliklere dön
        </Link>
      }
    />
  );
}
