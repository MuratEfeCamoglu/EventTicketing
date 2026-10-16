import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { useState } from 'react';
import { describe, expect, it } from 'vitest';
import { Modal } from './Modal';

function Harness() {
  const [open, setOpen] = useState(false);
  return (
    <>
      <button onClick={() => setOpen(true)}>Aç</button>
      <Modal open={open} title="Emin misiniz?" confirmLabel="Sil" onConfirm={() => setOpen(false)} onClose={() => setOpen(false)}>
        Geri alınamaz.
      </Modal>
    </>
  );
}

describe('Modal', () => {
  it('keeps Tab focus inside the dialog', async () => {
    const user = userEvent.setup();
    render(<Harness />);
    await user.click(screen.getByRole('button', { name: 'Aç' }));

    expect(screen.getByRole('button', { name: 'Sil' })).toHaveFocus();
    await user.tab();
    expect(screen.getByRole('button', { name: 'Vazgeç' })).toHaveFocus();
    await user.tab({ shift: true });
    expect(screen.getByRole('button', { name: 'Sil' })).toHaveFocus();
  });

  it('gives focus back to the button that opened it', async () => {
    const user = userEvent.setup();
    render(<Harness />);
    await user.click(screen.getByRole('button', { name: 'Aç' }));

    await user.keyboard('{Escape}');

    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Aç' })).toHaveFocus();
  });
});
