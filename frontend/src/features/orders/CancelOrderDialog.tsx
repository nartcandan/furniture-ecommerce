import type { Order } from '@/api/orders'
import { FormError } from '@/components/FormError'
import { Button } from '@/components/ui/button'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { formatPrice } from '@/lib/format'
import { useCancelOrder } from './useCancelOrder'

interface CancelOrderDialogProps {
  order: Pick<Order, 'id' | 'status' | 'totalAmount'>
  open: boolean
  onOpenChange: (open: boolean) => void
  onCancelled?: () => void
}

/** Asks before cancelling. What is said depends on whether the order was paid. */
export function CancelOrderDialog({ order, open, onOpenChange, onCancelled }: CancelOrderDialogProps) {
  const cancel = useCancelOrder(order.id)
  const isUnpaid = order.status === 'PendingPayment'

  function handleOpenChange(next: boolean) {
    if (cancel.isPending) return
    if (!next) cancel.reset()
    onOpenChange(next)
  }

  return (
    <Dialog open={open} onOpenChange={handleOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Cancel this order?</DialogTitle>
          <DialogDescription>
            {isUnpaid
              ? 'Nothing has been charged. Your items stay in your cart, so you can order again. If you still pay on the open payment page afterwards, that payment is refunded.'
              : `The whole order is cancelled and ${formatPrice(order.totalAmount)} is refunded to your card. This cannot be undone.`}
          </DialogDescription>
        </DialogHeader>
        <FormError message={cancel.error?.message} />
        <DialogFooter>
          <Button
            variant="ghost"
            size="lg"
            className="h-10"
            disabled={cancel.isPending}
            onClick={() => handleOpenChange(false)}
          >
            Keep order
          </Button>
          <Button
            variant="destructive"
            size="lg"
            className="h-10"
            disabled={cancel.isPending}
            onClick={() =>
              cancel.mutate(undefined, {
                onSuccess: () => {
                  onOpenChange(false)
                  onCancelled?.()
                },
              })
            }
          >
            {cancel.isPending ? 'Cancelling…' : 'Cancel order'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
