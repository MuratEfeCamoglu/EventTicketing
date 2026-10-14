import { createBrowserRouter, Navigate } from 'react-router';
import { ReservationsPage } from '../account/ReservationsPage';
import { TicketsPage } from '../account/TicketsPage';
import { AdminLayout } from '../admin/AdminLayout';
import { RequireRole } from '../auth/RequireRole';
import { ReservationPage } from '../checkout/ReservationPage';
import { EventPage } from '../events/EventPage';
import { EventsPage } from '../events/EventsPage';
import { EventFormPage } from '../organizer/EventFormPage';
import { MyEventsPage } from '../organizer/MyEventsPage';
import { LoginPage } from '../pages/LoginPage';
import { RegisterPage } from '../pages/RegisterPage';
import { Layout } from './Layout';
import { NotFoundPage } from './NotFoundPage';

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
      {
        path: 'yonetim',
        element: (
          <RequireRole roles={['Organizer', 'Admin']}>
            <AdminLayout />
          </RequireRole>
        ),
        children: [
          { index: true, element: <Navigate to="etkinlikler" replace /> },
          { path: 'etkinlikler', element: <MyEventsPage /> },
          { path: 'etkinlikler/yeni', element: <EventFormPage /> },
          { path: 'etkinlikler/:id/duzenle', element: <EventFormPage /> },
        ],
      },
      { path: '*', element: <NotFoundPage /> },
    ],
  },
]);
