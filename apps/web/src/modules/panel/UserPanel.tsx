import { MasterTeamPanel } from '../../app/dashboard/MasterTeamPanel';
import type { UserPanelAccess } from '../../app/layout/AppNavigation';
import type { AuthenticatedUser } from '../auth/types';
import { OccurrenceAssignmentPanel } from '../occurrenceAssignments/OccurrenceAssignmentPanel';
import { OccurrenceCenter } from '../occurrences/OccurrenceCenter';
import { PostManagementPanel } from '../posts/PostManagementPanel';

interface UserPanelProps {
  user: AuthenticatedUser;
  access: UserPanelAccess;
}

export function UserPanel({ user, access }: UserPanelProps) {
  const occurrenceLabel = access.mode === 'citizen' ? 'Minhas ocorrências' : 'Ocorrências recebidas';

  return (
    <main className="user-panel" aria-labelledby="user-panel-title">
      <section className="user-panel__intro">
        <div>
          <span className="user-panel__eyebrow">Área do usuário</span>
          <h1 id="user-panel-title">Painel</h1>
          <p>Acompanhe o que exige sua atenção sem misturar a operação com a página inicial.</p>
        </div>

        <div className="user-panel__identity" aria-label={`Painel de ${user.displayName}`}>
          <span>{user.displayName}</span>
          <small>{user.email}</small>
        </div>
      </section>

      {(access.canViewOccurrences || access.mode === 'master') && (
        <nav className="user-panel__shortcuts" aria-label="Seções do painel">
          {access.canViewOccurrences && <a href="#painel-ocorrencias">{occurrenceLabel}</a>}
          {access.mode === 'master' && <a href="#painel-publicacoes">Publicações</a>}
          {access.mode === 'master' && <a href="#painel-equipe">Equipe e permissões</a>}
        </nav>
      )}

      <div className="user-panel__modules">
        {access.canViewOccurrences && (
          <section className="user-panel__module" id="painel-ocorrencias" aria-label={occurrenceLabel}>
            {access.mode === 'citizen' && <OccurrenceCenter />}
            {access.mode === 'master' && <OccurrenceAssignmentPanel mode="master" />}
            {access.mode === 'subaccount' && <OccurrenceAssignmentPanel mode="subaccount" />}
          </section>
        )}

        {access.mode === 'master' && (
          <section className="user-panel__module" id="painel-publicacoes" aria-label="Publicações">
            <PostManagementPanel />
          </section>
        )}

        {access.mode === 'master' && (
          <section className="user-panel__module" id="painel-equipe" aria-label="Equipe e permissões">
            <MasterTeamPanel />
          </section>
        )}
      </div>
    </main>
  );
}
