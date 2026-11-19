import type { Page } from '@playwright/test'
import { E2E_API_URL } from '../../playwright.config.ts'

/**
 * Sets up test data through the API, which is faster and steadier than through the screens. Each test
 * creates its own data with unique names, so tests can run in parallel and in any order.
 */

/** The admin account the API creates in the E2E environment (appsettings.E2E.json). */
export const ADMIN = { email: 'admin@e2e.test', password: 'E2eAdmin123' }
export const CUSTOMER_PASSWORD = 'Password1'

export interface Auth {
  accessToken: string
  expiresAt: string
  user: { id: string; email: string; firstName: string; lastName: string; roles: string[] }
}

export interface Product {
  id: string
  name: string
  slug: string
  price: number
  stockQuantity: number
}

export interface Order {
  id: string
  status: string
  totalAmount: number
  paymentPageUrl: string | null
}

let counter = 0

/** A short value that no other test or run uses. */
export function unique(): string {
  counter += 1
  return `${Date.now().toString(36)}${process.pid.toString(36)}${counter}`
}

async function request<T>(path: string, init: { method?: string; body?: unknown; auth?: Auth } = {}): Promise<T> {
  const response = await fetch(`${E2E_API_URL}${path}`, {
    method: init.method ?? 'GET',
    headers: {
      'Content-Type': 'application/json',
      ...(init.auth ? { Authorization: `Bearer ${init.auth.accessToken}` } : {}),
    },
    body: init.body === undefined ? undefined : JSON.stringify(init.body),
  })
  const text = await response.text()
  if (!response.ok) {
    throw new Error(`${init.method ?? 'GET'} ${path} failed with ${response.status}: ${text}`)
  }
  return (text ? JSON.parse(text) : undefined) as T
}

export function signInAsAdmin(): Promise<Auth> {
  return request<Auth>('/api/auth/login', { method: 'POST', body: ADMIN })
}

export function registerCustomer(firstName = 'Deniz'): Promise<Auth> {
  return request<Auth>('/api/auth/register', {
    method: 'POST',
    body: { email: `customer-${unique()}@e2e.test`, password: CUSTOMER_PASSWORD, firstName, lastName: 'Test' },
  })
}

/** An active category with one product in stock. */
export async function createProduct(
  admin: Auth,
  options: { price?: number; stock?: number; name?: string } = {},
): Promise<Product> {
  const id = unique()
  const category = await request<{ id: string }>('/api/categories', {
    method: 'POST',
    auth: admin,
    body: { name: `Category ${id}`, description: null },
  })
  return request<Product>('/api/products', {
    method: 'POST',
    auth: admin,
    body: {
      categoryId: category.id,
      name: options.name ?? `Oak chair ${id}`,
      description: 'A sturdy chair made for the browser tests.',
      price: options.price ?? 1250.5,
      stockQuantity: options.stock ?? 5,
      widthCm: 45,
      depthCm: null,
      heightCm: null,
      weightKg: null,
      material: 'Oak',
      color: null,
    },
  })
}

export function getAdminProduct(admin: Auth, id: string): Promise<Product> {
  return request<Product>(`/api/admin/products/${id}`, { auth: admin })
}

export async function createAddress(customer: Auth): Promise<{ id: string }> {
  return request<{ id: string }>('/api/addresses', {
    method: 'POST',
    auth: customer,
    body: {
      title: 'Home',
      recipientFirstName: customer.user.firstName,
      recipientLastName: 'Test',
      phoneNumber: '05321234567',
      city: 'İstanbul',
      district: 'Kadıköy',
      addressLine: 'Moda Cad. No: 10',
      postalCode: '34710',
    },
  })
}

export function addToCart(customer: Auth, productId: string, quantity = 1): Promise<unknown> {
  return request('/api/cart/items', { method: 'POST', auth: customer, body: { productId, quantity } })
}

/** Places an order from the cart, like the "Pay" button, without opening the payment page. */
export async function placeOrder(customer: Auth, product: Product, quantity = 1): Promise<Order> {
  const address = await createAddress(customer)
  await addToCart(customer, product.id, quantity)
  return request<Order>('/api/orders', { method: 'POST', auth: customer, body: { addressId: address.id } })
}

/** Finishes the test payment page and returns the callback, the way the browser would. */
export async function finishPayment(order: Order, outcome: 'pay' | 'decline'): Promise<void> {
  if (!order.paymentPageUrl) throw new Error(`Order ${order.id} has no payment page.`)

  const form = { 'Content-Type': 'application/x-www-form-urlencoded' }
  const page = await fetch(order.paymentPageUrl, { method: 'POST', headers: form, body: `outcome=${outcome}` })
  const html = await page.text()
  const action = /action="([^"]+)"/.exec(html)?.[1]
  const token = /name="token" value="([^"]+)"/.exec(html)?.[1]
  if (!action || !token) throw new Error(`The test payment page answered ${page.status}: ${html}`)

  await fetch(action, { method: 'POST', headers: form, body: `token=${encodeURIComponent(token)}`, redirect: 'manual' })
}

export function getOrder(customer: Auth, id: string): Promise<Order> {
  return request<Order>(`/api/orders/${id}`, { auth: customer })
}

/** Signs the browser in by storing the session where the store keeps it, so tests skip the sign-in form. */
export async function signInBrowser(page: Page, auth: Auth): Promise<void> {
  const session = {
    state: { session: { token: auth.accessToken, expiresAt: auth.expiresAt, user: auth.user } },
    version: 0,
  }
  await page.goto('/')
  await page.evaluate((value) => localStorage.setItem('furniture-store.auth', value), JSON.stringify(session))
}
