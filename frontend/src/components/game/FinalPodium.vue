<script setup lang="ts">
import { onMounted, ref } from 'vue'
import confetti from 'canvas-confetti'
import PlayerAvatar from './PlayerAvatar.vue'
import type { LeaderboardEntry } from '../../types/game'
import { useRouter } from 'vue-router'

const props = defineProps<{
  standings: LeaderboardEntry[]
  code: string
}>()

const emit = defineEmits<{
  playAgain: []
}>()

const router = useRouter()
const ready = ref(false)

// Podium order: 2nd, 1st, 3rd (visual podium style)
const podiumOrder = [1, 0, 2] // indices into top3

const top3 = props.standings.slice(0, 3)

function podiumHeight(rank: number) {
  if (rank === 1) return 'h-44'
  if (rank === 2) return 'h-32'
  return 'h-24'
}

function podiumColor(rank: number) {
  if (rank === 1) return 'bg-yellow-400'
  if (rank === 2) return 'bg-slate-300'
  return 'bg-orange-400'
}

const MEDALS = ['🥇', '🥈', '🥉']

onMounted(() => {
  ready.value = true

  // Confetti burst
  const duration = 4000
  const end = Date.now() + duration

  function frame() {
    confetti({
      particleCount: 4,
      angle: 60,
      spread: 55,
      origin: { x: 0 },
      colors: ['#46178F', '#E21B3C', '#FFA602', '#1368CE', '#26890C'],
    })
    confetti({
      particleCount: 4,
      angle: 120,
      spread: 55,
      origin: { x: 1 },
      colors: ['#46178F', '#E21B3C', '#FFA602', '#1368CE', '#26890C'],
    })
    if (Date.now() < end) requestAnimationFrame(frame)
  }

  frame()
})
</script>

<template>
  <div class="min-h-screen bg-[#1a0533] text-white flex flex-col items-center justify-center px-6">
    <h1 class="text-5xl font-black text-center mb-2">🏆 Final Results!</h1>
    <p class="text-white/50 mb-12">Thanks for playing!</p>

    <!-- Podium -->
    <div class="flex items-end justify-center gap-4 mb-12">
      <template v-for="slot in podiumOrder" :key="slot">
        <div
          v-if="top3[slot]"
          class="flex flex-col items-center gap-2"
        >
          <!-- Avatar + name above podium block -->
          <PlayerAvatar :avatar-id="top3[slot].avatarId" size="xl" />
          <span class="text-xl">{{ MEDALS[slot] }}</span>
          <p class="font-black text-center max-w-24 truncate">{{ top3[slot].nickname }}</p>
          <p class="text-koot-yellow font-black text-lg">
            {{ top3[slot].totalScore.toLocaleString() }}
          </p>
          <!-- Podium block -->
          <div
            :class="['w-28 rounded-t-xl flex items-center justify-center font-black text-2xl text-black/70', podiumHeight(slot + 1), podiumColor(slot + 1)]"
          >
            {{ slot + 1 }}
          </div>
        </div>
      </template>
    </div>

    <!-- Remaining rankings -->
    <div
      v-if="standings.length > 3"
      class="w-full max-w-md bg-white/10 rounded-2xl divide-y divide-white/10 mb-8"
    >
      <div
        v-for="entry in standings.slice(3)"
        :key="entry.participantId"
        class="flex items-center gap-3 px-5 py-3"
      >
        <span class="w-6 text-right font-bold opacity-60">{{ entry.rank }}</span>
        <PlayerAvatar :avatar-id="entry.avatarId" size="sm" />
        <span class="flex-1 font-semibold truncate">{{ entry.nickname }}</span>
        <span class="font-black tabular-nums">{{ entry.totalScore.toLocaleString() }}</span>
      </div>
    </div>

    <!-- Actions -->
    <div class="flex flex-wrap gap-4 justify-center">
      <button
        class="px-8 py-3 rounded-2xl font-black text-lg bg-koot-blue text-white shadow-lg hover:opacity-90 active:scale-95 transition-all"
        @click="emit('playAgain')"
      >
        🔄 Play Again
      </button>
      <button
        class="px-8 py-3 rounded-2xl font-black text-lg bg-white/20 text-white shadow-lg hover:opacity-90 active:scale-95 transition-all"
        @click="router.push('/dashboard')"
      >
        ← Back to Dashboard
      </button>
    </div>
  </div>
</template>
