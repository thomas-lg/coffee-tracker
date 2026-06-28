import { describe, expect, it, beforeEach } from 'vitest';
import { TestBed } from '@angular/core/testing';
import { provideZonelessChangeDetection } from '@angular/core';
import { CountUp } from './count-up';

describe('CountUp', () => {
  beforeEach(() => {
    // Force reduced-motion so the tween resolves to its final value synchronously.
    (globalThis as unknown as { matchMedia: (q: string) => MediaQueryList }).matchMedia = () =>
      ({ matches: true }) as MediaQueryList;
    TestBed.configureTestingModule({ providers: [provideZonelessChangeDetection()] });
  });

  function render(value: number, decimals = 0) {
    const fixture = TestBed.createComponent(CountUp);
    fixture.componentRef.setInput('value', value);
    if (decimals) fixture.componentRef.setInput('decimals', decimals);
    fixture.detectChanges();
    return fixture;
  }

  it('renders the final integer value', () => {
    const fixture = render(14);
    expect((fixture.componentInstance as unknown as { display: () => string }).display()).toBe('14');
  });

  it('formats with the requested decimal places', () => {
    const fixture = render(4.4, 1);
    expect((fixture.componentInstance as unknown as { display: () => string }).display()).toBe('4.4');
  });
});
