// Mirrors the backend DTOs in Koot.Api/Dtos/Games (history & analytics endpoints
// added in KOOT-28). Reuses the QuestionType const-object from ./quiz.

import type { QuestionType } from './quiz'

/** One row in the host's past-games list (GET /api/games/history). */
export interface GameHistorySummary {
  id: number
  code: string
  quizId: number
  quizTitle: string
  startedAt: string | null
  endedAt: string | null
  /** Duration of the played session in seconds. Null if start/end missing. */
  durationSeconds: number | null
  participantCount: number
  averageScore: number
  topScorerNickname: string | null
  topScorerScore: number | null
}

/** Generic paginated envelope used for history responses. */
export interface HistoryPage {
  items: GameHistorySummary[]
  total: number
  page: number
  pageSize: number
}

/** One row in the standings table for SessionDetail. */
export interface SessionStanding {
  rank: number
  nickname: string
  avatarId: number
  totalScore: number
  correctCount: number
  totalQuestions: number
}

/** Per-option pick distribution (MC/Poll/TrueFalse). */
export interface OptionDistribution {
  optionId: number
  optionText: string
  pickCount: number
  /** 0..100 share of answers picking this option. */
  pickPct: number
}

/** Stats for a single question within a finished session. */
export interface QuestionStat {
  questionId: number
  questionText: string
  questionType: QuestionType
  orderIndex: number
  answerCount: number
  correctCount: number
  /** 0..100 — share of submitted answers that were correct. */
  accuracyPct: number
  avgTimeTakenMs: number
  medianTimeTakenMs: number
  /** MC/TrueFalse/Poll only. */
  optionDistribution: OptionDistribution[]
  /** TypeAnswer only. */
  correctAnswerTexts: string[]
}

/** Full analytics payload for a single finished session. */
export interface SessionDetail {
  id: number
  code: string
  quizId: number
  quizTitle: string
  startedAt: string | null
  endedAt: string | null
  durationSeconds: number | null
  standings: SessionStanding[]
  questionStats: QuestionStat[]
}

/** Flat answer row used by CSV export (GET /api/games/sessions/{id}/answers). */
export interface SessionAnswerRow {
  participantNickname: string
  questionText: string
  questionType: QuestionType
  /** Free-text answer (TypeAnswer); null for choice-style questions. */
  answerText: string | null
  /** Text of the selected option (MC/TF/Poll); null for TypeAnswer. */
  selectedOptionText: string | null
  isCorrect: boolean
  pointsEarned: number
  timeTakenMs: number
  answeredAt: string
}
