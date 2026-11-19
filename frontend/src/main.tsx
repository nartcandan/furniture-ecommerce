import { QueryClientProvider } from '@tanstack/react-query'
import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { createBrowserRouter, Navigate, RouterProvider } from 'react-router'
import { AdminLayout } from '@/components/AdminLayout'
import { Layout } from '@/components/Layout'
import { Toaster } from '@/components/ui/sonner'
import { GuestOnly, RequireAdmin, RequireAuth } from '@/features/auth/RouteGuards'
import { queryClient } from '@/lib/queryClient'
import { AccountPage } from '@/pages/AccountPage'
import { AdminCategoriesPage } from '@/pages/admin/AdminCategoriesPage'
import { AdminNewProductPage } from '@/pages/admin/AdminNewProductPage'
import { AdminOrderPage } from '@/pages/admin/AdminOrderPage'
import { AdminOrdersPage } from '@/pages/admin/AdminOrdersPage'
import { AdminProductPage } from '@/pages/admin/AdminProductPage'
import { AdminProductsPage } from '@/pages/admin/AdminProductsPage'
import { CartPage } from '@/pages/CartPage'
import { CheckoutPage } from '@/pages/CheckoutPage'
import { HomePage } from '@/pages/HomePage'
import { LoginPage } from '@/pages/LoginPage'
import { NotFoundPage } from '@/pages/NotFoundPage'
import { OrderPage } from '@/pages/OrderPage'
import { OrdersPage } from '@/pages/OrdersPage'
import { ProductDetailPage } from '@/pages/ProductDetailPage'
import { ProductsPage } from '@/pages/ProductsPage'
import { RegisterPage } from '@/pages/RegisterPage'
import './index.css'

const router = createBrowserRouter([
  {
    element: <Layout />,
    children: [
      { path: '/', element: <HomePage /> },
      { path: '/products', element: <ProductsPage /> },
      { path: '/products/:slug', element: <ProductDetailPage /> },
      {
        element: <GuestOnly />,
        children: [
          { path: '/login', element: <LoginPage /> },
          { path: '/register', element: <RegisterPage /> },
        ],
      },
      {
        element: <RequireAuth />,
        children: [
          { path: '/account', element: <AccountPage /> },
          { path: '/cart', element: <CartPage /> },
          { path: '/checkout', element: <CheckoutPage /> },
          { path: '/orders', element: <OrdersPage /> },
          { path: '/orders/:id', element: <OrderPage /> },
        ],
      },
      { path: '*', element: <NotFoundPage /> },
    ],
  },
  {
    element: <RequireAdmin />,
    children: [
      {
        path: '/admin',
        element: <AdminLayout />,
        children: [
          { index: true, element: <Navigate to="/admin/orders" replace /> },
          { path: 'orders', element: <AdminOrdersPage /> },
          { path: 'orders/:id', element: <AdminOrderPage /> },
          { path: 'categories', element: <AdminCategoriesPage /> },
          { path: 'products', element: <AdminProductsPage /> },
          { path: 'products/new', element: <AdminNewProductPage /> },
          { path: 'products/:id', element: <AdminProductPage /> },
        ],
      },
    ],
  },
])

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <QueryClientProvider client={queryClient}>
      <RouterProvider router={router} />
      <Toaster position="bottom-right" />
    </QueryClientProvider>
  </StrictMode>,
)
