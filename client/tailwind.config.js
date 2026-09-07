/** @type {import('tailwindcss').Config} */
export default {
  content: ['./index.html', './src/**/*.{js,ts,jsx,tsx}'],
  theme: {
    extend: {
      colors: Object.fromEntries(
        [
          'canvas',
          'panel',
          'raised',
          'line',
          'control',
          'ink',
          'secondary',
          'muted',
          'accent',
          'violet',
          'cyan',
          'selection',
          'positive',
          'warning',
          'negative',
        ].map(name => [name, `var(--${name})`])
      ),
    },
  },
  plugins: [],
};
