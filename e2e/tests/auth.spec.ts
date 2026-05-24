/**
 * auth.spec.ts — Authentication flow E2E tests
 *
 * Covers:
 *  - / redirects to /login when unauthenticated
 *  - Register new user → /dashboard
 *  - Login with valid credentials → /dashboard
 *  - Invalid login shows error message
 *  - Logout returns to /login
 */
import { test, expect } from '@playwright/test'
import { uid, apiRegister, uiRegister, uiLogin } from './helpers'

// ── Redirect ──────────────────────────────────────────────────────────────────

test('unauthenticated / redirects to /login', async ({ page }) => {
  await page.goto('/')
  // Home is public; dashboard is protected.  Verify dashboard redirects.
  await page.goto('/dashboard')
  await expect(page).toHaveURL(/\/login/)
})

// ── Register ──────────────────────────────────────────────────────────────────

test('register new user navigates to dashboard', async ({ page }) => {
  const username = uid('reg')
  const email    = `${username}@test.invalid`
  await uiRegister(page, { username, email, password: 'password123' })
  await expect(page.getByText(/quizzes/i)).toBeVisible()
})

test('register form is accessible from login page', async ({ page }) => {
  await page.goto('/login')
  await page.getByRole('link', { name: /create an account|register|sign up/i }).click()
  await expect(page).toHaveURL(/\/register/)
  await expect(page.locator('#reg-username')).toBeVisible()
})

test('register: passwords do not match shows error', async ({ page }) => {
  await page.goto('/register')
  await page.locator('#reg-username').fill(uid('mismatch'))
  await page.locator('#reg-email').fill(`mismatch_${Date.now()}@test.invalid`)
  await page.locator('#reg-password').fill('password123')
  await page.locator('#reg-confirm').fill('different456')
  await page.getByRole('button', { name: /create account/i }).click()
  await expect(page.getByText(/passwords do not match/i)).toBeVisible()
})

test('register: duplicate email shows conflict error', async ({ page, request }) => {
  // Seed a user via API
  const u = await apiRegister(request)

  await page.goto('/register')
  await page.locator('#reg-username').fill(uid('dup'))
  await page.locator('#reg-email').fill(u.email)          // same email
  await page.locator('#reg-password').fill('password123')
  await page.locator('#reg-confirm').fill('password123')
  await page.getByRole('button', { name: /create account/i }).click()
  await expect(page.getByText(/already exists|taken/i)).toBeVisible()
})

// ── Login ─────────────────────────────────────────────────────────────────────

test('login with valid credentials navigates to dashboard', async ({ page, request }) => {
  const u = await apiRegister(request)
  await uiLogin(page, u.email)
  await expect(page.getByText(/quizzes/i)).toBeVisible()
})

test('login with wrong password shows error', async ({ page, request }) => {
  const u = await apiRegister(request)
  await page.goto('/login')
  await page.locator('#login-email').fill(u.email)
  await page.locator('#login-password').fill('wrongpassword')
  await page.getByRole('button', { name: /log in/i }).click()
  await expect(page.getByText(/invalid email or password/i)).toBeVisible()
  await expect(page).not.toHaveURL(/\/dashboard/)
})

test('login with unknown email shows error', async ({ page }) => {
  await page.goto('/login')
  await page.locator('#login-email').fill('nobody_at_all@test.invalid')
  await page.locator('#login-password').fill('password123')
  await page.getByRole('button', { name: /log in/i }).click()
  await expect(page.getByText(/invalid email or password/i)).toBeVisible()
})

// ── Logout ────────────────────────────────────────────────────────────────────

test('logout clears session and redirects', async ({ page, request }) => {
  const u = await apiRegister(request)
  await uiLogin(page, u.email)

  // Find and click the logout button (may be in a nav/menu)
  const logoutBtn = page.getByRole('button', { name: /log out|logout|sign out/i })
  await logoutBtn.click()

  // Should no longer be on /dashboard
  await expect(page).not.toHaveURL(/\/dashboard/)

  // Trying to go to /dashboard should redirect to /login
  await page.goto('/dashboard')
  await expect(page).toHaveURL(/\/login/)
})
