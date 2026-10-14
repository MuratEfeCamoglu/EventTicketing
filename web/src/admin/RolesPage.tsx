import { UserCog } from 'lucide-react';
import { useState, type FormEvent } from 'react';
import { api } from '../api/client';
import { fieldError, messageFor } from '../api/errors';
import { Button } from '../ui/Button';
import { Card, PageHeader } from '../ui/Card';
import { Field, Select, TextInput } from '../ui/Field';
import { useToast } from '../ui/Toast';

const GUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

/**
 * PUT /admin/users/{id}/role (design §7). The API has no user search, so the id is pasted; every user can copy theirs
 * from the account menu ("Kimliğimi kopyala").
 */
export function RolesPage() {
  const [userId, setUserId] = useState('');
  const [role, setRole] = useState<'Organizer' | 'Customer'>('Organizer');
  const [error, setError] = useState<string>();
  const [busy, setBusy] = useState(false);
  const toast = useToast();

  async function submit(e: FormEvent) {
    e.preventDefault();
    const id = userId.trim();
    if (!GUID.test(id)) {
      setError('Geçerli bir kullanıcı kimliği (GUID) girin.');
      return;
    }
    setError(undefined);
    setBusy(true);
    try {
      await api(`/admin/users/${id}/role`, { method: 'PUT', json: { role } });
      toast.success(`Kullanıcının rolü "${role === 'Organizer' ? 'Organizatör' : 'Müşteri'}" yapıldı. Yeni rol bir sonraki girişte geçerli olur.`);
      setUserId('');
    } catch (err) {
      setError(fieldError(err, 'role') ?? messageFor(err));
    } finally {
      setBusy(false);
    }
  }

  return (
    <div>
      <PageHeader title="Kullanıcı rolleri" subtitle="Bir kullanıcıyı organizatör yapın ya da müşteriye geri döndürün." />
      <Card className="max-w-xl p-5 sm:p-6">
        <form className="flex flex-col gap-4" onSubmit={submit} noValidate>
          <Field label="Kullanıcı kimliği" error={error} hint="Kullanıcı, kendi kimliğini sağ üstteki hesap menüsünden “Kimliğimi kopyala” ile alır.">
            {(p) => <TextInput {...p} invalid={!!error} className="font-mono" placeholder="00000000-0000-0000-0000-000000000000" value={userId} onChange={(e) => setUserId(e.target.value)} />}
          </Field>
          <Field label="Yeni rol">
            {(p) => (
              <Select {...p} value={role} onChange={(e) => setRole(e.target.value as 'Organizer' | 'Customer')}>
                <option value="Organizer">Organizatör</option>
                <option value="Customer">Müşteri</option>
              </Select>
            )}
          </Field>
          <Button type="submit" loading={busy} className="self-start">
            <UserCog className="size-4" /> Rolü değiştir
          </Button>
        </form>
      </Card>
    </div>
  );
}
