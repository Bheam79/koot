import axios, {
  AxiosError,
  type AxiosRequestConfig,
  type InternalAxiosRequestConfig,
} from 'axios'

const baseURL =
  (import.meta.env.VITE_API_URL as string | undefined) ??
  (import.meta.env.VITE_API_BASE_URL as string | undefined) ??
  'http://localhost:5024'

const STORAGE_KEY = 'koot.token'
const REFRESH_STORAGE_KEY = 'koot.refreshToken'

/** Endpoint suffix used to detect a refresh call inside the interceptor. */
const REFRESH_ENDPOINT = '/api/auth/refresh'

export const api = axios.create({
  baseURL,
  withCredentials: false,
})

/** Attach the JWT (if any) on every outgoing request. */
api.interceptors.request.use((config: InternalAxiosRequestConfig) => {
  const token = localStorage.getItem(STORAGE_KEY)
  if (token) {
    config.headers = config.headers ?? {}
    ;(config.headers as Record<string, string>).Authorization = `Bearer ${token}`
  }
  return config
})

/**
 * Callbacks wired by the auth store at boot to avoid a circular import
 * (api.ts ↔ stores/auth.ts).
 *
 *   - onUnauthorized: hard logout — clear local session state.
 *   - refreshHandler: silently swap the access token using a refresh token;
 *     resolves to `true` on success, `false` on failure.
 */
let onUnauthorized: (() => void) | null = null
let refreshHandler: (() => Promise<boolean>) | null = null

export function setUnauthorizedHandler(handler: (() => void) | null) {
  onUnauthorized = handler
}

export function setRefreshHandler(handler: (() => Promise<boolean>) | null) {
  refreshHandler = handler
}

/**
 * Internal flag we set on retried requests so a second 401 (post-refresh) is
 * treated as a hard logout instead of triggering another refresh attempt.
 */
interface RetriableConfig extends InternalAxiosRequestConfig {
  _retry?: boolean
}

/**
 * Test whether an axios config is targeting the refresh endpoint itself.
 * We must never retry a refresh-failed request — that would loop forever.
 */
function isRefreshRequest(config: AxiosRequestConfig | undefined): boolean {
  const url = config?.url ?? ''
  return url.endsWith(REFRESH_ENDPOINT)
}

/**
 * Redirect to the login screen with a `reason=session-expired` query param
 * so the login view can surface a useful message. Uses a hard navigation so
 * we don't have to import the vue-router instance (which would be a circular
 * dependency) and so any in-flight Vue state is discarded.
 */
function redirectToLogin() {
  if (typeof window === 'undefined') return
  const current = window.location.pathname + window.location.search
  // Avoid bouncing if we're already on /login
  if (window.location.pathname === '/login') return
  const next = encodeURIComponent(current)
  window.location.assign(`/login?reason=session-expired&next=${next}`)
}

api.interceptors.response.use(
  (response) => response,
  async (error: AxiosError) => {
    const status = error.response?.status
    const config = error.config as RetriableConfig | undefined

    if (status !== 401 || !config) {
      return Promise.reject(error)
    }

    // 401 on the refresh endpoint itself = the refresh token is gone/bad.
    // Hard logout immediately; do not loop.
    if (isRefreshRequest(config)) {
      onUnauthorized?.()
      redirectToLogin()
      return Promise.reject(error)
    }

    // Already retried once — refresh either failed or the new token is also bad.
    if (config._retry) {
      onUnauthorized?.()
      redirectToLogin()
      return Promise.reject(error)
    }

    if (!refreshHandler) {
      onUnauthorized?.()
      redirectToLogin()
      return Promise.reject(error)
    }

    const refreshed = await refreshHandler()
    if (!refreshed) {
      // Refresh handler already cleared the session; just redirect.
      redirectToLogin()
      return Promise.reject(error)
    }

    // Mark as retried so a second failure doesn't loop, and re-issue with
    // the freshly-stored JWT (the request interceptor will pick it up).
    config._retry = true
    if (config.headers) {
      const newToken = localStorage.getItem(STORAGE_KEY)
      if (newToken) {
        ;(config.headers as Record<string, string>).Authorization = `Bearer ${newToken}`
      }
    }
    return api.request(config)
  },
)

export const TOKEN_STORAGE_KEY = STORAGE_KEY
export const REFRESH_TOKEN_STORAGE_KEY = REFRESH_STORAGE_KEY

export default api
