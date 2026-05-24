<script setup lang="ts">
import { RouterLink, RouterView, useRouter } from 'vue-router'
import { useAuthStore } from './stores/auth'

const auth = useAuthStore()
const router = useRouter()

function onLogout() {
  auth.logout()
  router.push('/')
}
</script>

<template>
  <div class="min-h-screen flex flex-col">
    <header class="bg-koot-purple text-white shadow">
      <div class="max-w-6xl mx-auto px-4 py-3 flex items-center justify-between">
        <RouterLink to="/" class="text-xl font-bold tracking-wide">Koot!</RouterLink>
        <nav class="flex gap-4 text-sm items-center">
          <RouterLink to="/join" class="hover:underline">Join</RouterLink>

          <template v-if="auth.isAuthenticated">
            <RouterLink to="/dashboard" class="hover:underline">Dashboard</RouterLink>
            <span class="text-white/70">{{ auth.user?.username }}</span>
            <button type="button" class="hover:underline" @click="onLogout">Log out</button>
          </template>
          <template v-else>
            <RouterLink to="/login" class="hover:underline">Log in</RouterLink>
            <RouterLink to="/register" class="hover:underline">Sign up</RouterLink>
          </template>
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
