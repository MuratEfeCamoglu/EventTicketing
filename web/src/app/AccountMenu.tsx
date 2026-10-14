import { ChevronDown, Copy, LogOut } from 'lucide-react';
import { useEffect, useRef, useState } from 'react';
import { useNavigate } from 'react-router';
import { useAuth } from '../auth/AuthProvider';
import type { Role } from '../auth/session';
import { Badge } from '../ui/Badge';
import { useToast } from '../ui/Toast';

export const roleLabel: Record<Role, string> = { Customer: 'Müşteri', Organizer: 'Organizatör', Admin: 'Admin' };

function AccountActions({ onDone }: { onDone?: () => void }) {
  const { session, logout } = useAuth();
  const toast = useToast();
  const navigate = useNavigate();
  if (!session) return null;

  async function copyId() {
    try {
      await navigator.clipboard.writeText(session!.userId);
      toast.success('Kullanıcı kimliğiniz kopyalandı.');
    } catch {
      toast.error(`Kopyalanamadı. Kimliğiniz: ${session!.userId}`);
    }
    onDone?.();
  }

  async function signOut() {
    onDone?.();
    await logout();
    navigate('/');
  }

  return (
    <div className="flex flex-col">
      <div className="px-3 py-2">
        <p className="truncate text-sm font-semibold text-ink">{session.email}</p>
        <div className="mt-1 flex items-center gap-2">
          <Badge tone="info">{roleLabel[session.role]}</Badge>
          <code className="truncate text-[11px] text-muted" title={session.userId}>
            {session.userId.slice(0, 8)}…
          </code>
        </div>
      </div>
      <button type="button" onClick={() => void copyId()} className="flex items-center gap-2 rounded-lg px-3 py-2 text-left text-sm text-ink-soft hover:bg-page">
        <Copy className="size-4" /> Kimliğimi kopyala
      </button>
      <button type="button" onClick={() => void signOut()} className="flex items-center gap-2 rounded-lg px-3 py-2 text-left text-sm text-danger hover:bg-danger-soft">
        <LogOut className="size-4" /> Çıkış yap
      </button>
    </div>
  );
}

/** Desktop dropdown with the signed-in user's e-mail, role, id (for the admin role screen) and sign-out. */
export function AccountMenu() {
  const { session } = useAuth();
  const [open, setOpen] = useState(false);
  const ref = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!open) return;
    const close = (e: MouseEvent) => ref.current && !ref.current.contains(e.target as Node) && setOpen(false);
    document.addEventListener('mousedown', close);
    return () => document.removeEventListener('mousedown', close);
  }, [open]);

  if (!session) return null;
  return (
    <div className="relative" ref={ref}>
      <button
        type="button"
        aria-haspopup="menu"
        aria-expanded={open}
        onClick={() => setOpen((v) => !v)}
        className="flex items-center gap-2 rounded-lg border border-line px-2.5 py-1.5 text-sm text-ink-soft hover:bg-page"
      >
        <span className="grid size-6 place-items-center rounded-full bg-brand text-xs font-bold uppercase text-white">{session.email[0]}</span>
        <span className="max-w-40 truncate">{session.email}</span>
        <ChevronDown className="size-4" />
      </button>
      {open && (
        <div role="menu" className="absolute right-0 mt-2 w-64 rounded-xl border border-line bg-surface p-1.5 shadow-pop">
          <AccountActions onDone={() => setOpen(false)} />
        </div>
      )}
    </div>
  );
}

/** Same actions inline, for the phone drawer. */
export function AccountPanel() {
  return <AccountActions />;
}
