import React from 'react';
import ReactDOM from 'react-dom/client';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { BrowserRouter, Route, Routes } from 'react-router-dom';
import { App } from './app/App';
import { AuthProvider } from './modules/auth/AuthProvider';
import { PlansRoute } from './modules/plans/PlansRoute';
import '@fortawesome/fontawesome-free/css/all.min.css';
import './styles/global.css';
import './styles/dashboard.css';
import './styles/subaccounts.css';
import './styles/occurrences.css';
import './styles/maps.css';
import './styles/assignments.css';
import './styles/posts.css';
import './styles/institutions.css';
import './styles/chat.css';
import './modules/home/home.css';
import './styles/responsive.css';
import './modules/home/home-assets.css';
import './modules/home/home-refinement.css';
import './modules/plans/plans.css';
import './modules/plans/plans-v2.css';

const queryClient = new QueryClient();

ReactDOM.createRoot(document.getElementById('root')!).render(
  <React.StrictMode>
    <QueryClientProvider client={queryClient}>
      <BrowserRouter>
        <AuthProvider>
          <Routes>
            <Route path="/planos" element={<PlansRoute />} />
            <Route path="*" element={<App />} />
          </Routes>
        </AuthProvider>
      </BrowserRouter>
    </QueryClientProvider>
  </React.StrictMode>,
);
