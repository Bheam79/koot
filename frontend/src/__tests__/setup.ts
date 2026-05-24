// Global test setup
// Provide a minimal localStorage mock for tests that use it
import { vi } from 'vitest'

// Polyfill matchMedia (used by some UI libs; not available in jsdom by default)
Object.defineProperty(window, 'matchMedia', {
  writable: true,
  value: vi.fn().mockImplementation((query: string) => ({
    matches: false,
    media: query,
    onchange: null,
    addListener: vi.fn(),
    removeListener: vi.fn(),
    addEventListener: vi.fn(),
    removeEventListener: vi.fn(),
    dispatchEvent: vi.fn(),
  })),
})
