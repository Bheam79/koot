<script setup lang="ts">
// One question's editor card. Edits are emitted upward via v-model:question so the
// parent (QuizEditorView) keeps the canonical question list.
import { computed, watch } from 'vue'
import AnswerOptionEditor from './AnswerOptionEditor.vue'
import ImageUpload from './ImageUpload.vue'
import {
  POINT_VALUES,
  QuestionType,
  TIME_LIMITS,
  questionTypeLabel,
} from '../types/quiz'
import type { AnswerOption, Question } from '../types/quiz'

const props = defineProps<{
  question: Question
  index: number
  total: number
  errors?: string[]
}>()

const emit = defineEmits<{
  (e: 'update:question', q: Question): void
  (e: 'delete'): void
  (e: 'move-up'): void
  (e: 'move-down'): void
}>()

function patch(partial: Partial<Question>) {
  emit('update:question', { ...props.question, ...partial })
}

function onOptions(next: AnswerOption[]) {
  patch({ answerOptions: next })
}

// Keep answerOptions sized correctly when the type changes. Polls and MC can be 2-4;
// TrueFalse is always [True,False]; TypeAnswer is a single option.
watch(
  () => props.question.type,
  (type) => {
    const current = props.question.answerOptions
    if (type === QuestionType.TrueFalse) {
      const trueIsCorrect = current.find((o) => o.text.toLowerCase() === 'true')?.isCorrect ?? true
      patch({
        answerOptions: [
          { text: 'True', isCorrect: trueIsCorrect, orderIndex: 0 },
          { text: 'False', isCorrect: !trueIsCorrect, orderIndex: 1 },
        ],
      })
    } else if (type === QuestionType.TypeAnswer) {
      const first = current[0] ?? { text: '', isCorrect: true, orderIndex: 0 }
      patch({ answerOptions: [{ ...first, isCorrect: true, orderIndex: 0 }] })
    } else {
      // MC / Poll - ensure at least 2 options up to 4.
      const padded = [...current]
      while (padded.length < 2) {
        padded.push({ text: '', isCorrect: false, orderIndex: padded.length })
      }
      patch({ answerOptions: padded.slice(0, 4) })
    }
  },
)

const typeLabel = computed(() => questionTypeLabel(props.question.type))
</script>

<template>
  <article class="rounded-xl bg-white shadow border border-slate-200 p-4 sm:p-6">
    <header class="flex items-center justify-between gap-3 mb-4">
      <div class="flex items-center gap-2">
        <div
          class="cursor-grab text-slate-400 select-none px-1"
          title="Drag to reorder (use arrows for now)"
          aria-hidden="true"
        >
          ⠿
        </div>
        <div>
          <p class="text-xs uppercase tracking-wide text-slate-400">
            Question {{ index + 1 }} of {{ total }}
          </p>
          <p class="font-semibold text-slate-800">{{ typeLabel }}</p>
        </div>
      </div>
      <div class="flex items-center gap-1">
        <button
          type="button"
          class="px-2 py-1 text-slate-500 hover:text-slate-800 disabled:opacity-40"
          :disabled="index === 0"
          @click="emit('move-up')"
          aria-label="Move question up"
        >
          ↑
        </button>
        <button
          type="button"
          class="px-2 py-1 text-slate-500 hover:text-slate-800 disabled:opacity-40"
          :disabled="index === total - 1"
          @click="emit('move-down')"
          aria-label="Move question down"
        >
          ↓
        </button>
        <button
          type="button"
          class="px-3 py-1 text-sm text-koot-magenta hover:underline"
          @click="emit('delete')"
        >
          Delete
        </button>
      </div>
    </header>

    <div class="space-y-4">
      <div>
        <label class="block text-sm font-medium text-slate-700 mb-1">Question</label>
        <input
          type="text"
          :value="question.questionText"
          class="w-full rounded-md border border-slate-300 px-3 py-2 focus:outline-none focus:ring-2 focus:ring-koot-blue"
          placeholder="What do you want to ask?"
          @input="patch({ questionText: ($event.target as HTMLInputElement).value })"
        />
      </div>

      <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
        <div>
          <label class="block text-sm font-medium text-slate-700 mb-1">Time limit</label>
          <select
            :value="question.timeLimit"
            class="w-full rounded-md border border-slate-300 px-3 py-2 focus:outline-none focus:ring-2 focus:ring-koot-blue"
            @change="patch({ timeLimit: Number(($event.target as HTMLSelectElement).value) })"
          >
            <option v-for="t in TIME_LIMITS" :key="t" :value="t">{{ t }} s</option>
          </select>
        </div>
        <div>
          <label class="block text-sm font-medium text-slate-700 mb-1">Points</label>
          <select
            :value="question.points"
            class="w-full rounded-md border border-slate-300 px-3 py-2 focus:outline-none focus:ring-2 focus:ring-koot-blue"
            @change="patch({ points: Number(($event.target as HTMLSelectElement).value) })"
          >
            <option v-for="p in POINT_VALUES" :key="p" :value="p">{{ p }}</option>
          </select>
        </div>
      </div>

      <ImageUpload
        label="Question image (optional)"
        :model-value="question.imageUrl ?? null"
        @update:model-value="patch({ imageUrl: $event })"
      />

      <div>
        <p class="block text-sm font-medium text-slate-700 mb-2">Answers</p>
        <AnswerOptionEditor
          :type="question.type"
          :options="question.answerOptions"
          :show-errors="!!errors?.length"
          @update:options="onOptions"
        />
        <ul v-if="errors?.length" class="mt-2 space-y-0.5" role="alert">
          <li
            v-for="(err, i) in errors"
            :key="i"
            class="text-sm text-red-600 font-medium"
          >
            ⚠ {{ err }}
          </li>
        </ul>
      </div>
    </div>
  </article>
</template>
