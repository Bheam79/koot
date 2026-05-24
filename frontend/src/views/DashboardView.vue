<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { RouterLink } from 'vue-router'
import QuizCard from '../components/QuizCard.vue'
import QuizTopNav from '../components/QuizTopNav.vue'
import { deleteQuiz, listQuizzes } from '../api/quizzes'
import type { QuizSummary } from '../types/quiz'

const quizzes = ref<QuizSummary[]>([])
const loading = ref(true)
const errorMsg = ref<string | null>(null)

async function refresh() {
  loading.value = true
  errorMsg.value = null
  try {
    quizzes.value = await listQuizzes()
  } catch (e: unknown) {
    const err = e as { response?: { data?: { error?: string } }; message?: string }
    errorMsg.value = err.response?.data?.error ?? err.message ?? 'Failed to load quizzes.'
  } finally {
    loading.value = false
  }
}

async function onDelete(id: number) {
  if (!confirm('Delete this quiz? This will also remove all its questions.')) return
  try {
    await deleteQuiz(id)
    quizzes.value = quizzes.value.filter((q) => q.id !== id)
  } catch (e: unknown) {
    const err = e as { response?: { data?: { error?: string } }; message?: string }
    errorMsg.value = err.response?.data?.error ?? err.message ?? 'Failed to delete quiz.'
  }
}

onMounted(refresh)
</script>

<template>
  <QuizTopNav title="My quizzes" />

  <section class="max-w-6xl mx-auto px-4 py-8">
    <div class="flex items-center justify-between mb-6">
      <div>
        <h2 class="text-2xl font-bold text-slate-900">Quizzes</h2>
        <p class="text-sm text-slate-500">Build, edit, and launch your quizzes.</p>
      </div>
      <RouterLink
        to="/quiz/create"
        class="px-4 py-2 rounded-lg bg-koot-purple text-white font-semibold shadow hover:opacity-90"
      >
        + Create new quiz
      </RouterLink>
    </div>

    <p v-if="errorMsg" class="mb-4 text-sm text-koot-magenta" role="alert">{{ errorMsg }}</p>

    <div v-if="loading" class="text-slate-500 py-12 text-center">Loading…</div>

    <div v-else-if="quizzes.length === 0" class="rounded-xl border border-dashed border-slate-300 bg-white py-16 text-center">
      <svg viewBox="0 0 24 24" class="mx-auto w-16 h-16 text-slate-300" fill="currentColor">
        <path
          d="M4 4h16a2 2 0 0 1 2 2v12a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2Zm0 2v12h16V6H4Zm2 2h8v2H6V8Zm0 4h12v2H6v-2Zm0 4h6v2H6v-2Z"
        />
      </svg>
      <h3 class="mt-4 text-lg font-semibold text-slate-700">No quizzes yet</h3>
      <p class="mt-1 text-sm text-slate-500">Get started by creating your first quiz.</p>
      <RouterLink
        to="/quiz/create"
        class="mt-6 inline-block px-4 py-2 rounded-lg bg-koot-purple text-white font-semibold shadow hover:opacity-90"
      >
        Create new quiz
      </RouterLink>
    </div>

    <div v-else class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
      <QuizCard v-for="quiz in quizzes" :key="quiz.id" :quiz="quiz" @delete="onDelete" />
    </div>
  </section>
</template>
