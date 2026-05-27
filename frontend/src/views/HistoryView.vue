<script setup lang="ts">
// Past-games list. Paginated, 20 per page. Each row links to /history/:id.
import { computed, onMounted, ref, watch } from 'vue'
import { RouterLink } from 'vue-router'
import QuizTopNav from '../components/QuizTopNav.vue'
import { listHistory } from '../api/history'
import { useToast } from '../composables/useToast'
import type { GameHistorySummary } from '../types/history'

const PAGE_SIZE = 20

const toast = useToast()
const loading = ref(true)
const items = ref<GameHistorySummary[]>([])
const total = ref(0)
const page = ref(1)

const pageCount = computed(() => Math.max(1, Math.ceil(total.value / PAGE_SIZE)))
const hasPrev = computed(() => page.value > 1)
const hasNext = computed(() => page.value < pageCount.value)

async function load() {
  loading.value = true
  try {
    const result = await listHistory({ page: page.value, pageSize: PAGE_SIZE })
    items.value = result.items
    total.value = result.total
    // Defensive: server might clamp page; reflect it locally so prev/next stay
    // consistent with what's actually shown.
    if (result.page && result.page !== page.value) {
      page.value = result.page
    }
  } catch (e: unknown) {
    const err = e as { response?: { data?: { error?: string } }; message?: string }
    toast.error(err.response?.data?.error ?? err.message ?? 'Failed to load history.')
    items.value = []
    total.value = 0
  } finally {
    loading.value = false
  }
}

function prevPage() {
  if (hasPrev.value) page.value -= 1
}

function nextPage() {
  if (hasNext.value) page.value += 1
}

watch(page, load)
onMounted(load)

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

function fmtScore(n: number): string {
  return Math.round(n).toLocaleString()
}
</script>

