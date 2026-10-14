import { createBrowserRouter } from 'react-router';
import { ReservationsPage } from '../account/ReservationsPage';
import { TicketsPage } from '../account/TicketsPage';
import { RequireRole } from '../auth/RequireRole';
import { ReservationPage } from '../checkout/ReservationPage';
import { EventPage } from '../events/EventPage';
import { EventsPage } from '../events/EventsPage';
import { LoginPage } from '../pages/LoginPage';
import { RegisterPage } from '../pages/RegisterPage';
import { Layout } from './Layout';
import { NotFoundPage } from './NotFoundPage';

const placeholder = (text: string) => <p className="text-muted">{text}</p>;

export const router = createBrowserRouter([
  {
    element: <Layout />,
    children: [
      { index: true, element: <EventsPage /> },
      { path: 'etkinlik/:id', element: <EventPage /> },
      { path: 'giris', element: <LoginPage /> },
      { path: 'kayit', element: <RegisterPage /> },
      { path: 'rezervasyon/:id', element: <RequireRole roles={['Customer', 'Admin']}><ReservationPage /></RequireRole> },
      { path: 'biletlerim', element: <RequireRole roles={['Customer']}><TicketsPage /></RequireRole> },
      { path: 'rezervasyonlarim', element: <RequireRole roles={['Customer']}><ReservationsPage /></RequireRole> },
      { path: 'yonetim', element: <RequireRole roles={['Organizer', 'Admin']}>{placeholder('Yönetim yakında.')}</RequireRole> },
      { path: '*', element: <NotFoundPage /> },
    ],
  },
]);
