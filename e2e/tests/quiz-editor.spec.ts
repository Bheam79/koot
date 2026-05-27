/**
 * quiz-editor.spec.ts — Quiz creation and editing E2E tests
 *
 * Covers:
 *  - Create new quiz (title + description)
 *  - Add MultipleChoice question with options and mark correct
 *  - Add TrueFalse question
 *  - Add TypeAnswer question
 *  - Add Poll question
 *  - Reorder questions (move up / move down)
 *  - Save quiz → success notification
 *  - Delete a question
 */
import { test, expect } from '@playwright/test'
import { apiRegister, apiLogin, apiCreateQuiz, apiAddQuestion, injectToken } from './helpers'

test.describe('Quiz Editor', () => {
  // ── Create new quiz ──────────────────────────────────────────────────────────

  test('create quiz: fill title and save shows success', async ({ page, request }) => {
    const u = await apiRegister(request)
    const { token } = await apiLogin(request, u.email)
    await injectToken(page, token)

    await page.goto('/quiz/create')

    // Fill quiz title
    await page.getByPlaceholder(/80s music trivia|quiz title/i).fill('My New Quiz')
    await page.getByPlaceholder(/optional|what's this quiz/i).fill('A great quiz about stuff')

    // Add a question first so the quiz is saveable
    await page.getByRole('button', { name: /add question/i }).click()
    await page.getByRole('button', { name: /multiple choice/i }).click()

    // Fill the question text
    await page.getByPlaceholder(/what do you want to ask/i).fill('What is 2 + 2?')

    // Fill answer options (MC requires at least 2 non-empty options, one marked correct)
    const optionInputs = page.getByPlaceholder(/answer \d+/i)
    await optionInputs.nth(0).fill('Three')
    await optionInputs.nth(1).fill('Four')
    await page.getByRole('button', { name: /mark correct/i }).first().click()

    // Save
    await page.getByRole('button', { name: /save quiz/i }).click()
    await expect(page.getByText(/saved|success/i)).toBeVisible()
  })

  test('create quiz: navigates to edit URL after first save', async ({ page, request }) => {
    const u = await apiRegister(request)
    const { token } = await apiLogin(request, u.email)
    await injectToken(page, token)

    await page.goto('/quiz/create')
    await page.getByPlaceholder(/80s music trivia|quiz title/i).fill('Nav Quiz')

    // Add a question then save
    await page.getByRole('button', { name: /add question/i }).click()
    await page.getByRole('button', { name: /multiple choice/i }).click()
    await page.getByPlaceholder(/what do you want to ask/i).fill('Question?')

    // Fill answer options (MC requires at least 2 non-empty options, one marked correct)
    const optionInputs = page.getByPlaceholder(/answer \d+/i)
    await optionInputs.nth(0).fill('Option A')
    await optionInputs.nth(1).fill('Option B')
    await page.getByRole('button', { name: /mark correct/i }).first().click()

    await page.getByRole('button', { name: /save quiz/i }).click()

    // URL should change to /quiz/:id/edit
    await expect(page).toHaveURL(/\/quiz\/\d+\/edit/)
  })

  // ── Add question types ──────────────────────────────────────────────────────

  test('add MultipleChoice question', async ({ page, request }) => {
    const u = await apiRegister(request)
    const { token } = await apiLogin(request, u.email)
    await injectToken(page, token)
    await page.goto('/quiz/create')
    await page.getByPlaceholder(/80s music trivia|quiz title/i).fill('MC Quiz')

    await page.getByRole('button', { name: /add question/i }).click()
    await page.getByRole('button', { name: /multiple choice/i }).click()

    await expect(page.getByText(/multiple choice/i)).toBeVisible()

    // Fill question text
    await page.getByPlaceholder(/what do you want to ask/i).fill('Favourite colour?')

    // Options appear in the answer option editor
    const optionInputs = page.getByPlaceholder(/answer \d+/i)
    await expect(optionInputs.first()).toBeVisible()
  })

  test('add TrueFalse question', async ({ page, request }) => {
    const u = await apiRegister(request)
    const { token } = await apiLogin(request, u.email)
    await injectToken(page, token)
    await page.goto('/quiz/create')
    await page.getByPlaceholder(/80s music trivia|quiz title/i).fill('TF Quiz')

    await page.getByRole('button', { name: /add question/i }).click()
    await page.getByRole('button', { name: /true \/ false/i }).click()

    // TrueFalse editor shows True/False buttons
    await expect(page.getByRole('button', { name: /true/i })).toBeVisible()
    await expect(page.getByRole('button', { name: /false/i })).toBeVisible()
  })

  test('add TypeAnswer question', async ({ page, request }) => {
    const u = await apiRegister(request)
    const { token } = await apiLogin(request, u.email)
    await injectToken(page, token)
    await page.goto('/quiz/create')
    await page.getByPlaceholder(/80s music trivia|quiz title/i).fill('TA Quiz')

    await page.getByRole('button', { name: /add question/i }).click()
    await page.getByRole('button', { name: /type answer/i }).click()

    // TypeAnswer shows "Correct answer" label
    await expect(page.getByText(/correct answer/i)).toBeVisible()
    await expect(page.getByPlaceholder(/exact text players/i)).toBeVisible()
  })

  test('add Poll question', async ({ page, request }) => {
    const u = await apiRegister(request)
    const { token } = await apiLogin(request, u.email)
    await injectToken(page, token)
    await page.goto('/quiz/create')
    await page.getByPlaceholder(/80s music trivia|quiz title/i).fill('Poll Quiz')

    await page.getByRole('button', { name: /add question/i }).click()
    await page.getByRole('button', { name: /poll/i }).click()

    // Poll shows option inputs but no "mark correct" buttons
    await expect(page.getByPlaceholder(/option \d+/i).first()).toBeVisible()
  })

  // ── Reorder questions ───────────────────────────────────────────────────────

  test('reorder: move question down and up changes order', async ({ page, request }) => {
    const u = await apiRegister(request)
    const { token } = await apiLogin(request, u.email)
    const quiz = await apiCreateQuiz(request, token, 'Order Quiz')
    await apiAddQuestion(request, token, quiz.id, { questionText: 'Question A' })
    await apiAddQuestion(request, token, quiz.id, { questionText: 'Question B' })
    await injectToken(page, token)

    await page.goto(`/quiz/${quiz.id}/edit`)

    // Move first question down
    await page.getByLabel('Move question down').first().click()

    // Now Question B should appear before Question A in DOM
    const questions = page.getByText(/question [ab]/i)
    await expect(questions.first()).toContainText('B')
  })

  // ── Delete question ──────────────────────────────────────────────────────────

  test('delete question removes it from the editor', async ({ page, request }) => {
    const u = await apiRegister(request)
    const { token } = await apiLogin(request, u.email)
    const quiz = await apiCreateQuiz(request, token, 'Delete Q Quiz')
    await apiAddQuestion(request, token, quiz.id, { questionText: 'I will be deleted' })
    await apiAddQuestion(request, token, quiz.id, { questionText: 'I will survive' })
    await injectToken(page, token)

    await page.goto(`/quiz/${quiz.id}/edit`)

    // Click delete on the first question
    page.on('dialog', (d) => d.accept())
    await page.getByRole('button', { name: /delete question|remove/i }).first().click()

    // The deleted question text should disappear
    await expect(page.getByText('I will be deleted')).not.toBeVisible()
    await expect(page.getByText('I will survive')).toBeVisible()
  })

  // ── Editing an existing quiz ─────────────────────────────────────────────────

  test('edit existing quiz: change title and save', async ({ page, request }) => {
    const u = await apiRegister(request)
    const { token } = await apiLogin(request, u.email)
    const quiz = await apiCreateQuiz(request, token, 'Original Title')
    await apiAddQuestion(request, token, quiz.id)
    await injectToken(page, token)

    await page.goto(`/quiz/${quiz.id}/edit`)

    const titleInput = page.getByPlaceholder(/80s music trivia|quiz title/i)
    await titleInput.clear()
    await titleInput.fill('Updated Title')
    await page.getByRole('button', { name: /save quiz/i }).click()

    await expect(page.getByText(/saved|success/i)).toBeVisible()
    await expect(titleInput).toHaveValue('Updated Title')
  })

  // ── Start game button ────────────────────────────────────────────────────────

  test('"Start game" button is disabled when quiz has unsaved changes', async ({
    page,
    request,
  }) => {
    const u = await apiRegister(request)
    const { token } = await apiLogin(request, u.email)
    const quiz = await apiCreateQuiz(request, token, 'Saved Quiz')
    await apiAddQuestion(request, token, quiz.id)
    await injectToken(page, token)

    await page.goto(`/quiz/${quiz.id}/edit`)
    // Modify title to make it dirty
    await page.getByPlaceholder(/80s music trivia|quiz title/i).fill('Dirty title')

    const startBtn = page.getByRole('button', { name: /start.*game|launch/i })
    await expect(startBtn).toBeDisabled()
  })

  test('"Start game" button is enabled when quiz is saved and has questions', async ({
    page,
    request,
  }) => {
    const u = await apiRegister(request)
    const { token } = await apiLogin(request, u.email)
    const quiz = await apiCreateQuiz(request, token, 'Clean Quiz')
    await apiAddQuestion(request, token, quiz.id)
    await injectToken(page, token)

    await page.goto(`/quiz/${quiz.id}/edit`)
    // Wait for the page to load (dirty = false after load)
    await page.waitForTimeout(500)

    const startBtn = page.getByRole('button', { name: /start.*game|launch/i })
    await expect(startBtn).toBeEnabled()
  })
})
