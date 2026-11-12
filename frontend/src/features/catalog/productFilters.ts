import { productSorts, type ProductQuery, type ProductSort } from '@/api/products'

export const PRODUCTS_PAGE_SIZE = 12
export const DEFAULT_SORT: ProductSort = 'newest'

export interface ProductFilters {
  category?: string
  search?: string
  minPrice?: number
  maxPrice?: number
  sort: ProductSort
  page: number
}

function readPrice(value: string | null): number | undefined {
  if (value === null || value.trim() === '') {
    return undefined
  }
  const price = Number(value)
  return Number.isFinite(price) && price >= 0 ? price : undefined
}

/**
 * Reads the filters from the address. The address is the only place they are kept, so a filtered list
 * can be shared, reloaded and reached with the back button. Anyone can type an address, so values the
 * API would reject are dropped here instead of being sent.
 */
export function readFilters(params: URLSearchParams): ProductFilters {
  const sort = params.get('sort') as ProductSort | null
  const page = Number(params.get('page'))
  const minPrice = readPrice(params.get('minPrice'))
  let maxPrice = readPrice(params.get('maxPrice'))

  if (minPrice !== undefined && maxPrice !== undefined && maxPrice < minPrice) {
    maxPrice = undefined
  }

  return {
    category: params.get('category')?.trim() || undefined,
    search: params.get('search')?.trim() || undefined,
    minPrice,
    maxPrice,
    sort: sort && productSorts.includes(sort) ? sort : DEFAULT_SORT,
    page: Number.isInteger(page) && page >= 1 && page <= 1_000_000 ? page : 1,
  }
}

/** Writes filters back to an address. Defaults are left out, so plain lists keep a plain address. */
export function toSearchParams(filters: ProductFilters): URLSearchParams {
  const params = new URLSearchParams()

  if (filters.category) params.set('category', filters.category)
  if (filters.search) params.set('search', filters.search)
  if (filters.minPrice !== undefined) params.set('minPrice', String(filters.minPrice))
  if (filters.maxPrice !== undefined) params.set('maxPrice', String(filters.maxPrice))
  if (filters.sort !== DEFAULT_SORT) params.set('sort', filters.sort)
  if (filters.page > 1) params.set('page', String(filters.page))

  return params
}

export function productsPath(filters: ProductFilters): string {
  const query = toSearchParams(filters).toString()
  return query ? `/products?${query}` : '/products'
}

export function toProductQuery(filters: ProductFilters): ProductQuery {
  return { ...filters, pageSize: PRODUCTS_PAGE_SIZE }
}
