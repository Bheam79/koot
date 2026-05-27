<script setup lang="ts">
import { computed, ref } from 'vue'
import { useRouter } from 'vue-router'
import api from '../services/api'
import { useToast } from '../composables/useToast'

const currentPassword = ref('')
const newPassword = ref('')
const confirmPassword = ref('')
const localError = ref<string | null>(null)
const apiError = ref<string | null>(null)
const loading = ref(false)

const router = useRouter()
const toast = useToast()

const passwordsMatch = computed(() => newPassword.value === confirmPassword.value)

function extractError(err: unknown, fallback: string): string {
  const e = err as { response?: { data?: unknown } }
  const data = e?.response?.data as Record<string, unknown> | string | undefined
  if (typeof data === 'string') return data
  if (data && typeof data === 'object') {
    if (typeof data.error === 'string') return data.error
    if (typeof data.title === 'string') return data.title
    if (data.errors && typeof data.errors === 'object') {
      const first = Object.values(data.errors as Record<string, string[]>)[0]
      if (Array.isArray(first) && first.length) return first[0]
    }
  }
  return fallback
}

async function onSubmit() {
  localError.value = null
  apiError.value = null

  if (newPassword.value.length < 8) {
    localError.value = 'New password must be at least 8 characters.'
    return
  }
  if (!passwordsMatch.value) {
    localError.value = 'New passwords do not match.'
    return
  }

  loading.value = true
  try {
    await api.post('/api/auth/change-password', {
      currentPassword: currentPassword.value,
      newPassword: newPassword.value,
    })
    toast.success('Password changed successfully.')
    router.push('/')
  } catch (e: unknown) {
    apiError.value = extractError(e, 'Failed to change password.')
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <section class="max-w-md mx-auto px-4 py-12">
    <h1 class="text-2xl font-bold mb-6">Change password</h1>

    <form class="space-y-4" @submit.prevent="onSubmit">
      <div>
        <label for="cp-current" class="block text-sm font-medium text-slate-700 mb-1">
          Current password
        </label>
        <input
          id="cp-current"
          v-model="currentPassword"
          type="password"
          required
          autocomplete="current-password"
          class="w-full rounded-lg border border-slate-300 px-3 py-2 focus:outline-none focus:ring-2 focus:ring-koot-blue"
        />
      </div>

      <div>
        <label for="cp-new" class="block text-sm font-medium text-slate-700 mb-1">
          New password
        </label>
        <input
          id="cp-new"
          v-model="newPassword"
          type="password"
          required
          minlength="8"
          autocomplete="new-password"
          class="w-full rounded-lg border border-slate-300 px-3 py-2 focus:outline-none focus:ring-2 focus:ring-koot-blue"
        />
      </div>

      <div>
        <label for="cp-confirm" class="block text-sm font-medium text-slate-700 mb-1">
          Confirm new password
        </label>
        <input
          id="cp-confirm"
          v-model="confirmPassword"
          type="password"
          required
          minlength="8"
          autocomplete="new-password"
          class="w-full rounded-lg border border-slate-300 px-3 py-2 focus:outline-none focus:ring-2 focus:ring-koot-blue"
        />
      </div>

      <p v-if="localError || apiError" class="text-sm text-koot-magenta" role="alert">
        {{ localError || apiError }}
      </p>

      <button
        type="submit"
        :disabled="loading"
        class="w-full bg-koot-blue text-white font-semibold py-2 rounded-lg disabled:opacity-50 hover:opacity-90"
      >
        {{ loading ? 'Saving…' : 'Change password' }}
      </button>
    </form>
  </section>
</template>
