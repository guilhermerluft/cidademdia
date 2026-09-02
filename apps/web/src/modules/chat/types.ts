export type ChatConversationStatus = 'ACTIVE' | 'CLOSED';
export type ChatMessageType = 'TEXT' | 'AUDIO';

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

export interface ChatAudioAttachment {
  mediaId: string;
  originalFileName: string;
  contentType: string;
  sizeBytes: number;
}

export interface ChatMessage {
  id: string;
  sequence: number;
  conversationId: string;
  senderUserId: string;
  clientMessageId: string;
  type: ChatMessageType;
  content: string | null;
  audio: ChatAudioAttachment | null;
  sentAt: string;
}

export interface ChatMessagePage {
  items: ChatMessage[];
  nextCursor: string | null;
  hasMore: boolean;
}

export interface ChatAudioUpload {
  mediaId: string;
  contentType: string;
  sizeBytes: number;
  uploadUrl: string;
  uploadUrlExpiresAt: string;
}

export interface ChatAudioReadUrl {
  messageId: string;
  mediaId: string;
  readUrl: string;
  readUrlExpiresAt: string;
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
