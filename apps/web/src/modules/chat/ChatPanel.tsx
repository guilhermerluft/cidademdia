import { useEffect, useMemo, useRef, useState } from 'react';
import type { ChangeEvent, FormEvent } from 'react';
import { useAuth } from '../auth/AuthProvider';
import { getChatAudioReadUrl, normalizeAudioContentType } from './chatService';
import type { ChatMessage } from './types';
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

function extensionForAudio(contentType: string) {
  switch (normalizeAudioContentType(contentType)) {
    case 'audio/ogg': return 'ogg';
    case 'audio/mp4': return 'm4a';
    case 'audio/mpeg': return 'mp3';
    case 'audio/wav': return 'wav';
    default: return 'webm';
  }
}

function ChatAudioPlayer({ message }: { message: ChatMessage }) {
  const [readUrl, setReadUrl] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let active = true;
    setLoading(true);
    setError(null);

    void getChatAudioReadUrl(message.conversationId, message.id)
      .then((result) => {
        if (!active) return;
        setReadUrl(result.readUrl);
        setLoading(false);
      })
      .catch(() => {
        if (!active) return;
        setReadUrl(null);
        setLoading(false);
        setError('Áudio indisponível.');
      });

    return () => {
      active = false;
    };
  }, [message.conversationId, message.id]);

  if (loading) return <span className="chat-panel__audio-status">Carregando áudio...</span>;
  if (error || !readUrl) return <span className="chat-panel__audio-status">{error ?? 'Áudio indisponível.'}</span>;

  return (
    <div className="chat-panel__audio-message">
      <span>{message.audio?.originalFileName ?? 'Áudio'}</span>
      <audio controls preload="metadata" src={readUrl}>
        Seu navegador não suporta reprodução de áudio.
      </audio>
    </div>
  );
}

export function ChatPanel({ targetId, title = 'Conversa da ocorrência' }: ChatPanelProps) {
  const { user } = useAuth();
  const { messages, state, error, sendText, sendAudio } = useChatConversation(targetId);
  const [draft, setDraft] = useState('');
  const [sending, setSending] = useState(false);
  const [sendingAudio, setSendingAudio] = useState(false);
  const [recording, setRecording] = useState(false);
  const [sendError, setSendError] = useState<string | null>(null);
  const fileInputRef = useRef<HTMLInputElement | null>(null);
  const recorderRef = useRef<MediaRecorder | null>(null);
  const recordingStreamRef = useRef<MediaStream | null>(null);
  const audioChunksRef = useRef<Blob[]>([]);
  const cancelRecordingRef = useRef(false);

  const canSend = state === 'connected'
    && !sending
    && !sendingAudio
    && !recording
    && draft.trim().length > 0;
  const canSendAudio = state === 'connected' && !sending && !sendingAudio;
  const canRecord = canSendAudio
    && typeof MediaRecorder !== 'undefined'
    && typeof navigator !== 'undefined'
    && Boolean(navigator.mediaDevices?.getUserMedia);
  const statusLabel = useMemo(() => connectionLabels[state], [state]);

  useEffect(() => () => {
    cancelRecordingRef.current = true;
    const recorder = recorderRef.current;
    if (recorder && recorder.state !== 'inactive') recorder.stop();
    recordingStreamRef.current?.getTracks().forEach((track) => track.stop());
  }, []);

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

  async function handleAudioFile(file: File) {
    if (!canSendAudio || file.size <= 0) return;

    setSendingAudio(true);
    setSendError(null);
    try {
      await sendAudio(file);
    } catch (audioError) {
      setSendError(
        audioError instanceof Error
          ? audioError.message
          : 'Não foi possível enviar o áudio.',
      );
    } finally {
      setSendingAudio(false);
      if (fileInputRef.current) fileInputRef.current.value = '';
    }
  }

  function handleAudioSelection(event: ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0];
    if (file) void handleAudioFile(file);
  }

  async function startRecording() {
    if (!canRecord) return;

    setSendError(null);
    try {
      const stream = await navigator.mediaDevices.getUserMedia({ audio: true });
      const preferredType = [
        'audio/webm;codecs=opus',
        'audio/webm',
        'audio/ogg;codecs=opus',
      ].find((type) => MediaRecorder.isTypeSupported(type));
      const recorder = preferredType
        ? new MediaRecorder(stream, { mimeType: preferredType })
        : new MediaRecorder(stream);

      cancelRecordingRef.current = false;
      audioChunksRef.current = [];
      recorderRef.current = recorder;
      recordingStreamRef.current = stream;

      recorder.ondataavailable = (event) => {
        if (event.data.size > 0) audioChunksRef.current.push(event.data);
      };
      recorder.onerror = () => {
        setSendError('Não foi possível gravar o áudio.');
      };
      recorder.onstop = () => {
        const chunks = audioChunksRef.current;
        const contentType = normalizeAudioContentType(
          recorder.mimeType || chunks[0]?.type || preferredType || 'audio/webm',
        );
        const cancelled = cancelRecordingRef.current;

        recordingStreamRef.current?.getTracks().forEach((track) => track.stop());
        recordingStreamRef.current = null;
        recorderRef.current = null;
        audioChunksRef.current = [];
        setRecording(false);

        if (cancelled || chunks.length === 0) return;
        const blob = new Blob(chunks, { type: contentType });
        const file = new File(
          [blob],
          `audio-${Date.now()}.${extensionForAudio(contentType)}`,
          { type: contentType },
        );
        void handleAudioFile(file);
      };

      recorder.start(250);
      setRecording(true);
    } catch {
      recordingStreamRef.current?.getTracks().forEach((track) => track.stop());
      recordingStreamRef.current = null;
      recorderRef.current = null;
      setRecording(false);
      setSendError('Não foi possível acessar o microfone. Confira a permissão do navegador.');
    }
  }

  function stopRecording() {
    const recorder = recorderRef.current;
    if (!recorder || recorder.state === 'inactive') return;
    cancelRecordingRef.current = false;
    recorder.stop();
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
                {message.type === 'AUDIO' ? (
                  <ChatAudioPlayer message={message} />
                ) : (
                  <p>{message.content}</p>
                )}
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
          <button disabled={!canSend} type="submit">
            {sending ? 'Enviando...' : 'Enviar'}
          </button>

          <div className="chat-panel__audio-actions">
            <label className="chat-panel__audio-file">
              Selecionar áudio
              <input
                ref={fileInputRef}
                accept="audio/webm,audio/ogg,audio/mp4,audio/mpeg,audio/wav,audio/*"
                disabled={!canSendAudio || recording}
                onChange={handleAudioSelection}
                type="file"
              />
            </label>
            {recording ? (
              <button className="chat-panel__audio-button" onClick={stopRecording} type="button">
                Parar e enviar
              </button>
            ) : (
              <button
                className="chat-panel__audio-button"
                disabled={!canRecord}
                onClick={() => void startRecording()}
                type="button"
              >
                Gravar áudio
              </button>
            )}
            {sendingAudio ? <span aria-live="polite">Enviando áudio...</span> : null}
          </div>

          {sendError ? <p role="alert">{sendError}</p> : null}
        </form>
      )}
    </section>
  );
}
