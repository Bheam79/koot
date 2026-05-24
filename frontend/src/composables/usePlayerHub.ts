import { ref, onUnmounted } from 'vue'
import * as signalR from '@microsoft/signalr'
import type {
  Participant,
  QuestionBroadcast,
  AnswerResult,
  CorrectAnswerInfo,
  LeaderboardEntry,
  AnswerAccepted,
} from '../types/game'

const BASE_URL =
  (import.meta.env.VITE_API_URL as string | undefined) ??
  (import.meta.env.VITE_API_BASE_URL as string | undefined) ??
  'http://localhost:5024'

interface PlayerHubHandlers {
  JoinedGame?: (p: Participant) => void
  AnswerAccepted?: (a: AnswerAccepted) => void
  GameStarted?: () => void
  QuestionStarted?: (q: QuestionBroadcast, tl: number) => void
  TimerTick?: (s: number) => void
  QuestionEnded?: (ca: CorrectAnswerInfo[], r: AnswerResult[]) => void
  LeaderboardUpdate?: (e: LeaderboardEntry[]) => void
  GameEnded?: (s: LeaderboardEntry[]) => void
  Error?: (msg: string) => void
  /** Fired when all reconnect attempts fail (host likely disconnected) */
  Disconnected?: () => void
}

export function usePlayerHub() {
  const connection = ref<signalR.HubConnection | null>(null)
  const connected = ref(false)
  const reconnecting = ref(false)
  const error = ref<string | null>(null)

  const handlers: PlayerHubHandlers = {}

  function on<K extends keyof PlayerHubHandlers>(event: K, cb: NonNullable<PlayerHubHandlers[K]>) {
    handlers[event] = cb as PlayerHubHandlers[K]
  }

  async function connect() {
    // Players connect without JWT — the hub doesn't require auth for JoinGame
    const conn = new signalR.HubConnectionBuilder()
      .withUrl(`${BASE_URL}/hubs/game`)
      .withAutomaticReconnect([0, 2000, 5000, 10000, 20000])
      .configureLogging(signalR.LogLevel.Warning)
      .build()

    conn.on('JoinedGame', (p: Participant) => handlers.JoinedGame?.(p))
    conn.on('AnswerAccepted', (a: AnswerAccepted) => handlers.AnswerAccepted?.(a))
    conn.on('GameStarted', () => handlers.GameStarted?.())
    conn.on('QuestionStarted', (q: QuestionBroadcast, tl: number) =>
      handlers.QuestionStarted?.(q, tl),
    )
    conn.on('TimerTick', (s: number) => handlers.TimerTick?.(s))
    conn.on('QuestionEnded', (ca: CorrectAnswerInfo[], r: AnswerResult[]) =>
      handlers.QuestionEnded?.(ca, r),
    )
    conn.on('LeaderboardUpdate', (e: LeaderboardEntry[]) => handlers.LeaderboardUpdate?.(e))
    conn.on('GameEnded', (s: LeaderboardEntry[]) => handlers.GameEnded?.(s))
    conn.on('Error', (msg: string) => handlers.Error?.(msg))

    conn.onreconnecting(() => {
      connected.value = false
      reconnecting.value = true
    })

    conn.onreconnected(() => {
      connected.value = true
      reconnecting.value = false
    })

    conn.onclose(() => {
      connected.value = false
      reconnecting.value = false
      // All reconnect attempts failed — notify listeners
      handlers.Disconnected?.()
    })

    connection.value = conn

    await conn.start()
    connected.value = true
  }

  async function joinGame(code: string, nickname: string, avatarId: number) {
    await connection.value?.invoke('JoinGame', code, nickname, avatarId)
  }

  async function submitAnswer(
    code: string,
    questionId: number,
    answerOptionId: number | null,
    answerText: string | null,
    timeTakenMs: number,
  ) {
    await connection.value?.invoke(
      'SubmitAnswer',
      code,
      questionId,
      answerOptionId,
      answerText,
      timeTakenMs,
    )
  }

  async function disconnect() {
    await connection.value?.stop()
    connected.value = false
    reconnecting.value = false
  }

  onUnmounted(disconnect)

  return { connected, reconnecting, error, on, connect, joinGame, submitAnswer, disconnect }
}
