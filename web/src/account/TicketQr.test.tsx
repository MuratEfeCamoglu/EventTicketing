import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { TicketQr } from './TicketQr';

describe('TicketQr', () => {
  const fetchMock = vi.fn<typeof fetch>();
  beforeEach(() => vi.stubGlobal('fetch', fetchMock));
  afterEach(() => {
    vi.unstubAllGlobals();
    fetchMock.mockReset();
  });

  it('shows the QR as a data URL, so nothing has to be revoked later', async () => {
    fetchMock.mockResolvedValueOnce(new Response('png', { headers: { 'Content-Type': 'image/png' } }));
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });

    render(
      <QueryClientProvider client={client}>
        <TicketQr ticketId="t1" label="A-1" />
      </QueryClientProvider>,
    );

    const image = await screen.findByRole('img', { name: 'A-1 bileti QR kodu' });
    expect(image.getAttribute('src')).toMatch(/^data:image\/png;base64,/);
  });
});
