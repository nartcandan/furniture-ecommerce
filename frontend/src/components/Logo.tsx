import { Link } from 'react-router'
import { cn } from '@/lib/utils'

export function Logo({ className }: { className?: string }) {
  return (
    <Link
      to="/"
      className={cn('flex items-center gap-2.5 font-heading text-xl font-semibold tracking-tight', className)}
    >
      <svg viewBox="0 0 32 32" aria-hidden className="size-8 shrink-0">
        <rect width="32" height="32" rx="8" className="fill-primary" />
        <path
          d="M10 7v18M22 16v9M10 16h12M10 7h3"
          fill="none"
          strokeWidth="2.5"
          strokeLinecap="round"
          className="stroke-primary-foreground"
        />
      </svg>
      Furniture Store
    </Link>
  )
}
