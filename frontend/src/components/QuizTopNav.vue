<script setup lang="ts">
// Sub-nav used by the dashboard / editor / history screens. The main header in
// App.vue still renders the global brand + auth links; this is a contextual bar
// underneath it.
import { RouterLink, useRouter } from 'vue-router'
import { useAuthStore } from '../stores/auth'

defineProps<{
  title?: string
}>()

const auth = useAuthStore()
const router = useRouter()

function onLogout() {
  auth.logout()
  router.push('/')
}
</script>

<template>
  <div class="border-b border-slate-200 bg-white">
    <div class="max-w-6xl mx-auto px-4 py-3 flex items-center justify-between gap-3">
      <h1 class="text-lg font-semibold text-slate-800 truncate">{{ title ?? 'Koot' }}</h1>
      <div class="flex items-center gap-3 text-sm">
        <nav v-if="auth.isAuthenticated" class="flex items-center gap-1">
          <RouterLink
            to="/dashboard"
            class="px-3 py-1.5 rounded-md text-slate-700 hover:bg-slate-100"
            active-class="bg-slate-100 text-slate-900 font-medium"
          >
            Quizzes
          </RouterLink>
          <RouterLink
            to="/history"
            class="px-3 py-1.5 rounded-md text-slate-700 hover:bg-slate-100"
            active-class="bg-slate-100 text-slate-900 font-medium"
          >
            Past games
          </RouterLink>
        </nav>
        <span v-if="auth.user" class="text-slate-600">
          Signed in as <strong>{{ auth.user.username }}</strong>
        </span>
        <button
          type="button"
          class="px-3 py-1.5 rounded-md border border-slate-300 hover:bg-slate-50"
          @click="onLogout"
        >
          Log out
        </button>
      </div>
    </div>
  </div>
</template>
