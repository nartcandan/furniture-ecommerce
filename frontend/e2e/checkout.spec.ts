import { expect, test, type Page } from '@playwright/test'
import {
  createProduct,
  getAdminProduct,
  registerCustomer,
  signInAsAdmin,
  signInBrowser,
  type Product,
} from './support/api.ts'

async function addToCartFromProductPage(page: Page, product: Product, quantity: number) {
  await page.goto(`/products/${product.slug}`)
  await expect(page.getByRole('heading', { name: product.name })).toBeVisible()
  for (let added = 1; added < quantity; added++) {
    await page.getByRole('button', { name: 'Increase quantity' }).click()
  }
  await page.getByRole('button', { name: 'Add to cart' }).click()
  await expect(page.getByRole('link', { name: `Cart, ${quantity} ${quantity === 1 ? 'item' : 'items'}` })).toBeVisible()
}

/** Fills the address form that checkout shows to a customer without saved addresses. */
async function addFirstAddress(page: Page) {
  await page.getByLabel('Address name').fill('Home')
  await page.getByLabel('Phone').fill('0532 123 45 67')
  await page.getByLabel('Province').click()
  await page.getByRole('option', { name: 'İstanbul', exact: true }).click()
  await page.getByLabel('District').click()
  await page.getByRole('option', { name: 'Kadıköy', exact: true }).click()
  await page.getByLabel('Address', { exact: true }).fill('Moda Cad. No: 10')
  await page.getByRole('button', { name: 'Save address' }).click()
  await expect(page.getByText('Moda Cad. No: 10')).toBeVisible()
}

test('a customer buys a product, pays, and cancels on the same day with a refund', async ({ page }) => {
  const admin = await signInAsAdmin()
  const product = await createProduct(admin, { price: 1250.5, stock: 5 })
  await signInBrowser(page, await registerCustomer())

  await addToCartFromProductPage(page, product, 2)

  await page.getByRole('link', { name: /^Cart, / }).click()
  await expect(page.getByText('₺2.501,00').first()).toBeVisible()
  await page.getByRole('link', { name: 'Checkout' }).click()

  await addFirstAddress(page)
  await page.getByRole('button', { name: 'Pay ₺2.501,00' }).click()

  // The test payment page stands where iyzico's would.
  await expect(page.getByRole('heading', { name: 'Test payment' })).toBeVisible()
  await expect(page.getByText('₺2.501,00')).toBeVisible()
  await page.getByRole('button', { name: 'Pay' }).click()

  await expect(page).toHaveURL(/\/orders\/[0-9a-f-]+\?payment=success$/)
  await expect(page.getByText('Thank you, your order is confirmed')).toBeVisible()
  expect((await getAdminProduct(admin, product.id)).stockQuantity).toBe(3)

  // A paid order is emptied from the cart.
  await expect(page.getByRole('link', { name: 'Cart', exact: true })).toBeVisible()

  await page.getByRole('button', { name: 'Cancel order' }).click()
  const dialog = page.getByRole('dialog')
  await expect(dialog).toContainText('₺2.501,00 is refunded to your card')
  await dialog.getByRole('button', { name: 'Cancel order' }).click()

  await expect(page.getByText('This order was cancelled')).toBeVisible()
  await expect(page.getByText('Your payment was refunded in full.')).toBeVisible()
  expect((await getAdminProduct(admin, product.id)).stockQuantity).toBe(5)
})

test('a declined card leaves the order unpaid and the items in the cart', async ({ page }) => {
  const admin = await signInAsAdmin()
  const product = await createProduct(admin, { stock: 3 })
  await signInBrowser(page, await registerCustomer())

  await addToCartFromProductPage(page, product, 1)
  await page.goto('/checkout')
  await addFirstAddress(page)
  await page.getByRole('button', { name: /^Pay / }).click()
  await page.getByRole('button', { name: 'Decline' }).click()

  await expect(page).toHaveURL(/\?payment=failed$/)
  await expect(page.getByText('The payment did not go through')).toBeVisible()
  // The stock taken for the order is given back, and the cart still holds the item.
  expect((await getAdminProduct(admin, product.id)).stockQuantity).toBe(3)
  await expect(page.getByRole('link', { name: 'Cart, 1 item' })).toBeVisible()
})

test('a customer who leaves the payment page can come back and pay', async ({ page }) => {
  const admin = await signInAsAdmin()
  const product = await createProduct(admin)
  await signInBrowser(page, await registerCustomer())

  await addToCartFromProductPage(page, product, 1)
  await page.goto('/checkout')
  await addFirstAddress(page)
  await page.getByRole('button', { name: /^Pay / }).click()
  await expect(page.getByRole('heading', { name: 'Test payment' })).toBeVisible()

  // Back to the store without paying: checkout now offers the waiting order instead of a new one.
  await page.goto('/checkout')
  await page.getByRole('link', { name: 'Continue to payment' }).click()
  await page.getByRole('button', { name: 'Pay' }).click()

  await expect(page.getByText('Thank you, your order is confirmed')).toBeVisible()
})
