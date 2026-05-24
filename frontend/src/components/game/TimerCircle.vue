<script setup lang="ts">
import { computed } from 'vue'

const props = defineProps<{
  seconds: number
  total: number
}>()

const SIZE = 120
const STROKE = 10
const R = (SIZE - STROKE) / 2
const CIRC = 2 * Math.PI * R

const progress = computed(() => {
  const fraction = Math.max(0, Math.min(1, props.seconds / props.total))
  return CIRC * (1 - fraction)
})

const colour = computed(() => {
  const frac = props.seconds / props.total
  if (frac > 0.5) return '#26890C'
  if (frac > 0.25) return '#FFA602'
  return '#E21B3C'
})
</script>

<template>
  <div class="relative inline-flex items-center justify-center">
    <svg :width="SIZE" :height="SIZE" class="-rotate-90">
      <!-- background ring -->
      <circle
        :cx="SIZE / 2"
        :cy="SIZE / 2"
        :r="R"
        fill="none"
        stroke="rgba(255,255,255,0.2)"
        :stroke-width="STROKE"
      />
      <!-- progress ring -->
      <circle
        :cx="SIZE / 2"
        :cy="SIZE / 2"
        :r="R"
        fill="none"
        :stroke="colour"
        :stroke-width="STROKE"
        stroke-linecap="round"
        :stroke-dasharray="CIRC"
        :stroke-dashoffset="progress"
        style="transition: stroke-dashoffset 0.9s linear, stroke 0.3s"
      />
    </svg>
    <span class="absolute text-3xl font-black text-white tabular-nums">{{ seconds }}</span>
  </div>
</template>
