import { beforeAll, describe, expect, it } from 'vitest';
import { Component, provideZonelessChangeDetection, signal, viewChild } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ImageLightbox } from './image-lightbox';

beforeAll(() => {
  // jsdom renders <dialog> but doesn't implement its modal methods (jsdom#3294).
  // Browsers have shipped them since 2022, so this is a gap in the test environment,
  // not in the component — polyfill the minimum needed to drive the real component
  // rather than weakening it with a fallback that production would never take.
  const proto = HTMLDialogElement.prototype as unknown as {
    showModal?: () => void;
    close?: () => void;
  };

  proto.showModal ??= function (this: HTMLDialogElement) {
    this.open = true;
  };

  proto.close ??= function (this: HTMLDialogElement) {
    if (!this.open) return;
    this.open = false;
    this.dispatchEvent(new Event('close'));
  };
});

/** Host wrapper driving the lightbox the way callers do — by template reference. */
@Component({
  imports: [ImageLightbox],
  template: `<ct-image-lightbox #lightbox [src]="src()" alt="Selected coffee" />`,
})
class Host {
  readonly src = signal<string | null>('blob:photo');
  readonly lightbox = viewChild.required(ImageLightbox);
}

function setup() {
  TestBed.configureTestingModule({ providers: [provideZonelessChangeDetection()] });
  const fixture = TestBed.createComponent(Host);
  fixture.autoDetectChanges();
  const dialog = () => fixture.nativeElement.querySelector('dialog') as HTMLDialogElement;
  return { fixture, host: fixture.componentInstance, dialog };
}

describe('ImageLightbox', () => {
  it('stays closed until asked to open', () => {
    const { dialog } = setup();

    expect(dialog().open).toBe(false);
  });

  it('opens as a modal dialog and shows the image uncropped', async () => {
    const { fixture, host, dialog } = setup();

    host.lightbox().open();
    await fixture.whenStable();

    expect(dialog().open).toBe(true);
    const img = dialog().querySelector('img') as HTMLImageElement;
    expect(img.getAttribute('src')).toBe('blob:photo');
    // object-contain is the whole point: object-cover would crop it again.
    expect(img.className).toContain('object-contain');
  });

  it('closes when the close button is used', async () => {
    const { fixture, host, dialog } = setup();

    host.lightbox().open();
    await fixture.whenStable();

    const close = dialog().querySelector('button[aria-label="Close photo"]') as HTMLButtonElement;
    close.click();
    await fixture.whenStable();

    expect(dialog().open).toBe(false);
  });

  it('closes when the backdrop is clicked', async () => {
    const { fixture, host, dialog } = setup();

    host.lightbox().open();
    await fixture.whenStable();

    (dialog().querySelector('[aria-hidden="true"]') as HTMLElement).click();
    await fixture.whenStable();

    expect(dialog().open).toBe(false);
  });

  it('does not open with no image to show', async () => {
    const { fixture, host, dialog } = setup();

    host.src.set(null);
    await fixture.whenStable();
    host.lightbox().open();
    await fixture.whenStable();

    expect(dialog().open).toBe(false);
  });

  it('can be reopened after being closed', async () => {
    const { fixture, host, dialog } = setup();

    host.lightbox().open();
    await fixture.whenStable();
    host.lightbox().close();
    await fixture.whenStable();
    host.lightbox().open();
    await fixture.whenStable();

    expect(dialog().open).toBe(true);
  });

  it('ignores open() while already showing', async () => {
    const { fixture, host, dialog } = setup();

    host.lightbox().open();
    await fixture.whenStable();

    // showModal() throws on an already-open dialog, so this must be a no-op rather
    // than a second call.
    expect(() => host.lightbox().open()).not.toThrow();
    expect(dialog().open).toBe(true);
  });

  it('exposes exactly one control named "Close photo"', async () => {
    const { fixture, host, dialog } = setup();

    host.lightbox().open();
    await fixture.whenStable();

    // The backdrop is clickable but must stay out of the accessibility tree: a
    // full-viewport button duplicating the close button's name gives assistive tech
    // two identical targets, one of them screen-sized.
    const named = [...dialog().querySelectorAll('[aria-label="Close photo"]')];
    expect(named).toHaveLength(1);
    expect(named[0]?.tagName).toBe('BUTTON');
    expect(dialog().querySelector('[aria-hidden="true"]')).not.toBeNull();
  });

  it('names the dialog for screen readers', async () => {
    const { fixture, host, dialog } = setup();

    host.lightbox().open();
    await fixture.whenStable();

    expect(dialog().getAttribute('aria-label')).toBe('Selected coffee');
  });
});
