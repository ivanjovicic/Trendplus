import { describe, it, expect } from 'vitest';
import { fmtRsd, fmtPct, fmtNumber } from '../analyticsFormatters';

describe('analyticsFormatters', () => {
  it('fmtRsd returns fallback for null', () => {
    expect(fmtRsd(null)).toBe('N/A');
  });

  it('fmtPct returns fallback for null', () => {
    expect(fmtPct(null)).toBe('N/A');
  });

  it('fmtRsd appends RSD', () => {
    const out = fmtRsd(1500);
    expect(out).toContain('RSD');
    expect(out).not.toBe('N/A');
  });

  it('fmtPct appends percent sign', () => {
    const out = fmtPct(12.34);
    expect(out).toContain('%');
    expect(out).not.toBe('N/A');
  });

  it('fmtNumber returns digits or fallback', () => {
    expect(fmtNumber(1234.5, 1)).not.toBe('N/A');
    expect(fmtNumber(null)).toBe('N/A');
  });
});
