<script setup lang="ts">
import { RouterLink } from 'vue-router'
import { useAuthStore } from '../stores/auth'

const auth = useAuthStore()
</script>

<template>
  <section class="min-h-[80vh] flex items-center justify-center px-4 py-16">
    <div class="max-w-3xl mx-auto text-center">
      <!-- Hero -->
      <div class="text-7xl sm:text-8xl mb-4 animate-bounce">🎮</div>
      <h1 class="text-5xl sm:text-7xl font-black text-koot-purple mb-4 tracking-tight">Koot!</h1>
      <p class="text-lg sm:text-xl text-slate-600 mb-10 max-w-lg mx-auto leading-relaxed">
        A live quiz game you play with friends. Host a session, share a PIN, and compete in real time.
      </p>

      <!-- CTA buttons -->
      <div class="flex flex-col sm:flex-row justify-center gap-3 sm:gap-4 mb-16">
        <RouterLink
          to="/join"
          class="px-8 py-4 rounded-2xl bg-koot-magenta text-white font-black text-xl shadow-lg hover:opacity-90 active:scale-95 transition-all"
        >
          🎯 Join a game
        </RouterLink>
        <RouterLink
          v-if="auth.isAuthenticated"
          to="/dashboard"
          class="px-8 py-4 rounded-2xl bg-koot-purple text-white font-black text-xl shadow-lg hover:opacity-90 active:scale-95 transition-all"
        >
          📋 My quizzes
        </RouterLink>
        <RouterLink
          v-else
          to="/register"
          class="px-8 py-4 rounded-2xl bg-koot-blue text-white font-black text-xl shadow-lg hover:opacity-90 active:scale-95 transition-all"
        >
          ✨ Create a quiz
        </RouterLink>
      </div>

      <!-- Feature highlights -->
      <div class="grid grid-cols-1 sm:grid-cols-3 gap-4 text-left max-w-2xl mx-auto">
        <div class="bg-white rounded-2xl p-5 shadow-sm border border-slate-100">
          <div class="text-3xl mb-2">⚡</div>
          <h3 class="font-bold text-slate-800 mb-1">Real-time</h3>
          <p class="text-sm text-slate-500">Live updates with SignalR — no refresh needed.</p>
        </div>
        <div class="bg-white rounded-2xl p-5 shadow-sm border border-slate-100">
          <div class="text-3xl mb-2">🎨</div>
          <h3 class="font-bold text-slate-800 mb-1">Multiple types</h3>
          <p class="text-sm text-slate-500">Multiple choice, true/false, type-in, and poll.</p>
        </div>
        <div class="bg-white rounded-2xl p-5 shadow-sm border border-slate-100">
          <div class="text-3xl mb-2">🏆</div>
          <h3 class="font-bold text-slate-800 mb-1">Leaderboard</h3>
          <p class="text-sm text-slate-500">Score by accuracy and speed, with live rankings.</p>
        </div>
      </div>
    </div>
  </section>
</template>
