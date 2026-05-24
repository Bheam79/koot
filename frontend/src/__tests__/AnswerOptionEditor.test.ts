import { describe, it, expect } from 'vitest'
import { mount } from '@vue/test-utils'
import AnswerOptionEditor from '../components/AnswerOptionEditor.vue'
import { QuestionType } from '../types/quiz'
import type { AnswerOption } from '../types/quiz'

// ── Helpers ───────────────────────────────────────────────────────────────────

function makeOptions(count: number, correctIdx = 0): AnswerOption[] {
  return Array.from({ length: count }, (_, i) => ({
    id: i + 1,
    text: `Option ${i + 1}`,
    isCorrect: i === correctIdx,
    orderIndex: i,
  }))
}

describe('AnswerOptionEditor — MultipleChoice', () => {
  const type = QuestionType.MultipleChoice

  it('renders all options', () => {
    const wrapper = mount(AnswerOptionEditor, {
      props: { options: makeOptions(4), type },
    })
    const inputs = wrapper.findAll('input[type="text"]')
    expect(inputs).toHaveLength(4)
  })

  it('toggles isCorrect when "mark correct" button is clicked', async () => {
    const options = makeOptions(4, -1) // none correct
    const wrapper = mount(AnswerOptionEditor, {
      props: { options, type },
    })

    // Click the first option's button
    const btns = wrapper.findAll('button')
    await btns[0].trigger('click')

    const emitted = wrapper.emitted('update:options')
    expect(emitted).toBeTruthy()
    const updated = (emitted![0][0] as AnswerOption[])
    expect(updated[0].isCorrect).toBe(true)
  })

  it('emits update:options when text changes', async () => {
    const options = makeOptions(2)
    const wrapper = mount(AnswerOptionEditor, {
      props: { options, type },
    })

    const input = wrapper.findAll('input[type="text"]')[1]
    await input.setValue('New text')

    const emitted = wrapper.emitted('update:options')
    expect(emitted).toBeTruthy()
    const updated = (emitted![0][0] as AnswerOption[])
    expect(updated[1].text).toBe('New text')
  })
})

describe('AnswerOptionEditor — TrueFalse', () => {
  const type = QuestionType.TrueFalse

  it('renders two buttons', () => {
    const wrapper = mount(AnswerOptionEditor, {
      props: {
        options: [
          { id: 1, text: 'True', isCorrect: false, orderIndex: 0 },
          { id: 2, text: 'False', isCorrect: false, orderIndex: 1 },
        ],
        type,
      },
    })
    expect(wrapper.findAll('button')).toHaveLength(2)
  })

  it('sets exactly one option correct when a button is clicked', async () => {
    const wrapper = mount(AnswerOptionEditor, {
      props: {
        options: [
          { id: 1, text: 'True', isCorrect: false, orderIndex: 0 },
          { id: 2, text: 'False', isCorrect: false, orderIndex: 1 },
        ],
        type,
      },
    })

    const btns = wrapper.findAll('button')
    await btns[1].trigger('click')

    const emitted = wrapper.emitted('update:options')
    expect(emitted).toBeTruthy()
    const updated = (emitted![0][0] as AnswerOption[])
    expect(updated[0].isCorrect).toBe(false)
    expect(updated[1].isCorrect).toBe(true)
  })
})

describe('AnswerOptionEditor — TypeAnswer', () => {
  const type = QuestionType.TypeAnswer

  it('renders a single text input', () => {
    const wrapper = mount(AnswerOptionEditor, {
      props: {
        options: [{ id: 1, text: 'answer', isCorrect: true, orderIndex: 0 }],
        type,
      },
    })
    expect(wrapper.find('input[type="text"]').exists()).toBe(true)
  })

  it('emits text update when input changes', async () => {
    const wrapper = mount(AnswerOptionEditor, {
      props: {
        options: [{ id: 1, text: '', isCorrect: true, orderIndex: 0 }],
        type,
      },
    })

    const input = wrapper.find('input[type="text"]')
    await input.setValue('Paris')

    const emitted = wrapper.emitted('update:options')
    expect(emitted).toBeTruthy()
    const updated = (emitted![0][0] as AnswerOption[])
    expect(updated[0].text).toBe('Paris')
  })

  it('shows help text about case-insensitive matching', () => {
    const wrapper = mount(AnswerOptionEditor, {
      props: {
        options: [{ id: 1, text: 'x', isCorrect: true, orderIndex: 0 }],
        type,
      },
    })
    expect(wrapper.text()).toMatch(/case-insensitive/i)
  })
})

describe('AnswerOptionEditor — Poll', () => {
  const type = QuestionType.Poll

  it('renders all options without a correctness toggle button', () => {
    const wrapper = mount(AnswerOptionEditor, {
      props: { options: makeOptions(3), type },
    })
    // Poll shows inputs but no "mark correct" buttons
    expect(wrapper.findAll('input[type="text"]')).toHaveLength(3)
    expect(wrapper.findAll('button')).toHaveLength(0)
  })
})
