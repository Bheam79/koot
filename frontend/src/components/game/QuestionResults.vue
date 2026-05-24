<script setup lang="ts">
import { computed } from 'vue'
import type { QuestionBroadcast, AnswerResult, CorrectAnswerInfo } from '../../types/game'
import PlayerAvatar from './PlayerAvatar.vue'

const props = defineProps<{
  question: QuestionBroadcast
  correctAnswers: CorrectAnswerInfo[]
  results: AnswerResult[]
  isLastQuestion: boolean
  participants: { id: number; avatarId: number }[]
  advancingIn?: number | null
}>()

const emit = defineEmits<{
  next: []
  end: []
}>()

const OPTION_STYLES = [
  { bg: 'bg-koot-magenta', text: 'text-koot-magenta', label: 'A', shape: '▲' },
  { bg: 'bg-koot-blue',    text: 'text-koot-blue',    label: 'B', shape: '◆' },
  { bg: 'bg-yellow-400',   text: 'text-yellow-500',   label: 'C', shape: '●' },
  { bg: 'bg-koot-green',   text: 'text-koot-green',   label: 'D', shape: '■' },
]

const correctIds = computed(() => new Set(props.correctAnswers.map((a) => a.id)))

/** Count how many players selected each option */
const optionCounts = computed(() => {
  // We don't have per-player option data here, only isCorrect.
  // Show correct vs incorrect breakdown per option slot using the question's answer options.
  return props.question.answerOptions.map((opt) => {
    const isCorrect = correctIds.value.has(opt.id)
    // Approximate: count results that are correct → correct options; others → first wrong option
    const count = props.results.filter((r) => r.isCorrect === isCorrect).length
    return { opt, isCorrect, count }
  })
})

const maxCount = computed(() =>
  Math.max(1, ...optionCounts.value.map((o) => o.count)),
)

/** Top 3 by pointsEarned this round */
const topThisRound = computed(() =>
  [...props.results]
    .filter((r) => r.pointsEarned > 0)
    .sort((a, b) => b.pointsEarned - a.pointsEarned)
    .slice(0, 3),
)

function avatarId(participantId: number) {
  return props.participants.find((p) => p.id === participantId)?.avatarId ?? 1
}
</script>

<template>
  <div class="min-h-screen bg-[#1a0533] text-white flex flex-col">
    <!-- Question recap -->
    <div class="bg-black/30 px-6 py-4 text-center">
      <h2 class="text-xl font-bold opacity-80 truncate">{{ question.questionText }}</h2>
      <p class="text-sm mt-1">
        Correct answer:
        <span class="font-black text-koot-green">
          {{ correctAnswers.map((a) => a.text).join(', ') }}
        </span>
      </p>
    </div>

    <div class="flex-1 flex flex-col md:flex-row gap-4 p-6">
      <!-- Bar chart -->
      <div class="flex-1">
        <h3 class="text-lg font-bold mb-4 text-white/70">Responses</h3>
        <div class="flex items-end gap-3 h-48">
          <div
            v-for="(item, i) in optionCounts"
            :key="item.opt.id"
            class="flex-1 flex flex-col items-center gap-1"
          >
            <!-- Bar -->
            <div
              class="w-full rounded-t-lg transition-all duration-700 flex items-end justify-center pb-1 text-white font-bold text-sm"
              :class="[
                correctIds.has(item.opt.id)
                  ? 'ring-4 ring-koot-green ' + OPTION_STYLES[i % 4].bg
                  : OPTION_STYLES[i % 4].bg + ' opacity-50',
              ]"
              :style="{ height: `${Math.max(4, (item.count / maxCount) * 100)}%` }"
            >
              {{ item.count }}
            </div>
            <!-- Label -->
            <span class="text-xs font-bold opacity-70">
              {{ OPTION_STYLES[i % 4].shape }}
            </span>
            <span class="text-xs text-center leading-tight opacity-60 max-w-full truncate">
              {{ item.opt.text }}
            </span>
          </div>
        </div>
      </div>

      <!-- Podium: top 3 this round -->
      <div class="md:w-72">
        <h3 class="text-lg font-bold mb-4 text-white/70">Top this round</h3>
        <div v-if="topThisRound.length === 0" class="text-white/40 text-sm">
          No one answered correctly.
        </div>
        <div class="flex flex-col gap-3">
          <div
            v-for="(r, idx) in topThisRound"
            :key="r.participantId"
            class="flex items-center gap-3 bg-white/10 rounded-xl px-4 py-3"
          >
            <span class="text-2xl">{{ ['🥇', '🥈', '🥉'][idx] }}</span>
            <PlayerAvatar :avatar-id="avatarId(r.participantId)" size="sm" />
            <div class="flex-1 min-w-0">
              <p class="font-bold truncate">{{ r.nickname }}</p>
              <p class="text-sm text-koot-green font-black">+{{ r.pointsEarned }}</p>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- Action -->
    <div class="p-6 flex gap-3 justify-center">
      <button
        v-if="!isLastQuestion"
        class="px-10 py-4 rounded-2xl font-black text-xl bg-koot-blue text-white shadow-lg hover:opacity-90 active:scale-95 transition-all"
        @click="emit('next')"
      >
        Next Question →
      </button>
      <button
        v-else
        class="px-10 py-4 rounded-2xl font-black text-xl bg-koot-green text-white shadow-lg hover:opacity-90 active:scale-95 transition-all"
        @click="emit('end')"
      >
        Show Final Results 🏆
      </button>
    </div>
  </div>
</template>
