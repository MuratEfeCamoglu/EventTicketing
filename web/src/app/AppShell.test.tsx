import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router';
import { describe, expect, it } from 'vitest';
import { AppShell } from './AppShell';

function renderShell() {
  return render(
    <MemoryRouter>
      <AppShell>
        <p>içerik</p>
      </AppShell>
    </MemoryRouter>,
  );
}

describe('AppShell', () => {
  it('renders brand and primary navigation', () => {
    renderShell();

    expect(screen.getByRole('link', { name: /EventTicketing/ })).toHaveAttribute('href', '/');
    expect(screen.getAllByRole('link', { name: 'Etkinlikler' }).length).toBeGreaterThan(0);
    expect(screen.getByText('içerik')).toBeInTheDocument();
  });

  it('opens mobile menu when the menu button is pressed', async () => {
    renderShell();
    const button = screen.getByRole('button', { name: 'Menüyü aç' });
    expect(button).toHaveAttribute('aria-expanded', 'false');

    await userEvent.click(button);

    expect(screen.getByRole('button', { name: 'Menüyü kapat' })).toHaveAttribute('aria-expanded', 'true');
  });
});
