/**
 * Theme tokens: lighter greys and text/surface tokens for improved contrast
 */
export const THEME_TOKENS = {
  gray: {
    50: 'var(--c-f8fafb)',
    100: 'var(--c-f1f5f9)',
    200: 'var(--c-e2e8f0)',
    300: 'var(--c-cbd5e1)',
    400: 'var(--c-94a3b8)',
    500: 'var(--c-64748b)',
    600: 'var(--c-475569)',
    700: 'var(--c-334155)',
    800: 'var(--c-1f2933)',
    900: 'var(--c-0b1220)',
  },

  surface: {
    default: 'var(--c-0f1318)',
    light: 'var(--c-10141c)',
    elevated: 'var(--c-12161f)',
  },

  text: {
    primary: 'var(--c-dbe6fb)',
    secondary: 'var(--c-9aa9c6)',
    muted: 'var(--c-73809a)',
  },

  focus: 'var(--c-44d0ff)',
} as const;

export default THEME_TOKENS;
