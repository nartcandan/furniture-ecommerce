import { apiRequest } from './client'

export interface Category {
  id: string
  name: string
  slug: string
  description: string | null
  isActive: boolean
}

export function getCategories(): Promise<Category[]> {
  return apiRequest<Category[]>('/api/categories')
}
