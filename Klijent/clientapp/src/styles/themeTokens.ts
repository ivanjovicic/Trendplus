/**
 * Theme tokens: lighter greys and text/surface tokens for improved contrast
 */
export const THEME_TOKENS = {
  gray: {
    50: '#f8fafb',
    100: '#f1f5f9',
    200: '#e2e8f0',
    300: '#cbd5e1',
    400: '#94a3b8',
    500: '#64748b',
    600: '#475569',
    700: '#334155',
    800: '#1f2933',
    900: '#0b1220',
  },

  surface: {
    default: '#0f1318',
    light: '#10141c',
    elevated: '#12161f',
  },

  text: {
    primary: '#dbe6fb',
    secondary: '#9aa9c6',
    muted: '#73809a',
  },

  focus: '#44d0ff',
} as const;

export default THEME_TOKENS;
