import { createRouter, createWebHistory, type RouteRecordRaw } from 'vue-router'

const routes: RouteRecordRaw[] = [
  { path: '/', name: 'home', component: () => import('../views/HomeView.vue') },
  { path: '/login', name: 'login', component: () => import('../views/LoginView.vue') },
  { path: '/register', name: 'register', component: () => import('../views/RegisterView.vue') },
  { path: '/dashboard', name: 'dashboard', component: () => import('../views/DashboardView.vue') },
  { path: '/quiz/create', name: 'quiz-create', component: () => import('../views/QuizCreateView.vue') },
  { path: '/quiz/:id/edit', name: 'quiz-edit', component: () => import('../views/QuizEditView.vue'), props: true },
  { path: '/host/:code', name: 'host-game', component: () => import('../views/HostGameView.vue'), props: true },
  { path: '/join', name: 'join', component: () => import('../views/JoinView.vue') },
  { path: '/play/:code', name: 'play-game', component: () => import('../views/PlayGameView.vue'), props: true },
  { path: '/:pathMatch(.*)*', name: 'not-found', component: () => import('../views/NotFoundView.vue') },
]

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes,
})

export default router
