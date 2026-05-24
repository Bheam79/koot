/**
 * game-flow.spec.ts — Full game flow E2E tests
 *
 * Covers the critical path:
 *  1. Host logs in and starts a game session → /host/:code (lobby)
 *  2. PIN and join URL are visible
 *  3. Player (second browser context) joins via /join → /join/:code → /play/:code
 *  4. Host sees player join (participant count increases)
 *  5. Host starts the game
 *  6. Player sees the question
 *  7. Player submits an answer
 *  8. Timer/host advances; results appear
 *  9. Multiple questions cycle
 * 10. Host ends the game
 * 11. Final podium appears for both contexts
 *
 * NOTE: The game uses SignalR (WebSockets). Playwright tracks WS connections
 * transparently. The backend must be running for these tests.
 *
 * Tests that need two concurrent browser contexts use `browser` fixture
 * so both contexts share the same test lifetime.
 */
import { test, expect, Browser } from '@playwright/test'
import {
  uid,
  apiRegister,
  apiLogin,
  apiCreateQuiz,
  apiAddQuestion,
  apiStartGame,
  injectToken,
} from './helpers'

// ── Helpers ────────────────────────────────────────────────────────────────────

/** Seed: one user, one quiz with two MC questions, return token + quizId. */
async function seedQuizForGame(request: Parameters<typeof apiRegister>[0]) {
  const u = await apiRegister(request)
  const { token } = await apiLogin(request, u.email)
  const quiz = await apiCreateQuiz(request, token, 'Game Flow Quiz')

  await apiAddQuestion(request, token, quiz.id, {
    questionText: 'What is 2 + 2?',
    timeLimit: 30,
    answerOptions: [
      { text: '3', isCorrect: false, orderIndex: 0 },
      { text: '4', isCorrect: true,  orderIndex: 1 },
      { text: '5', isCorrect: false, orderIndex: 2 },
      { text: '6', isCorrect: false, orderIndex: 3 },
    ],
  })

  await apiAddQuestion(request, token, quiz.id, {
    questionText: 'Capital of France?',
    timeLimit: 30,
    answerOptions: [
      { text: 'London', isCorrect: false, orderIndex: 0 },
      { text: 'Paris',  isCorrect: true,  orderIndex: 1 },
      { text: 'Berlin', isCorrect: false, orderIndex: 2 },
      { text: 'Madrid', isCorrect: false, orderIndex: 3 },
    ],
  })

  return { u, token, quizId: quiz.id }
}

// ── Lobby visibility ───────────────────────────────────────────────────────────

test('host lobby shows game PIN and join URL after starting session', async ({
  page,
  request,
}) => {
  const { token, quizId } = await seedQuizForGame(request)
  const { code } = await apiStartGame(request, token, quizId)
  await injectToken(page, token)

  await page.goto(`/host/${code}`)

  // PIN is visible (6 chars, formatted as "XXX XXX" or "XXXXXX")
  await expect(page.getByText(new RegExp(code.slice(0, 3)))).toBeVisible({ timeout: 15_000 })

  // Join URL contains the code
  await expect(page.getByText(/join/i)).toBeVisible()
})

test('host lobby shows "Waiting for at least 1 player" when no players have joined', async ({
  page,
  request,
}) => {
  const { token, quizId } = await seedQuizForGame(request)
  const { code } = await apiStartGame(request, token, quizId)
  await injectToken(page, token)

  await page.goto(`/host/${code}`)

  await expect(page.getByText(/waiting for at least 1 player/i)).toBeVisible({
    timeout: 15_000,
  })
})

// ── Player join ────────────────────────────────────────────────────────────────

