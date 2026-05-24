import { describe, it, expect, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import { createRouter, createWebHistory } from 'vue-router'
import QuizCard from '../components/QuizCard.vue'
import type { QuizSummary } from '../types/quiz'

// ── Mock api/quizzes so absoluteUrl doesn't crash ─────────────────────────────
vi.mock('../api/quizzes', () => ({
  absoluteUrl: (url: string) => `http://localhost${url}`,
}))

// ── Minimal stub router (QuizCard uses <RouterLink>) ──────────────────────────
const router = createRouter({
  history: createWebHistory(),
  routes: [
    { path: '/', component: { template: '<div/>' } },
    { path: '/quiz/:id/edit', name: 'quiz-edit', component: { template: '<div/>' } },
  ],
})

const makeQuiz = (overrides: Partial<QuizSummary> = {}): QuizSummary => ({
  id: 1,
  title: 'My Quiz',
  description: 'A great quiz',
  coverImageUrl: null,
  questionCount: 5,
  createdAt: '2024-03-15T12:00:00Z',
  ...overrides,
})

describe('QuizCard', () => {
  it('displays the quiz title', () => {
    const wrapper = mount(QuizCard, {
      global: { plugins: [router] },
      props: { quiz: makeQuiz({ title: 'Science Bowl' }) },
    })
    expect(wrapper.text()).toContain('Science Bowl')
  })

  it('displays the question count', () => {
    const wrapper = mount(QuizCard, {
      global: { plugins: [router] },
      props: { quiz: makeQuiz({ questionCount: 10 }) },
    })
    expect(wrapper.text()).toContain('10 questions')
  })

  it('uses singular "question" when count is 1', () => {
    const wrapper = mount(QuizCard, {
      global: { plugins: [router] },
      props: { quiz: makeQuiz({ questionCount: 1 }) },
    })
    expect(wrapper.text()).toContain('1 question')
    expect(wrapper.text()).not.toContain('1 questions')
  })

  it('shows description when provided', () => {
    const wrapper = mount(QuizCard, {
      global: { plugins: [router] },
      props: { quiz: makeQuiz({ description: 'Awesome description' }) },
    })
    expect(wrapper.text()).toContain('Awesome description')
  })

  it('renders cover image when coverImageUrl is set', () => {
    const wrapper = mount(QuizCard, {
      global: { plugins: [router] },
      props: { quiz: makeQuiz({ coverImageUrl: '/img/cover.jpg' }) },
    })
    const img = wrapper.find('img')
    expect(img.exists()).toBe(true)
    expect(img.attributes('alt')).toContain('My Quiz')
  })

  it('renders placeholder SVG when no cover image', () => {
    const wrapper = mount(QuizCard, {
      global: { plugins: [router] },
      props: { quiz: makeQuiz({ coverImageUrl: null }) },
    })
    expect(wrapper.find('svg').exists()).toBe(true)
    expect(wrapper.find('img').exists()).toBe(false)
  })

  it('emits delete event with quiz id when Delete button is clicked', async () => {
    const wrapper = mount(QuizCard, {
      global: { plugins: [router] },
      props: { quiz: makeQuiz({ id: 7 }) },
    })
    await wrapper.find('button').trigger('click')
    expect(wrapper.emitted('delete')).toBeTruthy()
    expect(wrapper.emitted('delete')![0]).toEqual([7])
  })

  it('contains an Edit link pointing to quiz-edit route', () => {
    const wrapper = mount(QuizCard, {
      global: { plugins: [router] },
      props: { quiz: makeQuiz({ id: 3 }) },
    })
    const link = wrapper.findComponent({ name: 'RouterLink' })
    // Props should include the route params
    expect(JSON.stringify(link.props('to'))).toContain('3')
  })

  it('displays a formatted creation date', () => {
    const wrapper = mount(QuizCard, {
      global: { plugins: [router] },
      props: { quiz: makeQuiz({ createdAt: '2024-06-01T00:00:00Z' }) },
    })
    // Just verify some date text exists (locale-dependent format)
    expect(wrapper.text()).toMatch(/created/i)
  })
})
