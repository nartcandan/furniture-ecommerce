import { useId, useState, type FormEvent } from 'react'
import { toast } from 'sonner'
import type { Product } from '@/api/products'
import { FormError } from '@/components/FormError'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { formatNumber } from '@/lib/format'
import { errorMessage } from '@/lib/formErrors'
import { useAdjustStock } from './useAdminProducts'

/** Reads `+5`, `5` or `-2`. Anything else, and zero, is `null`. */
function readChange(text: string): number | null {
  const match = /^([+-]?)(\d{1,7})$/.exec(text.trim())
  if (!match) return null
  const change = Number(match[2]) * (match[1] === '-' ? -1 : 1)
  return change === 0 || Math.abs(change) > 1_000_000 ? null : change
}

/**
 * Stock is changed by an amount, not set to a number. The API adds it in one statement, so an order placed
 * at the same moment is not overwritten, and it refuses a change that would take the stock below zero.
 */
export function StockPanel({ product }: { product: Product }) {
  const adjust = useAdjustStock(product.id)
  const inputId = useId()
  const hintId = useId()
  const [text, setText] = useState('')
  const [error, setError] = useState<string>()
  const change = readChange(text)
  const newStock = change === null ? null : product.stockQuantity + change

  function handleSubmit(event: FormEvent) {
    event.preventDefault()
    if (change === null) {
      setError('Enter a whole number other than 0, for example 10 to add or -2 to take away.')
      return
    }
    if (newStock !== null && newStock < 0) {
      setError(`Only ${formatNumber(product.stockQuantity)} in stock, so at most ${formatNumber(product.stockQuantity)} can be taken away.`)
      return
    }
    setError(undefined)
    adjust.mutate(change, {
      onSuccess: (updated) => {
        toast.success(`Stock is now ${formatNumber(updated.stockQuantity)}`)
        setText('')
      },
      onError: (apiError) => setError(errorMessage(apiError)),
    })
  }

  return (
    <section aria-labelledby={`${inputId}-title`} className="rounded-xl border bg-card p-5">
      <h2 id={`${inputId}-title`} className="font-sans text-sm font-semibold tracking-normal">
        Stock
      </h2>
      <p className="mt-3 text-3xl font-semibold tabular-nums">{formatNumber(product.stockQuantity)}</p>
      <p className="text-xs text-muted-foreground">
        {product.stockQuantity === 0 ? 'Out of stock, customers cannot order it.' : 'units available'}
      </p>

      <form onSubmit={handleSubmit} noValidate className="mt-5 grid gap-2">
        <FormError message={error} />
        <Label htmlFor={inputId}>Add or take away</Label>
        <div className="flex gap-2">
          <Input
            id={inputId}
            inputMode="numeric"
            placeholder="10 or -2"
            value={text}
            aria-describedby={hintId}
            onChange={(event) => {
              setText(event.target.value)
              setError(undefined)
            }}
            className="bg-background"
          />
          <Button type="submit" variant="outline" disabled={adjust.isPending}>
            {adjust.isPending ? 'Saving…' : 'Apply'}
          </Button>
        </div>
        <p id={hintId} className="text-xs text-muted-foreground" aria-live="polite">
          {newStock !== null && newStock >= 0
            ? `New stock: ${formatNumber(newStock)}`
            : 'Orders take stock automatically, and cancelled orders put it back.'}
        </p>
      </form>
    </section>
  )
}
