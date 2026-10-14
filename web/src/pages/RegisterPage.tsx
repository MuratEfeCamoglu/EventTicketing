import { UserPlus } from 'lucide-react';
import { useState, type FormEvent } from 'react';
import { Link, Navigate, useNavigate, useSearchParams } from 'react-router';
import { fieldError, messageFor } from '../api/errors';
import { useAuth } from '../auth/AuthProvider';
import { Button } from '../ui/Button';
import { Card } from '../ui/Card';
import { Field, TextInput } from '../ui/Field';
import { safeReturnPath } from './LoginPage';

type Values = { email: string; fullName: string; password: string };
type Errors = Partial<Record<keyof Values, string>>;

/** Client-side rules mirroring the API validator: password ≥ 8 characters with a letter and a digit, name 1–100. */
export function validateRegistration(values: Values): Errors {
  const errors: Errors = {};
  if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(values.email.trim())) errors.email = 'Geçerli bir e-posta adresi girin.';
  const name = values.fullName.trim();
  if (name.length < 1 || name.length > 100) errors.fullName = 'Ad soyad girin (en fazla 100 karakter).';
  if (values.password.length < 8 || !/\p{L}/u.test(values.password) || !/\d/.test(values.password)) {
    errors.password = 'Parola en az 8 karakter olmalı ve harf ile rakam içermeli.';
  }
  return errors;
}

export function RegisterPage() {
  const { session, register } = useAuth();
  const navigate = useNavigate();
  const [params] = useSearchParams();
  const returnTo = safeReturnPath(params.get('donus'));
  const [values, setValues] = useState<Values>({ email: '', fullName: '', password: '' });
  const [errors, setErrors] = useState<Errors>({});
  const [serverError, setServerError] = useState<unknown>();
  const [busy, setBusy] = useState(false);

  if (session) return <Navigate to={returnTo} replace />;

  const set = (key: keyof Values) => (e: React.ChangeEvent<HTMLInputElement>) => setValues((v) => ({ ...v, [key]: e.target.value }));
  const errorFor = (key: keyof Values) => errors[key] ?? fieldError(serverError, key);

  async function submit(e: FormEvent) {
    e.preventDefault();
    const found = validateRegistration(values);
    setErrors(found);
    setServerError(undefined);
    if (Object.keys(found).length > 0) return;
    setBusy(true);
    try {
      await register({ email: values.email.trim(), fullName: values.fullName.trim(), password: values.password });
      navigate(returnTo, { replace: true });
    } catch (err) {
      setServerError(err);
    } finally {
      setBusy(false);
    }
  }

  return (
    <Card className="mx-auto max-w-md p-6 sm:p-8">
      <h1 className="text-2xl font-bold tracking-tight">Kayıt ol</h1>
      <p className="mt-1 text-sm text-muted">Yeni hesaplar müşteri olarak açılır.</p>
      <form className="mt-6 flex flex-col gap-4" onSubmit={submit} noValidate>
        <Field label="Ad soyad" error={errorFor('fullName')}>
          {(p) => <TextInput {...p} invalid={!!errorFor('fullName')} autoComplete="name" value={values.fullName} onChange={set('fullName')} />}
        </Field>
        <Field label="E-posta" error={errorFor('email')}>
          {(p) => <TextInput {...p} invalid={!!errorFor('email')} type="email" autoComplete="email" value={values.email} onChange={set('email')} />}
        </Field>
        <Field label="Parola" error={errorFor('password')} hint="En az 8 karakter; harf ve rakam içermeli.">
          {(p) => <TextInput {...p} invalid={!!errorFor('password')} type="password" autoComplete="new-password" value={values.password} onChange={set('password')} />}
        </Field>
        {serverError !== undefined && <p role="alert" className="rounded-lg bg-danger-soft px-3 py-2 text-sm text-danger">{messageFor(serverError)}</p>}
        <Button type="submit" loading={busy}>
          <UserPlus className="size-4" /> Hesap oluştur
        </Button>
      </form>
      <p className="mt-6 text-sm text-muted">
        Zaten hesabınız var mı?{' '}
        <Link className="font-semibold text-brand hover:underline" to="/giris">
          Giriş yapın
        </Link>
      </p>
    </Card>
  );
}
