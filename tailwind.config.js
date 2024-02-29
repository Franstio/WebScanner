/** @type {import('tailwindcss').Config} */
module.exports = {
    content: ["./**/*.{razor,html,cshtml}",
        "./node_modules/flowbite/**/*.js"],
  theme: {
      extend: {
          colors: {
              'primary': '#0094CE'
          }
      },
  },
    plugins: [
        require('flowbite/plugin')({
            charts: true
        })],
}