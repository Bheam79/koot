// Quiz / question / image-upload REST client. Thin wrapper around the shared axios
// instance — all auth headers and 401 handling already live there.

import api from '../services/api'
import type {
  CreateQuestionRequest,
  CreateQuizRequest,
  QuizDetail,
  QuizSummary,
  UpdateQuestionRequest,
  UpdateQuizRequest,
  UploadResponse,
} from '../types/quiz'

const QUIZ_ROOT = '/api/quizzes'

export async function listQuizzes(): Promise<QuizSummary[]> {
  const { data } = await api.get<QuizSummary[]>(QUIZ_ROOT)
  return data
}

export async function getQuiz(id: number): Promise<QuizDetail> {
  const { data } = await api.get<QuizDetail>(`${QUIZ_ROOT}/${id}`)
  return data
}

export async function createQuiz(body: CreateQuizRequest): Promise<QuizDetail> {
  const { data } = await api.post<QuizDetail>(QUIZ_ROOT, body)
  return data
}

export async function updateQuiz(id: number, body: UpdateQuizRequest): Promise<QuizDetail> {
  const { data } = await api.put<QuizDetail>(`${QUIZ_ROOT}/${id}`, body)
  return data
}

export async function deleteQuiz(id: number): Promise<void> {
  await api.delete(`${QUIZ_ROOT}/${id}`)
}

// ---------- questions ----------

export async function createQuestion(quizId: number, body: CreateQuestionRequest) {
  const { data } = await api.post(`${QUIZ_ROOT}/${quizId}/questions`, body)
  return data
}

export async function updateQuestion(
  quizId: number,
  questionId: number,
  body: UpdateQuestionRequest,
) {
  const { data } = await api.put(`${QUIZ_ROOT}/${quizId}/questions/${questionId}`, body)
  return data
}

export async function deleteQuestion(quizId: number, questionId: number) {
  await api.delete(`${QUIZ_ROOT}/${quizId}/questions/${questionId}`)
}

export interface ReorderItem {
  id: number
  orderIndex: number
}

export async function reorderQuestions(quizId: number, items: ReorderItem[]) {
  await api.put(`${QUIZ_ROOT}/${quizId}/questions/reorder`, { items })
}

// ---------- image upload ----------

export async function uploadImage(file: File): Promise<UploadResponse> {
  const form = new FormData()
  form.append('file', file)
  const { data } = await api.post<UploadResponse>('/api/uploads/image', form, {
    headers: { 'Content-Type': 'multipart/form-data' },
  })
  return data
}

/**
 * Convert a server-relative URL like "/uploads/abc.png" to an absolute URL pointing
 * at the API host. Pass-throughs absolute URLs (http/https/data:) unchanged.
 */
export function absoluteUrl(url: string | null | undefined): string {
  if (!url) return ''
  if (/^(https?:|data:)/i.test(url)) return url
  const base = (api.defaults.baseURL ?? '').replace(/\/$/, '')
  return `${base}${url.startsWith('/') ? '' : '/'}${url}`
}
