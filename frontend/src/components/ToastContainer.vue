<script setup lang="ts">
import { useToast } from '../composables/useToast'

const { toasts, remove } = useToast()

const ICONS: Record<string, string> = {
  success: '✓',
  error:   '✕',
  warning: '⚠',
  info:    'ℹ',
}

const BG: Record<string, string> = {
  success: 'bg-emerald-600',
  error:   'bg-koot-magenta',
  warning: 'bg-amber-500',
  info:    'bg-koot-blue',
}
</script>

<template>
  <Teleport to="body">
    <div
      class="fixed bottom-6 left-1/2 -translate-x-1/2 z-[9999] flex flex-col gap-2 items-center pointer-events-none"
      style="min-width: 280px; max-width: 90vw;"
    >
      <TransitionGroup name="toast">
        <div
          v-for="toast in toasts"
          :key="toast.id"
          :class="['text-white px-5 py-3 rounded-2xl shadow-xl font-semibold flex items-center gap-3 pointer-events-auto', BG[toast.type]]"
        >
          <span class="text-lg">{{ ICONS[toast.type] }}</span>
          <span class="flex-1">{{ toast.message }}</span>
          <button
            class="opacity-70 hover:opacity-100 ml-2 text-sm leading-none"
            @click="remove(toast.id)"
          >
            ✕
          </button>
        </div>
      </TransitionGroup>
    </div>
  </Teleport>
</template>

<style scoped>
.toast-enter-active,
.toast-leave-active {
  transition: all 0.3s ease;
}
.toast-enter-from,
.toast-leave-to {
  opacity: 0;
  transform: translateY(16px) scale(0.95);
}
</style>
