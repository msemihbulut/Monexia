module.exports = {
    content: [
        "./Views/**/*.cshtml",
        "./Pages/**/*.cshtml",
        "./wwwroot/**/*.html"
    ],
    darkMode: 'class',
    theme: {
        extend: {
            colors: {
                primary: {
                    light: '#d8b4fe',
                    DEFAULT: '#a855f7',
                    dark: '#6b21a8'
                }
            }
        }
    },
    plugins: [],
}
