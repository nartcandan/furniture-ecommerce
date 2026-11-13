import { apiRequest } from './client'

export type OrderStatus =
  | 'PendingPayment'
  | 'PaymentFailed'
  | 'Paid'
  | 'Cancelling'
  | 'Cancelled'
  | 'Shipped'
  | 'Delivered'

export interface OrderItem {
  productId: string
  /** The name and price at the time of the order. Later changes to the product do not affect them. */
  productName: string
  unitPrice: number
  quantity: number
  lineTotal: number
}

export interface Order {
  id: string
  status: OrderStatus
  totalAmount: number
  /** The payment page. Present while the order waits for payment and the page is still open. */
  paymentPageUrl: string | null
  recipientFirstName: string
  recipientLastName: string
  phoneNumber: string
  city: string
  district: string
  addressLine: string
  postalCode: string | null
  paymentError: string | null
  paidAt: string | null
  createdAt: string
  /** True while the customer can still cancel: waiting for payment, or paid today and not shipped. */
  canCancel: boolean
  items: OrderItem[]
}

export interface OrderSummary {
  id: string
  status: OrderStatus
  totalAmount: number
  totalQuantity: number
  createdAt: string
}

/** Newest first. */
export function getOrders(): Promise<OrderSummary[]> {
  return apiRequest<OrderSummary[]>('/api/orders')
}

export function getOrder(id: string): Promise<Order> {
  return apiRequest<Order>(`/api/orders/${encodeURIComponent(id)}`)
}

/** Creates an order from the cart, with a copy of the address. */
export function createOrder(addressId: string): Promise<Order> {
  return apiRequest<Order>('/api/orders', { method: 'POST', body: { addressId } })
}

/**
 * Cancels an order that waits for payment, or a paid one on the day of payment (with a refund).
 * The order comes back as `Cancelled`, or as `Cancelling` when the refund is still being confirmed.
 */
export function cancelOrder(id: string): Promise<Order> {
  return apiRequest<Order>(`/api/orders/${id}/cancel`, { method: 'POST' })
}
