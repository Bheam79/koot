<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'

const props = defineProps<{ code: string }>()
const router = useRouter()

const BASE_URL =
  (import.meta.env.VITE_API_URL as string | undefined) ??
  (import.meta.env.VITE_API_BASE_URL as string | undefined) ??
  'http://localhost:5024'

const AVATARS: Record<number, string> = {
  1: '🐶', 2: '🐱', 3: '🐭', 4: '🐹',
  5: '🐰', 6: '🦊', 7: '🐻', 8: '🐼',
  9: '🐨', 10: '🐯', 11: '🦁', 12: '🐸',
}

const AVATAR_BG: Record<number, string> = {
  1: 'bg-orange-400', 2: 'bg-gray-400', 3: 'bg-gray-300', 4: 'bg-amber-400',
  5: 'bg-slate-200', 6: 'bg-orange-500', 7: 'bg-amber-700', 8: 'bg-slate-900',
  9: 'bg-gray-400', 10: 'bg-orange-600', 11: 'bg-yellow-600', 12: 'bg-green-500',
}

const quizTitle = ref('')
const nickname = ref('')
const selectedAvatar = ref(1)
const errorMsg = ref<string | null>(null)
const loading = ref(false)

// Basic validation
const nicknameError = computed(() => {
  const n = nickname.value.trim()
  if (n.length === 0) return null
  if (n.length < 2) return 'Nickname must be at least 2 characters.'
  if (n.length > 20) return 'Nickname must be 20 characters or fewer.'
  return null
})

const canProceed = computed(
  () => nickname.value.trim().length >= 2 && nickname.value.trim().length <= 20 && selectedAvatar.value > 0,
)

async function onGo() {
  if (!canProceed.value) return
  loading.value = true
  errorMsg.value = null

  try {
    // Navigate to play page passing nickname + avatarId as query params
    await router.push({
      name: 'play-game',
      params: { code: props.code },
      query: { nickname: nickname.value.trim(), avatarId: selectedAvatar.value },
    })
  } catch {
    errorMsg.value = 'Navigation failed.'
    loading.value = false
  }
}

onMounted(async () => {
  try {
    const resp = await fetch(`${BASE_URL}/api/games/${props.code}`)
    if (resp.ok) {
      const data = (await resp.json()) as { quizTitle: string; status: string }
      quizTitle.value = data.quizTitle
      if (data.status === 'Finished') {
        errorMsg.value = 'This game has already ended.'
      }
    }
  } catch { /* non-fatal */ }
})
</script>

<template>
  <div class="min-h-screen bg-koot-purple flex items-center justify-center px-4 py-8">
    <div class="w-full max-w-md">
      <!-- Header -->
      <div class="text-center mb-6">
        <p class="text-white/60 text-sm font-medium uppercase tracking-widest mb-1">Joining</p>
        <h1 class="text-2xl font-black text-white truncate">{{ quizTitle || 'Game ' + code }}</h1>
        <p class="text-white/50 text-sm mt-1">PIN: {{ code }}</p>
      </div>

      <div class="bg-white rounded-3xl p-6 shadow-2xl flex flex-col gap-6">
        <!-- Nickname -->
        <div>
          <label class="block text-sm font-bold text-slate-700 mb-2">Your nickname</label>
          <input
            v-model="nickname"
            type="text"
            maxlength="20"
            placeholder="Enter your name…"
            autocomplete="off"
            class="w-full px-4 py-3 rounded-xl border-2 border-slate-200 focus:border-koot-purple
                   focus:outline-none text-lg font-semibold"
            @keyup.enter="canProceed && onGo()"
          />
          <p v-if="nicknameError" class="mt-1 text-xs text-koot-magenta font-medium">
            {{ nicknameError }}
          </p>
          <p class="mt-1 text-xs text-slate-400 text-right">{{ nickname.trim().length }}/20</p>
        </div>

        <!-- Avatar grid -->
        <div>
          <label class="block text-sm font-bold text-slate-700 mb-3">Pick your avatar</label>
          <div class="grid grid-cols-4 gap-3">
            <button
              v-for="(emoji, id) in AVATARS"
              :key="id"
              type="button"
              :class="[
                'rounded-2xl aspect-square flex items-center justify-center text-3xl transition-all',
                AVATAR_BG[Number(id)],
                selectedAvatar === Number(id)
                  ? 'ring-4 ring-koot-purple ring-offset-2 scale-110'
                  : 'hover:scale-105 opacity-70 hover:opacity-100',
              ]"
              @click="selectedAvatar = Number(id)"
            >
              {{ emoji }}
            </button>
          </div>
        </div>

        <p v-if="errorMsg" class="text-koot-magenta text-sm font-medium text-center" role="alert">
          {{ errorMsg }}
        </p>

        <button
          :disabled="!canProceed || loading"
          class="py-4 rounded-2xl font-black text-xl bg-koot-purple text-white shadow-lg
                 transition-all hover:opacity-90 active:scale-95
                 disabled:opacity-40 disabled:cursor-not-allowed"
          @click="onGo"
        >
          {{ loading ? 'Joining…' : "Let's Go! 🚀" }}
        </button>
      </div>
    </div>
  </div>
</template>
