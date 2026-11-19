import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  createCategory,
  deleteCategory,
  getAdminCategories,
  updateCategory,
  type CategoryInput,
  type CategoryUpdate,
} from '@/api/categories'

const adminCategoriesKey = ['admin', 'categories'] as const

export function useAdminCategories() {
  return useQuery({ queryKey: adminCategoriesKey, queryFn: getAdminCategories })
}

/** A change shows in two lists: the admin list and the store's category bar. */
function useRefreshCategories() {
  const queryClient = useQueryClient()
  return () =>
    Promise.all([
      queryClient.invalidateQueries({ queryKey: adminCategoriesKey }),
      queryClient.invalidateQueries({ queryKey: ['categories'] }),
    ])
}

export function useCreateCategory() {
  const refresh = useRefreshCategories()
  return useMutation({
    mutationFn: (input: CategoryInput) => createCategory(input),
    onSuccess: refresh,
  })
}

export function useUpdateCategory(id: string) {
  const refresh = useRefreshCategories()
  return useMutation({
    mutationFn: (input: CategoryUpdate) => updateCategory(id, input),
    onSuccess: refresh,
  })
}

export function useDeleteCategory() {
  const refresh = useRefreshCategories()
  return useMutation({
    mutationFn: deleteCategory,
    onSuccess: refresh,
  })
}
