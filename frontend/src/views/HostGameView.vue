<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '../stores/auth'
import { useHostHub } from '../composables/useHostHub'
import { useToast } from '../composables/useToast'
import HostLobby from '../components/game/HostLobby.vue'
import QuestionDisplay from '../components/game/QuestionDisplay.vue'
import QuestionResults from '../components/game/QuestionResults.vue'
import Leaderboard from '../components/game/Leaderboard.vue'
import FinalPodium from '../components/game/FinalPodium.vue'
import type {
  Participant,
  QuestionBroadcast,
  AnswerResult,
  CorrectAnswerInfo,
  LeaderboardEntry,
} from '../types/game'
import { HostPhase } from '../types/game'

// ── Props ────────────────────────────────────────────────────────────────────

const props = defineProps<{ code: string }>()

// ── Stores / composables ──────────────────────────────────────────────────────

const auth = useAuthStore()
const router = useRouter()
const hub = useHostHub()
const toast = useToast()

// ── Reactive state ────────────────────────────────────────────────────────────

const phase = ref<HostPhase>(HostPhase.Connecting)
const errorMsg = ref<string | null>(null)
const starting = ref(false)

// Session / quiz info (populated from SessionState event)
const quizTitle = ref('Loading…')
const totalQuestions = ref(0)
const questionIndex = ref(-1)

// Participants
const participants = ref<Participant[]>([])

// Current question
const currentQuestion = ref<QuestionBroadcast | null>(null)
const secondsLeft = ref(0)
const answeredCount = ref(0)

// Results
const correctAnswers = ref<CorrectAnswerInfo[]>([])
const results = ref<AnswerResult[]>([])

// Leaderboard
const leaderboard = ref<LeaderboardEntry[]>([])

// Final standings
const finalStandings = ref<LeaderboardEntry[]>([])

// ── Computed ──────────────────────────────────────────────────────────────────

const canStart = computed(() => participants.value.length >= 1)

const isLastQuestion = computed(
  () => questionIndex.value >= totalQuestions.value - 1,
)

const participantAvatarMap = computed(() =>
  participants.value.map((p) => ({ id: p.id, avatarId: p.avatarId })),
)

// ── SignalR wiring ────────────────────────────────────────────────────────────

hub.on('SessionState', (state) => {
  const s = state as {
    id: number
    status: string
    currentQuestionIndex: number
    quizTitle: string
    totalQuestions: number
    participants: Participant[]
  }
  quizTitle.value = s.quizTitle
  totalQuestions.value = s.totalQuestions
  participants.value = s.participants
  questionIndex.value = s.currentQuestionIndex

  if (s.status === 'Lobby') {
    phase.value = HostPhase.Lobby
  }
})

hub.on('PlayerJoined', (p) => {
  if (!participants.value.find((x) => x.id === p.id)) {
    participants.value.push(p)
  }
})

hub.on('PlayerLeft', (id) => {
  const p = participants.value.find((x) => x.id === id)
  if (p) p.isDisconnected = true
})

hub.on('QuestionStarted', (q, timeLimit) => {
  currentQuestion.value = q
  secondsLeft.value = timeLimit
  answeredCount.value = 0
  questionIndex.value = q.orderIndex
  phase.value = HostPhase.Question
  starting.value = false
})

hub.on('TimerTick', (s) => {
  secondsLeft.value = s
})

hub.on('QuestionEnded', (ca, res) => {
  correctAnswers.value = ca
  results.value = res

  for (const r of res) {
    const p = participants.value.find((x) => x.id === r.participantId)
    if (p) p.totalScore = r.totalScore
  }

  phase.value = HostPhase.Results
})

hub.on('LeaderboardUpdate', (entries) => {
  leaderboard.value = entries
})

hub.on('GameEnded', (standings) => {
  finalStandings.value = standings
  phase.value = HostPhase.Podium
})

hub.on('Error', (msg) => {
  toast.error(msg)
  starting.value = false
})

// ── Actions ───────────────────────────────────────────────────────────────────

async function onStart() {
  starting.value = true
  errorMsg.value = null
  try {
    await hub.startGame(props.code)
  } catch {
    toast.error('Failed to start game. Please try again.')
    starting.value = false
  }
}

async function onNext() {
  errorMsg.value = null
  phase.value = HostPhase.Leaderboard
}

async function onNextFromLeaderboard() {
  errorMsg.value = null
  try {
    await hub.nextQuestion(props.code)
  } catch {
    toast.error('Failed to advance to next question.')
  }
}

async function onEndFromResults() {
  phase.value = HostPhase.Leaderboard
}

async function onEndFromLeaderboard() {
  try {
    await hub.endGame(props.code)
  } catch {
    toast.error('Failed to end game.')
  }
}

async function onPlayAgain() {
  router.push('/dashboard')
}

// ── Fullscreen ─────────────────────────────────────────────────────────────────

