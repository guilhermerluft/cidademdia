export type PostType = 'text' | 'image' | 'video' | 'link' | 'carousel';
export type PostStatus = 'draft' | 'published' | 'archived';
export type PostPlacementKey = 'feed' | 'horizontal' | 'vertical';

export interface PostPlacement {
  placementKey: PostPlacementKey;
  priority: number;
  displayOrder: number;
}

export interface PostMediaItem {
  id: string;
  status: 'pending_upload' | 'ready';
  contentType: string;
  sizeBytes: number;
  sortOrder: number;
  readUrl?: string | null;
  readUrlExpiresAt?: string | null;
}

export interface PostItem {
  id: string;
  publisherUserId: string;
  masterUserId?: string | null;
  type: PostType;
  status: PostStatus;
  title?: string | null;
  body?: string | null;
  linkUrl?: string | null;
  createdAt: string;
  publishedAt?: string | null;
  archivedAt?: string | null;
  placements: PostPlacement[];
  media: PostMediaItem[];
}

export interface ManagedPostPage {
  items: PostItem[];
  page: number;
  pageSize: number;
  totalItems: number;
}

export interface PlacementPostPage {
  items: PostItem[];
  nextCursor?: string | null;
}

export interface CreatePostPayload {
  type: PostType;
  title?: string | null;
  body?: string | null;
  linkUrl?: string | null;
  placements: PostPlacement[];
}

export interface PostMediaUpload {
  id: string;
  postId: string;
  status: 'pending_upload' | 'ready';
  contentType: string;
  expectedSizeBytes: number;
  sortOrder: number;
  uploadUrl: string;
  uploadUrlExpiresAt: string;
}

export interface PostMutationResult {
  post: PostItem;
  changed: boolean;
}
