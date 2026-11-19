import { expect, test } from '@playwright/test'
import { CUSTOMER_PASSWORD, registerCustomer, unique } from './support/api.ts'

test('a new customer registers, signs out and signs in again', async ({ page }) => {
  const email = `new-${unique()}@e2e.test`

  await page.goto('/register')
  await page.getByLabel('First name').fill('Elif')
  await page.getByLabel('Last name').fill('Test')
  await page.getByLabel('Email').fill(email)
  await page.getByLabel('Password', { exact: true }).fill(CUSTOMER_PASSWORD)
  await page.getByLabel('Confirm password').fill(CUSTOMER_PASSWORD)
  await page.getByRole('button', { name: 'Create account' }).click()

  // Signed in: the account menu shows the first name.
  const accountMenu = page.getByRole('button', { name: 'Elif' })
  await expect(accountMenu).toBeVisible()

  await accountMenu.click()
  await page.getByRole('menuitem', { name: 'Sign out' }).click()
  await expect(page.getByRole('link', { name: 'Sign in' }).first()).toBeVisible()

  await page.goto('/login')
  await page.getByLabel('Email').fill(email)
  await page.getByLabel('Password').fill(CUSTOMER_PASSWORD)
  await page.getByRole('button', { name: 'Sign in' }).click()
  await expect(accountMenu).toBeVisible()
})

test('a wrong password is refused without saying whether the email exists', async ({ page }) => {
  const customer = await registerCustomer()

  await page.goto('/login')
  await page.getByLabel('Email').fill(customer.user.email)
  await page.getByLabel('Password').fill('WrongPassword1')
  await page.getByRole('button', { name: 'Sign in' }).click()

  await expect(page.getByRole('alert')).toBeVisible()
  await expect(page).toHaveURL(/\/login$/)
})

test('a guest who opens the cart signs in and comes back to it', async ({ page }) => {
  const customer = await registerCustomer('Ozan')

  await page.goto('/cart')
  await expect(page).toHaveURL(/\/login$/)

  await page.getByLabel('Email').fill(customer.user.email)
  await page.getByLabel('Password').fill(CUSTOMER_PASSWORD)
  await page.getByRole('button', { name: 'Sign in' }).click()

  await expect(page).toHaveURL(/\/cart$/)
  await expect(page.getByRole('heading', { name: 'Your cart is empty' })).toBeVisible()
})
