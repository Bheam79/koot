<script setup lang="ts">
// Shared editor for both /quiz/create and /quiz/:id/edit. The route is detected by
// the presence of an `id` prop; if absent, we create-then-edit in place so question
// CRUD can hit the backend immediately.
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { onBeforeRouteLeave, RouterLink, useRouter } from 'vue-router'
import ImageUpload from '../components/ImageUpload.vue'
import QuestionEditor from '../components/QuestionEditor.vue'
import QuizTopNav from '../components/QuizTopNav.vue'
import { useToast } from '../composables/useToast'
import {
  createQuestion,
  createQuiz,
  deleteQuestion as apiDeleteQuestion,
  getQuiz,
  reorderQuestions,
  updateQuestion,
  updateQuiz,
} from '../api/quizzes'
import {
  QUESTION_TYPE_OPTIONS,
  QuestionType,
} from '../types/quiz'
import type { Question, QuizDetail } from '../types/quiz'

const props = defineProps<{ id?: string }>()

const router = useRouter()
const toast = useToast()

// ---------- state ----------
const quizId = ref<number | null>(props.id ? Number(props.id) : null)
const title = ref('')
const description = ref<string>('')
const coverImageUrl = ref<string | null>(null)
const questions = ref<Question[]>([])
const loading = ref(false)
const saving = ref(false)
const errorMsg = ref<string | null>(null)
const successMsg = ref<string | null>(null)
const showTypePicker = ref(false)
const dirty = ref(false)

// Tracks whether the local state differs from what's been persisted. We set this
// to false right after a successful save, and back to true on any input change.
function markDirty() {
  dirty.value = true
  successMsg.value = null
}

watch([title, description, coverImageUrl], markDirty, { deep: false })
watch(questions, markDirty, { deep: true })

const canStart = computed(() => quizId.value !== null && questions.value.length > 0 && !dirty.value)

// ---------- load ----------
async function load(id: number) {
  loading.value = true
  errorMsg.value = null
  try {
    const quiz = await getQuiz(id)
    applyServerQuiz(quiz)
    dirty.value = false
  } catch (e: unknown) {
    errorMsg.value = extractErr(e, 'Failed to load quiz.')
  } finally {
    loading.value = false
  }
}

function applyServerQuiz(quiz: QuizDetail) {
  quizId.value = quiz.id
  title.value = quiz.title
  description.value = quiz.description ?? ''
  coverImageUrl.value = quiz.coverImageUrl ?? null
  questions.value = (quiz.questions ?? []).map((q) => ({ ...q }))
}

onMounted(() => {
  if (quizId.value !== null) {
    void load(quizId.value)
  }
})

// ---------- save ----------
async function saveQuizMetadata() {
  saving.value = true
  errorMsg.value = null
  try {
    const body = {
      title: title.value.trim(),
      description: description.value.trim() || null,
      coverImageUrl: coverImageUrl.value,
    }
    if (quizId.value === null) {
      const created = await createQuiz(body)
      // Move the URL to the canonical edit route so future reloads work.
      quizId.value = created.id
      applyServerQuiz(created)
      await router.replace({ name: 'quiz-edit', params: { id: String(created.id) } })
    } else {
      const updated = await updateQuiz(quizId.value, body)
      applyServerQuiz(updated)
    }
    dirty.value = false
    successMsg.value = 'Saved.'
    toast.success('Quiz saved.')
    return true
  } catch (e: unknown) {
    errorMsg.value = extractErr(e, 'Save failed.')
    toast.error(errorMsg.value ?? 'Save failed.')
    return false
  } finally {
    saving.value = false
  }
}

async function saveAll() {
  if (!title.value.trim()) {
    errorMsg.value = 'Quiz title is required.'
    return false
  }

  const ok = await saveQuizMetadata()
  if (!ok || quizId.value === null) return false

  // Persist each question. Anything without an id was added locally and needs to
  // be created server-side; everything else is updated.
  try {
    for (let i = 0; i < questions.value.length; i++) {
      const q = questions.value[i]
      q.orderIndex = i
      if (q.id == null) {
        const created = await createQuestion(quizId.value, {
          type: q.type,
          questionText: q.questionText,
          timeLimit: q.timeLimit,
          points: q.points,
          imageUrl: q.imageUrl ?? null,
          orderIndex: i,
          answerOptions: q.answerOptions,
        })
        // Backend returns the created QuestionDto - stash id/option ids.
        questions.value[i] = { ...q, id: created.id, answerOptions: created.answerOptions }
      } else {
        const updated = await updateQuestion(quizId.value, q.id, {
          type: q.type,
          questionText: q.questionText,
          timeLimit: q.timeLimit,
          points: q.points,
          imageUrl: q.imageUrl ?? null,
          orderIndex: i,
          answerOptions: q.answerOptions,
        })
        questions.value[i] = { ...q, answerOptions: updated.answerOptions }
      }
    }

    // Push the final ordering (already implicit from the per-question save, but
    // belt-and-braces: a single reorder makes intent obvious in the audit log).
    const ordering = questions.value
      .filter((q): q is Question & { id: number } => q.id != null)
      .map((q, i) => ({ id: q.id, orderIndex: i }))
    if (ordering.length) {
      await reorderQuestions(quizId.value, ordering)
    }

    dirty.value = false
    successMsg.value = 'Quiz saved.'
    return true
  } catch (e: unknown) {
    errorMsg.value = extractErr(e, 'Failed to save questions.')
    return false
  }
}

