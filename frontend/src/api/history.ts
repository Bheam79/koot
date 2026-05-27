// Thin REST client for the host-side history / analytics endpoints added in
// KOOT-28. Auth headers / 401 handling all live in the shared axios instance.

import api from '../services/api'
import type {
  HistoryPage,
  SessionAnswerRow,
  SessionDetail,
} from '../types/history'

const GAMES_ROOT = '/api/games'

export interface ListHistoryParams {
  quizId?: number
  page?: number
  pageSize?: number
}

/** GET /api/games/history — paginated list of the user's finished sessions. */
export async function listHistory(params?: ListHistoryParams): Promise<HistoryPage> {
  const { data } = await api.get<HistoryPage>(`${GAMES_ROOT}/history`, {
    params: {
      quizId: params?.quizId,
      page: params?.page,
      pageSize: params?.pageSize,
    },
  })
  return data
}

/** GET /api/games/sessions/{id} — full analytics payload for one session. */
export async function getSessionDetail(id: number): Promise<SessionDetail> {
  const { data } = await api.get<SessionDetail>(`${GAMES_ROOT}/sessions/${id}`)
  return data
}

/**
 * Returns the URL string for the raw CSV-shaped answer rows endpoint. Useful
 * for callers that want to build their own download flow (e.g. an `<a href>` —
 * though note such a link cannot send the Authorization header, so the
 * recommended flow is to fetch the rows via {@link getSessionAnswers} and
 * build the CSV client-side).
 */
export function getSessionAnswersUrl(id: number): string {
  const base = (api.defaults.baseURL ?? '').replace(/\/$/, '')
  return `${base}${GAMES_ROOT}/sessions/${id}/answers`
}

/**
 * GET /api/games/sessions/{id}/answers — fetches the flat answer rows for
 * CSV export. Goes through the axios instance so the JWT is attached.
 */
export async function getSessionAnswers(id: number): Promise<SessionAnswerRow[]> {
  const { data } = await api.get<SessionAnswerRow[]>(
    `${GAMES_ROOT}/sessions/${id}/answers`,
  )
  return data
}
