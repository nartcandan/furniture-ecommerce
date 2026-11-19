import { RotateCw } from 'lucide-react'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { cn } from '@/lib/utils'

interface RetryableQuery {
  error: Error | null
  refetch: () => unknown
  isFetching: boolean
}

interface QueryErrorProps {
  title: string
  /** The failed query. Several can be passed when the page needs all of them. */
  query: RetryableQuery | RetryableQuery[]
  className?: string
}

/**
 * A request that failed, with a way to try again. Most failures pass: the connection drops, or the API is
 * waking up. A reload would also work, but would lose what is on the page.
 */
export function QueryError({ title, query, className }: QueryErrorProps) {
  const queries = Array.isArray(query) ? query : [query]
  const failed = queries.filter((item) => item.error)
  const retrying = queries.some((item) => item.isFetching)

  return (
    <Alert variant="destructive" className={cn('motion-safe:animate-in motion-safe:fade-in', className)}>
      <AlertTitle>{title}</AlertTitle>
      <AlertDescription>
        <p className="mb-0!">{failed[0]?.error?.message}</p>
        <Button
          variant="outline"
          size="sm"
          className="mt-2.5 text-foreground"
          disabled={retrying}
          onClick={() => failed.forEach((item) => item.refetch())}
        >
          <RotateCw aria-hidden className={cn(retrying && 'motion-safe:animate-spin')} />
          {retrying ? 'Trying again…' : 'Try again'}
        </Button>
      </AlertDescription>
    </Alert>
  )
}
