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
  const params = new URLSearchParams()
  for (const [name, value] of Object.entries(query)) {
    if (value !== undefined && value !== '') {
      params.set(name, String(value))
    }
  }
  return apiRequest<PagedResult<Product>>(`/api/products?${params}`)
}

export function getProduct(slug: string): Promise<Product> {
  return apiRequest<Product>(`/api/products/${encodeURIComponent(slug)}`)
}
