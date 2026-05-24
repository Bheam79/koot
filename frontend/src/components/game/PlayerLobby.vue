<script setup lang="ts">
import PlayerAvatar from './PlayerAvatar.vue'

defineProps<{
  nickname: string
  avatarId: number
  quizTitle: string
  playerCount: number
}>()
</script>

<template>
  <div class="min-h-screen bg-koot-purple flex flex-col items-center justify-center px-4 text-white">
    <!-- Player card -->
    <div class="bg-white/15 rounded-3xl px-8 py-8 text-center shadow-xl mb-8 w-full max-w-xs">
      <PlayerAvatar :avatar-id="avatarId" size="xl" />
      <p class="text-2xl font-black mt-4">{{ nickname }}</p>
      <p class="text-white/60 text-sm mt-1">That's you!</p>
    </div>

    <!-- Game info -->
    <div class="text-center mb-8">
      <p class="text-white/60 text-sm uppercase tracking-widest font-medium mb-1">Playing</p>
      <h2 class="text-xl font-bold">{{ quizTitle }}</h2>
    </div>

    <!-- Waiting animation -->
    <div class="flex flex-col items-center gap-4">
      <div class="flex gap-2">
        <span
          v-for="i in 3"
          :key="i"
          class="w-3 h-3 rounded-full bg-white animate-bounce"
          :style="{ animationDelay: `${(i - 1) * 0.15}s` }"
        />
      </div>
      <p class="text-white/70 font-semibold">Waiting for the host to start…</p>
    </div>

    <!-- Player count -->
    <div class="mt-8 bg-white/10 rounded-full px-6 py-2 text-sm font-bold">
      👥 {{ playerCount }} {{ playerCount === 1 ? 'player' : 'players' }} in the lobby
    </div>
  </div>
</template>
