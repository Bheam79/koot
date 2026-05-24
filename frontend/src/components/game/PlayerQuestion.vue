<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import type { QuestionBroadcast } from '../../types/game'
import { QType } from '../../types/game'
import { useSound } from '../../composables/useSound'

const props = defineProps<{
  question: QuestionBroadcast
  secondsLeft: number
  totalTime: number
  answered: boolean
  selectedOptionId: number | null
  code: string
}>()

const emit = defineEmits<{
  submitOption: [optionId: number, timeTakenMs: number]
  submitText: [text: string, timeTakenMs: number]
}>()

const { playClick, playCountdown } = useSound()

const textAnswer = ref('')
const questionStartTime = ref(Date.now())

watch(
  () => props.question.id,
  () => {
    textAnswer.value = ''
    questionStartTime.value = Date.now()
  },
  { immediate: true },
)

// Play countdown tick when time is low
watch(
  () => props.secondsLeft,
  (s) => {
    if (s <= 5 && s > 0 && !props.answered) {
      playCountdown()
    }
  },
)

const timerPercent = computed(() =>
  Math.max(0, Math.min(100, (props.secondsLeft / props.totalTime) * 100)),
)

const timerColor = computed(() => {
  const pct = props.secondsLeft / props.totalTime
  if (pct > 0.5) return 'bg-koot-green'
  if (pct > 0.25) return 'bg-yellow-400'
  return 'bg-koot-magenta'
})

const OPTION_STYLES = [
  { bg: 'bg-koot-magenta', ring: 'ring-koot-magenta', shape: '▲', label: 'A' },
  { bg: 'bg-koot-blue',    ring: 'ring-koot-blue',    shape: '◆', label: 'B' },
  { bg: 'bg-yellow-400',   ring: 'ring-yellow-400',   shape: '●', label: 'C' },
  { bg: 'bg-koot-green',   ring: 'ring-koot-green',   shape: '■', label: 'D' },
]

function onPickOption(optionId: number) {
  if (props.answered) return
  playClick()
  const ms = Date.now() - questionStartTime.value
  emit('submitOption', optionId, ms)
}

function onSubmitText() {
  if (props.answered || !textAnswer.value.trim()) return
  playClick()
  const ms = Date.now() - questionStartTime.value
  emit('submitText', textAnswer.value.trim(), ms)
}

const isTrueFalse = computed(() => props.question.type === QType.TrueFalse)
const isTypeAnswer = computed(() => props.question.type === QType.TypeAnswer)
</script>

<template>
  <div class="min-h-screen bg-[#1a0533] text-white flex flex-col">
    <!-- Timer bar -->
    <div class="h-3 bg-white/20 w-full">
      <div
        class="h-full transition-all duration-900"
        :class="timerColor"
        :style="{ width: timerPercent + '%' }"
      />
    </div>

    <!-- Question text -->
    <div class="flex-1 flex flex-col items-center justify-center px-4 sm:px-6 py-6 gap-4">
      <img
        v-if="question.imageUrl"
        :src="question.imageUrl"
        class="max-h-36 rounded-xl object-cover shadow-xl"
        alt=""
      />
      <h2 class="text-xl sm:text-2xl md:text-3xl font-black text-center leading-snug max-w-2xl">
        {{ question.questionText }}
      </h2>
      <div class="flex items-center gap-3 text-white/50 text-sm">
        <span>{{ question.points }} pts</span>
        <span>·</span>
        <span :class="secondsLeft <= 5 ? 'text-koot-magenta font-black animate-pulse' : ''">
          {{ secondsLeft }}s left
        </span>
      </div>
    </div>

    <!-- Answer area -->
    <div class="p-3">
      <!-- True/False -->
      <div v-if="isTrueFalse" class="grid grid-cols-2 gap-3">
        <button
          v-for="opt in question.answerOptions"
          :key="opt.id"
          :class="[
            'rounded-2xl py-8 sm:py-10 font-black text-2xl transition-all shadow-lg',
            opt.text.toLowerCase() === 'true' ? 'bg-koot-green' : 'bg-koot-magenta',
            answered && selectedOptionId === opt.id ? 'ring-4 ring-white scale-95' : '',
            answered && selectedOptionId !== opt.id ? 'opacity-30' : '',
            !answered ? 'hover:scale-105 active:scale-95' : 'cursor-default',
          ]"
          :disabled="answered"
          @click="onPickOption(opt.id)"
        >
          {{ opt.text }}
        </button>
      </div>

      <!-- Type answer -->
      <div v-else-if="isTypeAnswer" class="flex flex-col gap-3 max-w-lg mx-auto">
        <input
          v-model="textAnswer"
          type="text"
          maxlength="200"
          placeholder="Type your answer…"
          :disabled="answered"
          class="w-full px-4 py-4 rounded-xl text-xl font-bold text-slate-900 bg-white
                 focus:outline-none focus:ring-4 focus:ring-koot-purple
                 disabled:opacity-60"
          @keyup.enter="onSubmitText"
        />
        <button
          :disabled="answered || !textAnswer.trim()"
          class="py-5 rounded-2xl font-black text-xl bg-koot-blue text-white shadow-lg
                 transition-all hover:opacity-90 active:scale-95
                 disabled:opacity-40 disabled:cursor-not-allowed"
          @click="onSubmitText"
        >
          {{ answered ? 'Answer locked in! ✓' : 'Submit Answer' }}
        </button>
      </div>

      <!-- Multiple choice / Poll -->
      <div v-else class="grid grid-cols-2 gap-2 sm:gap-3">
        <button
          v-for="(opt, i) in question.answerOptions"
          :key="opt.id"
          :class="[
            'rounded-2xl py-6 sm:py-8 px-3 sm:px-4 flex items-center gap-2 min-h-[5rem] font-bold text-base sm:text-lg transition-all shadow-lg',
            OPTION_STYLES[i % 4].bg,
            answered && selectedOptionId === opt.id
              ? 'ring-4 ring-white scale-95'
              : '',
            answered && selectedOptionId !== opt.id
              ? 'opacity-30'
              : '',
            !answered
              ? 'hover:scale-105 active:scale-95'
              : 'cursor-default',
          ]"
          :disabled="answered"
          @click="onPickOption(opt.id)"
        >
          <span class="text-xl opacity-80 shrink-0">{{ OPTION_STYLES[i % 4].shape }}</span>
          <span class="leading-tight text-left">{{ opt.text }}</span>
        </button>
      </div>
    </div>

    <!-- Answered overlay message -->
    <Transition name="fade">
      <div
        v-if="answered"
        class="fixed inset-0 flex items-end justify-center pb-8 pointer-events-none"
      >
        <div class="bg-black/80 text-white px-8 py-4 rounded-2xl text-lg font-black shadow-2xl">
          🔒 Answer locked in! Waiting for results…
        </div>
      </div>
    </Transition>
  </div>
</template>

<style scoped>
.fade-enter-active,
.fade-leave-active {
  transition: opacity 0.3s ease;
}
.fade-enter-from,
.fade-leave-to {
  opacity: 0;
}
</style>
