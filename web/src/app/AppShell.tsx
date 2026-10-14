import { Menu, Ticket, X } from 'lucide-react';
import { useState, type ReactNode } from 'react';
import { Link, NavLink } from 'react-router';
import { buttonClasses } from '../ui/Button';
import { cx } from '../ui/cx';

export type NavItem = { to: string; label: string };

const publicNav: NavItem[] = [{ to: '/', label: 'Etkinlikler' }];

function Brand() {
  return (
    <Link to="/" className="flex items-center gap-2 font-extrabold tracking-tight text-ink">
      <span className="grid size-8 place-items-center rounded-lg bg-brand text-white">
        <Ticket className="size-4" aria-hidden />
      </span>
      <span>
        Event<span className="text-brand">Ticketing</span>
      </span>
    </Link>
  );
}

const linkClasses = ({ isActive }: { isActive: boolean }) =>
  cx('rounded-lg px-3 py-2 text-sm font-medium transition-colors', isActive ? 'bg-brand-soft text-brand' : 'text-ink-soft hover:bg-page hover:text-ink');

/** Page frame: sticky header with the primary navigation (a drawer on phones) and the content area. */
export function AppShell({ children, nav = publicNav, account, mobileAccount }: { children: ReactNode; nav?: NavItem[]; account?: ReactNode; mobileAccount?: ReactNode }) {
  const [menuOpen, setMenuOpen] = useState(false);
  const close = () => setMenuOpen(false);

  return (
    <div className="flex min-h-dvh flex-col">
      <header className="sticky top-0 z-40 border-b border-line bg-surface/95 backdrop-blur">
        <div className="mx-auto flex h-16 max-w-6xl items-center gap-4 px-4">
          <Brand />
          <nav className="ml-4 hidden items-center gap-1 md:flex" aria-label="Ana menü">
            {nav.map((item) => (
              <NavLink key={item.to} to={item.to} end={item.to === '/'} className={linkClasses}>
                {item.label}
              </NavLink>
            ))}
          </nav>
          <div className="ml-auto hidden items-center gap-2 md:flex">
            {account ?? (
              <Link to="/giris" className={buttonClasses('primary', 'sm')}>
                Giriş yap
              </Link>
            )}
          </div>
          <button
            type="button"
            className="ml-auto grid size-10 place-items-center rounded-lg text-ink-soft hover:bg-page md:hidden"
            aria-label={menuOpen ? 'Menüyü kapat' : 'Menüyü aç'}
            aria-expanded={menuOpen}
            aria-controls="mobile-menu"
            onClick={() => setMenuOpen((open) => !open)}
          >
            {menuOpen ? <X className="size-5" /> : <Menu className="size-5" />}
          </button>
        </div>
        {menuOpen && (
          <div id="mobile-menu" className="border-t border-line bg-surface px-4 pb-4 pt-2 md:hidden">
            <nav className="flex flex-col gap-1" aria-label="Mobil menü" onClick={close}>
              {nav.map((item) => (
                <NavLink key={item.to} to={item.to} end={item.to === '/'} className={linkClasses}>
                  {item.label}
                </NavLink>
              ))}
            </nav>
            <div className="mt-3 border-t border-line pt-3" onClick={close}>
              {mobileAccount ?? (
                <Link to="/giris" className={buttonClasses('primary', 'md', 'w-full')}>
                  Giriş yap
                </Link>
              )}
            </div>
          </div>
        )}
      </header>
      <main className="mx-auto w-full max-w-6xl flex-1 px-4 py-6 sm:py-8">{children}</main>
      <footer className="border-t border-line bg-surface">
        <div className="mx-auto max-w-6xl px-4 py-5 text-xs text-muted">EventTicketing · deneme ön yüzü · ödemeler sahte, e-postalar Mailpit'e gider</div>
      </footer>
    </div>
  );
}
