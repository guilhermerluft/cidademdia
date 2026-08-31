import axios from 'axios';
import { useCallback, useEffect, useRef, useState } from 'react';
import type { HubConnection } from '@microsoft/signalr';
import {
  createChatConnection,
  getConversationByTarget,
  getMessages,
  joinConversation,
  sendMessageRealtime,
} from './chatService';
import type {
  ChatConnectionState,
  ChatConversation,
  ChatMessage,
} from './types';

function mergeMessages(
  current: ChatMessage[],
  incoming: ChatMessage | ChatMessage[],
) {
  const merged = new Map(current.map((message) => [message.id, message]));
  const rows = Array.isArray(incoming) ? incoming : [incoming];

  for (const message of rows) {
    merged.set(message.id, message);
  }

  return [...merged.values()].sort((left, right) => left.sequence - right.sequence);
}

function toErrorMessage(error: unknown) {
  return error instanceof Error ? error.message : 'Chat operation failed.';
}

function isClosedConversationError(error: unknown) {
  if (!axios.isAxiosError(error) || error.response?.status !== 410) {
    return false;
  }

  const data = error.response.data as { code?: string } | undefined;
  return data?.code === 'conversation_closed';
}

export interface UseChatConversationResult {
  conversation: ChatConversation | null;
  messages: ChatMessage[];
  state: ChatConnectionState;
  error: string | null;
  sendText: (content: string) => Promise<ChatMessage>;
  resync: () => Promise<void>;
}

export function useChatConversation(
  targetId: string | null | undefined,
): UseChatConversationResult {
  const [conversation, setConversation] = useState<ChatConversation | null>(null);
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [state, setState] = useState<ChatConnectionState>('idle');
  const [error, setError] = useState<string | null>(null);

  const connectionRef = useRef<HubConnection | null>(null);
  const conversationRef = useRef<ChatConversation | null>(null);
  const cursorRef = useRef<string | null>(null);
  const generationRef = useRef(0);

  const syncMessages = useCallback(async (
    conversationId: string,
    generation = generationRef.current,
  ) => {
    let cursor = cursorRef.current;
    let hasMore = true;

    while (hasMore) {
      const page = await getMessages(conversationId, cursor, 100);
      if (generationRef.current !== generation) return;

      if (page.items.length > 0) {
        setMessages((current) => mergeMessages(current, page.items));
      }

      const nextCursor = page.nextCursor ?? cursor;
      if (nextCursor === cursor && page.hasMore) {
        throw new Error('Chat synchronization cursor did not advance.');
      }

      cursor = nextCursor;
      cursorRef.current = nextCursor;
      hasMore = page.hasMore;
    }
  }, []);

  const resync = useCallback(async () => {
    const current = conversationRef.current;
    if (!current) return;

    await syncMessages(current.id, generationRef.current);
  }, [syncMessages]);

  useEffect(() => {
    const generation = generationRef.current + 1;
    generationRef.current = generation;
    cursorRef.current = null;
    conversationRef.current = null;
    setConversation(null);
    setMessages([]);
    setError(null);

    const isCurrent = () => generationRef.current === generation;

    if (!targetId) {
      setState('idle');
      return () => {
        if (isCurrent()) generationRef.current += 1;
      };
    }

    const connection = createChatConnection();
    connectionRef.current = connection;

    const handleMessage = (message: ChatMessage) => {
      if (!isCurrent()) return;
      setMessages((current) => mergeMessages(current, message));
    };

    connection.on('MessageReceived', handleMessage);
    connection.onreconnecting(() => {
      if (isCurrent()) setState('reconnecting');
    });
    connection.onreconnected(() => {
      const current = conversationRef.current;
      if (!current || !isCurrent()) return;

      void (async () => {
        try {
          await joinConversation(connection, current.id);
          await syncMessages(current.id, generation);
          if (isCurrent()) {
            setError(null);
            setState('connected');
          }
        } catch (reconnectError) {
          if (!isCurrent()) return;
          if (isClosedConversationError(reconnectError)) {
            setMessages([]);
            setError(null);
            setState('closed');
            return;
          }

          setError(toErrorMessage(reconnectError));
          setState('error');
        }
      })();
    });
    connection.onclose((closeError) => {
      if (!isCurrent()) return;
      setError(closeError ? toErrorMessage(closeError) : null);
      setState('disconnected');
    });

    void (async () => {
      try {
        setState('loading');
        const loadedConversation = await getConversationByTarget(targetId);
        if (!isCurrent()) return;

        conversationRef.current = loadedConversation;
        setConversation(loadedConversation);

        await syncMessages(loadedConversation.id, generation);
        if (!isCurrent()) return;

        setState('connecting');
        await connection.start();
        if (!isCurrent()) return;

        await joinConversation(connection, loadedConversation.id);
        await syncMessages(loadedConversation.id, generation);
        if (!isCurrent()) return;

        setError(null);
        setState('connected');
      } catch (initializationError) {
        if (!isCurrent()) return;
        if (isClosedConversationError(initializationError)) {
          setMessages([]);
          setError(null);
          setState('closed');
          return;
        }

        setError(toErrorMessage(initializationError));
        setState('error');
      }
    })();

    return () => {
      if (isCurrent()) generationRef.current += 1;
      connection.off('MessageReceived', handleMessage);
      if (connectionRef.current === connection) connectionRef.current = null;
      conversationRef.current = null;
      void connection.stop();
    };
  }, [syncMessages, targetId]);

  const sendText = useCallback(async (content: string) => {
    const currentConversation = conversationRef.current;
    if (!currentConversation) {
      throw new Error('Chat conversation is not available.');
    }

    const normalized = content.trim();
    if (!normalized) {
      throw new Error('Message content is required.');
    }

    const clientMessageId = crypto.randomUUID();
    const connection = connectionRef.current;
    if (!connection) {
      throw new Error('Realtime chat connection is not available.');
    }

    const message = await sendMessageRealtime(
      connection,
      currentConversation.id,
      clientMessageId,
      normalized,
    );

    if (conversationRef.current?.id === currentConversation.id) {
      setMessages((current) => mergeMessages(current, message));
    }

    return message;
  }, []);

  return {
    conversation,
    messages,
    state,
    error,
    sendText,
    resync,
  };
}
