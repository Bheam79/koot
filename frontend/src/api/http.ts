import axios from 'axios'

const baseURL = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5024'

export const http = axios.create({
  baseURL,
  withCredentials: false,
})

// Attach JWT (KOOT-4 will wire this in via auth store)
http.interceptors.request.use((config) => {
  const token = localStorage.getItem('koot.token')
  if (token) {
    config.headers.Authorization = `Bearer ${token}`
  }
  return config
})

export default http
