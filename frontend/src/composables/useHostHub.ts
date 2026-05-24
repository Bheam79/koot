import { ref, onUnmounted } from 'vue'
import * as signalR from '@microsoft/signalr'
import type {
  Participant,
  QuestionBroadcast,
  AnswerResult,
  CorrectAnswerInfo,
  LeaderboardEntry,
} from '../types/game'

const BASE_URL =
  (import.meta.env.VITE_API_URL as string | undefined) ??
  (import.meta.env.VITE_API_BASE_URL as string | undefined) ??
  'http://localhost:5024'

// ── typed callback registry ───────────────────────────────────────────────────

interface HubHandlers {
  PlayerJoined?: (p: Participant) => void
  PlayerLeft?: (id: number) => void
  GameStarted?: () => void
  QuestionStarted?: (q: QuestionBroadcast, tl: number) => void
  TimerTick?: (s: number) => void
  QuestionEnded?: (ca: CorrectAnswerInfo[], r: AnswerResult[]) => void
  LeaderboardUpdate?: (e: LeaderboardEntry[]) => void
  GameEnded?: (s: LeaderboardEntry[]) => void
  SessionState?: (s: unknown) => void
  Error?: (msg: string) => void
}

export function useHostHub() {
  const connection = ref<signalR.HubConnection | null>(null)
  const connected = ref(false)
  const reconnecting = ref(false)
  const error = ref<string | null>(null)

  const handlers: HubHandlers = {}

  /** Register a typed event handler. Call before connect(). */
  function on<K extends keyof HubHandlers>(event: K, cb: NonNullable<HubHandlers[K]>) {
    handlers[event] = cb as HubHandlers[K]
  }

  async function connect(token: string) {
    const conn = new signalR.HubConnectionBuilder()
      .withUrl(`${BASE_URL}/hubs/game`, {
        accessTokenFactory: () => token,
        transport: signalR.HttpTransportType.WebSockets,
        skipNegotiation: false,
      })
      .withAutomaticReconnect([0, 2000, 5000, 10000, 20000])
      .configureLogging(signalR.LogLevel.Warning)
      .build()

    conn.on('PlayerJoined', (p: Participant) => handlers.PlayerJoined?.(p))
    conn.on('PlayerLeft', (id: number) => handlers.PlayerLeft?.(id))
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
    conn.on('SessionState', (s: unknown) => handlers.SessionState?.(s))
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
    })

    connection.value = conn

    try {
      await conn.start()
      connected.value = true
    } catch (e) {
      error.value = 'Failed to connect to game server.'
      throw e
    }
  }

  async function joinAsHost(code: string) {
    await connection.value?.invoke('JoinAsHost', code)
  }

  async function startGame(code: string) {
    await connection.value?.invoke('StartGame', code)
  }

  async function nextQuestion(code: string) {
    await connection.value?.invoke('NextQuestion', code)
  }

  async function endGame(code: string) {
    await connection.value?.invoke('EndGame', code)
  }

  async function disconnect() {
    await connection.value?.stop()
    connected.value = false
    reconnecting.value = false
  }

  onUnmounted(disconnect)

  return {
    connected,
    reconnecting,
    error,
    on,
    connect,
    joinAsHost,
    startGame,
    nextQuestion,
    endGame,
    disconnect,
  }
}
