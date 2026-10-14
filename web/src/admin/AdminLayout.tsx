import { Building2, CalendarRange, ChevronDown, Globe, PlusCircle, UserCog } from 'lucide-react';
import { useState } from 'react';
import { NavLink, Outlet, useLocation } from 'react-router';
import { useAuth } from '../auth/AuthProvider';
import { cx } from '../ui/cx';

type Item = { to: string; label: string; icon: typeof Globe; end?: boolean };

/** Management area frame (design §7): side menu on desktop, a collapsible menu on phones. */
export function AdminLayout() {
  const { session } = useAuth();
  const [open, setOpen] = useState(false);
  const location = useLocation();
  const items: Item[] = [
    { to: '/yonetim/etkinlikler', label: 'Etkinliklerim', icon: CalendarRange, end: true },
    { to: '/yonetim/etkinlikler/yeni', label: 'Yeni etkinlik', icon: PlusCircle },
  ];
  if (session?.role === 'Admin') {
    items.push({ to: '/yonetim/mekanlar', label: 'Mekânlar ve salonlar', icon: Building2 }, { to: '/yonetim/roller', label: 'Kullanıcı rolleri', icon: UserCog });
  }
  items.push({ to: '/', label: 'Site görünümü', icon: Globe, end: true });
  const current = items.find((i) => location.pathname.startsWith(i.to) && i.to !== '/') ?? items[0]!;

  const menu = (
    <nav className="flex flex-col gap-1" aria-label="Yönetim menüsü" onClick={() => setOpen(false)}>
      {items.map(({ to, label, icon: Icon, end }) => (
        <NavLink
          key={to}
          to={to}
          end={end}
          className={({ isActive }) => cx('flex items-center gap-2.5 rounded-lg px-3 py-2 text-sm font-medium transition-colors', isActive && to !== '/' ? 'bg-brand-soft text-brand' : 'text-ink-soft hover:bg-page hover:text-ink')}
        >
          <Icon className="size-4" /> {label}
        </NavLink>
      ))}
    </nav>
  );

  return (
    <div className="grid gap-6 lg:grid-cols-[220px_minmax(0,1fr)]">
      <aside>
        <div className="hidden rounded-xl border border-line bg-surface p-2 lg:sticky lg:top-20 lg:block">{menu}</div>
        <div className="rounded-xl border border-line bg-surface lg:hidden">
          <button type="button" aria-expanded={open} onClick={() => setOpen((o) => !o)} className="flex w-full items-center justify-between px-4 py-3 text-sm font-semibold">
            <span className="flex items-center gap-2">
              <current.icon className="size-4 text-brand" /> {current.label}
            </span>
            <ChevronDown className={cx('size-4 transition-transform', open && 'rotate-180')} />
          </button>
          {open && <div className="border-t border-line p-2">{menu}</div>}
        </div>
      </aside>
      <div className="min-w-0">
        <Outlet />
      </div>
    </div>
  );
}
