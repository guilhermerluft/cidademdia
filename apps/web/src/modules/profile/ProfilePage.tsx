import { useEffect, useRef, useState } from 'react';
import { isAxiosError } from 'axios';
import { Button, Card, CardBody, SectionHeading } from '../../components/ui';
import { getInitials, getPrimaryRoleLabel } from '../../app/layout/AppNavigation';
import type { AuthenticatedUser } from '../auth/types';
import {
  getMyProfile,
  getMyProfileAvatar,
  prepareProfileAvatar,
  removeMyProfileAvatar,
  updateMyProfile,
  type PrivateUserProfile,
} from './profileService';

interface ProfilePageProps {
  user: AuthenticatedUser;
}

interface FeedbackState {
  type: 'success' | 'error';
  text: string;
}

function profileErrorMessage(error: unknown, fallback: string) {
  if (!isAxiosError(error)) return fallback;

  const code = error.response?.data?.error;
  if (code === 'invalid_document') return 'Informe um CPF ou CNPJ válido.';
  if (code === 'invalid_phone') return 'Informe um telefone válido com DDD.';
  if (code === 'avatar_type_not_allowed' || code === 'avatar_extension_not_allowed') {
    return 'A foto deve estar em JPEG, PNG ou WebP.';
  }
  if (code === 'avatar_size_not_allowed') return 'A foto excede o tamanho permitido.';
  if (code === 'avatar_signature_invalid') return 'O conteúdo da foto não corresponde ao formato informado.';
  if (code === 'storage_not_configured' || code === 'storage_verification_failed') {
    return 'O armazenamento de imagens está temporariamente indisponível.';
  }

  return fallback;
}

