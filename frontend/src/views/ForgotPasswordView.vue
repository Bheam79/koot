<script setup lang="ts">
import { ref } from 'vue'
import { RouterLink } from 'vue-router'
import api from '../services/api'

const email = ref('')
const submitted = ref(false)
const loading = ref(false)

const MESSAGE = 'If that email is registered, a reset link has been sent.'

async function onSubmit() {
  loading.value = true
  try {
    // Result intentionally ignored — we always show the same message so we
    // don't reveal whether the email is registered.
    await api.post('/api/auth/forgot-password', { email: email.value })
  } catch {
    // Swallow errors on purpose; see comment above.
  } finally {
    loading.value = false
    submitted.value = true
  }
}
</script>

<template>
  <section class="max-w-md mx-auto px-4 py-12">
    <h1 class="text-2xl font-bold mb-6">Forgot password</h1>

    <p
      v-if="submitted"
      class="mb-4 rounded-lg border border-emerald-200 bg-emerald-50 px-3 py-2 text-sm text-emerald-800"
      role="status"
    >
      {{ MESSAGE }}
    </p>

    <form v-if="!submitted" class="space-y-4" @submit.prevent="onSubmit">
      <p class="text-sm text-slate-600">
        Enter the email associated with your account and we'll send you a link to reset your password.
      </p>

      <div>
        <label for="fp-email" class="block text-sm font-medium text-slate-700 mb-1">Email</label>
        <input
          id="fp-email"
          v-model="email"
          type="email"
          required
          autocomplete="email"
          class="w-full rounded-lg border border-slate-300 px-3 py-2 focus:outline-none focus:ring-2 focus:ring-koot-blue"
        />
      </div>

      <button
        type="submit"
        :disabled="loading"
        class="w-full bg-koot-blue text-white font-semibold py-2 rounded-lg disabled:opacity-50 hover:opacity-90"
      >
        {{ loading ? 'Sending…' : 'Send reset link' }}
      </button>
    </form>

    <p class="text-sm text-slate-500 mt-6 text-center">
      Remembered your password?
      <RouterLink to="/login" class="text-koot-blue underline">Log in</RouterLink>
    </p>
  </section>
</template>
