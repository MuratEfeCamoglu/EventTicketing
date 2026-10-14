import { LogIn, ShieldCheck, Store, UserRound } from 'lucide-react';
import { useState, type FormEvent } from 'react';
import { Link, Navigate, useNavigate, useSearchParams } from 'react-router';
import { messageFor } from '../api/errors';
import { useAuth } from '../auth/AuthProvider';
import { Button } from '../ui/Button';
import { Card } from '../ui/Card';
import { Field, TextInput } from '../ui/Field';

const DEMO_PASSWORD = 'Dev-only-Passw0rd';
const demoAccounts = [
  { email: 'customer@example.com', label: 'Müşteri', description: 'Bilet al, öde, QR gör', icon: UserRound },
  { email: 'organizer@example.com', label: 'Organizatör', description: 'Etkinlik oluştur ve yayımla', icon: Store },
  { email: 'admin@example.com', label: 'Admin', description: 'Mekân, salon ve roller', icon: ShieldCheck },
];

/** Only same-site paths are accepted as the return address. */
export function safeReturnPath(value: string | null): string {
  return value && value.startsWith('/') && !value.startsWith('//') ? value : '/';
}

export function LoginPage() {
  const { session, login } = useAuth();
  const navigate = useNavigate();
  const [params] = useSearchParams();
  const returnTo = safeReturnPath(params.get('donus'));
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string>();
  const [busy, setBusy] = useState<string>();

  if (session) return <Navigate to={returnTo} replace />;

  async function signIn(address: string, secret: string, marker: string) {
    setError(undefined);
    setBusy(marker);
    try {
      await login(address, secret);
      navigate(returnTo, { replace: true });
    } catch (e) {
      setError(messageFor(e));
    } finally {
      setBusy(undefined);
    }
  }

  function submit(e: FormEvent) {
    e.preventDefault();
    if (!email.trim() || !password) {
      setError('E-posta ve parolayı girin.');
      return;
    }
    void signIn(email.trim(), password, 'form');
  }

  return (
    <div className="mx-auto grid max-w-4xl gap-6 lg:grid-cols-[1fr_1fr]">
      <Card className="p-6 sm:p-8">
        <h1 className="text-2xl font-bold tracking-tight">Giriş yap</h1>
        <p className="mt-1 text-sm text-muted">Biletlerinize ve rezervasyonlarınıza ulaşın.</p>
        <form className="mt-6 flex flex-col gap-4" onSubmit={submit} noValidate>
          <Field label="E-posta">{(p) => <TextInput {...p} type="email" autoComplete="email" value={email} onChange={(e) => setEmail(e.target.value)} />}</Field>
          <Field label="Parola">{(p) => <TextInput {...p} type="password" autoComplete="current-password" value={password} onChange={(e) => setPassword(e.target.value)} />}</Field>
          {error && <p role="alert" className="rounded-lg bg-danger-soft px-3 py-2 text-sm text-danger">{error}</p>}
          <Button type="submit" loading={busy === 'form'} disabled={!!busy}>
            <LogIn className="size-4" /> Giriş yap
          </Button>
        </form>
        <p className="mt-6 text-sm text-muted">
          Hesabınız yok mu?{' '}
          <Link className="font-semibold text-brand hover:underline" to={`/kayit${returnTo !== '/' ? `?donus=${encodeURIComponent(returnTo)}` : ''}`}>
            Kayıt olun
          </Link>
        </p>
      </Card>

      <Card className="p-6 sm:p-8">
        <h2 className="text-lg font-semibold">Demo hesapları</h2>
        <p className="mt-1 text-sm text-muted">Tek tıkla hazır kullanıcılardan biriyle girin (parola: {DEMO_PASSWORD}).</p>
        <div className="mt-5 flex flex-col gap-3">
          {demoAccounts.map(({ email: address, label, description, icon: Icon }) => (
            <button
              key={address}
              type="button"
              disabled={!!busy}
              onClick={() => void signIn(address, DEMO_PASSWORD, address)}
              className="flex items-center gap-3 rounded-xl border border-line p-3 text-left transition-colors hover:border-brand hover:bg-brand-soft disabled:opacity-60"
            >
              <span className="grid size-10 shrink-0 place-items-center rounded-lg bg-brand-soft text-brand">
                <Icon className="size-5" />
              </span>
              <span className="min-w-0 flex-1">
                <span className="block font-semibold">{label}</span>
                <span className="block truncate text-xs text-muted">
                  {address} · {description}
                </span>
              </span>
              {busy === address && <span className="text-xs text-brand">Giriliyor…</span>}
            </button>
          ))}
        </div>
      </Card>
    </div>
  );
}
