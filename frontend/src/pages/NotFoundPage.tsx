import { Link } from 'react-router'
import { Button } from '@/components/ui/button'

export function NotFoundPage() {
  return (
    <section>
      <h1 className="font-heading text-2xl font-semibold">Page not found</h1>
      <Button asChild variant="outline" className="mt-4">
        <Link to="/">Back to the store</Link>
      </Button>
    </section>
  )
}
