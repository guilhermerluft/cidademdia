export type ChatConversationStatus = 'ACTIVE' | 'CLOSED';

export interface ChatConversation {
  id: string;
  occurrenceId: string;
  occurrenceTargetId: string;
  citizenUserId: string;
  masterUserId: string;
  status: ChatConversationStatus;
  createdAt: string;
  closedAt: string | null;
}

export interface ChatMessage {
  id: string;
  sequence: number;
  conversationId: string;
  senderUserId: string;
  clientMessageId: string;
  content: string;
  sentAt: string;
}

export interface ChatMessagePage {
  items: ChatMessage[];
  nextCursor: string | null;
  hasMore: boolean;
}

export type ChatConnectionState =
  | 'idle'
  | 'loading'
  | 'connecting'
  | 'connected'
  | 'reconnecting'
  | 'disconnected'
  | 'closed'
  | 'error';
