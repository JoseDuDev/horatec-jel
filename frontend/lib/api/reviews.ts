import { apiFetch } from './client'
import type { ResourceReviewsResult } from '../types/review'

export const reviewsApi = {
  byResource: (resourceId: string, pageNumber = 1, pageSize = 20) =>
    apiFetch<ResourceReviewsResult>(
      `/api/v1/reviews/resources/${resourceId}?pageNumber=${pageNumber}&pageSize=${pageSize}`
    ),

  reply: (reviewId: string, reply: string) =>
    apiFetch<void>(`/api/v1/reviews/${reviewId}/reply`, {
      method: 'POST',
      body: JSON.stringify({ reply }),
    }),
}
