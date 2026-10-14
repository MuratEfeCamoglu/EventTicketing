import { createBrowserRouter, Outlet } from 'react-router';
import { AppShell } from './AppShell';
import { NotFoundPage } from './NotFoundPage';

function Layout() {
  return (
    <AppShell>
      <Outlet />
    </AppShell>
  );
}

export const router = createBrowserRouter([
  {
    element: <Layout />,
    children: [
      { index: true, element: <p className="text-muted">Etkinlikler yakında burada.</p> },
      { path: '*', element: <NotFoundPage /> },
    ],
  },
]);
