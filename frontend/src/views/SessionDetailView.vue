<script setup lang="ts">
// Full analytics for one finished session: header, standings, per-question
// breakdown, and CSV export. CSV is built client-side so the JWT can be sent
// via the shared axios instance (a plain <a href> can't attach headers).
import { computed, onMounted, ref } from 'vue'
import { RouterLink } from 'vue-router'
import QuizTopNav from '../components/QuizTopNav.vue'
import { getSessionAnswers, getSessionDetail } from '../api/history'
import { useToast } from '../composables/useToast'
import { QuestionType, questionTypeLabel } from '../types/quiz'
import type {
  QuestionStat,
  SessionAnswerRow,
  SessionDetail,
} from '../types/history'

const props = defineProps<{ id: string | number }>()

const toast = useToast()
const loading = ref(true)
const exporting = ref(false)
const detail = ref<SessionDetail | null>(null)
const sessionId = computed(() => Number(props.id))

async function load() {
  if (!Number.isFinite(sessionId.value)) {
    toast.error('Invalid session id.')
    loading.value = false
    return
  }
  loading.value = true
  try {
    detail.value = await getSessionDetail(sessionId.value)
  } catch (e: unknown) {
    const err = e as {
      response?: { status?: number; data?: { error?: string } }
      message?: string
    }
    const status = err.response?.status
    if (status === 404) {
      toast.error('Session not found.')
    } else if (status === 403) {
      toast.error('You do not have access to this session.')
    } else {
      toast.error(err.response?.data?.error ?? err.message ?? 'Failed to load session.')
    }
    detail.value = null
  } finally {
    loading.value = false
  }
}

onMounted(load)

// ── CSV export ──────────────────────────────────────────────────────────────

/** Escape one CSV field per RFC 4180-ish rules: wrap in quotes if it contains
 * comma, quote, CR or LF; double-up embedded quotes. */
