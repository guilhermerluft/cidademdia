import {
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
  type HubConnection,
} from '@microsoft/signalr';
import { api, getRealtimeAccessToken } from '../../services/api';
import type { ChatConversation, ChatMessage, ChatMessagePage } from './types';

const apiBaseUrl = import.meta.env.VITE_API_BASE_URL ?? '/api/v1';

function getHubUrl() {
  if (/^https?:\/\//i.test(apiBaseUrl)) {
    const url = new URL(apiBaseUrl);
    return `${url.origin}/hubs/chat`;
  }

  return '/hubs/chat';
}

export async function getConversationByTarget(
  targetId: string,
): Promise<ChatConversation> {
  const { data } = await api.get<ChatConversation>(
    `/chat/targets/${targetId}/conversation`,
  );
  return data;
}

export async function getMessages(
  conversationId: string,
  cursor?: string | null,
  pageSize = 100,
): Promise<ChatMessagePage> {
  const { data } = await api.get<ChatMessagePage>(
    `/chat/conversations/${conversationId}/messages`,
    {
      params: {
        cursor: cursor || undefined,
        pageSize,
      },
    },
  );
  return data;
}

export async function sendMessageRest(
  conversationId: string,
  clientMessageId: string,
  content: string,
): Promise<ChatMessage> {
  const { data } = await api.post<ChatMessage>(
    `/chat/conversations/${conversationId}/messages`,
    {
      clientMessageId,
      content,
    },
  );
  return data;
}

export function createChatConnection(): HubConnection {
  const connection = new HubConnectionBuilder()
    .withUrl(getHubUrl(), {
      accessTokenFactory: getRealtimeAccessToken,
      withCredentials: true,
    })
    .withAutomaticReconnect([0, 1_000, 3_000, 5_000, 10_000])
    .configureLogging(LogLevel.Warning)
    .build();

  connection.serverTimeoutInMilliseconds = 30_000;
  connection.keepAliveIntervalInMilliseconds = 15_000;

  return connection;
}

export async function joinConversation(
  connection: HubConnection,
  conversationId: string,
) {
  if (connection.state !== HubConnectionState.Connected) {
    throw new Error('Realtime chat connection is not connected.');
  }

  await connection.invoke('JoinConversation', conversationId);
}

export async function sendMessageRealtime(
  connection: HubConnection,
  conversationId: string,
  clientMessageId: string,
  content: string,
): Promise<ChatMessage> {
  if (connection.state !== HubConnectionState.Connected) {
    return sendMessageRest(conversationId, clientMessageId, content);
  }

  return connection.invoke<ChatMessage>(
    'SendMessage',
    conversationId,
    clientMessageId,
    content,
  );
}
