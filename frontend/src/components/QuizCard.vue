<script setup lang="ts">
import { RouterLink } from 'vue-router'
import { absoluteUrl } from '../api/quizzes'
import type { QuizSummary } from '../types/quiz'

defineProps<{ quiz: QuizSummary }>()

const emit = defineEmits<{
  (e: 'delete', id: number): void
}>()

function fmtDate(iso: string): string {
  try {
    return new Date(iso).toLocaleDateString()
  } catch {
    return iso
  }
}
</script>

<template>
  <article class="rounded-xl bg-white shadow border border-slate-200 overflow-hidden flex flex-col">
    <div class="aspect-[16/9] bg-slate-100 flex items-center justify-center text-slate-300">
      <img
        v-if="quiz.coverImageUrl"
        :src="absoluteUrl(quiz.coverImageUrl)"
        :alt="quiz.title"
        class="w-full h-full object-cover"
      />
      <svg v-else viewBox="0 0 24 24" class="w-12 h-12" fill="currentColor">
        <path
          d="M9 4a3 3 0 0 0-3 3v10a3 3 0 0 0 3 3h6a3 3 0 0 0 3-3V7a3 3 0 0 0-3-3H9Zm0 2h6a1 1 0 0 1 1 1v10a1 1 0 0 1-1 1H9a1 1 0 0 1-1-1V7a1 1 0 0 1 1-1Zm2 2v2H9V8h2Zm4 0v2h-2V8h2Zm-4 4v2H9v-2h2Zm4 0v2h-2v-2h2Z"
        />
      </svg>
    </div>

    <div class="p-4 flex-1 flex flex-col">
      <h3 class="font-semibold text-slate-900 truncate" :title="quiz.title">{{ quiz.title }}</h3>
      <p v-if="quiz.description" class="text-sm text-slate-500 line-clamp-2 mt-1">
        {{ quiz.description }}
      </p>

      <div class="mt-3 text-xs text-slate-500 flex items-center gap-3">
        <span>{{ quiz.questionCount }} question{{ quiz.questionCount === 1 ? '' : 's' }}</span>
        <span>·</span>
        <span>Created {{ fmtDate(quiz.createdAt) }}</span>
      </div>

      <div class="mt-4 flex items-center justify-end gap-2">
        <RouterLink
          :to="{ name: 'quiz-edit', params: { id: String(quiz.id) } }"
          class="px-3 py-1.5 text-sm rounded-md border border-slate-300 hover:bg-slate-50"
        >
          Edit
        </RouterLink>
        <button
          type="button"
          class="px-3 py-1.5 text-sm rounded-md border border-koot-magenta text-koot-magenta hover:bg-koot-magenta hover:text-white"
          @click="emit('delete', quiz.id)"
        >
          Delete
        </button>
      </div>
    </div>
  </article>
</template>