async function toggleFullscreen() {
  try {
    if (!document.fullscreenElement) {
      await document.documentElement.requestFullscreen()
    } else {
      await document.exitFullscreen()
    }
  } catch {
    // Fullscreen not supported or denied — ignore gracefully
  }
}

// ── Lifecycle ──────────────────────────────────────────────────────────────────

onMounted(async () => {
  if (!auth.token) {
    router.push('/login')
    return
  }

  try {
    const resp = await fetch(
      `${import.meta.env.VITE_API_URL ?? import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5024'}/api/games/${props.code}`,
      { headers: { Authorization: `Bearer ${auth.token}` } },
    )
    if (resp.ok) {
      const info = (await resp.json()) as { quizTitle: string; participantCount: number }
      quizTitle.value = info.quizTitle
    }
  } catch {
    // Non-fatal
  }

  try {
    await hub.connect(auth.token!)
    await hub.joinAsHost(props.code)
  } catch {
    phase.value = HostPhase.Error
  }
})

onUnmounted(() => {
  hub.disconnect()
})
</script>

<template>
  <!-- Connecting spinner -->
  <div
    v-if="phase === 'connecting'"
    class="min-h-screen bg-koot-purple flex items-center justify-center text-white"
  >
    <div class="text-center">
      <div class="text-5xl animate-bounce mb-4">🎮</div>
      <p class="text-xl font-bold">Connecting…</p>
    </div>
  </div>

  <!-- Error -->
  <div
    v-else-if="phase === 'error'"
    class="min-h-screen bg-koot-purple flex items-center justify-center text-white"
  >
    <div class="text-center max-w-md px-4">
      <div class="text-5xl mb-4">❌</div>
      <p class="text-xl font-bold mb-2">Connection Error</p>
      <p class="text-white/70 mb-6">Could not connect to the game server. Please refresh the page.</p>
      <button
        class="px-6 py-3 rounded-xl bg-white text-koot-purple font-bold hover:opacity-90"
        @click="router.push('/dashboard')"
      >
        Back to Dashboard
      </button>
    </div>
  </div>

  <!-- Lobby -->
  <HostLobby
    v-else-if="phase === 'lobby'"
    :code="props.code"
    :quiz-title="quizTitle"
    :participants="participants"
    :can-start="canStart"
    :starting="starting"
    @start="onStart"
    @fullscreen="toggleFullscreen"
  />

  <!-- Question in progress -->
  <QuestionDisplay
    v-else-if="phase === 'question' && currentQuestion"
    :question="currentQuestion"
    :seconds-left="secondsLeft"
    :answered-count="answeredCount"
    :total-players="participants.filter((p) => !p.isDisconnected).length"
    :question-index="questionIndex"
    :total-questions="totalQuestions || 1"
  />

  <!-- Results after question -->
  <QuestionResults
    v-else-if="phase === 'results' && currentQuestion"
    :question="currentQuestion"
    :correct-answers="correctAnswers"
    :results="results"
    :is-last-question="isLastQuestion"
    :participants="participantAvatarMap"
    @next="onNext"
    @end="onEndFromResults"
  />

  <!-- Leaderboard between questions -->
  <Leaderboard
    v-else-if="phase === 'leaderboard'"
    :entries="leaderboard"
    :question-index="questionIndex"
    :total-questions="totalQuestions || 1"
    @next="onNextFromLeaderboard"
    @end="onEndFromLeaderboard"
  />

  <!-- Final podium -->
  <FinalPodium
    v-else-if="phase === 'podium'"
    :standings="finalStandings"
    :code="props.code"
    @play-again="onPlayAgain"
  />

  <!-- Reconnecting overlay -->
  <Transition name="fade-overlay">
    <div
      v-if="hub.reconnecting"
      class="fixed inset-0 bg-black/70 flex items-center justify-center z-50"
    >
      <div class="bg-white rounded-2xl px-8 py-6 text-center shadow-2xl max-w-xs mx-4">
        <div class="text-4xl mb-3 animate-spin">🔄</div>
        <p class="text-xl font-black text-slate-800">Reconnecting…</p>
        <p class="text-slate-500 text-sm mt-1">Please wait while we restore the connection.</p>
      </div>
    </div>
  </Transition>

  <!-- Fullscreen hint button (always visible, top-right) -->
  <button
    v-if="phase !== 'connecting' && phase !== 'error'"
    class="fixed top-4 right-4 z-40 bg-black/30 hover:bg-black/50 text-white rounded-lg px-3 py-1.5 text-sm font-semibold transition-all backdrop-blur-sm"
    title="Toggle fullscreen"
    @click="toggleFullscreen"
  >
    ⛶
  </button>
</template>

<style scoped>
.fade-overlay-enter-active,
.fade-overlay-leave-active {
  transition: opacity 0.3s ease;
}
.fade-overlay-enter-from,
.fade-overlay-leave-to {
  opacity: 0;
}
</style>
