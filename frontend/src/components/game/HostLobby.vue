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
  fullscreen: []
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
    <header class="flex items-center justify-between px-4 sm:px-6 py-4 bg-black/20">
      <div class="min-w-0">
        <p class="text-xs font-medium text-white/70 uppercase tracking-widest">Quiz</p>
        <h1 class="text-lg sm:text-xl font-bold truncate max-w-xs">{{ quizTitle }}</h1>
      </div>
      <div class="flex items-center gap-3">
        <div class="text-right">
          <p class="text-xs text-white/70">Players</p>
          <p class="text-2xl sm:text-3xl font-black">{{ participants.length }}</p>
        </div>
        <button
          class="bg-white/10 hover:bg-white/20 transition-colors rounded-lg p-2 text-lg"
          title="Toggle fullscreen"
          @click="emit('fullscreen')"
        >
          ⛶
        </button>
      </div>
    </header>

    <div class="flex-1 flex flex-col lg:flex-row gap-0 overflow-hidden">
      <!-- Left: PIN + QR -->
      <div class="flex flex-col items-center justify-center gap-4 sm:gap-6 px-6 sm:px-8 py-6 sm:py-10 lg:w-80 bg-black/10 lg:overflow-y-auto">
        <div class="text-center">
          <p class="text-xs font-semibold uppercase tracking-widest text-white/60 mb-1">
            Join at
          </p>
          <p class="text-sm font-bold text-white/90">
            {{ joinUrl.replace(/^https?:\/\//, '') }}
          </p>
        </div>

        <!-- QR code — hidden on very small screens to save space -->
        <div class="bg-white rounded-2xl p-3 shadow-2xl hidden sm:block">
          <QrcodeVue :value="joinUrl" :size="160" level="M" render-as="svg" />
        </div>

        <div class="text-center">
          <p class="text-xs font-semibold uppercase tracking-widest text-white/60 mb-1">
            Game PIN
          </p>
          <p class="text-5xl sm:text-6xl font-black tracking-widest tabular-nums select-all">
            {{ displayCode }}
          </p>
        </div>

        <button
          :disabled="!canStart || starting"
          class="mt-2 sm:mt-4 w-full max-w-xs px-10 py-4 rounded-2xl font-black text-xl shadow-lg transition-all
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
      <div class="flex-1 p-4 sm:p-6 overflow-y-auto">
        <p v-if="participants.length === 0" class="text-center text-white/40 mt-12 sm:mt-20 text-lg">
          Waiting for players to join…
        </p>
        <TransitionGroup
          tag="div"
          name="player-in"
          class="grid grid-cols-3 sm:grid-cols-4 md:grid-cols-5 xl:grid-cols-6 gap-2 sm:gap-3"
        >
          <div
            v-for="p in participants"
            :key="p.id"
            :class="[
              'flex flex-col items-center gap-1 sm:gap-2 rounded-2xl py-3 sm:py-4 px-1 sm:px-2',
              p.isDisconnected ? 'bg-white/5 opacity-40' : 'bg-white/10',
            ]"
          >
            <PlayerAvatar :avatar-id="p.avatarId" size="md" />
            <span class="text-xs sm:text-sm font-semibold text-center truncate w-full text-center">
              {{ p.nickname }}
            </span>
            <span v-if="p.isDisconnected" class="text-xs text-white/50">left</span>
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