// ---------- question CRUD ----------

function addQuestion(type: QuestionType) {
  const base: Question = {
    type,
    questionText: '',
    timeLimit: 20,
    points: 1000,
    imageUrl: null,
    orderIndex: questions.value.length,
    answerOptions:
      type === QuestionType.TrueFalse
        ? [
            { text: 'True', isCorrect: true, orderIndex: 0 },
            { text: 'False', isCorrect: false, orderIndex: 1 },
          ]
        : type === QuestionType.TypeAnswer
          ? [{ text: '', isCorrect: true, orderIndex: 0 }]
          : [
              { text: '', isCorrect: false, orderIndex: 0 },
              { text: '', isCorrect: false, orderIndex: 1 },
              { text: '', isCorrect: false, orderIndex: 2 },
              { text: '', isCorrect: false, orderIndex: 3 },
            ],
  }
  questions.value.push(base)
  showTypePicker.value = false
  markDirty()
}

async function removeQuestion(idx: number) {
  const q = questions.value[idx]
  if (!confirm('Delete this question?')) return
  if (q?.id != null && quizId.value !== null) {
    try {
      await apiDeleteQuestion(quizId.value, q.id)
    } catch (e: unknown) {
      errorMsg.value = extractErr(e, 'Failed to delete question.')
      return
    }
  }
  questions.value.splice(idx, 1)
  // Reindex.
  questions.value.forEach((qq, i) => (qq.orderIndex = i))
  markDirty()
}

function moveQuestion(idx: number, delta: number) {
  const target = idx + delta
  if (target < 0 || target >= questions.value.length) return
  const arr = [...questions.value]
  const [item] = arr.splice(idx, 1)
  arr.splice(target, 0, item)
  arr.forEach((q, i) => (q.orderIndex = i))
  questions.value = arr
  markDirty()
}

function updateQuestionAt(idx: number, q: Question) {
  questions.value.splice(idx, 1, q)
  markDirty()
}

// ---------- start game ----------
async function startQuiz() {
  if (quizId.value === null) return
  // KOOT-7+ wires the real "create session" endpoint. For now we just route to
  // the host screen using the quiz id as a placeholder code so navigation works.
  if (dirty.value) {
    const ok = await saveAll()
    if (!ok) return
  }
  await router.push({ name: 'host-game', params: { code: String(quizId.value) } })
}

// ---------- nav-away confirmation (auto-save) ----------
const navConfirmed = ref(false)

onBeforeRouteLeave(async (_to, _from, next) => {
  if (!dirty.value || navConfirmed.value) {
    next()
    return
  }
  const wantSave = confirm('You have unsaved changes. Save before leaving?')
  if (wantSave) {
    const ok = await saveAll()
    navConfirmed.value = ok
    next(ok)
  } else {
    // User explicitly chose to leave without saving.
    navConfirmed.value = true
    next()
  }
})

// Warn on hard browser unload too.
function beforeUnload(ev: BeforeUnloadEvent) {
  if (dirty.value) {
    ev.preventDefault()
    ev.returnValue = ''
  }
}
onMounted(() => window.addEventListener('beforeunload', beforeUnload))
onBeforeUnmount(() => window.removeEventListener('beforeunload', beforeUnload))

// ---------- helpers ----------
function extractErr(e: unknown, fallback: string): string {
  const err = e as { response?: { data?: { error?: string; title?: string; errors?: Record<string, string[]> } }; message?: string }
  const data = err.response?.data
  if (data?.error) return data.error
  if (data?.title) return data.title
  if (data?.errors) {
    const first = Object.values(data.errors)[0]
    if (Array.isArray(first) && first.length) return first[0]
  }
  return err.message ?? fallback
}

const screenTitle = computed(() => (quizId.value === null ? 'New quiz' : 'Edit quiz'))
</script>

