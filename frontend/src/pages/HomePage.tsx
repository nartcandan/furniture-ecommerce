import { useQuery } from '@tanstack/react-query'
import { getCategories } from '../api/categories'

export function HomePage() {
  const categories = useQuery({ queryKey: ['categories'], queryFn: getCategories })

  return (
    <section>
      <h1 className="text-2xl font-semibold">Categories</h1>

      {categories.isPending && <p className="mt-4 text-stone-500">Loading…</p>}

      {categories.isError && (
        <p role="alert" className="mt-4 text-red-700">
          {categories.error.message}
        </p>
      )}

      {categories.data?.length === 0 && <p className="mt-4 text-stone-500">No categories yet.</p>}

      {categories.data && categories.data.length > 0 && (
        <ul className="mt-4 grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {categories.data.map((category) => (
            <li key={category.id} className="rounded-lg border border-stone-200 bg-white p-4">
              <h2 className="font-medium">{category.name}</h2>
              {category.description && (
                <p className="mt-1 text-sm text-stone-600">{category.description}</p>
              )}
            </li>
          ))}
        </ul>
      )}
    </section>
  )
}
