import { describe, expect, it } from 'vitest';
import { safeReturnPath } from './LoginPage';
import { validateRegistration } from './RegisterPage';

describe('validateRegistration', () => {
  it('accepts the API minimum: 8 characters with a letter and a digit', () => {
    expect(validateRegistration({ email: 'ayse@example.com', fullName: 'A', password: 'şifre123' })).toEqual({});
  });

  it('rejects a short password, a password without a digit and a missing name', () => {
    expect(validateRegistration({ email: 'ayse@example.com', fullName: ' ', password: 'abc1234' })).toEqual({
      fullName: 'Ad soyad girin (en fazla 100 karakter).',
      password: 'Parola en az 8 karakter olmalı ve harf ile rakam içermeli.',
    });
    expect(validateRegistration({ email: 'ayse@example.com', fullName: 'Ayşe', password: 'abcdefgh' }).password).toBeDefined();
  });

  it('rejects an invalid e-mail', () => {
    expect(validateRegistration({ email: 'ayse@', fullName: 'Ayşe', password: 'abc12345' }).email).toBe('Geçerli bir e-posta adresi girin.');
  });
});

describe('safeReturnPath', () => {
  it('keeps same-site paths and drops anything else', () => {
    expect(safeReturnPath('/etkinlik/1?x=2')).toBe('/etkinlik/1?x=2');
    expect(safeReturnPath('//evil.example')).toBe('/');
    expect(safeReturnPath('https://evil.example')).toBe('/');
    expect(safeReturnPath(null)).toBe('/');
  });
});
