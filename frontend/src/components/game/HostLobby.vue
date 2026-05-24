<script setup lang="ts">
import { computed } from 'vue'
import QrcodeVue from 'qrcode.vue'
import PlayerAvatar from './PlayerAvatar.vue'
import type { Participant } from '../../types/game'

const props = defineProps<{
  code: string
  quizTitle: string
  participants: Participant[]
  canStart: boolean
  starting: boolean
}>()

const emit = defineEmits<{
  start: []
}>()

const joinUrl = computed(() => `${window.location.origin}/join?code=${props.code}`)

/** Format the code as "ABC 123" for readability */
const displayCode = computed(() => {
  const c = props.code
  return c.length === 6 ? `${c.slice(0, 3)} ${c.slice(3)}` : c
})
</script>

<template>
  <div class="min-h-screen bg-koot-purple text-white flex flex-col">
    <!-- Top bar -->
    <header class="flex items-center justify-between px-6 py-4 bg-black/20">
      <div>
        <p class="text-sm font-medium text-white/70 uppercase tracking-widest">Quiz</p>
        <h1 class="text-xl font-bold truncate max-w-xs">{{ quizTitle }}</h1>
      </div>
      <div class="text-right">
        <p class="text-sm text-white/70">Players</p>
        <p class="text-3xl font-black">{{ participants.length }}</p>
      </div>
    </header>

    <div class="flex-1 flex flex-col lg:flex-row gap-0">
      <!-- Left: PIN + QR -->
      <div class="flex flex-col items-center justify-center gap-6 px-8 py-10 lg:w-80 bg-black/10">
        <div class="text-center">
          <p class="text-sm font-semibold uppercase tracking-widest text-white/60 mb-1">
            Join at
          </p>
          <p class="text-lg font-bold text-white/90">
            {{ joinUrl.replace(/^https?:\/\//, '') }}
          </p>
        </div>

        <div class="bg-white rounded-2xl p-3 shadow-2xl">
          <QrcodeVue :value="joinUrl" :size="180" level="M" render-as="svg" />
        </div>

        <div class="text-center">
          <p class="text-sm font-semibold uppercase tracking-widest text-white/60 mb-1">
            Game PIN
          </p>
          <p class="text-6xl font-black tracking-widest tabular-nums">{{ displayCode }}</p>
        </div>

        <button
          :disabled="!canStart || starting"
          class="mt-4 px-10 py-4 rounded-2xl font-black text-xl shadow-lg transition-all
                 bg-koot-green text-white
                 disabled:opacity-40 disabled:cursor-not-allowed
                 enabled:hover:scale-105 enabled:active:scale-95"
          @click="emit('start')"
        >
          {{ starting ? 'Starting…' : 'Start Game' }}
        </button>

        <p v-if="!canStart" class="text-sm text-white/60 text-center">
          Waiting for at least 1 player…
        </p>
      </div>

      <!-- Right: player grid -->
      <div class="flex-1 p-6 overflow-y-auto">
        <p v-if="participants.length === 0" class="text-center text-white/40 mt-20 text-lg">
          Waiting for players to join…
        </p>
        <TransitionGroup
          tag="div"
          name="player-in"
          class="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 xl:grid-cols-5 gap-3"
        >
          <div
            v-for="p in participants"
            :key="p.id"
            class="flex flex-col items-center gap-2 bg-white/10 rounded-2xl py-4 px-2"
          >
            <PlayerAvatar :avatar-id="p.avatarId" size="lg" />
            <span class="text-sm font-semibold text-center truncate w-full text-center">
              {{ p.nickname }}
            </span>
          </div>
        </TransitionGroup>
      </div>
    </div>
  </div>
</template>

<style scoped>
.player-in-enter-active {
  transition: all 0.35s cubic-bezier(0.34, 1.56, 0.64, 1);
}
.player-in-enter-from {
  opacity: 0;
  transform: scale(0.5);
}
</style>
