import type { OrderStatus } from '@/api/orders'
import { cn } from '@/lib/utils'

type Tone = 'good' | 'waiting' | 'bad' | 'neutral'

const statuses: Record<OrderStatus, { label: string; tone: Tone }> = {
  PendingPayment: { label: 'Waiting for payment', tone: 'waiting' },
  PaymentFailed: { label: 'Payment failed', tone: 'bad' },
  Paid: { label: 'Paid', tone: 'good' },
  Cancelling: { label: 'Cancelling', tone: 'waiting' },
  Cancelled: { label: 'Cancelled', tone: 'neutral' },
  Shipped: { label: 'Shipped', tone: 'good' },
  Delivered: { label: 'Delivered', tone: 'good' },
}

const toneClasses: Record<Tone, string> = {
  good: 'bg-primary/10 text-primary',
  waiting: 'bg-highlight/15 text-highlight',
  bad: 'bg-destructive/10 text-destructive',
  neutral: 'bg-muted text-muted-foreground',
}

export function OrderStatusBadge({ status }: { status: OrderStatus }) {
  const { label, tone } = statuses[status] ?? { label: status, tone: 'neutral' }
  return (
    <span
      className={cn(
        'inline-flex items-center rounded-full px-2.5 py-1 text-xs font-semibold whitespace-nowrap',
        toneClasses[tone],
      )}
    >
      {label}
    </span>
  )
}