export function ProfilePage({ user }: ProfilePageProps) {
  const [profile, setProfile] = useState<PrivateUserProfile | null>(null);
  const [document, setDocument] = useState('');
  const [phone, setPhone] = useState('');
  const [avatarUrl, setAvatarUrl] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [avatarBusy, setAvatarBusy] = useState(false);
  const [feedback, setFeedback] = useState<FeedbackState | null>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    let active = true;
    setLoading(true);
    setFeedback(null);

    void getMyProfile()
      .then(async (result) => {
        if (!active) return;
        setProfile(result);
        setDocument(result.document ?? '');
        setPhone(result.phone ?? '');

        if (result.avatarMediaId) {
          try {
            const avatar = await getMyProfileAvatar();
            if (active) setAvatarUrl(avatar.readUrl);
          } catch {
            if (active) setAvatarUrl(null);
          }
        }
      })
      .catch(() => {
        if (active) {
          setFeedback({
            type: 'error',
            text: 'Não foi possível carregar as informações do seu perfil agora.',
          });
        }
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

  async function handleSave(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!profile || saving) return;

    setSaving(true);
    setFeedback(null);
    try {
      const updated = await updateMyProfile({
        displayName: profile.displayName,
        document: document.trim() || null,
        phone: phone.trim() || null,
      });
      setProfile(updated);
      setDocument(updated.document ?? '');
      setPhone(updated.phone ?? '');
      setFeedback({ type: 'success', text: 'Perfil atualizado com sucesso.' });
    } catch (error) {
      setFeedback({
        type: 'error',
        text: profileErrorMessage(error, 'Não foi possível salvar as alterações do perfil.'),
      });
    } finally {
      setSaving(false);
    }
  }

  async function handleAvatarChange(event: React.ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0];
    event.target.value = '';
    if (!file || avatarBusy) return;

    setAvatarBusy(true);
    setFeedback(null);
    try {
      const confirmation = await prepareProfileAvatar(file);
      setProfile(confirmation.profile);
      setAvatarUrl(confirmation.avatar.readUrl);
      setFeedback({ type: 'success', text: 'Foto de perfil atualizada com sucesso.' });
    } catch (error) {
      setFeedback({
        type: 'error',
        text: profileErrorMessage(error, 'Não foi possível atualizar a foto de perfil.'),
      });
    } finally {
      setAvatarBusy(false);
    }
  }

  async function handleRemoveAvatar() {
    if (!profile?.avatarMediaId || avatarBusy) return;

    setAvatarBusy(true);
    setFeedback(null);
    try {
      const updated = await removeMyProfileAvatar();
      setProfile(updated);
      setAvatarUrl(null);
      setFeedback({ type: 'success', text: 'Foto de perfil removida.' });
    } catch (error) {
      setFeedback({
        type: 'error',
        text: profileErrorMessage(error, 'Não foi possível remover a foto de perfil.'),
      });
    } finally {
      setAvatarBusy(false);
    }
  }

  return (
    <main className="profile-page" aria-labelledby="profile-page-title">
      <div className="profile-page__container">
        <SectionHeading
          title="Meu perfil"
          subtitle="Atualize seus dados de contato e a foto vinculada à sua conta no CidadeEmDia."
        />

        <Card className="profile-page__card">
          <CardBody>
            <div className="profile-page__identity">
              <div className="profile-page__avatar-shell">
                {avatarUrl ? (
                  <img
                    className="profile-page__avatar profile-page__avatar--image"
                    src={avatarUrl}
                    alt={`Foto de perfil de ${displayName}`}
                  />
                ) : (
                  <span className="profile-page__avatar" aria-hidden="true">{getInitials(displayName)}</span>
                )}

                <div className="profile-page__avatar-actions">
                  <label className="profile-page__avatar-upload">
                    <input
                      ref={fileInputRef}
                      type="file"
                      accept="image/jpeg,image/png,image/webp"
                      disabled={avatarBusy || loading}
                      onChange={handleAvatarChange}
                    />
                    <i className="fa-solid fa-camera" aria-hidden="true" />
                    <span>{profile?.avatarMediaId ? 'Alterar foto' : 'Adicionar foto'}</span>
                  </label>

                  {profile?.avatarMediaId ? (
                    <button
                      className="profile-page__avatar-remove"
                      type="button"
                      disabled={avatarBusy}
                      onClick={handleRemoveAvatar}
                    >
                      <i className="fa-solid fa-trash-can" aria-hidden="true" />
                      <span>Remover</span>
                    </button>
                  ) : null}
                </div>
              </div>

              <div>
                <span className="profile-page__eyebrow">Sua conta</span>
                <h1 id="profile-page-title">{displayName}</h1>
                <p>{profile?.email ?? user.email}</p>
              </div>
              <span className="profile-page__role">{getPrimaryRoleLabel(roles)}</span>
            </div>

            {loading ? (
              <p className="profile-page__status" aria-live="polite">Carregando informações...</p>
            ) : profile ? (
              <>
                <form className="profile-page__form" onSubmit={handleSave}>
                  <div className="profile-page__fields">
                    <label className="profile-page__field">
                      <span>Documento</span>
                      <input
                        type="text"
                        value={document}
                        inputMode="numeric"
                        autoComplete="off"
                        maxLength={18}
                        placeholder="CPF ou CNPJ"
                        onChange={(event) => setDocument(event.target.value)}
                      />
                      <small>Informe CPF ou CNPJ. O documento é validado antes de salvar.</small>
                    </label>

                    <label className="profile-page__field">
                      <span>Telefone</span>
                      <input
                        type="tel"
                        value={phone}
                        autoComplete="tel"
                        maxLength={20}
                        placeholder="(00) 00000-0000"
                        onChange={(event) => setPhone(event.target.value)}
                      />
                      <small>Informe o telefone com DDD.</small>
                    </label>
                  </div>

                  <div className="profile-page__form-actions">
                    <Button type="submit" disabled={saving || avatarBusy}>
                      {saving ? 'Salvando...' : 'Salvar alterações'}
                    </Button>
                  </div>
                </form>

                <dl className="profile-page__details">
                  <div>
                    <dt>Nome</dt>
                    <dd>{displayName}</dd>
                  </div>
                  <div>
                    <dt>E-mail</dt>
                    <dd>{profile.email}</dd>
                  </div>
                  <div>
                    <dt>Tipo de conta</dt>
                    <dd>{getPrimaryRoleLabel(roles)}</dd>
                  </div>
                </dl>
              </>
            ) : null}

            {avatarBusy ? (
              <p className="profile-page__status" aria-live="polite">Processando foto de perfil...</p>
            ) : null}

            {feedback ? (
              <p
                className={feedback.type === 'error'
                  ? 'profile-page__status profile-page__status--error'
                  : 'profile-page__status profile-page__status--success'}
                role={feedback.type === 'error' ? 'alert' : 'status'}
              >
                {feedback.text}
              </p>
            ) : null}
          </CardBody>
        </Card>
      </div>
    </main>
  );
}
