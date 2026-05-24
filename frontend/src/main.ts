import { createApp } from 'vue'
import { createPinia } from 'pinia'

import './style.css'
import App from './App.vue'
import router from './router'
import { useAuthStore } from './stores/auth'

const app = createApp(App)
const pinia = createPinia()
app.use(pinia)

// Hydrate auth (fetch /me if a token is already stored) before mounting the
// router; the navigation guard reads isAuthenticated from this store.
const auth = useAuthStore(pinia)
auth.hydrate().finally(() => {
  app.use(router)
  app.mount('#app')
})
