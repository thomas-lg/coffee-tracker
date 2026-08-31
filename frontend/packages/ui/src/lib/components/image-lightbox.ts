import { ChangeDetectionStrategy, Component, ElementRef, input, viewChild } from '@angular/core';
import { Icon } from './icon';

/**
 * Full-size viewer for a photo that is displayed cropped elsewhere.
 *
 * Both places we show a bag photo use `object-cover`, which fills the frame by
 * cutting the edges off — fine as a thumbnail, useless when you actually want to
 * read the label. This shows the whole thing, scaled to fit the viewport
 * (`object-contain`), over a dimmed backdrop.
 *
 * Rendered as a native `<dialog>` so the browser supplies the modal semantics for
 * free — focus containment, inertness of the page behind, and top-layer stacking
 * that can't be broken by an ancestor's `overflow` or `z-index`.
 *
 * Driven by method call rather than by a bound `open` flag, because `<dialog>` is
 * an imperative API (`showModal()` throws if it is already open) and the element
 * already tracks whether it is showing. A bound flag would mean shadowing that
 * state in a signal and running an effect to keep the two in step — two sources of
 * truth for one boolean. Callers use a template reference instead:
 *
 * ```html
 * <button (click)="lightbox.open()">…</button>
 * <ct-image-lightbox #lightbox [src]="photoUrl" [alt]="name" />
 * ```
 *
 * Closing needs no caller involvement at all: the close button, the backdrop and
 * Escape are all handled here.
 */
@Component({
  selector: 'ct-image-lightbox',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [Icon],
  template: `
    <!-- ::backdrop can't be styled from Tailwind classes, so the dim layer is a child
         element rather than the pseudo-element. -->
    <dialog
      #dialog
      class="m-0 max-h-none max-w-none bg-transparent p-0 backdrop:bg-transparent"
      [attr.aria-label]="alt() || 'Photo'"
    >
      <div class="fixed inset-0 flex items-center justify-center bg-black/80 p-4 sm:p-8">
        <!-- Backdrop click closes. Deliberately NOT a <button>: a full-viewport control
             sharing the close button's accessible name gives assistive tech two
             identical "Close photo" targets, one of them the size of the screen.
             Keyboard users already have the close button and Escape, so this is a
             pointer-only affordance and is hidden from the accessibility tree. -->
        <div class="absolute inset-0 cursor-zoom-out" aria-hidden="true" (click)="close()"></div>

        @if (src(); as source) {
          <img
            [src]="source"
            [alt]="alt()"
            decoding="async"
            class="relative max-h-full max-w-full rounded-lg object-contain shadow-2xl"
          />
        }

        <button
          type="button"
          class="absolute right-3 top-3 z-10 grid size-10 place-items-center rounded-full bg-black/60 text-white transition hover:bg-black/80 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-white sm:right-6 sm:top-6"
          aria-label="Close photo"
          (click)="close()"
        >
          <ct-icon name="x" [size]="20" />
        </button>
      </div>
    </dialog>
  `,
})
export class ImageLightbox {
  /** Image to show at full size. */
  readonly src = input<string | null>(null);

  /** Alt text; also names the dialog for screen readers. */
  readonly alt = input('');

  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');

  /** Shows the photo. No-op with nothing to show, or when already showing. */
  open(): void {
    const el = this.dialog().nativeElement;
    if (el.open || !this.src()) return;
    el.showModal();
  }

  close(): void {
    this.dialog().nativeElement.close();
  }
}
