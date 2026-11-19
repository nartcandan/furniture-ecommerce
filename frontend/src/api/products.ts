import { apiRequest } from './client'

export interface ProductImage {
  id: string
  url: string
  altText: string | null
  sortOrder: number
}

export interface Product {
  id: string
  categoryId: string
  categoryName: string
  categorySlug: string
  name: string
  slug: string
  description: string | null
  price: number
  stockQuantity: number
  widthCm: number | null
  depthCm: number | null
  heightCm: number | null
  weightKg: number | null
  material: string | null
  color: string | null
  isActive: boolean
  /** Sorted, main image first. The list returns only the main image, the detail returns all. */
  images: ProductImage[]
}

export interface PagedResult<T> {
  items: T[]
  page: number
  pageSize: number
  totalCount: number
  totalPages: number
}

export const productSorts = ['newest', 'price_asc', 'price_desc', 'name'] as const
export type ProductSort = (typeof productSorts)[number]

export interface ProductQuery {
  category?: string
  search?: string
  minPrice?: number
  maxPrice?: number
  sort?: ProductSort
  page?: number
  pageSize?: number
}

export function getProducts(query: ProductQuery): Promise<PagedResult<Product>> {
  return apiRequest<PagedResult<Product>>(`/api/products?${toQueryString(query)}`)
}

export function getProduct(slug: string): Promise<Product> {
  return apiRequest<Product>(`/api/products/${encodeURIComponent(slug)}`)
}

// Admin endpoints. They include inactive products and categories.

export interface ProductInput {
  categoryId: string
  name: string
  description: string | null
  price: number
  widthCm: number | null
  depthCm: number | null
  heightCm: number | null
  weightKg: number | null
  material: string | null
  color: string | null
}

export interface NewProductInput extends ProductInput {
  /** The starting stock. Later changes go through `adjustStock`. */
  stockQuantity: number
}

export interface ProductUpdate extends ProductInput {
  isActive: boolean
}

function toQueryString(query: ProductQuery): string {
  const params = new URLSearchParams()
  for (const [name, value] of Object.entries(query)) {
    if (value !== undefined && value !== '') {
      params.set(name, String(value))
    }
  }
  return params.toString()
}

export function getAdminProducts(query: ProductQuery): Promise<PagedResult<Product>> {
  return apiRequest<PagedResult<Product>>(`/api/admin/products?${toQueryString(query)}`)
}

/** One product with all its images. Admins find products by id, since inactive ones have no store page. */
export function getAdminProduct(id: string): Promise<Product> {
  return apiRequest<Product>(`/api/admin/products/${id}`)
}

export function createProduct(input: NewProductInput): Promise<Product> {
  return apiRequest<Product>('/api/products', { method: 'POST', body: input })
}

export function updateProduct(id: string, input: ProductUpdate): Promise<Product> {
  return apiRequest<Product>(`/api/products/${id}`, { method: 'PUT', body: input })
}

/** Adds to or takes from the stock in one step. Fails with 409 when the stock would go below zero. */
export function adjustStock(id: string, change: number): Promise<Product> {
  return apiRequest<Product>(`/api/products/${id}/stock`, { method: 'PATCH', body: { change } })
}

export function uploadProductImage(productId: string, file: File): Promise<ProductImage> {
  const form = new FormData()
  form.append('file', file)
  return apiRequest<ProductImage>(`/api/products/${productId}/images`, { method: 'POST', body: form })
}

export function deleteProductImage(productId: string, imageId: string): Promise<void> {
  return apiRequest<void>(`/api/products/${productId}/images/${imageId}`, { method: 'DELETE' })
}

/** Saves a new image order. The list holds every image of the product, the main image first. */
export function reorderProductImages(productId: string, imageIds: string[]): Promise<ProductImage[]> {
  return apiRequest<ProductImage[]>(`/api/products/${productId}/images/order`, {
    method: 'PUT',
    body: { imageIds },
  })
}
