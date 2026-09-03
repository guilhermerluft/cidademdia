import { useEffect, useState } from 'react';
import { Card, CardBody, SectionHeading } from '../../components/ui';
import { getInitials, getPrimaryRoleLabel } from '../../app/layout/AppNavigation';
import type { AuthenticatedUser } from '../auth/types';
import { getMyProfile, type PrivateUserProfile } from './profileService';

interface ProfilePageProps {
  user: AuthenticatedUser;
}

function displayValue(value?: string | null) {
  return value?.trim() ? value : 'Não informado';
}

export function ProfilePage({ user }: ProfilePageProps) {
  const [profile, setProfile] = useState<PrivateUserProfile | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let active = true;
    setLoading(true);
    setError(null);

    void getMyProfile()
      .then((result) => {
        if (active) setProfile(result);
      })
      .catch(() => {
        if (active) setError('Não foi possível carregar as informações do seu perfil agora.');
      })
      .finally(() => {
        if (active) setLoading(false);
      });

    return () => {
      active = false;
    };
  }, []);

  const displayName = profile?.displayName ?? user.displayName;
  const roles = profile?.roles ?? user.roles;

  return (
    <main className="profile-page" aria-labelledby="profile-page-title">
      <div className="profile-page__container">
        <SectionHeading
          title="Meu perfil"
          subtitle="Consulte as informações vinculadas à sua conta no CidadeEmDia."
        />

        <Card className="profile-page__card">
          <CardBody>
            <div className="profile-page__identity">
              <span className="profile-page__avatar" aria-hidden="true">{getInitials(displayName)}</span>
              <div>
                <span className="profile-page__eyebrow">Sua conta</span>
                <h1 id="profile-page-title">{displayName}</h1>
                <p>{profile?.email ?? user.email}</p>
              </div>
              <span className="profile-page__role">{getPrimaryRoleLabel(roles)}</span>
            </div>

            {loading ? (
              <p className="profile-page__status" aria-live="polite">Carregando informações...</p>
            ) : error ? (
              <p className="profile-page__status profile-page__status--error" role="alert">{error}</p>
            ) : (
              <dl className="profile-page__details">
                <div>
                  <dt>Nome</dt>
                  <dd>{displayName}</dd>
                </div>
                <div>
                  <dt>E-mail</dt>
                  <dd>{profile?.email ?? user.email}</dd>
                </div>
                <div>
                  <dt>Documento</dt>
                  <dd>{displayValue(profile?.document)}</dd>
                </div>
                <div>
                  <dt>Telefone</dt>
                  <dd>{displayValue(profile?.phone)}</dd>
                </div>
                <div>
                  <dt>Tipo de conta</dt>
                  <dd>{getPrimaryRoleLabel(roles)}</dd>
                </div>
              </dl>
            )}
          </CardBody>
        </Card>
      </div>
    </main>
  );
}
