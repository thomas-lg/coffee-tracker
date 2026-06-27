import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

/** Surface container in the coffee theme. `interactive` adds a hover lift. */
@Component({
  selector: 'ct-card',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { '[class]': 'classes()' },
  template: `<ng-content />`,
})
export class Card {
  readonly interactive = input(false);

  protected readonly classes = computed(
    () =>
      'block overflow-hidden rounded-2xl border border-line bg-foam shadow-sm' +
      (this.interactive()
        ? ' cursor-pointer transition-[transform,box-shadow] duration-200 will-change-transform hover:-translate-y-1 hover:shadow-xl'
        : ''),
  );
}
