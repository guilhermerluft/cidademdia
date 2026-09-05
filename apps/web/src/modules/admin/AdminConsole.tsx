import { useState } from 'react';
import { Badge } from '../../components/ui';
import { PostManagementPanel } from '../posts/PostManagementPanel';
import { AdminHeroBannerPanel } from './AdminHeroBannerPanel';
import { AdminPanel } from './AdminPanel';
import { AdminPlansEditor } from './AdminPlansEditor';
import './styles/admin-management.css';

type AdminConsoleTab = 'overview' | 'plans' | 'banner' | 'media';

const TABS: Array<{ key: AdminConsoleTab; label: string; description: string }> = [
  { key: 'overview', label: 'Operação', description: 'Usuários, ocorrências, instituições, billing e auditoria' },
  { key: 'plans', label: 'Planos', description: 'Valores, adesão e limites do catálogo público' },
  { key: 'banner', label: 'Banner', description: 'Imagem principal da página inicial' },
  { key: 'media', label: 'Mídias CidadeEmDia', description: 'Publicações oficiais da plataforma' },
];

export function AdminConsole() {
  const [tab, setTab] = useState<AdminConsoleTab>('overview');

  return (
    <main className="admin-console">
      <header className="admin-console__hero">
        <div>
          <span className="admin-management-eyebrow">Área protegida</span>
          <h1>Painel de administração</h1>
          <p>Consulte a operação e gerencie os conteúdos e configurações comerciais do CidadeEmDia.</p>
        </div>
        <Badge variant="success">Administrador</Badge>
      </header>

      <nav className="admin-console__tabs" aria-label="Áreas do painel administrativo">
        {TABS.map((item) => (
          <button
            type="button"
            key={item.key}
            className={tab === item.key ? 'admin-console__tab admin-console__tab--active' : 'admin-console__tab'}
            aria-current={tab === item.key ? 'page' : undefined}
            onClick={() => setTab(item.key)}
          >
            <strong>{item.label}</strong>
            <span>{item.description}</span>
          </button>
        ))}
      </nav>

      <div className="admin-console__content">
        {tab === 'overview' ? <AdminPanel /> : null}
        {tab === 'plans' ? <AdminPlansEditor /> : null}
        {tab === 'banner' ? <AdminHeroBannerPanel /> : null}
        {tab === 'media' ? (
          <section className="admin-management-section">
            <div className="admin-console__section-intro">
              <span className="admin-management-eyebrow">Conteúdo oficial</span>
              <h2>Mídias CidadeEmDia</h2>
              <p>Crie, publique e arquive conteúdos institucionais exibidos pela própria plataforma.</p>
            </div>
            <PostManagementPanel />
          </section>
        ) : null}
      </div>
    </main>
  );
}
