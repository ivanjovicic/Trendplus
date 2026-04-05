/**
 * Centralized interaction design tokens
 * Used across all interactive components for consistency
 */

export const INTERACTION_TOKENS = {
  // Focus ring colors (theme-aware)
  focusRing: {
    primary: 'var(--focus-ring)',      // Blue primary
    critical: 'var(--error)',     // Critical actions
    success: 'var(--success)',      // Success actions
    warning: 'var(--warning)',      // Warning actions
  },

  // Link to theme tokens if available
  // NOTE: import THEME_TOKENS in places that need palette values at runtime
  theme: {
    surface: 'var(--surface-light)',
    textPrimary: 'var(--text-primary)',
    textSecondary: 'var(--text-secondary)',
  },

  // Interactive background states
  interactive: {
    bg: {
      default: 'var(--interactive-bg-default, rgba(68, 208, 255, 0.04))',
      hover: 'var(--interactive-bg-hover, rgba(68, 208, 255, 0.08))',
      active: 'var(--interactive-bg-active, rgba(68, 208, 255, 0.12))',
      disabled: 'var(--interactive-bg-disabled, rgba(255, 255, 255, 0.02))',
    },
    border: {
      default: 'var(--border-default)',
      hover: 'var(--border-hover)',
      focus: 'var(--focus-ring)',
      active: 'var(--focus-ring)',
      disabled: 'var(--surface-darker)',
    },
  },

  // Elevation/shadow levels
  elevation: {
    none: 'var(--elevation-none, 0 0 0 0 transparent)',
    subtle: 'var(--elevation-subtle, 0 2px 8px -2px rgba(0, 0, 0, 0.15))',
    soft: 'var(--elevation-soft, 0 4px 16px -4px rgba(0, 0, 0, 0.25))',
    medium: 'var(--elevation-medium, 0 8px 32px -8px rgba(0, 0, 0, 0.35))',
  },

  // Animation timing
  transition: {
    fast: '150ms',
    normal: '200ms',
    slow: '300ms',
    easing: 'cubic-bezier(0.4, 0, 0.2, 1)', // ease-out
  },

  // Opacity levels
  opacity: {
    disabled: 0.4,
    hover: 0.8,
    pressed: 0.9,
  },

  // Scale transforms
  scale: {
    pressed: 0.98,
    active: 0.95,
  },
} as const;

/**
 * Tailwind-compatible class generators
 */
export const INTERACTION_CLASSES = {
  // Base interactive element
  interactive: [
    'transition-all',
    'duration-200',
    'ease-out',
    'cursor-pointer',
    'relative',
  ].join(' '),

  // Focus ring (keyboard navigation)
  focusRing: [
    'focus-visible:outline-none',
    'focus-visible:ring-2',
    'focus-visible:ring-[var(--focus-ring)]',
    'focus-visible:ring-offset-2',
    'focus-visible:ring-offset-[var(--surface-default)]',
  ].join(' '),

  // Hover states
  hover: [
    'hover:shadow-md',
    'hover:shadow-black/20',
    'hover:-translate-y-0.5',
  ].join(' '),

  // Active/pressed states
  active: [
    'active:scale-[0.98]',
    'active:shadow-sm',
    'active:translate-y-0',
  ].join(' '),

  // Disabled states
  disabled: [
    'disabled:opacity-40',
    'disabled:cursor-not-allowed',
    'disabled:pointer-events-none',
    'disabled:shadow-none',
    'disabled:transform-none',
  ].join(' '),

  // Input-specific styles
  input: [
    'border',
    'border-[var(--border-default)]',
    'bg-[var(--surface-light)]',
    'text-white',
    'placeholder:text-[var(--text-muted)]',
    'focus-visible:border-[var(--focus-ring)]',
    'focus-visible:bg-[var(--surface-elevated)]',
  ].join(' '),

  // Button-specific styles
  button: [
    'inline-flex',
    'items-center',
    'gap-2',
    'rounded-xl',
    'border',
    'px-3',
    'py-2',
    'text-xs',
    'font-semibold',
    'hover:shadow-md',
    'active:scale-[0.98]',
  ].join(' '),

  // Table row styles
  tableRow: [
    'cursor-pointer',
    'transition-all',
    'duration-150',
    'border-t',
    'border-[var(--surface-darker)]',
    'bg-[var(--surface-default)]',
    'text-[var(--text-primary)]',
    'hover:bg-[var(--surface-elevated)]',
    'focus-visible:outline-none',
    'focus-visible:bg-[var(--surface-elevated)]',
    'focus-visible:ring-2',
    'focus-visible:ring-[var(--focus-ring)]',
    'focus-visible:ring-inset',
  ].join(' '),
} as const;