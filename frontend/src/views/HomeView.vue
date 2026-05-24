<script setup lang="ts">
import { RouterLink } from 'vue-router'
</script>

<template>
  <section class="max-w-3xl mx-auto px-4 py-16 text-center">
    <h1 class="text-5xl font-extrabold text-koot-purple mb-4">Koot!</h1>
    <p class="text-lg text-slate-600 mb-8">
      A friendly quiz game. Host a session, join with a code, and play together in real time.
    </p>
    <div class="flex justify-center gap-3">
      <RouterLink to="/join" class="px-6 py-3 rounded-lg bg-koot-magenta text-white font-semibold shadow hover:opacity-90">
        Join a game
      </RouterLink>
      <RouterLink to="/dashboard" class="px-6 py-3 rounded-lg bg-koot-blue text-white font-semibold shadow hover:opacity-90">
        Create a quiz
      </RouterLink>
    </div>
  </section>
</template>
