import { useState, type FormEvent } from 'react'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'

interface PriceFilterProps {
  minPrice?: number
  maxPrice?: number
  onApply: (minPrice: number | undefined, maxPrice: number | undefined) => void
}

function parse(value: string): number | undefined | null {
  if (value.trim() === '') {
    return undefined
  }
  const price = Number(value.replace(',', '.'))
  return Number.isFinite(price) && price >= 0 ? price : null
}

/** The page gives this a `key` made of the applied prices, so it starts again when they change. */
export function PriceFilter({ minPrice, maxPrice, onApply }: PriceFilterProps) {
  const [min, setMin] = useState(minPrice?.toString() ?? '')
  const [max, setMax] = useState(maxPrice?.toString() ?? '')
  const [error, setError] = useState<string>()

  function handleSubmit(event: FormEvent) {
    event.preventDefault()
    const parsedMin = parse(min)
    const parsedMax = parse(max)

    if (parsedMin === null || parsedMax === null) {
      setError('Enter prices as numbers.')
      return
    }
    if (parsedMin !== undefined && parsedMax !== undefined && parsedMax < parsedMin) {
      setError('The highest price cannot be below the lowest.')
      return
    }
    setError(undefined)
    onApply(parsedMin, parsedMax)
  }

  return (
    <form onSubmit={handleSubmit} noValidate>
      <div className="flex items-center gap-2">
        <Input
          inputMode="decimal"
          placeholder="Min"
          aria-label="Lowest price in lira"
          aria-invalid={error ? true : undefined}
          value={min}
          onChange={(event) => setMin(event.target.value)}
          className="h-10 bg-card"
        />
        <span aria-hidden className="text-muted-foreground">
          –
        </span>
        <Input
          inputMode="decimal"
          placeholder="Max"
          aria-label="Highest price in lira"
          aria-invalid={error ? true : undefined}
          value={max}
          onChange={(event) => setMax(event.target.value)}
          className="h-10 bg-card"
        />
      </div>
      {error && (
        <p role="alert" className="mt-2 text-sm text-destructive">
          {error}
        </p>
      )}
      <Button type="submit" variant="outline" size="lg" className="mt-3 h-10 w-full">
        Apply
      </Button>
    </form>
  )
}
