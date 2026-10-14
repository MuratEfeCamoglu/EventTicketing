import { createBrowserRouter } from 'react-router';
import { RequireRole } from '../auth/RequireRole';
import { LoginPage } from '../pages/LoginPage';
import { RegisterPage } from '../pages/RegisterPage';
import { Layout } from './Layout';
import { NotFoundPage } from './NotFoundPage';

const placeholder = (text: string) => <p className="text-muted">{text}</p>;

export const router = createBrowserRouter([
  {
    element: <Layout />,
    children: [
      { index: true, element: placeholder('Etkinlikler yakında burada.') },
      { path: 'giris', element: <LoginPage /> },
      { path: 'kayit', element: <RegisterPage /> },
      { path: 'biletlerim', element: <RequireRole roles={['Customer']}>{placeholder('Biletler yakında.')}</RequireRole> },
      { path: 'rezervasyonlarim', element: <RequireRole roles={['Customer']}>{placeholder('Rezervasyonlar yakında.')}</RequireRole> },
      { path: 'yonetim', element: <RequireRole roles={['Organizer', 'Admin']}>{placeholder('Yönetim yakında.')}</RequireRole> },
      { path: '*', element: <NotFoundPage /> },
    ],
  },
]);
