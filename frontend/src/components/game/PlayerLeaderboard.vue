<script setup lang="ts">
import PlayerAvatar from './PlayerAvatar.vue'
import type { LeaderboardEntry } from '../../types/game'

defineProps<{
  entries: LeaderboardEntry[]
  myParticipantId: number
}>()

const MEDALS = ['🥇', '🥈', '🥉']
</script>

<template>
  <div class="min-h-screen bg-[#1a0533] text-white flex flex-col">
    <header class="px-6 py-5 text-center bg-black/30">
      <h2 class="text-2xl font-black">Leaderboard</h2>
      <p class="text-white/50 text-sm mt-1">Top 10</p>
    </header>

    <div class="flex-1 px-4 py-4 overflow-y-auto">
      <TransitionGroup name="lb" tag="div" class="flex flex-col gap-2">
        <div
          v-for="entry in entries"
          :key="entry.participantId"
          :class="[
            'flex items-center gap-3 rounded-2xl px-4 py-3 shadow',
            entry.participantId === myParticipantId
              ? 'bg-koot-purple ring-2 ring-white'
              : 'bg-white/10',
          ]"
        >
          <div class="w-8 text-center">
            <span v-if="entry.rank <= 3" class="text-xl">{{ MEDALS[entry.rank - 1] }}</span>
            <span v-else class="font-black opacity-50">{{ entry.rank }}</span>
          </div>
          <PlayerAvatar :avatar-id="entry.avatarId" size="sm" />
          <div class="flex-1 min-w-0">
            <p class="font-bold truncate">
              {{ entry.nickname }}
              <span v-if="entry.participantId === myParticipantId" class="text-xs font-normal opacity-70 ml-1">(you)</span>
            </p>
          </div>
          <p class="font-black tabular-nums text-lg">{{ entry.totalScore.toLocaleString() }}</p>
        </div>
      </TransitionGroup>
    </div>

    <p class="text-center text-white/40 text-sm py-4 animate-pulse">
      Waiting for next question…
    </p>
  </div>
</template>

<style scoped>
.lb-move {
  transition: transform 0.5s ease;
}
.lb-enter-active {
  transition: all 0.3s ease;
}
.lb-enter-from {
  opacity: 0;
  transform: translateX(-20px);
}
</style>