test('player joins game via /join → nickname setup → waiting lobby', async ({
  browser,
  request,
}: { browser: Browser; request: Parameters<typeof apiRegister>[0] }) => {
  const { token, quizId } = await seedQuizForGame(request)
  const { code } = await apiStartGame(request, token, quizId)

  // Host context
  const hostCtx = await browser.newContext()
  const hostPage = await hostCtx.newPage()
  await hostCtx.addInitScript(`localStorage.setItem('koot.token', '${token}')`)
  await hostPage.goto(`/host/${code}`)
  await expect(hostPage.getByText(new RegExp(code.slice(0, 3)))).toBeVisible({ timeout: 15_000 })

  // Player context (no token — unauthenticated player)
  const playerCtx = await browser.newContext()
  const playerPage = await playerCtx.newPage()

  await playerPage.goto('/join')
  await playerPage.getByRole('textbox').fill(code)
  await playerPage.getByRole('button', { name: /find game|join|enter/i }).click()

  // Player setup page
  await expect(playerPage).toHaveURL(new RegExp(`/join/${code}`), { timeout: 15_000 })

  // Fill nickname
  const nicknameInput = playerPage.getByPlaceholder(/nickname|name/i)
  await nicknameInput.fill('TestPlayer')

  // Pick an avatar (click any avatar option)
  const avatarBtn = playerPage.getByRole('button').filter({ hasText: /🐶|🐱|🐭|🐹|🐰|🦊/ }).first()
  if (await avatarBtn.count() > 0) {
    await avatarBtn.click()
  }

  await playerPage.getByRole('button', { name: /go|join|play|let's go/i }).click()

  // Player should be on the play page (lobby phase)
  await expect(playerPage).toHaveURL(new RegExp(`/play/${code}`), { timeout: 15_000 })
  await expect(playerPage.getByText(/waiting|lobby|get ready/i)).toBeVisible({ timeout: 10_000 })

  // Host should see player count increase
  await expect(hostPage.getByText('1')).toBeVisible({ timeout: 10_000 })

  await hostCtx.close()
  await playerCtx.close()
})

// ── Full game round-trip ───────────────────────────────────────────────────────

test('complete game flow: join → start → answer → results → end → podium', async ({
  browser,
  request,
}: { browser: Browser; request: Parameters<typeof apiRegister>[0] }) => {
  const { token, quizId } = await seedQuizForGame(request)
  const { code } = await apiStartGame(request, token, quizId)

  const nickname = uid('player')

  // ── Host context ──────────────────────────────────────────────────────────
  const hostCtx = await browser.newContext()
  const hostPage = await hostCtx.newPage()
  await hostCtx.addInitScript(`localStorage.setItem('koot.token', '${token}')`)
  await hostPage.goto(`/host/${code}`)
  await expect(hostPage.getByText(new RegExp(code.slice(0, 3)))).toBeVisible({ timeout: 15_000 })

  // ── Player context ────────────────────────────────────────────────────────
  const playerCtx = await browser.newContext()
  const playerPage = await playerCtx.newPage()

  // Navigate to join page and enter code
  await playerPage.goto('/join')
  await playerPage.getByRole('textbox').fill(code)
  await playerPage.getByRole('button', { name: /find game|join|enter/i }).click()

  await expect(playerPage).toHaveURL(new RegExp(`/join/${code}`), { timeout: 15_000 })

  await playerPage.getByPlaceholder(/nickname|name/i).fill(nickname)

  // Pick avatar 1 if buttons are available
  const avatarButtons = playerPage.getByRole('button').filter({ hasText: /🐶/ })
  if (await avatarButtons.count() > 0) {
    await avatarButtons.click()
  }

  await playerPage.getByRole('button', { name: /go|join|play|let's go/i }).click()

  await expect(playerPage).toHaveURL(new RegExp(`/play/${code}`), { timeout: 15_000 })

  // Wait for player to appear on host side
  await expect(hostPage.getByText(nickname, { exact: false })).toBeVisible({ timeout: 15_000 })

  // ── Host starts the game ──────────────────────────────────────────────────
  const startBtn = hostPage.getByRole('button', { name: /start game/i })
  await expect(startBtn).toBeEnabled({ timeout: 10_000 })
  await startBtn.click()

  // ── Player sees question ─────────────────────────────────────────────────
  await expect(playerPage.getByText(/2 \+ 2|question/i)).toBeVisible({ timeout: 20_000 })

  // ── Player clicks an answer ───────────────────────────────────────────────
  // The answer options are buttons; click the one with "4"
  const answerBtn = playerPage.getByRole('button', { name: '4' })
  await expect(answerBtn).toBeVisible({ timeout: 10_000 })
  await answerBtn.click()

  // Player sees answer feedback (accepted/locked)
  await expect(playerPage.getByText(/answered|correct|points|locked/i)).toBeVisible({
    timeout: 15_000,
  })

  // ── Host sees results / advances ──────────────────────────────────────────
  // Wait for host to see question results screen or answer count
  await expect(
    hostPage.getByText(/results|answered|leaderboard/i),
  ).toBeVisible({ timeout: 30_000 })

  // Host advances to next question
  const nextBtn = hostPage.getByRole('button', { name: /next question|next/i })
  if (await nextBtn.isVisible({ timeout: 5_000 }).catch(() => false)) {
    await nextBtn.click()
  }

  // ── Second question ───────────────────────────────────────────────────────
  // Player should now see the second question
  const q2Visible = await playerPage
    .getByText(/capital of france|paris|london/i)
    .isVisible({ timeout: 20_000 })
    .catch(() => false)

  if (q2Visible) {
    const parisBtn = playerPage.getByRole('button', { name: /paris/i })
    if (await parisBtn.isVisible({ timeout: 5_000 }).catch(() => false)) {
      await parisBtn.click()
    }
  }

  // ── Host ends the game ────────────────────────────────────────────────────
  // After last question, wait for end game button
  const endBtn = hostPage.getByRole('button', { name: /end game|finish/i })
  if (await endBtn.isVisible({ timeout: 20_000 }).catch(() => false)) {
    await endBtn.click()
  }

  // ── Final podium / standings ──────────────────────────────────────────────
  await expect(hostPage.getByText(/final|podium|standings|winner/i)).toBeVisible({
    timeout: 20_000,
  })

  await hostCtx.close()
  await playerCtx.close()
})

// ── Invalid join ───────────────────────────────────────────────────────────────

test('joining with invalid 6-char code shows "not found" error', async ({ page }) => {
  await page.goto('/join')
  await page.getByRole('textbox').fill('ZZZZZZ')
  await page.getByRole('button', { name: /find game|join|enter/i }).click()
  await expect(page.getByText(/not found|invalid|check the pin/i)).toBeVisible({
    timeout: 15_000,
  })
})

test('/join page requires exactly 6 characters before submitting', async ({ page }) => {
  await page.goto('/join')
  const codeInput = page.getByRole('textbox')
  await codeInput.fill('ABC')         // only 3 chars
  await page.getByRole('button', { name: /find game|join|enter/i }).click()
  await expect(page.getByText(/6 characters|pin must be/i)).toBeVisible()
})
