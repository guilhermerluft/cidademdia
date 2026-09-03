import type { AuthenticatedUser } from '../auth/types';
import { ChatInbox } from '../chat/ChatInbox';
import { OccurrenceAssignmentPanel } from '../occurrenceAssignments/OccurrenceAssignmentPanel';
import { OccurrenceCenter } from '../occurrences/OccurrenceCenter';
import type { UserPanelAccess } from '../../app/layout/AppNavigation';

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

      {(access.canViewOccurrences || access.canViewChat) && (
        <nav className="user-panel__shortcuts" aria-label="Seções do painel">
          {access.canViewOccurrences && <a href="#painel-ocorrencias">{occurrenceLabel}</a>}
          {access.canViewChat && <a href="#painel-conversas">Conversas</a>}
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

        {access.canViewChat && (
          <section className="user-panel__module" id="painel-conversas" aria-label="Conversas">
            {access.mode === 'citizen' && <ChatInbox mode="citizen" />}
            {access.mode === 'master' && <ChatInbox mode="master" />}
            {access.mode === 'subaccount' && <ChatInbox mode="subaccount" />}
          </section>
        )}
      </div>
    </main>
  );
}
