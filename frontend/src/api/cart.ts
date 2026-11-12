import { apiRequest } from './client'

export interface CartItem {
  productId: string
  name: string
  slug: string
  imageUrl: string | null
  /** The product's current price. The cart does not store prices. */
  unitPrice: number
  quantity: number
  lineTotal: number
  stockQuantity: number
  /** False when the product or its category was deactivated after it was added. */
  isAvailable: boolean
  /** True when the stock fell below the quantity in the cart. */
  hasInsufficientStock: boolean
}

export interface Cart {
  items: CartItem[]
  totalQuantity: number
  /** Includes lines that have a warning. */
  totalAmount: number
  /** True when the cart has items and no line has a warning. */
  canCheckout: boolean
}

export function getCart(): Promise<Cart> {
  return apiRequest<Cart>('/api/cart')
}

/** Adds to the quantity already in the cart. */
export function addCartItem(productId: string, quantity: number): Promise<Cart> {
  return apiRequest<Cart>('/api/cart/items', { method: 'POST', body: { productId, quantity } })
}

export function setCartItemQuantity(productId: string, quantity: number): Promise<Cart> {
  return apiRequest<Cart>(`/api/cart/items/${productId}`, { method: 'PUT', body: { quantity } })
}

export function removeCartItem(productId: string): Promise<Cart> {
  return apiRequest<Cart>(`/api/cart/items/${productId}`, { method: 'DELETE' })
}

export function clearCart(): Promise<void> {
  return apiRequest<void>('/api/cart', { method: 'DELETE' })
}
