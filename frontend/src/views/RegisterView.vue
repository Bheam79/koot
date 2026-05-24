<script setup lang="ts">
import { computed, ref } from 'vue'
import { RouterLink, useRouter } from 'vue-router'
import { useAuthStore } from '../stores/auth'

const username = ref('')
const email = ref('')
const password = ref('')
const confirm = ref('')
const localError = ref<string | null>(null)
const auth = useAuthStore()
const router = useRouter()

const passwordsMatch = computed(() => password.value === confirm.value)

async function onSubmit() {
  localError.value = null
  if (!passwordsMatch.value) {
    localError.value = 'Passwords do not match.'
    return
  }
  if (password.value.length < 6) {
    localError.value = 'Password must be at least 6 characters.'
    return
  }
  const ok = await auth.register(username.value, email.value, password.value)
  if (ok) {
    router.push('/dashboard')
  }
}
</script>

<template>
  <section class="max-w-md mx-auto px-4 py-12">
    <h1 class="text-2xl font-bold mb-6">Create an account</h1>

    <form class="space-y-4" @submit.prevent="onSubmit">
      <div>
        <label for="reg-username" class="block text-sm font-medium text-slate-700 mb-1">Username</label>
        <input
          id="reg-username"
          v-model="username"
          type="text"
          required
          minlength="3"
          maxlength="50"
          autocomplete="username"
          class="w-full rounded-lg border border-slate-300 px-3 py-2 focus:outline-none focus:ring-2 focus:ring-koot-blue"
        />
      </div>

      <div>
        <label for="reg-email" class="block text-sm font-medium text-slate-700 mb-1">Email</label>
        <input
          id="reg-email"
          v-model="email"
          type="email"
          required
          autocomplete="email"
          class="w-full rounded-lg border border-slate-300 px-3 py-2 focus:outline-none focus:ring-2 focus:ring-koot-blue"
        />
      </div>

      <div>
        <label for="reg-password" class="block text-sm font-medium text-slate-700 mb-1">Password</label>
        <input
          id="reg-password"
          v-model="password"
          type="password"
          required
          minlength="6"
          autocomplete="new-password"
          class="w-full rounded-lg border border-slate-300 px-3 py-2 focus:outline-none focus:ring-2 focus:ring-koot-blue"
        />
      </div>

      <div>
        <label for="reg-confirm" class="block text-sm font-medium text-slate-700 mb-1">Confirm password</label>
        <input
          id="reg-confirm"
          v-model="confirm"
          type="password"
          required
          autocomplete="new-password"
          class="w-full rounded-lg border border-slate-300 px-3 py-2 focus:outline-none focus:ring-2 focus:ring-koot-blue"
        />
      </div>

      <p v-if="localError || auth.error" class="text-sm text-koot-magenta" role="alert">
        {{ localError || auth.error }}
      </p>

      <button
        type="submit"
        :disabled="auth.loading"
        class="w-full bg-koot-blue text-white font-semibold py-2 rounded-lg disabled:opacity-50 hover:opacity-90"
      >
        {{ auth.loading ? 'Creating account…' : 'Sign up' }}
      </button>
    </form>

    <p class="text-sm text-slate-500 mt-6 text-center">
      Already have an account?
      <RouterLink to="/login" class="text-koot-blue underline">Log in</RouterLink>
    </p>
  </section>
</template>
