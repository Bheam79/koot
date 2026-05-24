<script setup lang="ts">
defineProps<{
  avatarId: number
  size?: 'sm' | 'md' | 'lg' | 'xl'
}>()

// 12 emoji avatars keyed by ID 1..12
const AVATARS: Record<number, string> = {
  1: '🐶',
  2: '🐱',
  3: '🐭',
  4: '🐹',
  5: '🐰',
  6: '🦊',
  7: '🐻',
  8: '🐼',
  9: '🐨',
  10: '🐯',
  11: '🦁',
  12: '🐸',
}

function avatar(id: number) {
  return AVATARS[id] ?? '🎮'
}
</script>

<template>
  <span
    :class="[
      'inline-flex items-center justify-center rounded-full bg-white/20 font-emoji select-none',
      size === 'sm' ? 'w-8 h-8 text-lg' :
      size === 'lg' ? 'w-16 h-16 text-4xl' :
      size === 'xl' ? 'w-24 h-24 text-6xl' :
      'w-12 h-12 text-2xl',
    ]"
  >
    {{ avatar(avatarId) }}
  </span>
</template>
