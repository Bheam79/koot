import axios, { AxiosError, type InternalAxiosRequestConfig } from 'axios'

const baseURL =
  (import.meta.env.VITE_API_URL as string | undefined) ??
  (import.meta.env.VITE_API_BASE_URL as string | undefined) ??
  'http://localhost:5024'

const STORAGE_KEY = 'koot.token'

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
 * Auto-logout on 401. The auth store wires its handler in via
 * `setUnauthorizedHandler` so we don't have a circular import.
 */
let onUnauthorized: (() => void) | null = null

export function setUnauthorizedHandler(handler: (() => void) | null) {
  onUnauthorized = handler
}

api.interceptors.response.use(
  (response) => response,
  (error: AxiosError) => {
    if (error.response?.status === 401) {
      onUnauthorized?.()
    }
    return Promise.reject(error)
  },
)

export const TOKEN_STORAGE_KEY = STORAGE_KEY

export default api
