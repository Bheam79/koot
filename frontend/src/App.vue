<script setup lang="ts">
import { onBeforeUnmount, onMounted, ref } from 'vue'
import { RouterLink, RouterView, useRouter } from 'vue-router'
import { useAuthStore } from './stores/auth'
import { useSound } from './composables/useSound'
import ToastContainer from './components/ToastContainer.vue'

const auth = useAuthStore()
const router = useRouter()
const { soundEnabled } = useSound()

const profileMenuOpen = ref(false)
const profileMenuRef = ref<HTMLElement | null>(null)

function toggleProfileMenu() {
  profileMenuOpen.value = !profileMenuOpen.value
}

function closeProfileMenu() {
  profileMenuOpen.value = false
}

function onClickOutside(event: MouseEvent) {
  if (!profileMenuOpen.value) return
  const el = profileMenuRef.value
  if (el && !el.contains(event.target as Node)) {
    profileMenuOpen.value = false
  }
}

function onLogout() {
  closeProfileMenu()
  auth.logout()
  router.push('/')
}

function goChangePassword() {
  closeProfileMenu()
  router.push('/change-password')
}

onMounted(() => {
  document.addEventListener('click', onClickOutside)
})
onBeforeUnmount(() => {
  document.removeEventListener('click', onClickOutside)
})
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
            <div ref="profileMenuRef" class="relative">
              <button
                type="button"
                aria-haspopup="menu"
                :aria-expanded="profileMenuOpen"
                class="flex items-center gap-2 rounded-full bg-white/15 hover:bg-white/25 transition-colors px-2 py-1 font-medium"
                @click.stop="toggleProfileMenu"
              >
                <span
                  class="inline-flex items-center justify-center w-7 h-7 rounded-full bg-koot-magenta text-white text-xs font-bold uppercase"
                  aria-hidden="true"
                >
                  {{ auth.user?.username?.charAt(0) || '?' }}
                </span>
                <span class="hidden sm:inline">{{ auth.user?.username }}</span>
                <span aria-hidden="true" class="text-xs">▾</span>
              </button>

              <div
                v-if="profileMenuOpen"
                role="menu"
                class="absolute right-0 mt-2 w-48 rounded-lg bg-white text-slate-800 shadow-lg border border-slate-200 overflow-hidden z-50"
              >
                <button
                  type="button"
                  role="menuitem"
                  class="w-full text-left px-4 py-2 text-sm hover:bg-slate-100"
                  @click="goChangePassword"
                >
                  Change password
                </button>
                <button
                  type="button"
                  role="menuitem"
                  class="w-full text-left px-4 py-2 text-sm hover:bg-slate-100 border-t border-slate-100"
                  @click="onLogout"
                >
                  Log out
                </button>
              </div>
            </div>
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
