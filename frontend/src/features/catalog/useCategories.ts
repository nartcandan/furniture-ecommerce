import { useQuery } from '@tanstack/react-query'
import { getCategories } from '@/api/categories'

/** The active categories. The header and the home page share one request. */
export function useCategories() {
  return useQuery({
    queryKey: ['categories'],
    queryFn: getCategories,
    staleTime: 5 * 60 * 1000,
  })
}

export function categoryPath(slug: string): string {
  return `/products?category=${encodeURIComponent(slug)}`
}
