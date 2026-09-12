import {
  ChangeDetectionStrategy,
  Component,
  type OnDestroy,
  computed,
  inject,
  input,
  signal,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { FormField, FormRoot, form, min, required, validate } from '@angular/forms/signals';
import { Button, Icon, ImageLightbox, Skeleton } from '@coffee-tracker/ui';
import { ROAST_LEVELS } from '@coffee-tracker/data';
import { CoffeeFormStore, today } from '../services/coffee-form.store';
import { CoffeesStore } from '../services/coffees.store';
import { COFFEE_ORIGINS } from '../utils/coffee-origins';

/**
 * Add/edit screen. The form's value and the load/scan/save commands live in
 * CoffeeFormStore; what stays here needs a view: the validation rules the template
 * binds to, the object-URL lifecycle behind the photo preview, and unwrapping the
 * file inputs.
 */
@Component({
  selector: 'ct-coffee-form',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormField, FormRoot, RouterLink, Button, Icon, Skeleton, ImageLightbox],
  providers: [CoffeeFormStore],
  templateUrl: './coffee-form.html',
})
export class CoffeeForm implements OnDestroy {
  protected readonly store = inject(CoffeeFormStore);
  protected readonly catalog = inject(CoffeesStore);

  /** Route param on /coffees/:id/edit (absent when adding). */
  readonly id = input<string>();
  protected readonly coffeeId = computed(() => (this.id() ? Number(this.id()) : null));
  protected readonly editing = computed(() => this.coffeeId() != null);

  protected readonly f = form(this.store.model, (p) => {
    required(p.name);
    required(p.roaster);
    required(p.origin);
    required(p.roastLevel);
    min(p.price, 0);
    required(p.dateBought);
    // ISO date strings compare lexically — block anything after today.
    validate(p.dateBought, ({ value }) => (value() && value() > today() ? { kind: 'future' } : undefined));
  });

  /** Origin suggestions: curated producing countries merged with what's on the shelf. */
  protected readonly originSuggestions = computed(() =>
    [...new Set([...COFFEE_ORIGINS, ...this.catalog.origins()])].sort(),
  );

  protected readonly roastLevels = ROAST_LEVELS;
  protected readonly today = today;
  private readonly photoFile = signal<File | null>(null);
  protected readonly photoPreview = signal<string | null>(null);

  constructor() {
    this.store.load(this.coffeeId);
  }

  /** Set the chosen file + preview, revoking any previous blob URL to avoid leaks. */
  private setPhoto(file: File): void {
    const prev = this.photoPreview();
    if (prev?.startsWith('blob:')) URL.revokeObjectURL(prev);
    this.photoFile.set(file);
    this.photoPreview.set(URL.createObjectURL(file));
  }

  protected onPhotoSelected(event: Event): void {
    const file = (event.target as HTMLInputElement).files?.[0];
    if (file) this.setPhoto(file);
  }

  /** Snap-to-fill: keep the file as the coffee photo, and let the store read the bag. */
  protected onSnap(event: Event): void {
    const file = (event.target as HTMLInputElement).files?.[0];
    if (!file) return;
    this.setPhoto(file);
    this.store.scan(file);
  }

  protected onSubmit(): void {
    if (this.store.submitting()) return;
    if (this.f().invalid()) {
      // Surface why nothing happened: reveal every field's validation message.
      this.f().markAsTouched();
      return;
    }
    this.store.save({ id: this.coffeeId(), file: this.photoFile() });
  }

  ngOnDestroy(): void {
    const p = this.photoPreview();
    if (p?.startsWith('blob:')) URL.revokeObjectURL(p);
  }
}
