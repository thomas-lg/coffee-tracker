import { describe, expect, it } from 'vitest';
import { TestBed } from '@angular/core/testing';
import { provideZonelessChangeDetection } from '@angular/core';
import { ConfirmAction } from './confirm-action';

/**
 * Focus is the whole point of this component: every transition destroys the control that
 * had it, and a destroyed element drops focus to <body>, which strands a keyboard user
 * at the top of the document. Each test below is one such transition.
 */
describe('ConfirmAction', () => {
  function create() {
    TestBed.configureTestingModule({ providers: [provideZonelessChangeDetection()] });
    const fixture = TestBed.createComponent(ConfirmAction);
    fixture.componentRef.setInput('label', 'Delete');
    fixture.componentRef.setInput('prompt', 'Delete “Ethiopia”?');
    fixture.componentRef.setInput('busyLabel', 'Deleting…');
    fixture.detectChanges();
    return fixture;
  }

  const el = (fixture: ReturnType<typeof create>): HTMLElement =>
    fixture.nativeElement as HTMLElement;

  const click = (fixture: ReturnType<typeof create>, label: string): void => {
    const button = Array.from(el(fixture).querySelectorAll<HTMLButtonElement>('button')).find(
      (b) => b.textContent?.trim() === label,
    );
    expect(button, `no button labelled "${label}"`).toBeTruthy();
    button?.click();
  };

  /** afterNextRender runs on a macrotask after the render it was queued behind. */
  async function settle(fixture: ReturnType<typeof create>): Promise<void> {
    fixture.detectChanges();
    await new Promise((resolve) => setTimeout(resolve, 0));
    fixture.detectChanges();
  }

  const focused = (): string | undefined => document.activeElement?.textContent?.trim();

  it('arms the confirmation rather than acting on the first click', async () => {
    const fixture = create();
    let confirmed = 0;
    fixture.componentInstance.confirmed.subscribe(() => confirmed++);

    click(fixture, 'Delete');
    await settle(fixture);

    expect(el(fixture).textContent).toContain('Delete “Ethiopia”?');
    expect(confirmed).toBe(0);
  });

  it('moves focus onto Cancel when arming, and back to the action when cancelling', async () => {
    const fixture = create();

    click(fixture, 'Delete');
    await settle(fixture);
    expect(focused()).toBe('Cancel');

    click(fixture, 'Cancel');
    await settle(fixture);
    expect(focused()).toBe('Delete');
  });

  it('does not grab focus merely because it rendered', async () => {
    const fixture = create();
    await settle(fixture);

    expect(document.activeElement).toBe(document.body);
  });

  it('emits once on confirm and shows the busy label while the action runs', async () => {
    const fixture = create();
    let confirmed = 0;
    fixture.componentInstance.confirmed.subscribe(() => confirmed++);

    click(fixture, 'Delete');
    await settle(fixture);
    click(fixture, 'Confirm delete');
    fixture.componentRef.setInput('state', true); // a boolean caller
    await settle(fixture);

    expect(confirmed).toBe(1);
    expect(el(fixture).textContent).toContain('Deleting…');
  });

  it('ignores a second confirm while the action is in flight', async () => {
    const fixture = create();
    let confirmed = 0;
    fixture.componentInstance.confirmed.subscribe(() => confirmed++);

    click(fixture, 'Delete');
    await settle(fixture);
    click(fixture, 'Confirm delete');
    fixture.componentRef.setInput('state', 'pending'); // a request-status caller
    await settle(fixture);
    click(fixture, 'Deleting…');

    expect(confirmed).toBe(1);
  });

  it('stands the row down and returns focus once the action finishes', async () => {
    // The caller's own state removes this component after a successful delete, so this
    // is the refused case: nothing else would put the screen back in a usable state.
    const fixture = create();

    click(fixture, 'Delete');
    await settle(fixture);
    click(fixture, 'Confirm delete');
    fixture.componentRef.setInput('state', 'pending');
    await settle(fixture);

    fixture.componentRef.setInput('state', { error: 'forbidden' });
    await settle(fixture);

    expect(el(fixture).textContent).not.toContain('Delete “Ethiopia”?');
    expect(focused()).toBe('Delete');
  });

  it('keeps Cancel focusable while the action runs, and refuses its clicks', async () => {
    // Arming parks focus here, and a click on Confirm does not move focus in every
    // engine, so a `disabled` attribute would drop focus to <body> mid-request.
    const fixture = create();
    click(fixture, 'Delete');
    await settle(fixture);

    const cancel = Array.from(el(fixture).querySelectorAll('button')).find(
      (b) => b.textContent?.trim() === 'Cancel',
    ) as HTMLButtonElement;

    fixture.componentRef.setInput('state', 'pending');
    await settle(fixture);

    expect(cancel.disabled).toBe(false);
    expect(cancel.getAttribute('aria-disabled')).toBe('true');
    expect(document.activeElement).toBe(cancel);

    cancel.click();
    await settle(fixture);
    expect(el(fixture).textContent).toContain('Delete “Ethiopia”?');
  });

  it('cannot be armed while disabled', async () => {
    const fixture = create();
    fixture.componentRef.setInput('disabled', true);
    await settle(fixture);

    click(fixture, 'Delete');
    await settle(fixture);

    expect(el(fixture).textContent).not.toContain('Delete “Ethiopia”?');
  });

  it('announces the swap: the live region outlives both branches', async () => {
    const fixture = create();
    const live = (): Element | null => el(fixture).querySelector('[aria-live="polite"]');
    const before = live();

    click(fixture, 'Delete');
    await settle(fixture);

    expect(live()).toBe(before);
    expect(before?.textContent).toContain('Delete “Ethiopia”?');
  });
});
