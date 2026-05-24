import { ref, watch } from 'vue'

const STORAGE_KEY = 'koot.sound'

// Shared module-level state
const soundEnabled = ref(localStorage.getItem(STORAGE_KEY) === 'true')

// Persist preference
watch(soundEnabled, (val) => {
  localStorage.setItem(STORAGE_KEY, String(val))
})

let audioCtx: AudioContext | null = null

function getAudioCtx(): AudioContext | null {
  if (!audioCtx) {
    try {
      audioCtx = new AudioContext()
    } catch {
      return null
    }
  }
  return audioCtx
}

function playTone(frequency: number, duration: number, volume = 0.25) {
  const ctx = getAudioCtx()
  if (!ctx) return

  // Resume if suspended (browser policy)
  if (ctx.state === 'suspended') ctx.resume()

  const osc = ctx.createOscillator()
  const gain = ctx.createGain()
  osc.connect(gain)
  gain.connect(ctx.destination)

  osc.type = 'sine'
  osc.frequency.value = frequency
  gain.gain.setValueAtTime(volume, ctx.currentTime)
  gain.gain.exponentialRampToValueAtTime(0.001, ctx.currentTime + duration)
  osc.start(ctx.currentTime)
  osc.stop(ctx.currentTime + duration)
}

export function useSound() {
  function playClick() {
    if (!soundEnabled.value) return
    playTone(880, 0.08, 0.2)
  }

  function playCorrect() {
    if (!soundEnabled.value) return
    playTone(660, 0.1, 0.2)
    setTimeout(() => playTone(880, 0.15, 0.2), 80)
  }

  function playIncorrect() {
    if (!soundEnabled.value) return
    playTone(220, 0.25, 0.2)
  }

  function playCountdown() {
    if (!soundEnabled.value) return
    playTone(440, 0.08, 0.15)
  }

  return {
    soundEnabled,
    playClick,
    playCorrect,
    playIncorrect,
    playCountdown,
  }
}
