// Mirrors the backend DTOs in Koot.Api/Dtos/Quizzes.

/**
 * Backend enum Koot.Api.Models.QuestionType (int). Declared as a `const` object
 * rather than a TS `enum` because the project's tsconfig sets
 * `erasableSyntaxOnly` (enums emit runtime code, which is disallowed).
 */
export const QuestionType = {
  MultipleChoice: 0,
  TrueFalse: 1,
  TypeAnswer: 2,
  Poll: 3,
} as const
export type QuestionType = (typeof QuestionType)[keyof typeof QuestionType]

export interface AnswerOption {
  id?: number
  text: string
  isCorrect: boolean
  orderIndex: number
}

export interface Question {
  id?: number
  type: QuestionType
  questionText: string
  timeLimit: number
  points: number
  imageUrl?: string | null
  orderIndex: number
  answerOptions: AnswerOption[]
}

export interface QuizSummary {
  id: number
  title: string
  description?: string | null
  coverImageUrl?: string | null
  questionCount: number
  createdAt: string
}

export interface QuizDetail {
  id: number
  title: string
  description?: string | null
  coverImageUrl?: string | null
  createdAt: string
  updatedAt: string
  questions: Question[]
}

export interface CreateQuizRequest {
  title: string
  description?: string | null
  coverImageUrl?: string | null
}

export type UpdateQuizRequest = CreateQuizRequest

export interface CreateQuestionRequest {
  type: QuestionType
  questionText: string
  timeLimit: number
  points: number
  imageUrl?: string | null
  orderIndex?: number
  answerOptions: AnswerOption[]
}

export type UpdateQuestionRequest = CreateQuestionRequest

export interface UploadResponse {
  url: string
  fileName: string
  size: number
  contentType: string
}

/** Time-limit choices surfaced by QuestionEditor. */
export const TIME_LIMITS = [5, 10, 20, 30, 60, 90, 120] as const

/** Point-value choices surfaced by QuestionEditor. */
export const POINT_VALUES = [100, 200, 500, 1000, 2000] as const

export interface QuestionTypeMeta {
  type: QuestionType
  label: string
  description: string
}

export const QUESTION_TYPE_OPTIONS: QuestionTypeMeta[] = [
  {
    type: QuestionType.MultipleChoice,
    label: 'Multiple choice',
    description: '2-4 options, mark at least one correct.',
  },
  {
    type: QuestionType.TrueFalse,
    label: 'True / False',
    description: 'Pick which one is correct.',
  },
  {
    type: QuestionType.TypeAnswer,
    label: 'Type answer',
    description: 'Players type the correct answer.',
  },
  {
    type: QuestionType.Poll,
    label: 'Poll',
    description: 'No correct answer; just gather opinions.',
  },
]

export function questionTypeLabel(type: QuestionType): string {
  return QUESTION_TYPE_OPTIONS.find((o) => o.type === type)?.label ?? 'Question'
}
