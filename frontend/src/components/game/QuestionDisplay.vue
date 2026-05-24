<script setup lang="ts">
import TimerCircle from './TimerCircle.vue'
import type { QuestionBroadcast } from '../../types/game'

const props = defineProps<{
  question: QuestionBroadcast
  secondsLeft: number
  answeredCount: number
  totalPlayers: number
  questionIndex: number
  totalQuestions: number
}>()

// Kahoot-style shape + colour per option slot
const OPTION_STYLES = [
  { bg: 'bg-koot-magenta', label: 'A', shape: '▲' },
  { bg: 'bg-koot-blue',    label: 'B', shape: '◆' },
  { bg: 'bg-yellow-400',   label: 'C', shape: '●' },
  { bg: 'bg-koot-green',   label: 'D', shape: '■' },
]
</script>

<template>
  <div class="min-h-screen bg-[#1a0533] text-white flex flex-col">
    <!-- Top bar -->
    <header class="flex items-center justify-between px-6 py-3 bg-black/30">
      <span class="text-sm font-bold text-white/60 uppercase tracking-widest">
        Q{{ questionIndex + 1 }}/{{ totalQuestions }}
      </span>
      <TimerCircle :seconds="secondsLeft" :total="question.timeLimit" />
      <div class="text-right text-sm">
        <p class="text-white/60">Answered</p>
        <p class="font-bold text-lg">{{ answeredCount }} / {{ totalPlayers }}</p>
      </div>
    </header>

    <!-- Question text + image -->
    <div class="flex-1 flex flex-col items-center justify-center px-8 py-6 gap-4">
      <img
        v-if="question.imageUrl"
        :src="question.imageUrl"
        class="max-h-48 rounded-xl object-cover shadow-xl"
        alt=""
      />
      <h2 class="text-3xl md:text-5xl font-black text-center leading-tight max-w-4xl">
        {{ question.questionText }}
      </h2>
      <p class="text-white/50 text-sm">{{ question.points }} pts</p>
    </div>

    <!-- Answer options grid -->
    <div class="grid grid-cols-2 gap-2 p-3">
      <div
        v-for="(opt, i) in question.answerOptions"
        :key="opt.id"
        :class="['rounded-2xl p-4 flex items-center gap-3 min-h-[5rem] shadow-lg', OPTION_STYLES[i % 4].bg]"
      >
        <span class="text-2xl font-black opacity-80">{{ OPTION_STYLES[i % 4].shape }}</span>
        <span class="text-lg md:text-xl font-bold leading-snug">{{ opt.text }}</span>
      </div>
    </div>
  </div>
</template>
