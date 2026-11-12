import { Minus, Plus } from 'lucide-react'
import { cn } from '@/lib/utils'

interface QuantityStepperProps {
  value: number
  /** The stock. The plus button stops here. */
  max: number
  onChange: (value: number) => void
  disabled?: boolean
  /** Names the product for screen readers, for example "Oslo Sofa". */
  label: string
  className?: string
}

const buttonClass =
  'flex size-10 items-center justify-center rounded-full text-foreground transition-colors outline-none hover:bg-muted focus-visible:ring-3 focus-visible:ring-ring/50 disabled:pointer-events-none disabled:opacity-35'

export function QuantityStepper({ value, max, onChange, disabled, label, className }: QuantityStepperProps) {
  return (
    <div
      role="group"
      aria-label={`Quantity of ${label}`}
      className={cn('inline-flex items-center rounded-full border border-input bg-card', className)}
    >
      <button
        type="button"
        aria-label="Decrease quantity"
        disabled={disabled || value <= 1}
        onClick={() => onChange(value - 1)}
        className={buttonClass}
      >
        <Minus aria-hidden className="size-4" />
      </button>
      <span aria-live="polite" className="min-w-8 text-center text-sm font-medium tabular-nums">
        {value}
      </span>
      <button
        type="button"
        aria-label="Increase quantity"
        disabled={disabled || value >= max}
        onClick={() => onChange(value + 1)}
        className={buttonClass}
      >
        <Plus aria-hidden className="size-4" />
      </button>
    </div>
  )
}
