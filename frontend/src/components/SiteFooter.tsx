import { Link } from 'react-router'
import { Logo } from '@/components/Logo'
import { categoryPath, useCategories } from '@/features/catalog/useCategories'

const linkClass = 'text-muted-foreground transition-colors hover:text-foreground'

export function SiteFooter() {
  const categories = useCategories()

  return (
    <footer className="mt-16 border-t bg-muted/60">
      <div className="mx-auto grid max-w-6xl gap-10 px-4 py-12 sm:grid-cols-2 lg:grid-cols-[2fr_1fr_1fr]">
        <div>
          <Logo />
          <p className="mt-4 max-w-sm text-sm text-muted-foreground">
            Home furniture and decoration, delivered free across Türkiye.
          </p>
        </div>

        <nav aria-label="Shop">
          <h2 className="font-sans text-sm font-semibold tracking-normal">Shop</h2>
          <ul className="mt-4 grid gap-2.5 text-sm">
            <li>
              <Link to="/products" className={linkClass}>
                All products
              </Link>
            </li>
            {categories.data?.slice(0, 6).map((category) => (
              <li key={category.id}>
                <Link to={categoryPath(category.slug)} className={linkClass}>
                  {category.name}
                </Link>
              </li>
            ))}
          </ul>
        </nav>

        <nav aria-label="Account">
          <h2 className="font-sans text-sm font-semibold tracking-normal">Account</h2>
          <ul className="mt-4 grid gap-2.5 text-sm">
            <li>
              <Link to="/account" className={linkClass}>
                My account
              </Link>
            </li>
            <li>
              <Link to="/login" className={linkClass}>
                Sign in
              </Link>
            </li>
            <li>
              <Link to="/register" className={linkClass}>
                Create an account
              </Link>
            </li>
          </ul>
        </nav>
      </div>

      <div className="border-t">
        <p className="mx-auto max-w-6xl px-4 py-5 text-xs text-muted-foreground">
          A demo store. Payments run in the iyzico sandbox, so no real money is taken and no order is
          delivered.
        </p>
      </div>
    </footer>
  )
}
