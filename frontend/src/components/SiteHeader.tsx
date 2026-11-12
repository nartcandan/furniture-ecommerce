import { ChevronDown, LogOut, Package, ShoppingBag, User } from 'lucide-react'
import { Link, NavLink, useSearchParams } from 'react-router'
import { Logo } from '@/components/Logo'
import { SearchBox } from '@/components/SearchBox'
import { Button } from '@/components/ui/button'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { useAuthStore, useSession } from '@/features/auth/authStore'
import { useCart } from '@/features/cart/useCart'
import { categoryPath, useCategories } from '@/features/catalog/useCategories'
import { cn } from '@/lib/utils'

function AccountMenu() {
  const session = useSession()
  const clearSession = useAuthStore((state) => state.clearSession)

  if (!session) {
    return (
      <div className="flex items-center gap-1">
        <Button asChild variant="ghost" size="lg">
          <Link to="/login">Sign in</Link>
        </Button>
        <Button asChild size="lg" className="px-4">
          <Link to="/register">Register</Link>
        </Button>
      </div>
    )
  }

  const { firstName, lastName, email } = session.user

  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button variant="ghost" size="lg">
          <User aria-hidden />
          <span className="max-w-32 truncate">{firstName}</span>
          <ChevronDown aria-hidden className="text-muted-foreground" />
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end" className="w-56">
        <DropdownMenuLabel>
          <span className="block truncate text-sm font-medium text-foreground">
            {firstName} {lastName}
          </span>
          <span className="block truncate text-xs font-normal text-muted-foreground">{email}</span>
        </DropdownMenuLabel>
        <DropdownMenuSeparator />
        <DropdownMenuItem asChild>
          <Link to="/account">
            <User aria-hidden />
            My account
          </Link>
        </DropdownMenuItem>
        <DropdownMenuItem asChild>
          <Link to="/orders">
            <Package aria-hidden />
            My orders
          </Link>
        </DropdownMenuItem>
        <DropdownMenuItem onSelect={clearSession}>
          <LogOut aria-hidden />
          Sign out
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  )
}

function CartLink() {
  const count = useCart().data?.totalQuantity ?? 0

  return (
    <Button asChild variant="ghost" size="icon-lg" className="relative size-10">
      <Link to="/cart" aria-label={count > 0 ? `Cart, ${count} ${count === 1 ? 'item' : 'items'}` : 'Cart'}>
        <ShoppingBag aria-hidden className="size-5" />
        {count > 0 && (
          <span
            aria-hidden
            className="absolute -top-0.5 -right-0.5 flex h-5 min-w-5 items-center justify-center rounded-full bg-highlight px-1 text-[0.6875rem] leading-none font-semibold text-white tabular-nums"
          >
            {count > 99 ? '99+' : count}
          </span>
        )}
      </Link>
    </Button>
  )
}

const categoryLinkClass =
  'shrink-0 border-b-2 border-transparent py-3 text-sm text-muted-foreground transition-colors hover:text-foreground'
const activeCategoryLinkClass = 'border-primary font-medium text-foreground'

function CategoryNav() {
  const categories = useCategories()
  const [searchParams] = useSearchParams()
  const activeSlug = searchParams.get('category')

  return (
    <nav aria-label="Categories" className="border-t">
      <ul className="mx-auto flex max-w-6xl gap-6 overflow-x-auto px-4 [scrollbar-width:none]">
        <li className="flex">
          <NavLink
            to="/products"
            end
            className={({ isActive }) =>
              cn(categoryLinkClass, isActive && !activeSlug && activeCategoryLinkClass)
            }
          >
            All products
          </NavLink>
        </li>
        {categories.data?.map((category) => (
          <li key={category.id} className="flex">
            <Link
              to={categoryPath(category.slug)}
              aria-current={activeSlug === category.slug ? 'page' : undefined}
              className={cn(
                categoryLinkClass,
                activeSlug === category.slug && activeCategoryLinkClass,
              )}
            >
              {category.name}
            </Link>
          </li>
        ))}
      </ul>
    </nav>
  )
}

export function SiteHeader() {
  return (
    <header className="sticky top-0 z-40 border-b bg-background/90 backdrop-blur">
      <div className="mx-auto flex h-16 max-w-6xl items-center justify-between gap-4 px-4">
        <Logo />
        <SearchBox className="hidden max-w-md flex-1 md:block" />
        <div className="flex items-center gap-1">
          <AccountMenu />
          <CartLink />
        </div>
      </div>
      <div className="px-4 pb-3 md:hidden">
        <SearchBox />
      </div>
      <CategoryNav />
    </header>
  )
}
