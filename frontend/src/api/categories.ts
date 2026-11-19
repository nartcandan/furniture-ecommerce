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

export interface CategoryInput {
  name: string
  description: string | null
}

export interface CategoryUpdate extends CategoryInput {
  isActive: boolean
}

/** All categories, inactive ones included. Admins only. */
export function getAdminCategories(): Promise<Category[]> {
  return apiRequest<Category[]>('/api/admin/categories')
}

export function createCategory(input: CategoryInput): Promise<Category> {
  return apiRequest<Category>('/api/categories', { method: 'POST', body: input })
}

export function updateCategory(id: string, input: CategoryUpdate): Promise<Category> {
  return apiRequest<Category>(`/api/categories/${id}`, { method: 'PUT', body: input })
}

/** Fails with 409 while the category holds products, inactive ones included. */
export function deleteCategory(id: string): Promise<void> {
  return apiRequest<void>(`/api/categories/${id}`, { method: 'DELETE' })
}
