/**
 * dashboard.spec.ts — Dashboard page E2E tests
 *
 * Covers:
 *  - Create quiz button navigates to /quiz/create
 *  - Empty state is shown when no quizzes exist
 *  - Quiz cards appear after seeding via API
 *  - Delete quiz removes it from the list
 */
import { test, expect } from '@playwright/test'
import { apiRegister, apiLogin, apiCreateQuiz, injectToken } from './helpers'

test.describe('Dashboard', () => {
  test('shows empty state for brand-new user', async ({ page, request }) => {
    const u = await apiRegister(request)
    const { token } = await apiLogin(request, u.email)
    await injectToken(page, token)

    await page.goto('/dashboard')

    await expect(page.getByText(/no quizzes yet/i)).toBeVisible()
  })

  test('"Create new quiz" link navigates to /quiz/create', async ({ page, request }) => {
    const u = await apiRegister(request)
    const { token } = await apiLogin(request, u.email)
    await injectToken(page, token)

    await page.goto('/dashboard')
    await page.getByRole('link', { name: /create new quiz/i }).first().click()

    await expect(page).toHaveURL(/\/quiz\/create/)
  })

  test('shows quiz card after seeding quiz via API', async ({ page, request }) => {
    const u = await apiRegister(request)
    const { token } = await apiLogin(request, u.email)
    await apiCreateQuiz(request, token, 'My E2E Quiz')
    await injectToken(page, token)

    await page.goto('/dashboard')

    await expect(page.getByText('My E2E Quiz')).toBeVisible()
  })

  test('shows multiple quiz cards for multiple quizzes', async ({ page, request }) => {
    const u = await apiRegister(request)
    const { token } = await apiLogin(request, u.email)
    await apiCreateQuiz(request, token, 'Alpha Quiz')
    await apiCreateQuiz(request, token, 'Beta Quiz')
    await injectToken(page, token)

    await page.goto('/dashboard')

    await expect(page.getByText('Alpha Quiz')).toBeVisible()
    await expect(page.getByText('Beta Quiz')).toBeVisible()
  })

  test('delete quiz removes it from dashboard', async ({ page, request }) => {
    const u = await apiRegister(request)
    const { token } = await apiLogin(request, u.email)
    await apiCreateQuiz(request, token, 'ToDelete Quiz')
    await injectToken(page, token)

    await page.goto('/dashboard')
    await expect(page.getByText('ToDelete Quiz')).toBeVisible()

    // Click Delete — browser confirm dialog; auto-accept it
    page.on('dialog', (d) => d.accept())
    await page.getByRole('button', { name: /delete/i }).first().click()

    await expect(page.getByText('ToDelete Quiz')).not.toBeVisible()
  })

  test('dashboard is not accessible when not logged in', async ({ page }) => {
    await page.goto('/dashboard')
    await expect(page).toHaveURL(/\/login/)
  })
})
