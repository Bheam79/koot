<script setup lang="ts">
import { onMounted, computed } from 'vue'
import confetti from 'canvas-confetti'
import PlayerAvatar from './PlayerAvatar.vue'
import type { LeaderboardEntry } from '../../types/game'

const props = defineProps<{
  standings: LeaderboardEntry[]
  myParticipantId: number
  myNickname: string
  myAvatarId: number
}>()

const emit = defineEmits<{ playAgain: [] }>()

const myEntry = computed(() =>
  props.standings.find((e) => e.participantId === props.myParticipantId),
)

const myRank = computed(() => myEntry.value?.rank ?? props.standings.length + 1)
const isTopThree = computed(() => myRank.value <= 3)

const top3 = computed(() => props.standings.slice(0, 3))
// Podium visual order: 2nd place left, 1st place center, 3rd place right
const podiumSlots = [1, 0, 2]

const MEDALS = ['🥇', '🥈', '🥉']

function podiumHeight(rank: number) {
  if (rank === 1) return 'h-32'
  if (rank === 2) return 'h-24'
  return 'h-16'
}
function podiumColor(rank: number) {
  if (rank === 1) return 'bg-yellow-400'
  if (rank === 2) return 'bg-slate-300'
  return 'bg-orange-400'
}

onMounted(() => {
  if (isTopThree.value) {
    const end = Date.now() + 4000
    const frame = () => {
      confetti({ particleCount: 5, angle: 60, spread: 55, origin: { x: 0 } })
      confetti({ particleCount: 5, angle: 120, spread: 55, origin: { x: 1 } })
      if (Date.now() < end) requestAnimationFrame(frame)
    }
    frame()
  }
})
</script>

<template>
  <div class="min-h-screen bg-[#1a0533] text-white flex flex-col items-center justify-center px-4 py-8">
    <!-- Winner message for top 3 -->
    <template v-if="isTopThree">
      <div class="text-7xl mb-2">{{ MEDALS[myRank - 1] }}</div>
      <h1 class="text-4xl font-black text-center mb-1">
        {{ myRank === 1 ? '🎉 You won!' : myRank === 2 ? '🥈 2nd place!' : '🥉 3rd place!' }}
      </h1>
      <p class="text-white/60 mb-8">{{ myEntry?.totalScore.toLocaleString() ?? 0 }} points</p>
    </template>

    <!-- Non-winner message -->
    <template v-else>
      <h1 class="text-4xl font-black text-center mb-2">Game over!</h1>
      <p class="text-white/60 text-xl mb-1">
        You finished <span class="font-black text-white">#{{ myRank }}</span>
      </p>
      <p class="text-white/50 text-sm mb-8">{{ myEntry?.totalScore.toLocaleString() ?? 0 }} points</p>
    </template>

    <!-- Podium (top 3) -->
    <div class="flex items-end justify-center gap-3 mb-10">
      <div
        v-for="slot in podiumSlots"
        :key="slot"
        class="flex flex-col items-center gap-1"
      >
        <template v-if="top3[slot]">
          <PlayerAvatar :avatar-id="top3[slot].avatarId" size="lg" />
          <span class="text-lg">{{ MEDALS[slot] }}</span>
          <p class="text-sm font-bold max-w-20 text-center truncate">{{ top3[slot].nickname }}</p>
          <div
            :class="['w-20 rounded-t-xl flex items-center justify-center font-black text-slate-800', podiumHeight(slot + 1), podiumColor(slot + 1)]"
          >
            {{ slot + 1 }}
          </div>
        </template>
      </div>
    </div>

    <!-- Rest of standings (if any) -->
    <div
      v-if="standings.length > 3"
      class="w-full max-w-xs bg-white/10 rounded-2xl divide-y divide-white/10 mb-6"
    >
      <div
        v-for="entry in standings.slice(3, 8)"
        :key="entry.participantId"
        :class="[
          'flex items-center gap-3 px-4 py-2',
          entry.participantId === myParticipantId ? 'bg-koot-purple/60' : '',
        ]"
      >
        <span class="text-sm font-bold opacity-60 w-5">{{ entry.rank }}</span>
        <PlayerAvatar :avatar-id="entry.avatarId" size="sm" />
        <span class="flex-1 text-sm font-semibold truncate">
          {{ entry.nickname }}
          <span v-if="entry.participantId === myParticipantId" class="opacity-60 text-xs ml-1">(you)</span>
        </span>
        <span class="text-sm font-black tabular-nums">{{ entry.totalScore.toLocaleString() }}</span>
      </div>
    </div>

    <button
      class="px-10 py-4 rounded-2xl font-black text-xl bg-koot-blue text-white shadow-lg hover:opacity-90 active:scale-95 transition-all"
      @click="emit('playAgain')"
    >
      🔄 Play Again
    </button>
  </div>
</template>
