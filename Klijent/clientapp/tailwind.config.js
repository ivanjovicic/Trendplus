/** @type {import("tailwindcss").Config} */
export default {
    content: ["./index.html", "./src/**/*.{js,ts,jsx,tsx}"],
    theme: {
        fontFamily: {
            sans: [
                'Inter',
                'system-ui',
                '-apple-system',
                'BlinkMacSystemFont',
                'Segoe UI',
                'Apple Color Emoji',
                'Segoe UI Emoji',
                'Segoe UI Symbol'
            ]
        }
        ,
        extend: {
            colors: {
                surface: {
                    DEFAULT: 'var(--surface-default)',
                    light: 'var(--surface-light)',
                    elevated: 'var(--surface-elevated)',
                    darker: 'var(--surface-darker)',
                },
                contrast: {
                    DEFAULT: 'var(--text-primary)',
                    muted: 'var(--text-muted)',
                },
                grayCustom: {
                    50: '#f8fafb',
                    100: '#f1f5f9',
                    200: '#e2e8f0',
                    300: '#cbd5e1',
                    400: '#94a3b8',
                    500: '#64748b',
                    600: '#475569',
                    700: '#334155',
                    800: '#1f2933',
                    900: '#0b1220'
                }
            }
        }
    },
    plugins: [],
};