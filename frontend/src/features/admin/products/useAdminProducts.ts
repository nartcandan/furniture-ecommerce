import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  adjustStock,
  createProduct,
  deleteProductImage,
  getAdminProduct,
  getAdminProducts,
  reorderProductImages,
  updateProduct,
  uploadProductImage,
  type NewProductInput,
  type Product,
  type ProductQuery,
  type ProductUpdate,
} from '@/api/products'

const productKey = (id: string) => ['admin', 'product', id] as const

export function useAdminProducts(query: ProductQuery) {
  return useQuery({
    queryKey: ['admin', 'products', query],
    queryFn: () => getAdminProducts(query),
    // The table keeps the last page while the next one loads, so it does not jump.
    placeholderData: keepPreviousData,
  })
}

export function useAdminProduct(id: string) {
  return useQuery({ queryKey: productKey(id), queryFn: () => getAdminProduct(id) })
}

/**
 * After a change, the admin list and everything in the store that shows products is read again:
 * the product lists and pages, and the cart, which shows prices, stock and availability.
 */
function useRefreshProducts() {
  const queryClient = useQueryClient()
  return () => {
    for (const queryKey of [['admin', 'products'], ['products'], ['product'], ['cart']]) {
      void queryClient.invalidateQueries({ queryKey })
    }
  }
}

/** Puts the product an endpoint returned straight into the cache, so its page updates without a new request. */
function useStoreProduct() {
  const queryClient = useQueryClient()
  const refresh = useRefreshProducts()
  return (product: Product) => {
    queryClient.setQueryData(productKey(product.id), product)
    refresh()
  }
}

export function useCreateProduct() {
  const store = useStoreProduct()
  return useMutation({
    mutationFn: (input: NewProductInput) => createProduct(input),
    onSuccess: store,
  })
}

export function useUpdateProduct(id: string) {
  const store = useStoreProduct()
  return useMutation({
    mutationFn: (input: ProductUpdate) => updateProduct(id, input),
    onSuccess: store,
  })
}

export function useAdjustStock(id: string) {
  const store = useStoreProduct()
  return useMutation({
    mutationFn: (change: number) => adjustStock(id, change),
    onSuccess: store,
  })
}

/** Image changes return only the images, so the product is read again. */
export function useRefreshProduct(id: string) {
  const queryClient = useQueryClient()
  const refresh = useRefreshProducts()
  return async () => {
    refresh()
    await queryClient.invalidateQueries({ queryKey: productKey(id) })
  }
}

/** Uploads one file. Several files are sent one by one, and the caller refreshes the product at the end. */
export function useUploadProductImage(productId: string) {
  return useMutation({
    mutationFn: (file: File) => uploadProductImage(productId, file),
  })
}

export function useDeleteProductImage(productId: string) {
  const refresh = useRefreshProduct(productId)
  return useMutation({
    mutationFn: (imageId: string) => deleteProductImage(productId, imageId),
    onSuccess: refresh,
  })
}

export function useReorderProductImages(productId: string) {
  const refresh = useRefreshProduct(productId)
  return useMutation({
    mutationFn: (imageIds: string[]) => reorderProductImages(productId, imageIds),
    onSuccess: refresh,
  })
}
