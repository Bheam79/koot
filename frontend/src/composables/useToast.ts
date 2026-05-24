import { ref } from 'vue'

export type ToastType = 'success' | 'error' | 'info' | 'warning'

export interface Toast {
  id: number
  message: string
  type: ToastType
}

// Module-level shared state so any component can add toasts
const toasts = ref<Toast[]>([])
let counter = 0

export function useToast() {
  function add(message: string, type: ToastType = 'info', duration = 4000): number {
    const id = ++counter
    toasts.value.push({ id, message, type })
    if (duration > 0) {
      setTimeout(() => remove(id), duration)
    }
    return id
  }

  function remove(id: number) {
    const idx = toasts.value.findIndex((t) => t.id === id)
    if (idx >= 0) toasts.value.splice(idx, 1)
  }

  return {
    toasts,
    success: (msg: string, duration?: number) => add(msg, 'success', duration),
    error:   (msg: string, duration?: number) => add(msg, 'error',   duration),
    info:    (msg: string, duration?: number) => add(msg, 'info',    duration),
    warning: (msg: string, duration?: number) => add(msg, 'warning', duration),
    remove,
  }
}
