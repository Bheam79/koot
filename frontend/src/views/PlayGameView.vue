<script setup lang="ts">
import { ref, onMounted, onUnmounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { usePlayerHub } from '../composables/usePlayerHub'
import PlayerLobby from '../components/game/PlayerLobby.vue'
import PlayerQuestion from '../components/game/PlayerQuestion.vue'
import PlayerResult from '../components/game/PlayerResult.vue'
import PlayerLeaderboard from '../components/game/PlayerLeaderboard.vue'
import PlayerFinal from '../components/game/PlayerFinal.vue'
import type {
  Participant,
  QuestionBroadcast,
  AnswerResult,
  CorrectAnswerInfo,
  LeaderboardEntry,
  AnswerAccepted,
} from '../types/game'
import { PlayerPhase } from '../types/game'

// ── Props & routing ───────────────────────────────────────────────────────────

const props = defineProps<{ code: string }>()
const route = useRoute()
const router = useRouter()

// Nickname + avatarId come from query params set by PlayerSetupView
const nickname = ref((route.query.nickname as string | undefined) ?? '')
const avatarId = ref(Number(route.query.avatarId ?? 1))

// ── Composable ────────────────────────────────────────────────────────────────

const hub = usePlayerHub()

// ── State ─────────────────────────────────────────────────────────────────────

const phase = ref<PlayerPhase>(PlayerPhase.Joining)
const errorMsg = ref<string | null>(null)

// Player identity (set after JoinedGame)
const myParticipantId = ref(-1)

// Lobby
const quizTitle = ref('')
const playerCount = ref(0)

// Question
const currentQuestion = ref<QuestionBroadcast | null>(null)
const secondsLeft = ref(0)
const totalTime = ref(0)

// Answer tracking
const answered = ref(false)
const selectedOptionId = ref<number | null>(null)
const submittedText = ref<string | null>(null)

// Result feedback
const lastIsCorrect = ref(false)
const lastPointsEarned = ref(0)
const myTotalScore = ref(0)
const myRank = ref(0)
const totalPlayers = ref(0)
const streak = ref(0)

// Leaderboard / final
const leaderboardEntries = ref<LeaderboardEntry[]>([])
const finalStandings = ref<LeaderboardEntry[]>([])

// ── Computed ──────────────────────────────────────────────────────────────────


// ── SignalR handlers ──────────────────────────────────────────────────────────

hub.on('JoinedGame', (p: Participant) => {
  myParticipantId.value = p.id
  playerCount.value++
  phase.value = PlayerPhase.Lobby
})

hub.on('GameStarted', () => {
  // Hub will immediately follow with QuestionStarted
  // So just make sure we're not stuck in Lobby
})

hub.on('QuestionStarted', (q: QuestionBroadcast, tl: number) => {
  currentQuestion.value = q
  secondsLeft.value = tl
  totalTime.value = tl
  answered.value = false
  selectedOptionId.value = null
  submittedText.value = null
  phase.value = PlayerPhase.Question
})

hub.on('TimerTick', (s: number) => {
  secondsLeft.value = s
})

hub.on('AnswerAccepted', (a: AnswerAccepted) => {
  answered.value = true
  lastIsCorrect.value = a.isCorrect
  lastPointsEarned.value = a.points
  myTotalScore.value += a.points
  if (a.isCorrect) {
    streak.value++
  } else {
    streak.value = 0
  }
  phase.value = PlayerPhase.Answered
})

hub.on('QuestionEnded', (_correctAnswers: CorrectAnswerInfo[], results: AnswerResult[]) => {
  totalPlayers.value = results.length

  // Find my result
  const mine = results.find((r) => r.participantId === myParticipantId.value)
  if (mine) {
    myTotalScore.value = mine.totalScore
    lastIsCorrect.value = mine.isCorrect
    lastPointsEarned.value = mine.pointsEarned
    if (!mine.isCorrect) streak.value = 0
  }

  // Compute my rank from results
  const sorted = [...results].sort((a, b) => b.totalScore - a.totalScore)
  myRank.value = sorted.findIndex((r) => r.participantId === myParticipantId.value) + 1

  // If player never answered, show streak reset
  if (!answered.value) {
    streak.value = 0
  }

  phase.value = PlayerPhase.QuestionResult
})

hub.on('LeaderboardUpdate', (entries: LeaderboardEntry[]) => {
  leaderboardEntries.value = entries
  totalPlayers.value = Math.max(totalPlayers.value, entries.length)

  // Update rank from leaderboard
  const me = entries.find((e) => e.participantId === myParticipantId.value)
  if (me) {
    myRank.value = me.rank
    myTotalScore.value = me.totalScore
  }

  phase.value = PlayerPhase.Leaderboard
})

hub.on('GameEnded', (standings: LeaderboardEntry[]) => {
  finalStandings.value = standings
  const me = standings.find((e) => e.participantId === myParticipantId.value)
  if (me) {
    myRank.value = me.rank
    myTotalScore.value = me.totalScore
  }
  phase.value = PlayerPhase.Podium
})

hub.on('Error', (msg: string) => {
  errorMsg.value = msg
  if (phase.value === PlayerPhase.Joining) {
    phase.value = PlayerPhase.Error
  }
})

// ── Actions ───────────────────────────────────────────────────────────────────

async function onSubmitOption(optionId: number, timeTakenMs: number) {
  if (answered.value || !currentQuestion.value) return
  selectedOptionId.value = optionId
  try {
    await hub.submitAnswer(props.code, currentQuestion.value.id, optionId, null, timeTakenMs)
  } catch {
    errorMsg.value = 'Failed to submit answer.'
    selectedOptionId.value = null
  }
}

async function onSubmitText(text: string, timeTakenMs: number) {
  if (answered.value || !currentQuestion.value) return
  submittedText.value = text
  try {
    await hub.submitAnswer(props.code, currentQuestion.value.id, null, text, timeTakenMs)
  } catch {
    errorMsg.value = 'Failed to submit answer.'
    submittedText.value = null
  }
}

function onPlayAgain() {
  router.push('/join')
}

// ── Lifecycle ─────────────────────────────────────────────────────────────────

const BASE_URL =
  (import.meta.env.VITE_API_URL as string | undefined) ??
  (import.meta.env.VITE_API_BASE_URL as string | undefined) ??
  'http://localhost:5024'

onMounted(async () => {
  // If no nickname provided, redirect back to setup
  if (!nickname.value || avatarId.value < 1) {
    router.replace({ name: 'player-setup', params: { code: props.code } })
    return
  }

  // Fetch game info for quizTitle and initial player count
  try {
    const resp = await fetch(`${BASE_URL}/api/games/${props.code}`)
    if (resp.ok) {
      const info = (await resp.json()) as {
        quizTitle: string
        participantCount: number
        status: string
      }
      quizTitle.value = info.quizTitle
      playerCount.value = info.participantCount

      if (info.status === 'Finished') {
        errorMsg.value = 'This game has already ended.'
        phase.value = PlayerPhase.Error
        return
      }
    }
  } catch { /* non-fatal */ }

  try {
    await hub.connect()
    await hub.joinGame(props.code, nickname.value, avatarId.value)
  } catch (e) {
    errorMsg.value = 'Could not connect to the game server.'
    phase.value = PlayerPhase.Error
  }
})

onUnmounted(() => {
  hub.disconnect()
})
</script>

<template>
  <!-- Joining / connecting spinner -->
  <div
    v-if="phase === 'joining'"
    class="min-h-screen bg-koot-purple flex items-center justify-center text-white"
  >
    <div class="text-center">
      <div class="text-5xl animate-bounce mb-4">🎮</div>
      <p class="text-xl font-bold">Joining…</p>
    </div>
  </div>

  <!-- Error state -->
  <div
    v-else-if="phase === 'error'"
    class="min-h-screen bg-koot-purple flex items-center justify-center text-white px-4"
  >
    <div class="text-center max-w-sm">
      <div class="text-5xl mb-4">❌</div>
      <p class="text-xl font-bold mb-2">Couldn't join</p>
      <p class="text-white/70 mb-6">{{ errorMsg }}</p>
      <button
        class="px-6 py-3 rounded-xl bg-white text-koot-purple font-bold hover:opacity-90"
        @click="router.push('/join')"
      >
        Back to Join
      </button>
    </div>
  </div>

  <!-- Lobby wait -->
  <PlayerLobby
    v-else-if="phase === 'lobby'"
    :nickname="nickname"
    :avatar-id="avatarId"
    :quiz-title="quizTitle"
    :player-count="playerCount"
  />

  <!-- Question -->
  <PlayerQuestion
    v-else-if="(phase === 'question' || phase === 'answered') && currentQuestion"
    :question="currentQuestion"
    :seconds-left="secondsLeft"
    :total-time="totalTime"
    :answered="phase === 'answered' || answered"
    :selected-option-id="selectedOptionId"
    :code="props.code"
    @submit-option="onSubmitOption"
    @submit-text="onSubmitText"
  />

  <!-- Per-question result feedback -->
  <PlayerResult
    v-else-if="phase === 'questionResult'"
    :is-correct="lastIsCorrect"
    :points-earned="lastPointsEarned"
    :total-score="myTotalScore"
    :my-rank="myRank"
    :total-players="totalPlayers"
    :streak="streak"
  />

  <!-- Leaderboard between questions -->
  <PlayerLeaderboard
    v-else-if="phase === 'leaderboard'"
    :entries="leaderboardEntries"
    :my-participant-id="myParticipantId"
  />

  <!-- Final screen -->
  <PlayerFinal
    v-else-if="phase === 'podium'"
    :standings="finalStandings"
    :my-participant-id="myParticipantId"
    :my-nickname="nickname"
    :my-avatar-id="avatarId"
    @play-again="onPlayAgain"
  />

  <!-- Non-blocking error toast -->
  <Transition name="toast">
    <div
      v-if="errorMsg && phase !== 'error'"
      class="fixed bottom-6 left-1/2 -translate-x-1/2 bg-koot-magenta text-white px-6 py-3 rounded-xl shadow-xl font-semibold z-50 flex items-center gap-3"
    >
      <span>{{ errorMsg }}</span>
      <button class="opacity-70 hover:opacity-100" @click="errorMsg = null">✕</button>
    </div>
  </Transition>
</template>

<style scoped>
.toast-enter-active,
.toast-leave-active {
  transition: all 0.3s ease;
}
.toast-enter-from,
.toast-leave-to {
  opacity: 0;
  transform: translateX(-50%) translateY(20px);
}
</style>
