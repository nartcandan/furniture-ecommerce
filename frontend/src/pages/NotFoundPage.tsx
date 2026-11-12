import { Link } from 'react-router'
import { Button } from '@/components/ui/button'

export function NotFoundPage() {
  return (
    <section className="py-16 text-center">
      <p className="text-sm font-medium tracking-wide text-highlight uppercase">404</p>
      <h1 className="mt-3 text-4xl font-semibold">We could not find that page</h1>
      <p className="mt-3 text-muted-foreground">It may have been moved, or the address may be mistyped.</p>
      <Button asChild size="lg" className="mt-8 h-11 px-6 text-base">
        <Link to="/">Back to the store</Link>
      </Button>
    </section>
  )
}
