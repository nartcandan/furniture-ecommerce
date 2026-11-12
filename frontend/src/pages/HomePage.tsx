import { useQuery } from '@tanstack/react-query'
import { getCategories } from '@/api/categories'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Card, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'

export function HomePage() {
  const categories = useQuery({ queryKey: ['categories'], queryFn: getCategories })

  return (
    <section>
      <h1 className="font-heading text-2xl font-semibold">Categories</h1>

      {categories.isPending && (
        <div className="mt-4 grid gap-4 sm:grid-cols-2 lg:grid-cols-3" aria-label="Loading categories">
          {[0, 1, 2].map((index) => (
            <Skeleton key={index} className="h-24" />
          ))}
        </div>
      )}

      {categories.isError && (
        <Alert variant="destructive" className="mt-4">
          <AlertTitle>The categories could not be loaded</AlertTitle>
          <AlertDescription>{categories.error.message}</AlertDescription>
        </Alert>
      )}

      {categories.data?.length === 0 && (
        <p className="mt-4 text-muted-foreground">No categories yet.</p>
      )}

      {categories.data && categories.data.length > 0 && (
        <ul className="mt-4 grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {categories.data.map((category) => (
            <li key={category.id}>
              <Card className="h-full">
                <CardHeader>
                  <CardTitle>{category.name}</CardTitle>
                  {category.description && (
                    <CardDescription>{category.description}</CardDescription>
                  )}
                </CardHeader>
              </Card>
            </li>
          ))}
        </ul>
      )}
    </section>
  )
}
