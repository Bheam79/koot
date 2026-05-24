/**
 * Shared helpers for Koot E2E tests.
 *
 * API calls go directly to the backend (bypassing the SPA) so tests can
 * seed data quickly without going through the full UI.
 */
import { Page, APIRequestContext, expect } from '@playwright/test'

const API_BASE = process.env.PLAYWRIGHT_API_URL ?? 'http://localhost:5024'

// ── Unique-name generator ─────────────────────────────────────────────────────

let _seq = 0
export function uid(prefix = 'user'): string {
  return `${prefix}_${Date.now()}_${++_seq}`
}

// ── Direct API helpers (no auth header) ───────────────────────────────────────

export async function apiRegister(
  request: APIRequestContext,
  opts?: { username?: string; email?: string; password?: string },
): Promise<{ token: string; userId: number; username: string; email: string }> {
  const username = opts?.username ?? uid('u')
  const email    = opts?.email    ?? `${username}@test.invalid`
  const password = opts?.password ?? 'password123'

  const resp = await request.post(`${API_BASE}/api/auth/register`, {
    data: { username, email, password },
  })
  if (!resp.ok()) {
    throw new Error(`register failed: ${resp.status()} ${await resp.text()}`)
  }
  return resp.json()
}

export async function apiLogin(
  request: APIRequestContext,
  email: string,
  password = 'password123',
): Promise<{ token: string; userId: number; username: string }> {
  const resp = await request.post(`${API_BASE}/api/auth/login`, {
    data: { email, password },
  })
  if (!resp.ok()) {
    throw new Error(`login failed: ${resp.status()} ${await resp.text()}`)
  }
  return resp.json()
}

export async function apiCreateQuiz(
  request: APIRequestContext,
  token: string,
  title = 'E2E Quiz',
): Promise<{ id: number; title: string }> {
  const resp = await request.post(`${API_BASE}/api/quizzes`, {
    headers: { Authorization: `Bearer ${token}` },
    data: { title, description: 'Created by E2E test' },
  })
  if (!resp.ok()) {
    throw new Error(`createQuiz failed: ${resp.status()} ${await resp.text()}`)
  }
  return resp.json()
}

export async function apiAddQuestion(
  request: APIRequestContext,
  token: string,
  quizId: number,
  overrides: Record<string, unknown> = {},
): Promise<{ id: number }> {
  const body = {
    type: 0,              // MultipleChoice
    questionText: 'What is 2 + 2?',
    timeLimit: 20,
    points: 1000,
    answerOptions: [
      { text: '3', isCorrect: false, orderIndex: 0 },
      { text: '4', isCorrect: true,  orderIndex: 1 },
      { text: '5', isCorrect: false, orderIndex: 2 },
      { text: '6', isCorrect: false, orderIndex: 3 },
    ],
    ...overrides,
  }
  const resp = await request.post(`${API_BASE}/api/quizzes/${quizId}/questions`, {
    headers: { Authorization: `Bearer ${token}` },
    data: body,
  })
  if (!resp.ok()) {
    throw new Error(`addQuestion failed: ${resp.status()} ${await resp.text()}`)
  }
  return resp.json()
}

export async function apiStartGame(
  request: APIRequestContext,
  token: string,
  quizId: number,
): Promise<{ code: string; sessionId: number }> {
  const resp = await request.post(`${API_BASE}/api/games/start`, {
    headers: { Authorization: `Bearer ${token}` },
    data: { quizId },
  })
  if (!resp.ok()) {
    throw new Error(`startGame failed: ${resp.status()} ${await resp.text()}`)
  }
  return resp.json()
}

// ── UI helpers ────────────────────────────────────────────────────────────────

/**
 * Inject a JWT into localStorage so the app treats the browser as logged in.
 * Call this before navigating to any auth-protected page.
 */
export async function injectToken(page: Page, token: string): Promise<void> {
  await page.addInitScript((t) => {
    localStorage.setItem('koot.token', t)
  }, token)
}

/**
 * Register through the UI and end up on /dashboard.
 */
export async function uiRegister(
  page: Page,
  opts: { username: string; email: string; password: string },
): Promise<void> {
  await page.goto('/register')
  await page.locator('#reg-username').fill(opts.username)
  await page.locator('#reg-email').fill(opts.email)
  await page.locator('#reg-password').fill(opts.password)
  await page.locator('#reg-confirm').fill(opts.password)
  await page.getByRole('button', { name: /create account/i }).click()
  await expect(page).toHaveURL(/\/dashboard/)
}

/**
 * Log in through the UI and end up on /dashboard.
 */
export async function uiLogin(
  page: Page,
  email: string,
  password = 'password123',
): Promise<void> {
  await page.goto('/login')
  await page.locator('#login-email').fill(email)
  await page.locator('#login-password').fill(password)
  await page.getByRole('button', { name: /log in/i }).click()
  await expect(page).toHaveURL(/\/dashboard/)
}
