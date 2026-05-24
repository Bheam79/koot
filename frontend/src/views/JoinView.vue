<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'

const route = useRoute()
const router = useRouter()

const BASE_URL =
  (import.meta.env.VITE_API_URL as string | undefined) ??
  (import.meta.env.VITE_API_BASE_URL as string | undefined) ??
  'http://localhost:5024'

const rawCode = ref('')
const loading = ref(false)
const errorMsg = ref<string | null>(null)

function normalise(val: string) {
  return val.replace(/[^a-zA-Z0-9]/g, '').toUpperCase().slice(0, 6)
}

function onInput(e: Event) {
  const input = e.target as HTMLInputElement
  const cleaned = normalise(input.value)
  rawCode.value = cleaned
  input.value = cleaned
  errorMsg.value = null
}

async function onJoin() {
  const code = rawCode.value.trim()
  if (code.length !== 6) {
    errorMsg.value = 'Game PIN must be 6 characters.'
    return
  }

  loading.value = true
  errorMsg.value = null

  try {
    const resp = await fetch(`${BASE_URL}/api/games/${code}`)

    if (resp.status === 404) {
      errorMsg.value = 'Game not found. Check the PIN and try again.'
      return
    }

    if (!resp.ok) {
      errorMsg.value = 'Something went wrong. Please try again.'
      return
    }

    const session = (await resp.json()) as { status: string }

    if (session.status === 'Finished') {
      errorMsg.value = 'This game has already ended.'
      return
    }

    router.push({ name: 'player-setup', params: { code } })
  } catch {
    errorMsg.value = 'Could not reach the game server. Check your connection.'
  } finally {
    loading.value = false
  }
}

onMounted(() => {
  const q = route.query.code
  if (typeof q === 'string' && q) {
    rawCode.value = normalise(q)
  }
})
</script>

<template>
  <div class="min-h-[80vh] flex items-center justify-center px-4">
    <div class="w-full max-w-sm">
      <div class="text-center mb-8">
        <div class="text-6xl mb-3">🎮</div>
        <h1 class="text-3xl font-black text-slate-900">Join a game</h1>
        <p class="text-slate-500 mt-1">Enter the game PIN shown on screen</p>
      </div>

      <form @submit.prevent="onJoin" class="flex flex-col gap-4">
        <input
          type="text"
          :value="rawCode"
          inputmode="text"
          autocomplete="off"
          autocorrect="off"
          autocapitalize="characters"
          spellcheck="false"
          maxlength="6"
          placeholder="ENTER PIN"
          class="w-full text-center text-4xl font-black tracking-[0.3em] py-5 rounded-2xl border-2
                 border-slate-300 focus:border-koot-purple focus:outline-none
                 bg-white shadow-sm placeholder:text-slate-300 placeholder:text-2xl placeholder:tracking-widest"
          @input="onInput"
        />

        <p v-if="errorMsg" class="text-koot-magenta text-sm font-medium text-center" role="alert">
          {{ errorMsg }}
        </p>

        <button
          type="submit"
          :disabled="rawCode.length !== 6 || loading"
          class="py-4 rounded-2xl font-black text-xl bg-koot-purple text-white shadow-lg
                 transition-all hover:opacity-90 active:scale-95
                 disabled:opacity-40 disabled:cursor-not-allowed"
        >
          {{ loading ? 'Checking…' : 'Join →' }}
        </button>
      </form>

      <p class="text-center text-sm text-slate-400 mt-6">
        Are you a host?
        <RouterLink to="/login" class="text-koot-purple font-semibold hover:underline">
          Log in to create a game
        </RouterLink>
      </p>
    </div>
  </div>
</template>
