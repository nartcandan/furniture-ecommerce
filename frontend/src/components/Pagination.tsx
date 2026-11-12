import { ChevronLeft, ChevronRight } from 'lucide-react'
import { Link } from 'react-router'
import { buttonVariants } from '@/components/ui/button'
import { cn } from '@/lib/utils'

interface PaginationProps {
  page: number
  totalPages: number
  /** The address of a page. Pages are links, so they work with the back button and can be shared. */
  pathFor: (page: number) => string
}

/** The pages to show: the first, the last and those around the current one. `null` stands for a gap. */
function visiblePages(page: number, totalPages: number): (number | null)[] {
  const pages: (number | null)[] = []

  for (let candidate = 1; candidate <= totalPages; candidate++) {
    const isEdge = candidate === 1 || candidate === totalPages
    if (isEdge || Math.abs(candidate - page) <= 1) {
      pages.push(candidate)
    } else if (pages[pages.length - 1] !== null) {
      pages.push(null)
    }
  }
  return pages
}

const itemClass = 'h-10 min-w-10 px-3 text-sm'

export function Pagination({ page, totalPages, pathFor }: PaginationProps) {
  if (totalPages <= 1) {
    return null
  }

  return (
    <nav aria-label="Pages" className="mt-12 flex flex-wrap items-center justify-center gap-1.5">
      {page > 1 && (
        <Link to={pathFor(page - 1)} className={cn(buttonVariants({ variant: 'ghost' }), itemClass)}>
          <ChevronLeft aria-hidden />
          Previous
        </Link>
      )}

      {visiblePages(page, totalPages).map((candidate, index) =>
        candidate === null ? (
          <span key={`gap-${index}`} aria-hidden className="px-1 text-muted-foreground">
            …
          </span>
        ) : (
          <Link
            key={candidate}
            to={pathFor(candidate)}
            aria-label={`Page ${candidate}`}
            aria-current={candidate === page ? 'page' : undefined}
            className={cn(
              buttonVariants({ variant: candidate === page ? 'default' : 'ghost' }),
              itemClass,
            )}
          >
            {candidate}
          </Link>
        ),
      )}

      {page < totalPages && (
        <Link to={pathFor(page + 1)} className={cn(buttonVariants({ variant: 'ghost' }), itemClass)}>
          Next
          <ChevronRight aria-hidden />
        </Link>
      )}
    </nav>
  )
}
