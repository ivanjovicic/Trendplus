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
                    card: 'var(--surface-card)',
                },
                contrast: {
                    DEFAULT: 'var(--text-primary)',
                    muted: 'var(--text-muted)',
                },
                border: 'var(--border)',
                'border-strong': 'var(--border-strong)',
                primary: 'var(--primary)',
                'text-secondary': 'var(--text-secondary)',
                'text-on-primary': 'var(--text-on-primary)',
                'accent-primary': 'var(--accent-primary)',
                'accent-text': 'var(--accent-text)',
                'error-text': 'var(--error-text)',
                success: 'var(--success)',
                error: 'var(--error)',
                accent: {
                    success: 'var(--accent-success)',
                    soft: 'var(--accent-soft)',
                    border: 'var(--accent-border)',
                    primary: 'var(--accent-primary)',
                    text: 'var(--accent-text)',
                },
                'info-10': 'var(--info-10)',
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