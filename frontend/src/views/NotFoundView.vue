<script setup lang="ts">
import { RouterLink } from 'vue-router'
</script>

<template>
  <section class="max-w-md mx-auto px-4 py-16 text-center">
    <h1 class="text-3xl font-bold mb-3">404</h1>
    <p class="text-slate-500 mb-6">That route doesn't exist.</p>
    <RouterLink to="/" class="text-koot-blue underline">Back home</RouterLink>
  </section>
</template>