function csvEscape(value: string | number | boolean | null | undefined): string {
  if (value == null) return ''
  const s = String(value)
  if (/[",\r\n]/.test(s)) {
    return `"${s.replace(/"/g, '""')}"`
  }
  return s
}

/** Convert the flat answer rows into a CSV string with a header line. */
function buildCsv(rows: SessionAnswerRow[]): string {
  const headers = [
    'Nickname',
    'Question',
    'Type',
    'Answer',
    'IsCorrect',
    'PointsEarned',
    'TimeTakenMs',
    'AnsweredAt',
  ]
  const lines: string[] = [headers.join(',')]
  for (const r of rows) {
    // For choice questions the answer is the option text; for TypeAnswer the
    // free-text. Fall back to '' if neither (shouldn't happen but defensive).
    const answer = r.selectedOptionText ?? r.answerText ?? ''
    lines.push(
      [
        csvEscape(r.participantNickname),
        csvEscape(r.questionText),
        csvEscape(questionTypeLabel(r.questionType)),
        csvEscape(answer),
        csvEscape(r.isCorrect),
        csvEscape(r.pointsEarned),
        csvEscape(r.timeTakenMs),
        csvEscape(r.answeredAt),
      ].join(','),
    )
  }
  return lines.join('\n')
}

function sanitizeForFilename(s: string): string {
  return s.replace(/[^a-z0-9_-]+/gi, '_').replace(/^_+|_+$/g, '') || 'session'
}

async function onExportCsv() {
  if (!detail.value || exporting.value) return
  exporting.value = true
  try {
    const rows = await getSessionAnswers(sessionId.value)
    const csv = buildCsv(rows)
    const blob = new Blob([csv], { type: 'text/csv;charset=utf-8' })
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    const stem = sanitizeForFilename(detail.value.quizTitle || `session-${detail.value.id}`)
    a.download = `${stem}-${detail.value.id}-answers.csv`
    document.body.appendChild(a)
    a.click()
    document.body.removeChild(a)
    // Allow the browser to finish reading the blob before revoking.
    setTimeout(() => URL.revokeObjectURL(url), 1000)
    toast.success('CSV export ready.')
  } catch (e: unknown) {
    const err = e as { response?: { data?: { error?: string } }; message?: string }
    toast.error(err.response?.data?.error ?? err.message ?? 'CSV export failed.')
  } finally {
    exporting.value = false
  }
}

// ── Display helpers ─────────────────────────────────────────────────────────

function fmtDateTime(iso: string | null): string {
  if (!iso) return '—'
  try {
    return new Date(iso).toLocaleString()
  } catch {
    return iso
  }
}

function fmtDuration(seconds: number | null): string {
  if (seconds == null) return '—'
  if (seconds < 60) return `${seconds}s`
  const m = Math.floor(seconds / 60)
  const s = seconds % 60
  return s === 0 ? `${m}m` : `${m}m ${s}s`
}

function fmtMsAsSeconds(ms: number): string {
  if (!Number.isFinite(ms) || ms <= 0) return '0.00s'
  return `${(ms / 1000).toFixed(2)}s`
}

/** Tailwind doesn't accept dynamic arbitrary classes at runtime; inline-style
 * width keeps the bar implementation dependency-free as the task requested. */
function pctStyle(pct: number): string {
  const clamped = Math.max(0, Math.min(100, pct))
  return `width: ${clamped}%`
}

const expandedQuestions = ref<Set<number>>(new Set())
function toggleQuestion(id: number) {
  if (expandedQuestions.value.has(id)) {
    expandedQuestions.value.delete(id)
  } else {
    expandedQuestions.value.add(id)
  }
  // Trigger reactivity (Set mutations don't notify).
  expandedQuestions.value = new Set(expandedQuestions.value)
}
function isExpanded(id: number): boolean {
  return expandedQuestions.value.has(id)
}

function typeBadgeClass(type: QuestionStat['questionType']): string {
  switch (type) {
    case QuestionType.MultipleChoice:
      return 'bg-koot-purple/10 text-koot-purple'
    case QuestionType.TrueFalse:
      return 'bg-emerald-100 text-emerald-700'
    case QuestionType.TypeAnswer:
      return 'bg-amber-100 text-amber-700'
    case QuestionType.Poll:
      return 'bg-sky-100 text-sky-700'
    default:
      return 'bg-slate-100 text-slate-700'
  }
}
</script>

<template>
  <QuizTopNav :title="detail?.quizTitle ?? 'Session details'" />

  <section class="max-w-6xl mx-auto px-4 py-8">
    <!-- Loading skeleton -->
    <div v-if="loading" class="space-y-4">
      <div class="skeleton h-8 w-1/2" />
      <div class="skeleton h-4 w-1/3" />
      <div class="skeleton h-40 w-full" />
      <div class="skeleton h-60 w-full" />
    </div>

    <div v-else-if="!detail" class="rounded-xl border border-dashed border-slate-300 bg-white py-16 text-center">
      <p class="text-slate-600">This session could not be loaded.</p>
      <RouterLink to="/history" class="mt-4 inline-block text-koot-purple hover:underline">
        Back to history
      </RouterLink>
    </div>

    <template v-else>
      <!-- Header -->
      <header class="flex flex-col sm:flex-row sm:items-end sm:justify-between gap-3 mb-6">
        <div>
          <div class="text-xs uppercase tracking-wide text-slate-500">Session</div>
          <h2 class="text-2xl font-bold text-slate-900">{{ detail.quizTitle }}</h2>
          <div class="mt-1 text-sm text-slate-600 flex flex-wrap items-center gap-x-3 gap-y-1">
            <span>Code <span class="font-mono">{{ detail.code }}</span></span>
            <span>·</span>
            <span>{{ fmtDateTime(detail.endedAt) }}</span>
            <span>·</span>
            <span>Duration {{ fmtDuration(detail.durationSeconds) }}</span>
            <span>·</span>
            <span>{{ detail.standings.length }} player{{ detail.standings.length === 1 ? '' : 's' }}</span>
          </div>
        </div>
        <div class="flex items-center gap-2">
          <RouterLink
            to="/history"
            class="px-3 py-2 text-sm rounded-md border border-slate-300 hover:bg-slate-50"
          >
            ← History
          </RouterLink>
          <button
            type="button"
            class="px-4 py-2 text-sm rounded-md bg-koot-purple text-white font-semibold shadow hover:opacity-90 disabled:opacity-50 disabled:cursor-not-allowed"
            :disabled="exporting"
            @click="onExportCsv"
          >
            {{ exporting ? 'Exporting…' : 'Export CSV' }}
          </button>
        </div>
      </header>

      <!-- Standings -->
      <section class="mb-8">
        <h3 class="text-lg font-semibold text-slate-900 mb-3">Standings</h3>
        <div
          v-if="detail.standings.length === 0"
          class="rounded-xl border border-dashed border-slate-300 bg-white px-4 py-8 text-center text-sm text-slate-500"
        >
          No participants joined this session.
        </div>
        <div v-else class="rounded-xl border border-slate-200 bg-white overflow-hidden">
          <table class="w-full text-sm">
            <thead class="bg-slate-50 text-slate-600 text-left">
              <tr>
                <th class="px-4 py-3 font-medium w-16">#</th>
                <th class="px-4 py-3 font-medium w-12"></th>
                <th class="px-4 py-3 font-medium">Nickname</th>
                <th class="px-4 py-3 font-medium text-right">Score</th>
                <th class="px-4 py-3 font-medium text-right">Correct</th>
              </tr>
            </thead>
            <tbody>
              <tr
                v-for="s in detail.standings"
                :key="`${s.nickname}-${s.rank}`"
                class="border-t border-slate-100"
              >
                <td class="px-4 py-3 font-semibold text-slate-700">{{ s.rank }}</td>
                <td class="px-4 py-3">
                  <span
                    class="inline-flex items-center justify-center w-8 h-8 rounded-full bg-koot-purple/10 text-koot-purple text-xs font-bold"
                    :title="`Avatar #${s.avatarId}`"
                  >
                    {{ s.nickname.slice(0, 2).toUpperCase() }}
                  </span>
                </td>
                <td class="px-4 py-3 font-medium text-slate-900">{{ s.nickname }}</td>
                <td class="px-4 py-3 text-right font-mono">{{ s.totalScore.toLocaleString() }}</td>
                <td class="px-4 py-3 text-right text-slate-600">
                  {{ s.correctCount }} / {{ s.totalQuestions }}
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </section>

      <!-- Per-question -->
      <section>
        <h3 class="text-lg font-semibold text-slate-900 mb-3">Per-question breakdown</h3>
        <div
          v-if="detail.questionStats.length === 0"
          class="rounded-xl border border-dashed border-slate-300 bg-white px-4 py-8 text-center text-sm text-slate-500"
        >
          No questions were played.
        </div>
        <ul v-else class="space-y-3">
          <li
            v-for="q in detail.questionStats"
            :key="q.questionId"
            class="rounded-xl border border-slate-200 bg-white overflow-hidden"
          >
            <button
              type="button"
              class="w-full text-left px-4 py-3 flex items-start gap-3 hover:bg-slate-50"
              :aria-expanded="isExpanded(q.questionId)"
              @click="toggleQuestion(q.questionId)"
            >
              <span class="font-bold text-slate-400 mt-0.5 w-8 shrink-0">
                Q{{ q.orderIndex + 1 }}
              </span>
              <span class="flex-1">
                <span class="block font-medium text-slate-900">{{ q.questionText }}</span>
                <span class="mt-1 flex flex-wrap items-center gap-2 text-xs text-slate-500">
                  <span
                    class="inline-block px-2 py-0.5 rounded-full font-medium"
                    :class="typeBadgeClass(q.questionType)"
                  >
                    {{ questionTypeLabel(q.questionType) }}
                  </span>
                  <span>{{ q.answerCount }} answer{{ q.answerCount === 1 ? '' : 's' }}</span>
                  <span>·</span>
                  <span>Avg {{ fmtMsAsSeconds(q.avgTimeTakenMs) }}</span>
                  <span>·</span>
                  <span>Accuracy {{ q.accuracyPct.toFixed(0) }}%</span>
                </span>
              </span>
              <span
                class="shrink-0 text-slate-400 transition-transform"
                :class="{ 'rotate-180': isExpanded(q.questionId) }"
                aria-hidden="true"
              >▾</span>
            </button>

            <div v-show="isExpanded(q.questionId)" class="px-4 pb-4 border-t border-slate-100">
              <!-- Accuracy bar -->
              <div class="mt-4">
                <div class="flex items-center justify-between text-xs text-slate-500 mb-1">
                  <span>Accuracy</span>
                  <span>{{ q.correctCount }} / {{ q.answerCount }} ({{ q.accuracyPct.toFixed(1) }}%)</span>
                </div>
                <div class="h-2 rounded-full bg-slate-100 overflow-hidden">
                  <div
                    class="h-full bg-emerald-500 transition-all"
                    :style="pctStyle(q.accuracyPct)"
                  />
                </div>
              </div>

              <div class="mt-3 text-xs text-slate-500">
                Average response time: {{ fmtMsAsSeconds(q.avgTimeTakenMs) }} ·
                Median: {{ fmtMsAsSeconds(q.medianTimeTakenMs) }}
              </div>

              <!-- TypeAnswer: list of correct strings -->
              <div v-if="q.questionType === QuestionType.TypeAnswer" class="mt-4">
                <div class="text-xs uppercase tracking-wide text-slate-500 mb-1">
                  Accepted answers
                </div>
                <ul
                  v-if="q.correctAnswerTexts.length > 0"
                  class="flex flex-wrap gap-2"
                >
                  <li
                    v-for="text in q.correctAnswerTexts"
                    :key="text"
                    class="px-2 py-1 rounded-md bg-emerald-50 text-emerald-700 text-sm"
                  >
                    {{ text }}
                  </li>
                </ul>
                <p v-else class="text-sm text-slate-400">(none configured)</p>
              </div>

              <!-- MC / TF / Poll: hand-rolled option distribution -->
              <div v-else-if="q.optionDistribution.length > 0" class="mt-4">
                <div class="text-xs uppercase tracking-wide text-slate-500 mb-2">
                  Option distribution
                </div>
                <ul class="space-y-2">
                  <li
                    v-for="opt in q.optionDistribution"
                    :key="opt.optionId"
                    class="text-sm"
                  >
                    <div class="flex items-center justify-between text-slate-700 mb-1">
                      <span class="truncate">{{ opt.optionText }}</span>
                      <span class="text-xs text-slate-500 shrink-0 ml-3">
                        {{ opt.pickCount }} ({{ opt.pickPct.toFixed(0) }}%)
                      </span>
                    </div>
                    <div class="h-2 rounded-full bg-slate-100 overflow-hidden">
                      <div
                        class="h-full bg-koot-purple transition-all"
                        :style="pctStyle(opt.pickPct)"
                      />
                    </div>
                  </li>
                </ul>
              </div>
            </div>
          </li>
        </ul>
      </section>
    </template>
  </section>
</template>
