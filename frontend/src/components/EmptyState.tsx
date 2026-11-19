import type { LucideIcon } from 'lucide-react'
import type { ReactNode } from 'react'
import { cn } from '@/lib/utils'

interface EmptyStateProps {
  icon: LucideIcon
  title: string
  description?: ReactNode
  /** What to do next, usually a link. */
  action?: ReactNode
  /**
   * `page` when the whole page has nothing to show (an empty cart), with a larger heading.
   * `section` for one part of a page (no saved addresses), in a dashed box.
   */
  variant?: 'page' | 'section'
  className?: string
}

/** Shown in place of a list that has nothing in it. Says why, and what to do next. */
export function EmptyState({
  icon: Icon,
  title,
  description,
  action,
  variant = 'section',
  className,
}: EmptyStateProps) {
  const page = variant === 'page'

  return (
    <div
      className={cn(
        'flex flex-col items-center text-center motion-safe:animate-in motion-safe:fade-in motion-safe:duration-500',
        page ? 'py-16' : 'rounded-xl border border-dashed px-6 py-10',
        className,
      )}
    >
      {page ? (
        <span className="flex size-16 items-center justify-center rounded-full bg-secondary text-muted-foreground">
          <Icon aria-hidden className="size-7" strokeWidth={1.5} />
        </span>
      ) : (
        <Icon aria-hidden className="size-8 text-muted-foreground/60" strokeWidth={1.5} />
      )}
      {page ? (
        <h2 className="mt-5 text-2xl font-semibold">{title}</h2>
      ) : (
        <p className="mt-3 font-medium">{title}</p>
      )}
      {description && (
        <p className={cn('text-muted-foreground', page ? 'mt-2' : 'mt-1 text-sm')}>{description}</p>
      )}
      {action && <div className={page ? 'mt-8' : 'mt-4'}>{action}</div>}
    </div>
  )
}
