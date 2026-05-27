/**
 * Behaviour tests for the response interceptor in services/api.ts.
 *
 * We bypass jsdom's real fetch by stubbing the axios adapter; this lets us
 * deterministically queue 401-then-200 responses to exercise the silent
 * refresh + retry path without spinning up a real server.
 */
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import type { AxiosAdapter, AxiosRequestConfig, AxiosResponse } from 'axios'

import api, {
  TOKEN_STORAGE_KEY,
  REFRESH_TOKEN_STORAGE_KEY,
  setRefreshHandler,
  setUnauthorizedHandler,
} from '../services/api'

// ── Axios adapter helpers ─────────────────────────────────────────────────────

interface ScriptedResponse {
  status: number
  data?: unknown
}

/**
 * Returns an axios adapter that consumes from `queue` in order, resolving for
 * 2xx and rejecting (with an axios-shaped error) for non-2xx. Also exposes
 * the list of requests it received for assertion.
 */
function makeScriptedAdapter(queue: ScriptedResponse[]) {
  const calls: AxiosRequestConfig[] = []

  const adapter: AxiosAdapter = async (config) => {
    calls.push(config)
    const next = queue.shift()
    if (!next) {
      throw new Error(
        `scripted adapter exhausted: unexpected request to ${config.url}`,
      )
    }

    const response: AxiosResponse = {
      data: next.data ?? '',
      status: next.status,
      statusText: '',
      headers: {},
      config: config as AxiosResponse['config'],
    }

    if (next.status >= 200 && next.status < 300) {
      return response
    }

    // Mimic AxiosError shape that the interceptor inspects
    const err = new Error(`Request failed with status code ${next.status}`) as Error & {
      response: AxiosResponse
      config: AxiosRequestConfig
      isAxiosError: boolean
    }
    err.response = response
    err.config = config
    err.isAxiosError = true
    throw err
  }

  return { adapter, calls }
}

// ── Test setup ────────────────────────────────────────────────────────────────

let originalLocation: Location
let assignSpy: ReturnType<typeof vi.fn>

beforeEach(() => {
  localStorage.clear()
  setUnauthorizedHandler(null)
  setRefreshHandler(null)

  // jsdom's Location.assign is non-configurable, so we swap the whole object.
  // Use a plain object that satisfies the call sites in api.ts.
  originalLocation = window.location
  assignSpy = vi.fn()
  // @ts-expect-error replacing the whole Location object with a duck-typed stub
  delete window.location
  ;(window as unknown as { location: unknown }).location = {
    pathname: '/dashboard',
    search: '',
    assign: assignSpy,
  }
})

afterEach(() => {
  ;(window as unknown as { location: Location }).location = originalLocation
})

describe('axios response interceptor', () => {
  it('retries the original request after a successful refresh', async () => {
    localStorage.setItem(TOKEN_STORAGE_KEY, 'expired-jwt')
    localStorage.setItem(REFRESH_TOKEN_STORAGE_KEY, 'good-refresh')

    const refreshHandler = vi.fn(async () => {
      // Simulate the auth store stashing a new token
      localStorage.setItem(TOKEN_STORAGE_KEY, 'new-jwt')
      return true
    })
    setRefreshHandler(refreshHandler)

    const { adapter, calls } = makeScriptedAdapter([
      { status: 401 }, // first /api/quizzes call expires
      { status: 200, data: { quizzes: [] } }, // retry succeeds
    ])
    api.defaults.adapter = adapter

    const res = await api.get('/api/quizzes')

    expect(refreshHandler).toHaveBeenCalledTimes(1)
    expect(res.status).toBe(200)
    expect(calls).toHaveLength(2)
    // Second call should carry the refreshed bearer
    expect(calls[1].headers?.Authorization).toBe('Bearer new-jwt')
    expect(assignSpy).not.toHaveBeenCalled()
  })

  it('does not attempt to refresh when the failing request is the refresh endpoint itself', async () => {
    localStorage.setItem(TOKEN_STORAGE_KEY, 'jwt')
    localStorage.setItem(REFRESH_TOKEN_STORAGE_KEY, 'bad-refresh')

    const refreshHandler = vi.fn(async () => true)
    const unauthorized = vi.fn()
    setRefreshHandler(refreshHandler)
    setUnauthorizedHandler(unauthorized)

    const { adapter } = makeScriptedAdapter([{ status: 401 }])
    api.defaults.adapter = adapter

    await expect(
      api.post('/api/auth/refresh', { refreshToken: 'bad-refresh' }),
    ).rejects.toBeDefined()

    expect(refreshHandler).not.toHaveBeenCalled()
    expect(unauthorized).toHaveBeenCalledTimes(1)
    expect(assignSpy).toHaveBeenCalledWith(expect.stringContaining('/login?reason=session-expired'))
  })

  it('redirects to /login?reason=session-expired when refresh fails', async () => {
    localStorage.setItem(TOKEN_STORAGE_KEY, 'expired-jwt')
    localStorage.setItem(REFRESH_TOKEN_STORAGE_KEY, 'doomed-refresh')

    const refreshHandler = vi.fn(async () => false)
    setRefreshHandler(refreshHandler)

    const { adapter } = makeScriptedAdapter([{ status: 401 }])
    api.defaults.adapter = adapter

    await expect(api.get('/api/quizzes')).rejects.toBeDefined()

    expect(refreshHandler).toHaveBeenCalledTimes(1)
    expect(assignSpy).toHaveBeenCalledWith(expect.stringContaining('/login?reason=session-expired'))
  })

  it('does not loop if the retried request also 401s', async () => {
    localStorage.setItem(TOKEN_STORAGE_KEY, 'expired-jwt')
    localStorage.setItem(REFRESH_TOKEN_STORAGE_KEY, 'good-refresh')

    const refreshHandler = vi.fn(async () => {
      localStorage.setItem(TOKEN_STORAGE_KEY, 'new-jwt-still-bad')
      return true
    })
    const unauthorized = vi.fn()
    setRefreshHandler(refreshHandler)
    setUnauthorizedHandler(unauthorized)

    const { adapter, calls } = makeScriptedAdapter([
      { status: 401 }, // original request
      { status: 401 }, // retried request — still rejected
    ])
    api.defaults.adapter = adapter

    await expect(api.get('/api/quizzes')).rejects.toBeDefined()

    expect(refreshHandler).toHaveBeenCalledTimes(1) // not called a second time
    expect(calls).toHaveLength(2)
    expect(unauthorized).toHaveBeenCalledTimes(1)
    expect(assignSpy).toHaveBeenCalled()
  })
})
