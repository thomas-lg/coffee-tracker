import { describe, expect, it } from 'vitest';
import { Component, provideZonelessChangeDetection, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import type { ActionStateLike } from '@coffee-tracker/util';
import { Button } from './button';

@Component({
  imports: [Button],
  template: `
    <ct-button
      [state]="state()"
      [disabled]="disabled()"
      [runningLabel]="runningLabel()"
      (click)="clicks = clicks + 1"
    >
      Save
    </ct-button>
  `,
})
class Host {
  readonly state = signal<ActionStateLike>(false);
  readonly disabled = signal(false);
  readonly runningLabel = signal<string | null>('Saving…');
  clicks = 0;
}

/** Mostly: that `state` and `disabled` differ where it matters, which is focus. */
describe('Button', () => {
  function create() {
    TestBed.configureTestingModule({ providers: [provideZonelessChangeDetection()] });
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();
    return fixture;
  }

  const host = (fixture: ReturnType<typeof create>): HTMLElement =>
    (fixture.nativeElement as HTMLElement).querySelector('ct-button')!;
  const control = (fixture: ReturnType<typeof create>): HTMLButtonElement =>
    (fixture.nativeElement as HTMLElement).querySelector('button')!;

  it('keeps the projected label until the action runs, then puts it back', () => {
    const fixture = create();
    expect(control(fixture).textContent?.trim()).toBe('Save');

    fixture.componentInstance.state.set(true);
    fixture.detectChanges();
    expect(control(fixture).textContent?.trim()).toBe('Saving…');

    fixture.componentInstance.state.set(false);
    fixture.detectChanges();
    expect(control(fixture).textContent?.trim()).toBe('Save');
  });

  it('keeps the label and still shows it is busy when given no running label', () => {
    const fixture = create();
    fixture.componentInstance.runningLabel.set(null);
    fixture.componentInstance.state.set(true);
    fixture.detectChanges();

    expect(control(fixture).textContent?.trim()).toBe('Save');
    expect(control(fixture).getAttribute('aria-busy')).toBe('true');
  });

  it('swallows a click while running, so the caller never hears it twice', () => {
    const fixture = create();
    control(fixture).click();
    expect(fixture.componentInstance.clicks).toBe(1);

    fixture.componentInstance.state.set(true);
    fixture.detectChanges();
    control(fixture).click();
    expect(fixture.componentInstance.clicks).toBe(1);

    fixture.componentInstance.state.set(false);
    fixture.detectChanges();
    control(fixture).click();
    expect(fixture.componentInstance.clicks).toBe(2);
  });

  it('holds onto focus while running, which a disabled attribute would drop', () => {
    const fixture = create();
    control(fixture).focus();
    expect(document.activeElement).toBe(control(fixture));

    fixture.componentInstance.state.set(true);
    fixture.detectChanges();

    expect(document.activeElement).toBe(control(fixture));
    expect(control(fixture).disabled).toBe(false);
    expect(control(fixture).getAttribute('aria-disabled')).toBe('true');
  });

  it('marks a finished action, and stops marking it once the state returns to rest', () => {
    const fixture = create();
    expect(control(fixture).querySelector('svg')).toBe(null);

    fixture.componentInstance.state.set('done');
    fixture.detectChanges();
    expect(control(fixture).querySelector('svg')).not.toBe(null);

    fixture.componentInstance.state.set('idle');
    fixture.detectChanges();
    expect(control(fixture).querySelector('svg')).toBe(null);
  });

  it('really disables a control that has nothing to act on', () => {
    const fixture = create();
    fixture.componentInstance.disabled.set(true);
    fixture.detectChanges();

    expect(control(fixture).disabled).toBe(true);
  });

  it('takes a request status as readily as a boolean, and carries it on the element', () => {
    const fixture = create();
    expect(host(fixture).hasAttribute('data-state')).toBe(false);

    fixture.componentInstance.state.set('pending');
    fixture.detectChanges();
    expect(host(fixture).getAttribute('data-state')).toBe('running');
    expect(control(fixture).textContent?.trim()).toBe('Saving…');

    fixture.componentInstance.state.set({ error: 'nope' });
    fixture.detectChanges();
    expect(host(fixture).getAttribute('data-state')).toBe('failed');
    // Failed is not busy: the control has to work again for a retry to be possible.
    expect(control(fixture).getAttribute('aria-disabled')).toBe(null);
    expect(control(fixture).textContent?.trim()).toBe('Save');
  });
});
