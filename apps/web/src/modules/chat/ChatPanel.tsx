import { useMemo, useState } from 'react';
import type { FormEvent } from 'react';
import { useAuth } from '../auth/AuthProvider';
import { useChatConversation } from './useChatConversation';

export interface ChatPanelProps {
  targetId: string;
  title?: string;
}

const connectionLabels = {
  idle: 'Aguardando',
  loading: 'Carregando',
  connecting: 'Conectando',
  connected: 'Online',
  reconnecting: 'Reconectando',
  disconnected: 'Desconectado',
  closed: 'Encerrado',
  error: 'Indisponível',
} as const;

export function ChatPanel({ targetId, title = 'Conversa da ocorrência' }: ChatPanelProps) {
  const { user } = useAuth();
  const { messages, state, error, sendText } = useChatConversation(targetId);
  const [draft, setDraft] = useState('');
  const [sending, setSending] = useState(false);
  const [sendError, setSendError] = useState<string | null>(null);

  const canSend = state === 'connected' && !sending && draft.trim().length > 0;
  const statusLabel = useMemo(() => connectionLabels[state], [state]);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!canSend) return;

    const content = draft.trim();
    setSending(true);
    setSendError(null);

    try {
      await sendText(content);
      setDraft('');
    } catch (submitError) {
      setSendError(
        submitError instanceof Error
          ? submitError.message
          : 'Não foi possível enviar a mensagem.',
      );
    } finally {
      setSending(false);
    }
  }

  return (
    <section className="chat-panel" aria-label={title}>
      <header className="chat-panel__header">
        <div>
          <h2>{title}</h2>
          <p aria-live="polite">Status: {statusLabel}</p>
        </div>
      </header>

      {error ? (
        <p className="chat-panel__notice" role="status">
          {error}
        </p>
      ) : null}

      <div className="chat-panel__messages" aria-live="polite">
        {messages.length === 0 ? (
          <p className="chat-panel__empty">Nenhuma mensagem enviada ainda.</p>
        ) : (
          messages.map((message) => {
            const ownMessage = message.senderUserId === user?.id;
            return (
              <article
                className={`chat-panel__message${ownMessage ? ' chat-panel__message--own' : ''}`}
                key={message.id}
              >
                <p>{message.content}</p>
                <time dateTime={message.sentAt}>
                  {new Date(message.sentAt).toLocaleString('pt-BR')}
                </time>
              </article>
            );
          })
        )}
      </div>

      {state === 'closed' ? (
        <p className="chat-panel__notice">Esta conversa foi encerrada.</p>
      ) : (
        <form className="chat-panel__composer" onSubmit={handleSubmit}>
          <label htmlFor={`chat-message-${targetId}`}>Mensagem</label>
          <textarea
            id={`chat-message-${targetId}`}
            maxLength={4000}
            onChange={(event) => setDraft(event.target.value)}
            placeholder="Digite uma mensagem"
            rows={3}
            value={draft}
          />
          {sendError ? <p role="alert">{sendError}</p> : null}
          <button disabled={!canSend} type="submit">
            {sending ? 'Enviando...' : 'Enviar'}
          </button>
        </form>
      )}
    </section>
  );
}
