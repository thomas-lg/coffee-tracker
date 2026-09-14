import { describe, expect, it, beforeEach, afterEach } from 'vitest';
import { TestBed } from '@angular/core/testing';
import { Toast, ToastService } from './toast';

describe('Toast', () => {
  let fixture: ReturnType<typeof TestBed.createComponent<Toast>>;
  let toasts: ToastService;

  const region = (live: 'polite' | 'assertive') =>
    fixture.nativeElement.querySelector(`[aria-live="${live}"]`) as HTMLElement | null;

  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [Toast] });
    fixture = TestBed.createComponent(Toast);
    toasts = TestBed.inject(ToastService);
    fixture.detectChanges();
  });

  afterEach(() => TestBed.resetTestingModule());

  // The regression this component was rewritten for. The role used to sit on the toast
  // element, which the @for creates at the moment there is something to say, so the
  // region and its content appeared together and screen readers missed the change.
  it('has both live regions in the DOM before anything is announced', () => {
    expect(toasts.toasts()).toHaveLength(0);

    expect(region('polite')).not.toBeNull();
    expect(region('assertive')).not.toBeNull();
  });

  it('announces an error assertively and leaves the polite region alone', () => {
    toasts.show('Delete failed. Please retry.', 'error');
    fixture.detectChanges();

    expect(region('assertive')?.textContent).toContain('Delete failed');
    expect(region('polite')?.textContent?.trim()).toBe('');
  });

  it('announces success and info politely', () => {
    toasts.show('Catalog restored.', 'success');
    toasts.show('Bag scanned.', 'info');
    fixture.detectChanges();

    const polite = region('polite')?.textContent ?? '';
    expect(polite).toContain('Catalog restored.');
    expect(polite).toContain('Bag scanned.');
    expect(region('assertive')?.textContent?.trim()).toBe('');
  });

  it('keeps the regions once the queue empties again', () => {
    toasts.show('Catalog restored.', 'success');
    fixture.detectChanges();
    const before = region('polite');

    const [announced] = toasts.toasts();
    expect(announced).toBeDefined();
    toasts.dismiss(announced!.id);
    fixture.detectChanges();

    // The same element, not a replacement: a region torn down and rebuilt between
    // announcements is the bug this file exists to prevent.
    expect(region('polite')).toBe(before);
    expect(region('polite')?.textContent?.trim()).toBe('');
  });
});
