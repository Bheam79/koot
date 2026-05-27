<script setup lang="ts">
import { computed, ref } from 'vue'
import { RouterLink, useRouter, useRoute } from 'vue-router'
import { useAuthStore } from '../stores/auth'

const email = ref('')
const password = ref('')
const auth = useAuthStore()
const router = useRouter()
const route = useRoute()

const sessionExpired = computed(() => route.query.reason === 'session-expired')
const passwordReset = computed(() => route.query.reason === 'password-reset')

async function onSubmit() {
  const ok = await auth.login(email.value, password.value)
  if (ok) {
    const next = typeof route.query.next === 'string' ? route.query.next : '/dashboard'
    router.push(next)
  }
}
</script>

<template>
  <section class="max-w-md mx-auto px-4 py-12">
    <h1 class="text-2xl font-bold mb-6">Log in</h1>

    <p
      v-if="sessionExpired"
      class="mb-4 rounded-lg border border-amber-200 bg-amber-50 px-3 py-2 text-sm text-amber-800"
      role="status"
    >
      Your session expired. Please log in again to continue.
    </p>

    <p
      v-if="passwordReset"
      class="mb-4 rounded-lg border border-emerald-200 bg-emerald-50 px-3 py-2 text-sm text-emerald-800"
      role="status"
    >
      Your password has been reset. You can now log in with your new password.
    </p>

    <form class="space-y-4" @submit.prevent="onSubmit">
      <div>
        <label for="login-email" class="block text-sm font-medium text-slate-700 mb-1">Email</label>
        <input
          id="login-email"
          v-model="email"
          type="email"
          required
          autocomplete="email"
          class="w-full rounded-lg border border-slate-300 px-3 py-2 focus:outline-none focus:ring-2 focus:ring-koot-blue"
        />
      </div>

      <div>
        <label for="login-password" class="block text-sm font-medium text-slate-700 mb-1">Password</label>
        <input
          id="login-password"
          v-model="password"
          type="password"
          required
          autocomplete="current-password"
          class="w-full rounded-lg border border-slate-300 px-3 py-2 focus:outline-none focus:ring-2 focus:ring-koot-blue"
        />
      </div>

      <p v-if="auth.error" class="text-sm text-koot-magenta" role="alert">{{ auth.error }}</p>

      <button
        type="submit"
        :disabled="auth.loading"
        class="w-full bg-koot-blue text-white font-semibold py-2 rounded-lg disabled:opacity-50 hover:opacity-90"
      >
        {{ auth.loading ? 'Logging in…' : 'Log in' }}
      </button>
    </form>

    <p class="text-sm text-center mt-4">
      <RouterLink to="/forgot-password" class="text-koot-blue underline">
        Forgot your password?
      </RouterLink>
    </p>

    <p class="text-sm text-slate-500 mt-6 text-center">
      Don't have an account?
      <RouterLink to="/register" class="text-koot-blue underline">Sign up</RouterLink>
    </p>
  </section>
</template>