<template>
  <QuizTopNav title="Game history" />

  <section class="max-w-6xl mx-auto px-4 py-8">
    <div class="flex items-center justify-between mb-6">
      <div>
        <h2 class="text-2xl font-bold text-slate-900">Past games</h2>
        <p class="text-sm text-slate-500">
          Review participation, scores and per-question analytics from your hosted sessions.
        </p>
      </div>
    </div>

    <!-- Skeleton loader -->
    <div v-if="loading" class="rounded-xl border border-slate-200 bg-white overflow-hidden">
      <div
        v-for="i in 6"
        :key="i"
        class="px-4 py-3 border-b border-slate-100 last:border-b-0 flex items-center gap-3"
      >
        <div class="skeleton h-4 w-40" />
        <div class="skeleton h-4 w-32" />
        <div class="skeleton h-4 w-20 ml-auto" />
      </div>
    </div>

    <div
      v-else-if="items.length === 0"
      class="rounded-xl border border-dashed border-slate-300 bg-white py-16 text-center"
    >
      <svg viewBox="0 0 24 24" class="mx-auto w-16 h-16 text-slate-300" fill="currentColor">
        <path
          d="M13 3a9 9 0 1 0 8.94 10H19.9A7 7 0 1 1 13 5v3l5-4-5-4v3Zm-1 5v5l4.25 2.52.75-1.23L13.5 12V8H12Z"
        />
      </svg>
      <h3 class="mt-4 text-lg font-semibold text-slate-700">No past games yet</h3>
      <p class="mt-1 text-sm text-slate-500">
        Once you host and finish a game, you'll see its results here.
      </p>
    </div>

    <div v-else class="rounded-xl border border-slate-200 bg-white overflow-hidden">
      <!-- Table on >=sm; stacked cards on mobile -->
      <table class="hidden sm:table w-full text-sm">
        <thead class="bg-slate-50 text-slate-600 text-left">
          <tr>
            <th class="px-4 py-3 font-medium">Quiz</th>
            <th class="px-4 py-3 font-medium">Ended</th>
            <th class="px-4 py-3 font-medium">Duration</th>
            <th class="px-4 py-3 font-medium text-right">Players</th>
            <th class="px-4 py-3 font-medium text-right">Avg score</th>
            <th class="px-4 py-3 font-medium">Top scorer</th>
            <th class="px-4 py-3"></th>
          </tr>
        </thead>
        <tbody>
          <tr
            v-for="row in items"
            :key="row.id"
            class="border-t border-slate-100 hover:bg-slate-50 cursor-pointer"
          >
            <td class="px-4 py-3">
              <RouterLink
                :to="{ name: 'session-detail', params: { id: String(row.id) } }"
                class="font-medium text-slate-900 hover:text-koot-purple"
              >
                {{ row.quizTitle || `Session #${row.id}` }}
              </RouterLink>
              <div class="text-xs text-slate-500">Code {{ row.code }}</div>
            </td>
            <td class="px-4 py-3 text-slate-600">{{ fmtDateTime(row.endedAt) }}</td>
            <td class="px-4 py-3 text-slate-600">{{ fmtDuration(row.durationSeconds) }}</td>
            <td class="px-4 py-3 text-right">{{ row.participantCount }}</td>
            <td class="px-4 py-3 text-right">{{ fmtScore(row.averageScore) }}</td>
            <td class="px-4 py-3 text-slate-600">
              <template v-if="row.topScorerNickname">
                <span class="font-medium text-slate-800">{{ row.topScorerNickname }}</span>
                <span class="text-xs text-slate-500"> · {{ fmtScore(row.topScorerScore ?? 0) }}</span>
              </template>
              <template v-else>—</template>
            </td>
            <td class="px-4 py-3 text-right">
              <RouterLink
                :to="{ name: 'session-detail', params: { id: String(row.id) } }"
                class="px-3 py-1.5 text-xs rounded-md border border-slate-300 hover:bg-white"
              >
                View
              </RouterLink>
            </td>
          </tr>
        </tbody>
      </table>

      <!-- Mobile card list -->
      <ul class="sm:hidden divide-y divide-slate-100">
        <li v-for="row in items" :key="row.id" class="p-4">
          <RouterLink
            :to="{ name: 'session-detail', params: { id: String(row.id) } }"
            class="block"
          >
            <div class="font-medium text-slate-900">
              {{ row.quizTitle || `Session #${row.id}` }}
            </div>
            <div class="mt-1 text-xs text-slate-500">
              {{ fmtDateTime(row.endedAt) }} · {{ fmtDuration(row.durationSeconds) }}
            </div>
            <div class="mt-2 flex items-center gap-3 text-sm text-slate-600">
              <span>{{ row.participantCount }} player{{ row.participantCount === 1 ? '' : 's' }}</span>
              <span>·</span>
              <span>Avg {{ fmtScore(row.averageScore) }}</span>
            </div>
            <div v-if="row.topScorerNickname" class="mt-1 text-xs text-slate-500">
              Top: <span class="font-medium text-slate-700">{{ row.topScorerNickname }}</span>
              · {{ fmtScore(row.topScorerScore ?? 0) }}
            </div>
          </RouterLink>
        </li>
      </ul>
    </div>

    <!-- Pagination -->
    <div
      v-if="!loading && items.length > 0"
      class="mt-4 flex items-center justify-between text-sm text-slate-600"
    >
      <div>
        Showing {{ (page - 1) * PAGE_SIZE + 1 }}–{{ (page - 1) * PAGE_SIZE + items.length }}
        of {{ total }}
      </div>
      <div class="flex items-center gap-2">
        <button
          type="button"
          class="px-3 py-1.5 rounded-md border border-slate-300 hover:bg-slate-50 disabled:opacity-50 disabled:cursor-not-allowed"
          :disabled="!hasPrev"
          @click="prevPage"
        >
          Prev
        </button>
        <span class="px-2 text-xs text-slate-500">Page {{ page }} of {{ pageCount }}</span>
        <button
          type="button"
          class="px-3 py-1.5 rounded-md border border-slate-300 hover:bg-slate-50 disabled:opacity-50 disabled:cursor-not-allowed"
          :disabled="!hasNext"
          @click="nextPage"
        >
          Next
        </button>
      </div>
    </div>
  </section>
</template>
