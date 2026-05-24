import { createRouter, createWebHistory, type RouteRecordRaw } from 'vue-router'
import { useAuthStore } from '../stores/auth'

const routes: RouteRecordRaw[] = [
  { path: '/', name: 'home', component: () => import('../views/HomeView.vue') },
  {
    path: '/login',
    name: 'login',
    component: () => import('../views/LoginView.vue'),
    meta: { guestOnly: true },
  },
  {
    path: '/register',
    name: 'register',
    component: () => import('../views/RegisterView.vue'),
    meta: { guestOnly: true },
  },
  {
    path: '/dashboard',
    name: 'dashboard',
    component: () => import('../views/DashboardView.vue'),
    meta: { requiresAuth: true },
  },
  {
    path: '/quiz/create',
    name: 'quiz-create',
    component: () => import('../views/QuizCreateView.vue'),
    meta: { requiresAuth: true },
  },
  {
    path: '/quiz/:id/edit',
    name: 'quiz-edit',
    component: () => import('../views/QuizEditView.vue'),
    props: true,
    meta: { requiresAuth: true },
  },
  {
    path: '/host/:code',
    name: 'host-game',
    component: () => import('../views/HostGameView.vue'),
    props: true,
    meta: { requiresAuth: true },
  },
  { path: '/join', name: 'join', component: () => import('../views/JoinView.vue') },
  {
    path: '/play/:code',
    name: 'play-game',
    component: () => import('../views/PlayGameView.vue'),
    props: true,
  },
  {
    path: '/:pathMatch(.*)*',
    name: 'not-found',
    component: () => import('../views/NotFoundView.vue'),
  },
]

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes,
})

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

export default router
