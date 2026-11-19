import type { OrderStatus } from '@/api/orders'

export type Tone = 'good' | 'waiting' | 'bad' | 'neutral'

export const statuses: Record<OrderStatus, { label: string; tone: Tone }> = {
  PendingPayment: { label: 'Waiting for payment', tone: 'waiting' },
  PaymentFailed: { label: 'Payment failed', tone: 'bad' },
  Paid: { label: 'Paid', tone: 'good' },
  Cancelling: { label: 'Cancelling', tone: 'waiting' },
  Cancelled: { label: 'Cancelled', tone: 'neutral' },
  Shipped: { label: 'Shipped', tone: 'good' },
  Delivered: { label: 'Delivered', tone: 'good' },
}

export function orderStatusLabel(status: OrderStatus): string {
  return statuses[status]?.label ?? status
}
