<script setup lang="ts">
// Renders the answer-option editor appropriate for the parent question's type.
// Edits happen via v-model:options against the parent (a plain array reference).
import { computed } from 'vue'
import type { AnswerOption } from '../types/quiz'
import { QuestionType } from '../types/quiz'

const props = defineProps<{
  options: AnswerOption[]
  type: QuestionType
  showErrors?: boolean
}>()

const emit = defineEmits<{
  (e: 'update:options', value: AnswerOption[]): void
}>()

const showCorrectness = computed(() => props.type !== QuestionType.Poll)
const canAddOption = computed(() => props.options.length < 4)
const canRemoveOption = computed(() => props.options.length > 2)

const hasNoCorrect = computed(
  () => props.type === QuestionType.MultipleChoice && !props.options.some((o) => o.isCorrect),
)

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

function addOption() {
  if (!canAddOption.value) return
  const next = [
    ...props.options,
    { text: '', isCorrect: false, orderIndex: props.options.length },
  ]
  emit('update:options', next)
}

function removeOption(idx: number) {
  if (!canRemoveOption.value) return
  const next = props.options
    .filter((_, i) => i !== idx)
    .map((o, i) => ({ ...o, orderIndex: i }))
  emit('update:options', next)
}

const cardColors = ['bg-koot-magenta', 'bg-koot-blue', 'bg-koot-yellow', 'bg-koot-green']
</script>

<template>
  <!-- MultipleChoice: up to 4 colored boxes; click toggles "correct". -->
  <div v-if="type === QuestionType.MultipleChoice">
    <div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
      <div
        v-for="(opt, idx) in options"
        :key="idx"
        class="rounded-lg p-3 text-white shadow flex items-stretch gap-2"
        :class="[
          cardColors[idx % cardColors.length],
          opt.isCorrect ? 'ring-4 ring-emerald-300' : '',
          showErrors && !opt.text.trim() ? 'ring-4 ring-red-400' : '',
        ]"
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
          class="px-2 py-1 rounded-md bg-white/20 hover:bg-white/30 text-xs font-semibold shrink-0"
          :title="opt.isCorrect ? 'Marked correct' : 'Mark correct'"
          @click="toggleCorrect(idx)"
        >
          {{ opt.isCorrect ? '✓ correct' : 'mark correct' }}
        </button>
        <button
          v-if="canRemoveOption"
          type="button"
          class="px-2 py-1 rounded-md bg-white/20 hover:bg-red-500/60 text-sm font-bold leading-none shrink-0"
          title="Remove this option"
          @click="removeOption(idx)"
        >
          ×
        </button>
      </div>
    </div>

    <div class="mt-2 flex items-center justify-between gap-2">
      <p v-if="showErrors && hasNoCorrect" class="text-sm text-red-600 font-medium" role="alert">
        ⚠ Mark at least one option as correct.
      </p>
      <span v-else class="flex-1" />
      <button
        v-if="canAddOption"
        type="button"
        class="text-sm text-koot-blue hover:underline font-medium"
        @click="addOption"
      >
        + Add option
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
      :class="[
        'w-full rounded-md border px-3 py-2 focus:outline-none focus:ring-2',
        showErrors && !options[0]?.text.trim()
          ? 'border-red-400 focus:ring-red-400'
          : 'border-slate-300 focus:ring-koot-blue',
      ]"
      @input="setText(0, ($event.target as HTMLInputElement).value)"
    />
    <p v-if="showErrors && !options[0]?.text.trim()" class="mt-1 text-sm text-red-600" role="alert">
      ⚠ Correct answer text is required.
    </p>
    <p v-else class="mt-1 text-xs text-slate-500">
      Players must type this exact value (case-insensitive matching is enforced server-side).
    </p>
  </div>

  <!-- Poll: same as MC but without correctness UI. -->
  <div v-else>
    <div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
      <div
        v-for="(opt, idx) in options"
        :key="idx"
        class="rounded-lg p-3 text-white shadow flex items-stretch gap-2"
        :class="[
          cardColors[idx % cardColors.length],
          showErrors && !opt.text.trim() ? 'ring-4 ring-red-400' : '',
        ]"
      >
        <input
          type="text"
          :value="opt.text"
          :placeholder="`Option ${idx + 1}`"
          class="flex-1 bg-white/10 placeholder-white/60 text-white rounded-md px-2 py-1 focus:outline-none focus:ring-2 focus:ring-white/70"
          @input="setText(idx, ($event.target as HTMLInputElement).value)"
        />
        <button
          v-if="canRemoveOption"
          type="button"
          class="px-2 py-1 rounded-md bg-white/20 hover:bg-red-500/60 text-sm font-bold leading-none shrink-0"
          title="Remove this option"
          @click="removeOption(idx)"
        >
          ×
        </button>
      </div>
    </div>
    <div class="mt-2 flex justify-end">
      <button
        v-if="canAddOption"
        type="button"
        class="text-sm text-koot-blue hover:underline font-medium"
        @click="addOption"
      >
        + Add option
      </button>
    </div>
  </div>
</template>
