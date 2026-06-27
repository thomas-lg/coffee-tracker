import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/** A pulsing placeholder block shown while content loads. */
@Component({
  selector: 'ct-skeleton',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { '[style.width]': 'width()', '[style.height]': 'height()' },
  template: `<div class="size-full animate-pulse rounded-md bg-line/60"></div>`,
})
export class Skeleton {
  readonly width = input('100%');
  readonly height = input('1rem');
}
