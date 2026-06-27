import { ChangeDetectionStrategy, Component } from '@angular/core';

@Component({
  selector: 'ct-coffee-grid',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="mx-auto max-w-5xl px-5 py-10">
      <h1 class="font-display text-3xl font-semibold">Browse your shelf</h1>
      <p class="mt-2 text-muted">Grid coming up next.</p>
    </div>
  `,
})
export class CoffeeGrid {}
