import { ShieldAlert } from 'lucide-react';
import type { ReactNode } from 'react';
import { Navigate, useLocation } from 'react-router';
import { EmptyState } from '../ui/Card';
import { useAuth } from './AuthProvider';
import type { Role } from './session';

/** Login path that returns to the current page afterwards. */
export function loginPath(returnTo: string): string {
  return `/giris?donus=${encodeURIComponent(returnTo)}`;
}

/** Renders children only for the given roles; anonymous users go to login and come back here. */
export function RequireRole({ roles, children }: { roles: Role[]; children: ReactNode }) {
  const { session } = useAuth();
  const location = useLocation();

  if (!session) return <Navigate to={loginPath(location.pathname + location.search)} replace />;
  if (!roles.includes(session.role)) {
    return <EmptyState icon={<ShieldAlert className="size-10" />} title="Bu sayfa için yetkiniz yok" description="Bu sayfayı görmek için uygun rolde bir hesapla giriş yapın." />;
  }
  return <>{children}</>;
}
