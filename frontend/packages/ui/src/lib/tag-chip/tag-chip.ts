import { ChangeDetectionStrategy, Component } from '@angular/core';

/** A flavor-tag pill in the coffee theme. */
@Component({
  selector: 'ct-tag-chip',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<span
    class="inline-block rounded-full border border-line bg-crema/15 px-2.5 py-1 text-xs font-semibold text-cocoa"
    ><ng-content
  /></span>`,
})
export class TagChip {}
