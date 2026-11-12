import { Search } from 'lucide-react'
import type { FormEvent } from 'react'
import { useLocation, useNavigate, useSearchParams } from 'react-router'
import { Input } from '@/components/ui/input'
import { cn } from '@/lib/utils'

/** Searches the whole catalog by product name. */
export function SearchBox({ className }: { className?: string }) {
  const navigate = useNavigate()
  const location = useLocation()
  const [searchParams] = useSearchParams()
  const current = location.pathname === '/products' ? (searchParams.get('search') ?? '') : ''

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const search = String(new FormData(event.currentTarget).get('search') ?? '').trim()
    void navigate(search ? `/products?search=${encodeURIComponent(search)}` : '/products')
  }

  return (
    <form role="search" onSubmit={handleSubmit} className={cn('relative', className)}>
      <Search
        aria-hidden
        className="pointer-events-none absolute top-1/2 left-3.5 size-4 -translate-y-1/2 text-muted-foreground"
      />
      {/* The key resets the text when the search in the address changes, for example with the back button. */}
      <Input
        key={current}
        type="search"
        name="search"
        defaultValue={current}
        placeholder="Search products"
        aria-label="Search products"
        maxLength={100}
        className="h-10 rounded-full bg-card pl-10"
      />
    </form>
  )
}
