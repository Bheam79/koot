<script setup lang="ts">
// Thin wrapper - the actual editor lives in QuizEditorView. The `id` route param
// triggers an initial GET /api/quizzes/:id load.
import QuizEditorView from './QuizEditorView.vue'

defineProps<{ id: string }>()
</script>

<template>
  <QuizEditorView :id="id" />
</template>
