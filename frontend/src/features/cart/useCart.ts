import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  addCartItem,
  clearCart,
  getCart,
  removeCartItem,
  setCartItemQuantity,
  type Cart,
} from '@/api/cart'
import { useSession } from '@/features/auth/authStore'

const cartKey = ['cart'] as const

const emptyCart: Cart = { items: [], totalQuantity: 0, totalAmount: 0, canCheckout: false }

/** The signed-in user's cart. It lives on the server, so nothing is asked for a guest. */
export function useCart() {
  const session = useSession()
  return useQuery({ queryKey: cartKey, queryFn: getCart, enabled: session !== null })
}

/**
 * Every change returns the whole cart with fresh prices and stock, and that answer replaces the cached cart.
 * The header badge and the cart page read the same cache, so they update together.
 */
function useCartMutation<TInput>(change: (input: TInput) => Promise<Cart>) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: change,
    onSuccess: (cart) => queryClient.setQueryData(cartKey, cart),
    // After a failure the cart on screen may be out of date, for example when the stock has just changed.
    onError: () => queryClient.invalidateQueries({ queryKey: cartKey }),
  })
}

export function useAddToCart() {
  return useCartMutation(({ productId, quantity }: { productId: string; quantity: number }) =>
    addCartItem(productId, quantity),
  )
}

export function useSetCartQuantity() {
  return useCartMutation(({ productId, quantity }: { productId: string; quantity: number }) =>
    setCartItemQuantity(productId, quantity),
  )
}

export function useRemoveFromCart() {
  return useCartMutation((productId: string) => removeCartItem(productId))
}

export function useClearCart() {
  return useCartMutation(async () => {
    await clearCart()
    return emptyCart
  })
}
