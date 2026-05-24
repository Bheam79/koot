/** @type {import('tailwindcss').Config} */
// Tailwind v4 uses CSS-first configuration via `@import "tailwindcss"`,
// but this file is kept for tooling that still expects it (and as a
// future home for project-wide theme tweaks).
import forms from '@tailwindcss/forms'

export default {
  content: [
    './index.html',
    './src/**/*.{vue,js,ts,jsx,tsx}',
  ],
  theme: {
    extend: {
      colors: {
        // Kahoot-ish accent palette placeholders
        koot: {
          purple: '#46178F',
          magenta: '#E21B3C',
          blue: '#1368CE',
          green: '#26890C',
          yellow: '#FFA602',
        },
      },
    },
  },
  plugins: [forms],
}
