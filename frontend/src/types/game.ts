// Game-related types mirroring the backend DTOs.

export const GameStatus = {
  Lobby: 'Lobby',
  InProgress: 'InProgress',
  Finished: 'Finished',
} as const
export type GameStatus = (typeof GameStatus)[keyof typeof GameStatus]

export interface Participant {
  id: number
  nickname: string
  avatarId: number
  totalScore: number
  isDisconnected: boolean
  joinedAt: string
}

export interface AnswerOptionBroadcast {
  id: number
  text: string
  orderIndex: number
}

export interface QuestionBroadcast {
  id: number
  type: number // QuestionType value
  questionText: string
  timeLimit: number
  points: number
  imageUrl?: string | null
  orderIndex: number
  answerOptions: AnswerOptionBroadcast[]
}

export interface AnswerResult {
  participantId: number
  nickname: string
  pointsEarned: number
  totalScore: number
  isCorrect: boolean
}

export interface CorrectAnswerInfo {
  id: number
  text: string
}

export interface LeaderboardEntry {
  rank: number
  participantId: number
  nickname: string
  avatarId: number
  totalScore: number
}

export interface SessionInfo {
  id: number
  code: string
  quizTitle: string
  hostName: string
  status: GameStatus
  participantCount: number
}

// The phase the host UI is in
export const HostPhase = {
  Connecting: 'connecting',
  Lobby: 'lobby',
  Question: 'question',
  Results: 'results',
  Leaderboard: 'leaderboard',
  Podium: 'podium',
  Error: 'error',
} as const
export type HostPhase = (typeof HostPhase)[keyof typeof HostPhase]

// The phase the player UI is in
export const PlayerPhase = {
  Joining: 'joining',      // connecting + calling JoinGame
  Lobby: 'lobby',          // waiting for host to start
  Question: 'question',    // answering
  Answered: 'answered',    // answer submitted, waiting for question end
  QuestionResult: 'questionResult', // ✓/✗ feedback
  Leaderboard: 'leaderboard',
  Podium: 'podium',
  Error: 'error',
} as const
export type PlayerPhase = (typeof PlayerPhase)[keyof typeof PlayerPhase]

/** Sent back to the player after SubmitAnswer succeeds */
export interface AnswerAccepted {
  points: number
  isCorrect: boolean
}

/** QuestionType values as used in broadcasts */
export const QType = {
  MultipleChoice: 0,
  TrueFalse: 1,
  TypeAnswer: 2,
  Poll: 3,
} as const
