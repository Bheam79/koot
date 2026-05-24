import { defineStore } from 'pinia'
import { computed, ref } from 'vue'
import api, { TOKEN_STORAGE_KEY, setUnauthorizedHandler } from '../services/api'

export interface AuthUser {
  id: number
  username: string
  email: string
}

interface AuthResponse {
  token: string
  userId: number
  username: string
  email: string
  expiresAt: string
}

interface MeResponse {
  id: number
  username: string
  email: string
  createdAt: string
}

export const useAuthStore = defineStore('auth', () => {
  const user = ref<AuthUser | null>(null)
  const token = ref<string | null>(localStorage.getItem(TOKEN_STORAGE_KEY))
  const loading = ref(false)
  const error = ref<string | null>(null)

  const isAuthenticated = computed(() => !!token.value)

  function setSession(t: string, u: AuthUser) {
    token.value = t
    user.value = u
    localStorage.setItem(TOKEN_STORAGE_KEY, t)
  }

  function clearSession() {
    token.value = null
    user.value = null
    localStorage.removeItem(TOKEN_STORAGE_KEY)
  }

  async function login(email: string, password: string) {
    loading.value = true
    error.value = null
    try {
      const { data } = await api.post<AuthResponse>('/api/auth/login', { email, password })
      setSession(data.token, { id: data.userId, username: data.username, email: data.email })
      return true
    } catch (e: unknown) {
      error.value = extractError(e, 'Invalid email or password.')
      return false
    } finally {
      loading.value = false
    }
  }

  async function register(username: string, email: string, password: string) {
    loading.value = true
    error.value = null
    try {
      const { data } = await api.post<AuthResponse>('/api/auth/register', {
        username,
        email,
        password,
      })
      setSession(data.token, { id: data.userId, username: data.username, email: data.email })
      return true
    } catch (e: unknown) {
      error.value = extractError(e, 'Registration failed.')
      return false
    } finally {
      loading.value = false
    }
  }

  function logout() {
    clearSession()
  }

  /**
   * If we have a token on app boot, ask the backend who we are.
   * Bad tokens (401) get cleared by the response interceptor.
   */
  async function hydrate() {
    if (!token.value) return
    try {
      const { data } = await api.get<MeResponse>('/api/auth/me')
      user.value = { id: data.id, username: data.username, email: data.email }
    } catch {
      // 401 handler already cleared the session
    }
  }

  // Auto-logout on 401 anywhere in the app
  setUnauthorizedHandler(() => clearSession())

  return {
    user,
    token,
    loading,
    error,
    isAuthenticated,
    login,
    register,
    logout,
    hydrate,
  }
})

function extractError(err: unknown, fallback: string): string {
  // Axios-style error payload: { error: string } or ProblemDetails-ish
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