<template>
  <QuizTopNav :title="screenTitle" />

  <section class="max-w-4xl mx-auto px-4 py-6">
    <p v-if="errorMsg" class="mb-3 text-sm text-koot-magenta rounded-md bg-red-50 px-3 py-2" role="alert">
      {{ errorMsg }}
    </p>
    <p v-if="successMsg" class="mb-3 text-sm text-emerald-700 rounded-md bg-emerald-50 px-3 py-2">
      {{ successMsg }}
    </p>

    <div class="rounded-xl bg-white shadow border border-slate-200 p-4 sm:p-6 mb-6">
      <div class="grid grid-cols-1 md:grid-cols-3 gap-6">
        <div class="md:col-span-2 space-y-4">
          <div>
            <label class="block text-sm font-medium text-slate-700 mb-1">Quiz title *</label>
            <input
              v-model="title"
              type="text"
              required
              placeholder="e.g. 80s Music Trivia"
              class="w-full rounded-md border border-slate-300 px-3 py-2 focus:outline-none focus:ring-2 focus:ring-koot-blue"
            />
          </div>
          <div>
            <label class="block text-sm font-medium text-slate-700 mb-1">Description</label>
            <textarea
              v-model="description"
              rows="3"
              placeholder="Optional - what's this quiz about?"
              class="w-full rounded-md border border-slate-300 px-3 py-2 focus:outline-none focus:ring-2 focus:ring-koot-blue"
            />
          </div>
        </div>
        <div>
          <ImageUpload label="Cover image" v-model="coverImageUrl" />
        </div>
      </div>
    </div>

    <div class="flex items-center justify-between mb-3">
      <h2 class="text-lg font-semibold text-slate-800">Questions</h2>
      <p class="text-xs text-slate-500">{{ questions.length }} total</p>
    </div>

    <div v-if="loading" class="text-slate-500 py-12 text-center">Loading…</div>

    <div v-else-if="questions.length === 0" class="rounded-xl border border-dashed border-slate-300 bg-white py-12 text-center text-slate-500">
      No questions yet. Add your first question to get started.
    </div>

    <div v-else class="space-y-4">
      <QuestionEditor
        v-for="(q, idx) in questions"
        :key="q.id ?? `local-${idx}`"
        :question="q"
        :index="idx"
        :total="questions.length"
        @update:question="updateQuestionAt(idx, $event)"
        @delete="removeQuestion(idx)"
        @move-up="moveQuestion(idx, -1)"
        @move-down="moveQuestion(idx, 1)"
      />
    </div>

    <!-- Type-picker for "add question". -->
    <div class="mt-6">
      <button
        v-if="!showTypePicker"
        type="button"
        class="px-4 py-2 rounded-lg bg-koot-blue text-white font-semibold shadow hover:opacity-90"
        @click="showTypePicker = true"
      >
        + Add question
      </button>

      <div v-else class="rounded-xl bg-white shadow border border-slate-200 p-4">
        <p class="text-sm font-medium text-slate-700 mb-3">Pick a question type</p>
        <div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
          <button
            v-for="opt in QUESTION_TYPE_OPTIONS"
            :key="opt.type"
            type="button"
            class="text-left rounded-lg border border-slate-200 hover:border-koot-blue hover:bg-slate-50 px-3 py-2"
            @click="addQuestion(opt.type)"
          >
            <p class="font-semibold text-slate-800">{{ opt.label }}</p>
            <p class="text-xs text-slate-500">{{ opt.description }}</p>
          </button>
        </div>
        <div class="mt-3 text-right">
          <button type="button" class="text-sm text-slate-500 underline" @click="showTypePicker = false">
            Cancel
          </button>
        </div>
      </div>
    </div>

    <footer class="mt-8 flex flex-wrap items-center justify-between gap-3 border-t border-slate-200 pt-6">
      <RouterLink to="/dashboard" class="text-sm text-slate-500 hover:underline">← Back to dashboard</RouterLink>
      <div class="flex items-center gap-3">
        <button
          type="button"
          class="px-4 py-2 rounded-lg border border-slate-300 text-slate-700 hover:bg-slate-50 disabled:opacity-50"
          :disabled="saving"
          @click="saveAll"
        >
          {{ saving ? 'Saving…' : 'Save quiz' }}
        </button>
        <button
          type="button"
          class="px-4 py-2 rounded-lg bg-koot-green text-white font-semibold shadow disabled:opacity-50 hover:opacity-90"
          :disabled="!canStart"
          :title="canStart ? 'Start a live game' : 'Save the quiz with at least one question to start.'"
          @click="startQuiz"
        >
          ▶ Start quiz
        </button>
      </div>
    </footer>
  </section>
</template>
