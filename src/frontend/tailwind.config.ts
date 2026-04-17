import type { Config } from 'tailwindcss'

export default {
  darkMode: 'class',
  content: [
    "./components/**/*.{js,vue,ts}",
    "./layouts/**/*.vue",
    "./pages/**/*.vue",
    "./plugins/**/*.{js,ts}",
    "./app.vue",
    "./error.vue"
  ],
  theme: {
    extend: {
      colors: {
        // Minimal overrides for old brutal theme classes to prevent build errors
        brutal: {
          bg: '#f9fafb', // gray-50
          surface: '#ffffff', // white
          'surface-elevated': '#ffffff', // white
          border: '#e5e7eb', // gray-200
          
          purple: '#4f46e5', // indigo-600
          'purple-glow': '#6366f1', // indigo-500
          pink: '#db2777', // pink-600
          'pink-glow': '#ec4899', // pink-500
          cyan: '#0891b2', // cyan-600
          'cyan-glow': '#06b6d4', // cyan-500
          green: '#16a34a', // green-600
          'green-glow': '#22c55e', // green-500
          yellow: '#ca8a04', // yellow-600
          'yellow-glow': '#eab308', // yellow-500
          red: '#dc2626', // red-600
          'red-glow': '#ef4444', // red-500
          
          primary: '#111827', // gray-900
          secondary: '#4b5563', // gray-600
          muted: '#9ca3af', // gray-400
        }
      },
      borderRadius: {
        'brutal': '0.375rem', // rounded-md
      },
      fontFamily: {
        sans: ['Inter Variable', 'Inter', 'system-ui', 'sans-serif'],
        mono: ['JetBrains Mono Variable', 'JetBrains Mono', 'monospace'],
      },
    },
  },
  plugins: [],
} satisfies Config
