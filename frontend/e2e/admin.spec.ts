import { expect, test } from '@playwright/test'
import {
  createProduct,
  finishPayment,
  getAdminProduct,
  getOrder,
  placeOrder,
  registerCustomer,
  signInAsAdmin,
  signInBrowser,
  unique,
} from './support/api.ts'

// A real 1×1 PNG. The API checks the first bytes of a file, not its name.
const PNG = Buffer.from(
  'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==',
  'base64',
)

const orderNumber = (id: string) => id.replaceAll('-', '').slice(-8).toUpperCase()

test('an admin adds a category and a product with an image, stocks it and takes it off the store', async ({
  page,
}) => {
  await signInBrowser(page, await signInAsAdmin())
  const id = unique()
  const categoryName = `Lighting ${id}`
  const productName = `Brass lamp ${id}`

  await page.goto('/admin/categories')
  await page.getByRole('button', { name: 'New category' }).click()
  await page.getByRole('dialog').getByLabel('Name').fill(categoryName)
  await page.getByRole('button', { name: 'Create category' }).click()
  await expect(page.getByRole('cell', { name: categoryName, exact: true })).toBeVisible()

  await page.getByRole('link', { name: 'Products' }).click()
  await page.getByRole('link', { name: 'New product' }).click()
  await page.getByLabel('Name').fill(productName)
  await page.getByLabel('Category').click()
  await page.getByRole('option', { name: categoryName }).click()
  await page.getByLabel('Price (₺)').fill('899,90')
  await page.getByLabel('Starting stock').fill('3')
  await page.getByRole('button', { name: 'Create product' }).click()

  await expect(page.getByRole('heading', { name: productName })).toBeVisible()
  const productId = page.url().split('/').pop()!

  // Images: a real one is stored, a fake one is refused with the API's reason.
  await page.locator('input[type=file]').setInputFiles([
    { name: 'lamp.png', mimeType: 'image/png', buffer: PNG },
    { name: 'fake.png', mimeType: 'image/png', buffer: Buffer.from('not an image') },
  ])
  await expect(page.getByText('fake.png: The file must be a JPEG, PNG or WebP image.')).toBeVisible()
  await expect(page.getByText('Main', { exact: true })).toBeVisible()

  await page.getByLabel('Add or take away').fill('+7')
  await expect(page.getByText('New stock: 10')).toBeVisible()
  await page.getByRole('button', { name: 'Apply' }).click()
  await expect(page.getByText('Stock is now 10')).toBeVisible()

  // The product is in the store, with its image.
  const storeLink = page.getByRole('link', { name: 'View in store' })
  const storePath = await storeLink.getAttribute('href')
  const store = await page.context().newPage()
  await store.goto(storePath!)
  await expect(store.getByRole('heading', { name: productName })).toBeVisible()
  await expect(store.getByText('₺899,90')).toBeVisible()
  await expect(store.locator('main img').first()).toHaveAttribute('src', /\/uploads\//)

  // Inactive: hidden from the store, kept in the panel.
  await page.getByRole('switch').click()
  await page.getByRole('button', { name: 'Save changes' }).click()
  await expect(page.getByText(`“${productName}” was saved`)).toBeVisible()
  await store.reload()
  await expect(store.getByText('We could not find that page')).toBeVisible()

  const admin = await signInAsAdmin()
  const saved = await getAdminProduct(admin, productId)
  expect(saved.stockQuantity).toBe(10)
  expect(saved.price).toBe(899.9)
})

test('an admin ships and delivers a paid order, and the customer sees it', async ({ page }) => {
  const admin = await signInAsAdmin()
  const customer = await registerCustomer()
  const order = await placeOrder(customer, await createProduct(admin))
  await finishPayment(order, 'pay')

  await signInBrowser(page, admin)
  await page.goto('/admin')
  await page.getByRole('link', { name: 'To ship' }).click()
  await page.getByRole('link', { name: `#${orderNumber(order.id)}` }).click()

  await page.getByRole('button', { name: 'Mark as shipped' }).click()
  await page.getByRole('dialog').getByRole('button', { name: 'Mark as shipped' }).click()
  await expect(page.getByText('The order is marked as shipped')).toBeVisible()

  await page.getByRole('button', { name: 'Mark as delivered' }).click()
  await page.getByRole('dialog').getByRole('button', { name: 'Mark as delivered' }).click()
  await expect(page.getByText('This order is complete.')).toBeVisible()

  expect((await getOrder(customer, order.id)).status).toBe('Delivered')
})

test('the panel is hidden from customers', async ({ page }) => {
  await signInBrowser(page, await registerCustomer())

  await page.goto('/admin/orders')

  await expect(page.getByText('We could not find that page')).toBeVisible()
  await expect(page.getByRole('table')).toHaveCount(0)
})
