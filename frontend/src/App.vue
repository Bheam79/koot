<script setup lang="ts">
import { RouterLink, RouterView, useRouter } from 'vue-router'
import { useAuthStore } from './stores/auth'
import { useSound } from './composables/useSound'
import ToastContainer from './components/ToastContainer.vue'

const auth = useAuthStore()
const router = useRouter()
const { soundEnabled } = useSound()

function onLogout() {
  auth.logout()
  router.push('/')
}
</script>

<template>
  <div class="min-h-screen flex flex-col">
    <header class="bg-koot-purple text-white shadow">
      <div class="max-w-6xl mx-auto px-4 py-3 flex items-center justify-between">
        <RouterLink to="/" class="text-xl font-black tracking-wide hover:opacity-90 transition-opacity">
          🎮 Koot!
        </RouterLink>
        <nav class="flex gap-3 sm:gap-4 text-sm items-center">
          <RouterLink to="/join" class="hover:underline font-medium">Join</RouterLink>

          <template v-if="auth.isAuthenticated">
            <RouterLink to="/dashboard" class="hover:underline font-medium">Dashboard</RouterLink>
            <span class="text-white/70 hidden sm:inline">{{ auth.user?.username }}</span>
            <button type="button" class="hover:underline font-medium" @click="onLogout">Log out</button>
          </template>
          <template v-else>
            <RouterLink to="/login" class="hover:underline font-medium">Log in</RouterLink>
            <RouterLink
              to="/register"
              class="bg-white/20 hover:bg-white/30 transition-colors px-3 py-1 rounded-lg font-semibold"
            >
              Sign up
            </RouterLink>
          </template>

          <!-- Sound toggle -->
          <button
            type="button"
            :title="soundEnabled ? 'Sound on (click to mute)' : 'Sound off (click to enable)'"
            class="text-lg hover:scale-110 transition-transform"
            @click="soundEnabled = !soundEnabled"
          >
            {{ soundEnabled ? '🔊' : '🔇' }}
          </button>
        </nav>
      </div>
    </header>

    <main class="flex-1">
      <RouterView />
    </main>

    <footer class="bg-slate-100 border-t border-slate-200 text-xs text-slate-500">
      <div class="max-w-6xl mx-auto px-4 py-3">Koot! — play together in real time</div>
    </footer>
  </div>

  <!-- Global toast notifications -->
  <ToastContainer />
</template>
