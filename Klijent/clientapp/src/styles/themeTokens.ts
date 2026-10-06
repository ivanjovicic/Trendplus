/**
 * Theme tokens: lighter greys and text/surface tokens for improved contrast
 */
export const THEME_TOKENS = {
  gray: {
    50: 'var(--gray-50)',
    100: 'var(--gray-100)',
    200: 'var(--gray-200)',
    300: 'var(--gray-300)',
    400: 'var(--gray-400)',
    500: 'var(--gray-500)',
    600: 'var(--gray-600)',
    700: 'var(--gray-700)',
    800: 'var(--gray-800)',
    900: 'var(--gray-900)',
  },

  surface: {
    default: 'var(--surface-default)',
    light: 'var(--surface-light)',
    elevated: 'var(--surface-elevated)',
  },

  text: {
    primary: 'var(--text-primary)',
    secondary: 'var(--text-secondary)',
    muted: 'var(--text-muted)',
  },

  focus: 'var(--focus-ring)',
} as const;

export default THEME_TOKENS;
