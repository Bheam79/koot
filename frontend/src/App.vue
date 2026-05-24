<script setup lang="ts">
import { RouterLink, RouterView } from 'vue-router'
</script>

<template>
  <div class="min-h-screen flex flex-col">
    <header class="bg-koot-purple text-white shadow">
      <div class="max-w-6xl mx-auto px-4 py-3 flex items-center justify-between">
        <RouterLink to="/" class="text-xl font-bold tracking-wide">Koot!</RouterLink>
        <nav class="flex gap-4 text-sm">
          <RouterLink to="/join" class="hover:underline">Join</RouterLink>
          <RouterLink to="/dashboard" class="hover:underline">Dashboard</RouterLink>
          <RouterLink to="/login" class="hover:underline">Log in</RouterLink>
          <RouterLink to="/register" class="hover:underline">Sign up</RouterLink>
        </nav>
      </div>
    </header>

    <main class="flex-1">
      <RouterView />
    </main>

    <footer class="bg-slate-100 border-t border-slate-200 text-xs text-slate-500">
      <div class="max-w-6xl mx-auto px-4 py-3">Koot prototype</div>
    </footer>
  </div>
</template>
