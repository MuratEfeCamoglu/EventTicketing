import { Outlet } from 'react-router';
import { useAuth } from '../auth/AuthProvider';
import type { Role } from '../auth/session';
import { AccountMenu, AccountPanel } from './AccountMenu';
import { AppShell, type NavItem } from './AppShell';

/** Navigation by role (design §6.5, §7): customers get their tickets, organizers and admins the management area. */
export function navFor(role: Role | undefined): NavItem[] {
  const items: NavItem[] = [{ to: '/', label: 'Etkinlikler' }];
  if (role === 'Customer') items.push({ to: '/biletlerim', label: 'Biletlerim' }, { to: '/rezervasyonlarim', label: 'Rezervasyonlarım' });
  if (role === 'Organizer' || role === 'Admin') items.push({ to: '/yonetim', label: 'Yönetim' });
  return items;
}

export function Layout() {
  const { session } = useAuth();
  return (
    <AppShell nav={navFor(session?.role)} account={session ? <AccountMenu /> : undefined} mobileAccount={session ? <AccountPanel /> : undefined}>
      <Outlet />
    </AppShell>
  );
}
