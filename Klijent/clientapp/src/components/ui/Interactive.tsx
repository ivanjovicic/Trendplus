import React from 'react';
import { INTERACTION_CLASSES } from '../../styles/interactionTokens';
import { useTheme } from '../../context/ThemeContext';

/**
 * Interactive wrapper component that provides consistent hover/focus/active states
 * for any child element. Composable with existing components without layout disruption.
 */

interface InteractiveProps {
  children: React.ReactNode;
  className?: string;
  /** Element type to render - defaults to 'div' */
  as?: React.ElementType;
  /** Disable all interactive behaviors */
  disabled?: boolean;
  /** Additional ARIA attributes */
  'aria-label'?: string;
  'aria-describedby'?: string;
  'aria-expanded'?: boolean;
  'aria-selected'?: boolean;
  /** Click handler */
  onClick?: (event: React.MouseEvent) => void;
  /** Keyboard handler (Enter/Space) */
  onKeyDown?: (event: React.KeyboardEvent) => void;
  /** Tab index for keyboard navigation */
  tabIndex?: number;
  /** Role for accessibility */
  role?: string;
  /** Additional HTML attributes */
  [key: string]: any;
}

/**
 * Interactive wrapper component
 * 
 * @example
 * ```tsx
 * <Interactive 
 *   as="button" 
 *   aria-label="Export data"
 *   onClick={handleExport}
 *   className="bg-blue-600 text-white px-4 py-2"
 * >
 *   Export
 * </Interactive>
 * ```
 */
export function Interactive({ 
  children, 
  className = '',
  as: Component = 'div',
  disabled = false,
  onClick,
  onKeyDown,
  tabIndex = 0,
  ...props 
}: InteractiveProps) {
  
  // Handle keyboard interactions
  const handleKeyDown = (event: React.KeyboardEvent) => {
    // Call custom handler first
    onKeyDown?.(event);
    
    // Standard Enter/Space behavior for clickable elements
    if ((event.key === 'Enter' || event.key === ' ') && onClick) {
      event.preventDefault();
      onClick(event as any);
    }
  };

  // Combine interaction classes
  const interactiveClasses = [
    INTERACTION_CLASSES.interactive,
    INTERACTION_CLASSES.focusRing,
    INTERACTION_CLASSES.hover,
    INTERACTION_CLASSES.active,
    disabled ? INTERACTION_CLASSES.disabled : '',
    className,
  ].filter(Boolean).join(' ');

  return (
    <Component
      className={interactiveClasses}
      onClick={disabled ? undefined : onClick}
      onKeyDown={disabled ? undefined : handleKeyDown}
      tabIndex={disabled ? -1 : tabIndex}
      {...props}
    >
      {children}
    </Component>
  );
}

/**
 * Specialized Interactive variants
 */

/** Interactive input wrapper with focus ring and hover states */
export function InteractiveInput({ className = '', ...props }: InteractiveProps) {
  return (
    <Interactive
      className={`${INTERACTION_CLASSES.input} ${className}`}
      {...props}
    />
  );
}

/** Interactive button with consistent styling */
export function InteractiveButton({ 
  variant = 'primary',
  className = '', 
  ...props 
}: InteractiveProps & { variant?: 'primary' | 'secondary' | 'warning' | 'success' }) {
  
  const variantClasses = {
    primary: 'border-info bg-info/10 text-info hover:border-info/80',
    secondary: 'border-muted bg-surface-darker text-muted hover:border-muted/80',
    warning: 'border-warning bg-warning/10 text-warning hover:border-warning/80',
    success: 'border-success bg-success/10 text-success hover:border-success/80',
  };

  return (
    <Interactive
      as="button"
      className={`${INTERACTION_CLASSES.button} ${variantClasses[variant]} ${className}`}
      {...props}
    />
  );
}

/** Interactive table row with status border */
export function InteractiveTableRow({ 
  statusColor,
  className = '',
  children,
  ...props 
}: InteractiveProps & { statusColor?: 'critical' | 'warning' | 'healthy' }) {
  
  const statusBorderClasses = {
    critical: 'border-l-4 border-l-error',
    warning: 'border-l-4 border-l-warning',
    healthy: 'border-l-4 border-l-success',
  };

  const statusBorder = statusColor ? statusBorderClasses[statusColor] : '';

  return (
    <Interactive
      as="tr"
      role="row"
      className={`${INTERACTION_CLASSES.tableRow} ${statusBorder} ${className}`}
      {...props}
    >
      {children}
    </Interactive>
  );
}