<script setup lang="ts">
// Thin wrapper - the heavy lifting lives in QuizEditorView and is shared with the
// /quiz/:id/edit route. With no `id` prop the editor starts blank and POSTs on the
// first save.
import QuizEditorView from './QuizEditorView.vue'
</script>

<template>
  <QuizEditorView />
</template>
