import { ArrowRight } from 'lucide-react'
import { useState } from 'react'
import type { Order } from '@/api/orders'
import { Button } from '@/components/ui/button'
import { CancelOrderDialog } from './CancelOrderDialog'

interface UnpaidOrderActionsProps {
  order: Order
  onCancelled?: () => void
  className?: string
}

/**
 * The two ways out of an order that waits for payment: go back to the payment page while it is open,
 * or give the order up. Shown on the order page and at checkout.
 */
export function UnpaidOrderActions({ order, onCancelled, className }: UnpaidOrderActionsProps) {
  const [isConfirming, setIsConfirming] = useState(false)

  return (
    <div className={className}>
      <div className="flex flex-wrap gap-2">
        {order.paymentPageUrl && (
          <Button asChild size="lg" className="h-10 px-5">
            {/* The payment page belongs to the payment provider, so this is a plain link out of the app. */}
            <a href={order.paymentPageUrl}>
              Continue to payment
              <ArrowRight aria-hidden data-icon="inline-end" />
            </a>
          </Button>
        )}
        {order.canCancel && (
          <Button variant="outline" size="lg" className="h-10 px-5" onClick={() => setIsConfirming(true)}>
            Cancel order
          </Button>
        )}
      </div>
      <CancelOrderDialog
        order={order}
        open={isConfirming}
        onOpenChange={setIsConfirming}
        onCancelled={onCancelled}
      />
    </div>
  )
}
