import { describe, it, expect, beforeEach, vi } from 'vitest'
import { createRouter, createWebHistory } from 'vue-router'
import { setActivePinia, createPinia } from 'pinia'

// ── Mock the api module (auth store imports it) ────────────────────────────────
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
    REFRESH_TOKEN_STORAGE_KEY: 'koot.refreshToken',
    setUnauthorizedHandler: vi.fn(),
    setRefreshHandler: vi.fn(),
  }
})

import { useAuthStore } from '../stores/auth'

// ── Minimal router that mirrors route meta from the real router ────────────────
function createTestRouter() {
  const Stub = { template: '<div/>' }
  return createRouter({
    history: createWebHistory(),
    routes: [
      { path: '/', name: 'home', component: Stub },
      { path: '/login', name: 'login', component: Stub, meta: { guestOnly: true } },
      { path: '/register', name: 'register', component: Stub, meta: { guestOnly: true } },
      { path: '/dashboard', name: 'dashboard', component: Stub, meta: { requiresAuth: true } },
      { path: '/quiz/create', name: 'quiz-create', component: Stub, meta: { requiresAuth: true } },
    ],
  })
}

describe('Router guards', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
  })

  it('redirects unauthenticated user from protected route to login', async () => {
    const router = createTestRouter()

    // Wire the same guard used in the real router
    router.beforeEach((to) => {
      const auth = useAuthStore()
      if (to.meta.requiresAuth && !auth.isAuthenticated) {
        return { name: 'login', query: { next: to.fullPath } }
      }
      if (to.meta.guestOnly && auth.isAuthenticated) {
        return { name: 'dashboard' }
      }
      return true
    })

    await router.push('/dashboard')
    expect(router.currentRoute.value.name).toBe('login')
  })

  it('includes "next" query param when redirecting to login', async () => {
    const router = createTestRouter()
    router.beforeEach((to) => {
      const auth = useAuthStore()
      if (to.meta.requiresAuth && !auth.isAuthenticated) {
        return { name: 'login', query: { next: to.fullPath } }
      }
      return true
    })

    await router.push('/dashboard')
    expect(router.currentRoute.value.query.next).toBe('/dashboard')
  })

  it('allows authenticated user to access protected route', async () => {
    localStorage.setItem('koot.token', 'valid-token')
    setActivePinia(createPinia())

    const router = createTestRouter()
    router.beforeEach((to) => {
      const auth = useAuthStore()
      if (to.meta.requiresAuth && !auth.isAuthenticated) {
        return { name: 'login', query: { next: to.fullPath } }
      }
      if (to.meta.guestOnly && auth.isAuthenticated) {
        return { name: 'dashboard' }
      }
      return true
    })

    await router.push('/dashboard')
    expect(router.currentRoute.value.name).toBe('dashboard')
  })

  it('redirects authenticated user away from guestOnly routes to dashboard', async () => {
    localStorage.setItem('koot.token', 'valid-token')
    setActivePinia(createPinia())

    const router = createTestRouter()
    router.beforeEach((to) => {
      const auth = useAuthStore()
      if (to.meta.requiresAuth && !auth.isAuthenticated) {
        return { name: 'login', query: { next: to.fullPath } }
      }
      if (to.meta.guestOnly && auth.isAuthenticated) {
        return { name: 'dashboard' }
      }
      return true
    })

    await router.push('/login')
    expect(router.currentRoute.value.name).toBe('dashboard')
  })

  it('allows unauthenticated user to access guestOnly route', async () => {
    const router = createTestRouter()
    router.beforeEach((to) => {
      const auth = useAuthStore()
      if (to.meta.requiresAuth && !auth.isAuthenticated) {
        return { name: 'login', query: { next: to.fullPath } }
      }
      if (to.meta.guestOnly && auth.isAuthenticated) {
        return { name: 'dashboard' }
      }
      return true
    })

    await router.push('/login')
    expect(router.currentRoute.value.name).toBe('login')
  })

  it('allows unauthenticated user to access public routes', async () => {
    const router = createTestRouter()
    router.beforeEach((to) => {
      const auth = useAuthStore()
      if (to.meta.requiresAuth && !auth.isAuthenticated) {
        return { name: 'login', query: { next: to.fullPath } }
      }
      if (to.meta.guestOnly && auth.isAuthenticated) {
        return { name: 'dashboard' }
      }
      return true
    })

    await router.push('/')
    expect(router.currentRoute.value.name).toBe('home')
  })
})
