<script setup lang="ts">
import { ref, watch } from 'vue'
import PlayerAvatar from './PlayerAvatar.vue'
import type { LeaderboardEntry } from '../../types/game'

const props = defineProps<{
  entries: LeaderboardEntry[]
  questionIndex: number
  totalQuestions: number
}>()

const emit = defineEmits<{
  next: []
  end: []
}>()

const isLastQuestion = props.questionIndex + 1 >= props.totalQuestions

// Track previous ranks to animate rank changes
const prevRanks = ref<Map<number, number>>(new Map())

watch(
  () => props.entries,
  (newEntries) => {
    prevRanks.value = new Map(newEntries.map((e) => [e.participantId, e.rank]))
  },
  { immediate: true },
)

function rankChange(entry: LeaderboardEntry) {
  const prev = prevRanks.value.get(entry.participantId)
  if (prev == null) return null
  return prev - entry.rank // positive = moved up
}

const MEDALS = ['🥇', '🥈', '🥉']
</script>

<template>
  <div class="min-h-screen bg-[#1a0533] text-white flex flex-col">
    <header class="px-6 py-5 text-center bg-black/30">
      <h2 class="text-3xl font-black">Leaderboard</h2>
      <p class="text-white/60 text-sm mt-1">After Q{{ questionIndex + 1 }} / {{ totalQuestions }}</p>
    </header>

    <div class="flex-1 px-6 py-4 max-w-2xl mx-auto w-full">
      <TransitionGroup name="lb" tag="div" class="flex flex-col gap-2">
        <div
          v-for="entry in entries"
          :key="entry.participantId"
          class="flex items-center gap-4 bg-white/10 rounded-2xl px-5 py-3 shadow"
        >
          <!-- Rank -->
          <div class="w-10 text-center">
            <span v-if="entry.rank <= 3" class="text-2xl">{{ MEDALS[entry.rank - 1] }}</span>
            <span v-else class="text-xl font-black opacity-60">{{ entry.rank }}</span>
          </div>

          <PlayerAvatar :avatar-id="entry.avatarId" size="sm" />

          <div class="flex-1 min-w-0">
            <p class="font-bold truncate">{{ entry.nickname }}</p>
          </div>

          <!-- Rank change indicator -->
          <div
            v-if="rankChange(entry) !== 0 && rankChange(entry) != null"
            :class="rankChange(entry)! > 0 ? 'text-koot-green' : 'text-koot-magenta'"
            class="text-sm font-bold"
          >
            {{ rankChange(entry)! > 0 ? `▲${rankChange(entry)}` : `▼${Math.abs(rankChange(entry)!)}` }}
          </div>

          <p class="text-xl font-black tabular-nums">{{ entry.totalScore.toLocaleString() }}</p>
        </div>
      </TransitionGroup>
    </div>

    <div class="p-6 flex justify-center">
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
        Show Final Podium 🏆
      </button>
    </div>
  </div>
</template>

<style scoped>
.lb-move {
  transition: transform 0.5s cubic-bezier(0.25, 0.46, 0.45, 0.94);
}
.lb-enter-active {
  transition: all 0.35s ease;
}
.lb-enter-from {
  opacity: 0;
  transform: translateX(-20px);
}
</style>
