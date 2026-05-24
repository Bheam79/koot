<script setup lang="ts">
import { computed } from 'vue'

const props = defineProps<{
  isCorrect: boolean
  pointsEarned: number
  totalScore: number
  myRank: number
  totalPlayers: number
  streak: number
}>()

const feedbackIcon = computed(() => (props.isCorrect ? '✓' : '✗'))
const feedbackBg = computed(() => (props.isCorrect ? 'bg-koot-green' : 'bg-koot-magenta'))
const feedbackText = computed(() =>
  props.isCorrect
    ? props.pointsEarned > 0
      ? `Correct! +${props.pointsEarned} points`
      : 'Correct!'
    : 'Incorrect',
)
</script>

<template>
  <div
    class="min-h-screen flex flex-col items-center justify-center px-4 text-white"
    :class="feedbackBg"
  >
    <!-- Big icon -->
    <div class="text-9xl font-black mb-6 drop-shadow-2xl animate-[pop_0.4s_cubic-bezier(0.34,1.56,0.64,1)_forwards]">
      {{ feedbackIcon }}
    </div>

    <!-- Feedback text -->
    <p class="text-3xl font-black text-center mb-2">{{ feedbackText }}</p>

    <!-- Streak -->
    <div v-if="streak >= 2 && isCorrect" class="bg-white/20 rounded-full px-6 py-2 mt-2 font-bold text-lg">
      🔥 {{ streak }} in a row!
    </div>

    <!-- Score & rank -->
    <div class="mt-8 flex flex-col items-center gap-3">
      <div class="bg-white/20 rounded-2xl px-8 py-4 text-center">
        <p class="text-white/70 text-sm uppercase tracking-widest font-medium">Total score</p>
        <p class="text-4xl font-black tabular-nums">{{ totalScore.toLocaleString() }}</p>
      </div>
      <div class="bg-white/15 rounded-2xl px-8 py-3 text-center">
        <p class="text-white/70 text-sm uppercase tracking-widest font-medium">Your position</p>
        <p class="text-2xl font-black">
          #{{ myRank }}
          <span class="text-base font-normal opacity-70">of {{ totalPlayers }}</span>
        </p>
      </div>
    </div>

    <!-- Waiting indicator -->
    <p class="mt-8 text-white/50 text-sm animate-pulse">Waiting for leaderboard…</p>
  </div>
</template>

<style>
@keyframes pop {
  from { transform: scale(0); opacity: 0; }
  to   { transform: scale(1); opacity: 1; }
}
</style>
