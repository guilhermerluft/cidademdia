import React from 'react';
import ReactDOM from 'react-dom/client';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { BrowserRouter, Route, Routes } from 'react-router-dom';
import { App } from './app/App';
import { CommercialSignupModal } from './components/CommercialSignupModal';
import { ToastViewport } from './components/ToastViewport';
import { installNativeAlertToastBridge } from './components/toast';
import { AdminRoute } from './modules/admin/AdminRoute';
import { AuthProvider } from './modules/auth/AuthProvider';
import { HeroBannerBootstrap } from './modules/home/HeroBannerBootstrap';
import { RepresentativesRoute } from './modules/institutions/RepresentativesRoute';
import { PublicOccurrencesRoute } from './modules/occurrences/PublicOccurrencesRoute';
import { installOccurrenceFormValidationUi } from './modules/occurrences/occurrenceFormValidationUi';
import { UserPanelRoute } from './modules/panel/UserPanelRoute';
import { PlansRoute } from './modules/plans/PlansRoute';
import { ProfileRoute } from './modules/profile/ProfileRoute';
import '@fortawesome/fontawesome-free/css/all.min.css';
import './styles/global.css';
import './styles/app-header.css';
import './styles/dashboard.css';
import './styles/subaccounts.css';
import './styles/occurrences.css';
import './styles/maps.css';
import './styles/toast.css';
import './styles/commercial-signup-modal.css';
import './modules/occurrences/occurrence-required-labels.css';
import './styles/assignments.css';
import './styles/posts.css';
import './styles/institutions.css';
import './styles/chat.css';
import './modules/home/home.css';
import './styles/responsive.css';
import './modules/home/home-assets.css';
import './modules/home/home-refinement.css';
import './modules/home/home-session.css';
import './modules/home/how-it-works-modal.css';
import './modules/institutions/representatives.css';
import './modules/occurrences/occurrence-media.css';
import './modules/occurrences/public-occurrences.css';
import './modules/panel/panel.css';
import './modules/plans/plans.css';
import './modules/plans/plans-payment-inline.css';
import './modules/plans/plans-gold-commercial.css';
import './modules/profile/profile.css';

installNativeAlertToastBridge();
installOccurrenceFormValidationUi();

const queryClient = new QueryClient();

ReactDOM.createRoot(document.getElementById('root')!).render(
  <React.StrictMode>
    <QueryClientProvider client={queryClient}>
      <BrowserRouter>
        <AuthProvider>
          <HeroBannerBootstrap />
          <ToastViewport />
          <CommercialSignupModal />
          <Routes>
            <Route path="/admin" element={<AdminRoute />} />
            <Route path="/ocorrencias" element={<PublicOccurrencesRoute />} />
            <Route path="/representantes" element={<RepresentativesRoute />} />
            <Route path="/painel" element={<UserPanelRoute />} />
            <Route path="/perfil" element={<ProfileRoute />} />
            <Route path="/planos" element={<PlansRoute />} />
            <Route path="*" element={<App />} />
          </Routes>
        </AuthProvider>
      </BrowserRouter>
    </QueryClientProvider>
  </React.StrictMode>,
);
