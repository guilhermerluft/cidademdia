import { api } from '../../services/api';
import type {
  CreatePostPayload,
  ManagedPostPage,
  PlacementPostPage,
  PostItem,
  PostMediaItem,
  PostMediaUpload,
  PostMutationResult,
  PostPlacementKey,
} from './types';

export type PostPublisherScope = 'platform';

export async function listManagedPosts(page = 1, pageSize = 20) {
  const { data } = await api.get<ManagedPostPage>('/posts/manage', {
    params: { page, pageSize },
  });

  return data;
}

export async function listPlacementPosts(
  placementKey: PostPlacementKey,
  cursor?: string,
  limit = 20,
  publisher?: PostPublisherScope,
) {
  const { data } = await api.get<PlacementPostPage>(`/posts/placements/${placementKey}`, {
    params: { cursor, limit, publisher },
  });

  return data;
}

export async function createPostDraft(payload: CreatePostPayload) {
  const { data } = await api.post<PostItem>('/posts', payload);
  return data;
}

export async function requestPostMediaUpload(
  postId: string,
  file: File,
  sortOrder: number,
) {
  const { data } = await api.post<PostMediaUpload>(`/posts/${postId}/media/upload`, {
    fileName: file.name,
    contentType: file.type,
    sizeBytes: file.size,
    sortOrder,
  });

  return data;
}

export async function uploadPostMediaBinary(upload: PostMediaUpload, file: File) {
  const response = await fetch(upload.uploadUrl, {
    method: 'PUT',
    headers: {
      'Content-Type': file.type,
    },
    body: file,
    credentials: 'omit',
  });

  if (!response.ok) {
    throw new Error(`O armazenamento recusou o upload de ${file.name} (HTTP ${response.status}).`);
  }
}

export async function confirmPostMedia(postId: string, mediaId: string) {
  const { data } = await api.post<{ media: PostMediaItem; changed: boolean }>(
    `/posts/${postId}/media/${mediaId}/confirm`,
  );

  return data.media;
}

export async function preparePostMedia(postId: string, files: File[]) {
  const prepared: PostMediaItem[] = [];

  for (const [index, file] of files.entries()) {
    const upload = await requestPostMediaUpload(postId, file, index);
    await uploadPostMediaBinary(upload, file);
    const media = await confirmPostMedia(postId, upload.id);

    if (media.status !== 'ready') {
      throw new Error(`A mídia ${file.name} não ficou pronta para publicação.`);
    }

    prepared.push(media);
  }

  return prepared;
}

export async function publishPost(postId: string) {
  const { data } = await api.post<PostMutationResult>(`/posts/${postId}/publish`);
  return data;
}

export async function archivePost(postId: string) {
  const { data } = await api.post<PostMutationResult>(`/posts/${postId}/archive`);
  return data;
}
