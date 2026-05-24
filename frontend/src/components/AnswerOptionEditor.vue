<script setup lang="ts">
// Renders the answer-option editor appropriate for the parent question's type.
// Edits happen via v-model:options against the parent (a plain array reference).
import { computed } from 'vue'
import type { AnswerOption } from '../types/quiz'
import { QuestionType } from '../types/quiz'

const props = defineProps<{
  options: AnswerOption[]
  type: QuestionType
}>()

const emit = defineEmits<{
  (e: 'update:options', value: AnswerOption[]): void
}>()

const showCorrectness = computed(() => props.type !== QuestionType.Poll)

function setText(idx: number, text: string) {
  const next = props.options.map((o, i) => (i === idx ? { ...o, text } : o))
  emit('update:options', next)
}

function toggleCorrect(idx: number) {
  if (!showCorrectness.value) return
  const next = props.options.map((o, i) => (i === idx ? { ...o, isCorrect: !o.isCorrect } : o))
  emit('update:options', next)
}

function setSingleCorrect(idx: number) {
  const next = props.options.map((o, i) => ({ ...o, isCorrect: i === idx }))
  emit('update:options', next)
}

const cardColors = ['bg-koot-magenta', 'bg-koot-blue', 'bg-koot-yellow', 'bg-koot-green']
</script>

<template>
  <!-- MultipleChoice: up to 4 colored boxes; click toggles "correct". -->
  <div v-if="type === QuestionType.MultipleChoice" class="grid grid-cols-1 sm:grid-cols-2 gap-3">
    <div
      v-for="(opt, idx) in options"
      :key="idx"
      class="rounded-lg p-3 text-white shadow flex items-stretch gap-2"
      :class="[cardColors[idx % cardColors.length], opt.isCorrect ? 'ring-4 ring-emerald-300' : '']"
    >
      <input
        type="text"
        :value="opt.text"
        :placeholder="`Answer ${idx + 1}`"
        class="flex-1 bg-white/10 placeholder-white/60 text-white rounded-md px-2 py-1 focus:outline-none focus:ring-2 focus:ring-white/70"
        @input="setText(idx, ($event.target as HTMLInputElement).value)"
      />
      <button
        type="button"
        class="px-2 py-1 rounded-md bg-white/20 hover:bg-white/30 text-xs font-semibold"
        :title="opt.isCorrect ? 'Marked correct' : 'Mark correct'"
        @click="toggleCorrect(idx)"
      >
        {{ opt.isCorrect ? '✓ correct' : 'mark correct' }}
      </button>
    </div>
  </div>

  <!-- TrueFalse: two big buttons, exactly one is correct. -->
  <div v-else-if="type === QuestionType.TrueFalse" class="grid grid-cols-2 gap-3">
    <button
      v-for="(opt, idx) in options"
      :key="idx"
      type="button"
      class="rounded-lg py-4 font-bold text-white shadow"
      :class="[
        idx === 0 ? 'bg-koot-green' : 'bg-koot-magenta',
        opt.isCorrect ? 'ring-4 ring-emerald-300' : 'opacity-90',
      ]"
      @click="setSingleCorrect(idx)"
    >
      {{ opt.text }}<span v-if="opt.isCorrect" class="ml-2">✓</span>
    </button>
  </div>

  <!-- TypeAnswer: single text input, that's the correct answer. -->
  <div v-else-if="type === QuestionType.TypeAnswer">
    <label class="block text-sm font-medium text-slate-700 mb-1">Correct answer</label>
    <input
      type="text"
      :value="options[0]?.text ?? ''"
      placeholder="The exact text players should type"
      class="w-full rounded-md border border-slate-300 px-3 py-2 focus:outline-none focus:ring-2 focus:ring-koot-blue"
      @input="setText(0, ($event.target as HTMLInputElement).value)"
    />
    <p class="mt-1 text-xs text-slate-500">
      Players must type this exact value (case-insensitive matching is enforced server-side).
    </p>
  </div>

  <!-- Poll: same as MC but without correctness UI. -->
  <div v-else class="grid grid-cols-1 sm:grid-cols-2 gap-3">
    <div
      v-for="(opt, idx) in options"
      :key="idx"
      class="rounded-lg p-3 text-white shadow"
      :class="cardColors[idx % cardColors.length]"
    >
      <input
        type="text"
        :value="opt.text"
        :placeholder="`Option ${idx + 1}`"
        class="w-full bg-white/10 placeholder-white/60 text-white rounded-md px-2 py-1 focus:outline-none focus:ring-2 focus:ring-white/70"
        @input="setText(idx, ($event.target as HTMLInputElement).value)"
      />
    </div>
  </div>
</template>
