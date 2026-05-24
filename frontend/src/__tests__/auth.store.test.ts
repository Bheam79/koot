import { describe, it, expect, beforeEach, vi } from 'vitest'
import { setActivePinia, createPinia } from 'pinia'
import { useAuthStore } from '../stores/auth'

// ── Mock the api module ───────────────────────────────────────────────────────
vi.mock('../services/api', () => {
  const mockApi = {
    post: vi.fn(),
    get: vi.fn(),
    interceptors: {
      request: { use: vi.fn() },
      response: { use: vi.fn() },
    },
    defaults: { headers: { common: {} } },
  }
  return {
    default: mockApi,
    TOKEN_STORAGE_KEY: 'koot.token',
    setUnauthorizedHandler: vi.fn(),
  }
})

import api from '../services/api'

const mockApi = api as unknown as {
  post: ReturnType<typeof vi.fn>
  get: ReturnType<typeof vi.fn>
}

// ── Helpers ───────────────────────────────────────────────────────────────────

const makeAuthResponse = () => ({
  data: {
    token: 'jwt-token-abc',
    userId: 1,
    username: 'alice',
    email: 'alice@example.com',
    expiresAt: new Date(Date.now() + 7 * 24 * 3600 * 1000).toISOString(),
  },
})

describe('useAuthStore', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()
    localStorage.clear()
  })

  // ── Initial state ──────────────────────────────────────────────────────────

  it('starts with no user and not authenticated', () => {
    const store = useAuthStore()
    expect(store.user).toBeNull()
    expect(store.isAuthenticated).toBe(false)
    expect(store.token).toBeNull()
  })

  it('reads token from localStorage on init', () => {
    localStorage.setItem('koot.token', 'existing-token')
    // Re-create the store so it picks up the stored token
    setActivePinia(createPinia())
    const store = useAuthStore()
    expect(store.token).toBe('existing-token')
    expect(store.isAuthenticated).toBe(true)
  })

  // ── login ──────────────────────────────────────────────────────────────────

  it('login: sets token and user on success', async () => {
    mockApi.post.mockResolvedValueOnce(makeAuthResponse())
    const store = useAuthStore()

    const ok = await store.login('alice@example.com', 'password123')

    expect(ok).toBe(true)
    expect(store.isAuthenticated).toBe(true)
    expect(store.user).toEqual({ id: 1, username: 'alice', email: 'alice@example.com' })
    expect(store.token).toBe('jwt-token-abc')
    expect(localStorage.getItem('koot.token')).toBe('jwt-token-abc')
  })

  it('login: returns false and sets error on failure', async () => {
    mockApi.post.mockRejectedValueOnce({
      response: { data: { error: 'Invalid email or password.' } },
    })
    const store = useAuthStore()

    const ok = await store.login('bad@example.com', 'wrongpassword')

    expect(ok).toBe(false)
    expect(store.isAuthenticated).toBe(false)
    expect(store.error).toBe('Invalid email or password.')
  })

  it('login: sets loading to false after completion', async () => {
    mockApi.post.mockResolvedValueOnce(makeAuthResponse())
    const store = useAuthStore()

    await store.login('alice@example.com', 'password123')

    expect(store.loading).toBe(false)
  })

  // ── logout ─────────────────────────────────────────────────────────────────

  it('logout: clears token, user, and localStorage', async () => {
    mockApi.post.mockResolvedValueOnce(makeAuthResponse())
    const store = useAuthStore()
    await store.login('alice@example.com', 'password123')

    store.logout()

    expect(store.isAuthenticated).toBe(false)
    expect(store.user).toBeNull()
    expect(store.token).toBeNull()
    expect(localStorage.getItem('koot.token')).toBeNull()
  })

  // ── register ───────────────────────────────────────────────────────────────

  it('register: sets token and user on success', async () => {
    mockApi.post.mockResolvedValueOnce(makeAuthResponse())
    const store = useAuthStore()

    const ok = await store.register('alice', 'alice@example.com', 'password123')

    expect(ok).toBe(true)
    expect(store.isAuthenticated).toBe(true)
    expect(store.user?.username).toBe('alice')
  })

  it('register: returns false on failure', async () => {
    mockApi.post.mockRejectedValueOnce({
      response: { data: { error: 'A user with that email already exists.' } },
    })
    const store = useAuthStore()

    const ok = await store.register('dup', 'dup@example.com', 'password123')

    expect(ok).toBe(false)
    expect(store.error).toBe('A user with that email already exists.')
  })

  // ── hydrate ────────────────────────────────────────────────────────────────

  it('hydrate: populates user from /api/auth/me', async () => {
    localStorage.setItem('koot.token', 'valid-token')
    setActivePinia(createPinia())
    const store = useAuthStore()

    mockApi.get.mockResolvedValueOnce({
      data: { id: 2, username: 'bob', email: 'bob@example.com', createdAt: '2024-01-01' },
    })

    await store.hydrate()

    expect(store.user).toEqual({ id: 2, username: 'bob', email: 'bob@example.com' })
  })

  it('hydrate: does nothing when no token', async () => {
    const store = useAuthStore()
    await store.hydrate()
    expect(mockApi.get).not.toHaveBeenCalled()
  })
})
