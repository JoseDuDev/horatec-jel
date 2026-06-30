export interface PagedResult<T> {
  items: T[]
  totalCount: number
  pageNumber: number
  pageSize: number
  hasNextPage: boolean
  hasPreviousPage: boolean
}

export interface ReviewItem {
  id: string
  bookingId: string
  customerId: string
  stars: number
  comment?: string
  ownerReply?: string
  ownerRepliedAt?: string
  createdAt: string
}

export interface ResourceReviewsResult {
  averageStars: number
  totalReviews: number
  page: PagedResult<ReviewItem>
}
