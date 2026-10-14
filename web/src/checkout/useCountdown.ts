import { useEffect, useState } from 'react';

function secondsUntil(expiresAt: string, now: number): number {
  return Math.max(0, Math.ceil((new Date(expiresAt).getTime() - now) / 1000));
}

/**
 * Time left on a hold, always derived from the server's expiresAt (design §6.4), so reloading the page or a slow
 * response never extends it. Ticks once a second; stops at zero.
 */
export function useCountdown(expiresAt: string) {
  const [now, setNow] = useState(() => Date.now());

  useEffect(() => {
    const timer = setInterval(() => setNow(Date.now()), 1000);
    return () => clearInterval(timer);
  }, []);

  const secondsLeft = secondsUntil(expiresAt, now);
  const minutes = String(Math.floor(secondsLeft / 60)).padStart(2, '0');
  const seconds = String(secondsLeft % 60).padStart(2, '0');
  return { secondsLeft, expired: secondsLeft === 0, label: `${minutes}:${seconds}` };
}
