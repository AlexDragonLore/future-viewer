/** @type {import('tailwindcss').Config} */
export default {
  content: ['./index.html', './src/**/*.{vue,js,ts}'],
  theme: {
    extend: {
      colors: {
        mystic: {
          deepest: '#0b0618',
          deep: '#13082a',
          mid: '#1a0a2e',
          veil: '#2a1248',
          accent: '#f5c26b',
          accentDim: '#b98946',
          silver: '#e8d5f2',
        },
      },
      fontFamily: {
        display: ['"Cinzel"', 'serif'],
        body: ['"Inter"', 'system-ui', 'sans-serif'],
      },
      boxShadow: {
        glow: '0 0 40px rgba(245, 194, 107, 0.25)',
        glowStrong: '0 0 80px rgba(245, 194, 107, 0.5)',
      },
    },
  },
  plugins: [],
}
