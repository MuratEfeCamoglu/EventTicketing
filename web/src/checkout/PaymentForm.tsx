import { CreditCard, Lock } from 'lucide-react';
import { useState } from 'react';
import { formatPrice } from '../lib/format';
import { Button } from '../ui/Button';
import { cx } from '../ui/cx';
import type { PaymentToken } from './usePayment';

const cards: { token: PaymentToken; title: string; number: string; note: string }[] = [
  { token: 'tok_success', title: 'Başarılı kart', number: '4242 4242 4242 4242', note: 'Ödeme onaylanır, biletler oluşur' },
  { token: 'tok_declined', title: 'Reddedilen kart', number: '4000 0000 0000 0002', note: 'Banka reddeder, koltuklar serbest kalır' },
];

/**
 * Mock card form (design §6.4): the API only knows the test tokens, so the "card" is one of two ready-made choices
 * and no card data is typed or sent.
 */
export function PaymentForm({ total, disabled, pending, onPay }: { total: number; disabled: boolean; pending: boolean; onPay: (token: PaymentToken) => void }) {
  const [token, setToken] = useState<PaymentToken>('tok_success');
  const card = cards.find((c) => c.token === token)!;

  return (
    <div className="flex flex-col gap-4">
      <fieldset className="grid gap-2 sm:grid-cols-2">
        <legend className="mb-2 text-sm font-medium text-ink-soft">Test kartı seçin</legend>
        {cards.map((c) => (
          <label
            key={c.token}
            className={cx(
              'flex cursor-pointer items-start gap-3 rounded-xl border p-3 transition-colors',
              token === c.token ? 'border-brand bg-brand-soft' : 'border-line hover:border-line-strong',
            )}
          >
            <input type="radio" name="card" value={c.token} checked={token === c.token} onChange={() => setToken(c.token)} className="mt-1 accent-brand" />
            <span>
              <span className="block text-sm font-semibold">{c.title}</span>
              <span className="block text-xs text-muted">{c.note}</span>
            </span>
          </label>
        ))}
      </fieldset>

      <div className="relative overflow-hidden rounded-2xl bg-gradient-to-br from-[#1e3a8a] to-brand p-5 text-white shadow-pop">
        <CreditCard className="absolute right-4 top-4 size-6 opacity-70" />
        <p className="text-xs uppercase tracking-widest opacity-70">Test kartı</p>
        <p className="mt-6 font-mono text-lg tracking-[0.18em]">{card.number}</p>
        <div className="mt-4 flex justify-between text-xs opacity-80">
          <span>DEMO KULLANICI</span>
          <span>12/30</span>
        </div>
      </div>

      <Button onClick={() => onPay(token)} loading={pending} disabled={disabled} className="h-12 text-base">
        <Lock className="size-4" /> {formatPrice(total)} öde
      </Button>
      <p className="text-center text-xs text-muted">Bu bir demo ödemesidir; gerçek kart bilgisi alınmaz ve para çekilmez.</p>
    </div>
  );
}
