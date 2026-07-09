import {
  ChangeDetectionStrategy,
  Component,
  type OnDestroy,
  computed,
  effect,
  inject,
  input,
  signal,
} from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { FormField, FormRoot, form, min, required, validate } from '@angular/forms/signals';
import { firstValueFrom } from 'rxjs';
import { HttpErrorResponse } from '@angular/common/http';
import { Button, Icon, Skeleton, ToastService } from '@coffee-tracker/ui';
import { CoffeesApi, ScanApi, ROAST_LEVELS, type CoffeeCreate, type RoastLevel } from '@coffee-tracker/data';
import { CoffeesStore } from '../services/coffees.store';
import { roastBucket } from '../utils/coffee-visual';
import { COFFEE_ORIGINS } from '../utils/coffee-origins';

/** Flat, all-required editable shape (Signal Forms binds cleanly to non-optional fields). */
interface CoffeeFormModel {
  name: string;
  roaster: string;
  origin: string;
  roastLevel: RoastLevel | '';
  price: number;
  dateBought: string;
  shopName: string;
  purchaseUrl: string;
}

function today(): string {
  return new Date().toISOString().slice(0, 10);
}

@Component({
  selector: 'ct-coffee-form',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormField, FormRoot, RouterLink, Button, Icon, Skeleton],
  templateUrl: './coffee-form.html',
})
export class CoffeeForm implements OnDestroy {
  private readonly api = inject(CoffeesApi);
  private readonly scanApi = inject(ScanApi);
  protected readonly store = inject(CoffeesStore);
  private readonly router = inject(Router);
  private readonly toast = inject(ToastService);

  /** Route param on /coffees/:id/edit (absent when adding). */
  readonly id = input<string>();
  protected readonly coffeeId = computed(() => (this.id() ? Number(this.id()) : null));
  protected readonly editing = computed(() => this.coffeeId() != null);

  protected readonly model = signal<CoffeeFormModel>({
    name: '',
    roaster: '',
    origin: '',
    roastLevel: '',
    price: 0,
    dateBought: today(),
    shopName: '',
    purchaseUrl: '',
  });
  protected readonly f = form(this.model, (p) => {
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
    [...new Set([...COFFEE_ORIGINS, ...this.store.origins()])].sort(),
  );

  protected readonly roastLevels = ROAST_LEVELS;
  protected readonly today = today;
  protected readonly loading = signal(false);
  protected readonly submitting = signal(false);
  protected readonly scanning = signal(false);
  private readonly photoFile = signal<File | null>(null);
  protected readonly photoPreview = signal<string | null>(null);

  constructor() {
    effect(() => {
      const id = this.coffeeId();
      if (id != null) void this.loadExisting(id);
    });
  }

  private toDto(m: CoffeeFormModel): CoffeeCreate {
    return {
      name: m.name,
      roaster: m.roaster,
      origin: m.origin,
      // required() guarantees a non-empty selection before submit.
      roastLevel: m.roastLevel as RoastLevel,
      price: m.price,
      dateBought: m.dateBought,
      shopName: m.shopName || null,
      purchaseUrl: m.purchaseUrl || null,
    };
  }

  private async loadExisting(id: number): Promise<void> {
    this.loading.set(true);
    try {
      const c = await firstValueFrom(this.api.get(id));
      this.model.set({
        name: c.name,
        roaster: c.roaster,
        origin: c.origin,
        roastLevel: c.roastLevel,
        price: c.price,
        dateBought: c.dateBought,
        shopName: c.shopName ?? '',
        purchaseUrl: c.purchaseUrl ?? '',
      });
      if (c.photoUrl) this.photoPreview.set(c.photoUrl);
    } catch {
      this.toast.show('Could not load that coffee.', 'error');
    } finally {
      this.loading.set(false);
    }
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

  ngOnDestroy(): void {
    const p = this.photoPreview();
    if (p?.startsWith('blob:')) URL.revokeObjectURL(p);
  }

  /** Snap-to-fill: scan the bag, pre-fill fields, keep the file as the coffee photo. */
  protected async onSnap(event: Event): Promise<void> {
    const file = (event.target as HTMLInputElement).files?.[0];
    if (!file) return;
    this.setPhoto(file);
    this.scanning.set(true);
    try {
      const { parsed } = await firstValueFrom(this.scanApi.scan(file));
      this.model.update((m) => ({
        ...m,
        name: parsed.name ?? m.name,
        roaster: parsed.roaster ?? m.roaster,
        origin: parsed.origin ?? m.origin,
        // OCR returns free text (e.g. "medium-dark"); map it onto the enum.
        roastLevel: parsed.roastLevel ? roastBucket(parsed.roastLevel) : m.roastLevel,
      }));
      this.toast.show('Bag scanned — fields pre-filled. Check them before saving.', 'success');
    } catch (err) {
      if (err instanceof HttpErrorResponse && err.status === 503) {
        this.toast.show('OCR is off on this host — fill the form in manually.', 'info');
      } else {
        this.toast.show('Could not read that photo.', 'error');
      }
    } finally {
      this.scanning.set(false);
    }
  }

  protected async onSubmit(): Promise<void> {
    if (this.submitting()) return;
    if (this.f().invalid()) {
      // Surface why nothing happened: reveal every field's validation message.
      this.f().markAsTouched();
      return;
    }
    this.submitting.set(true);
    try {
      const existingId = this.coffeeId();
      const dto = this.toDto(this.model());

      // Step 1 — save the coffee itself. Only THIS failure should keep the user on
      // the form; retrying after step 1 succeeded would create a duplicate.
      let savedId: number;
      try {
        if (existingId != null) {
          await firstValueFrom(this.api.update(existingId, dto));
          savedId = existingId;
        } else {
          const created = await firstValueFrom(this.api.create(dto));
          savedId = created.id;
        }
      } catch {
        this.toast.show('Could not save the coffee.', 'error');
        return;
      }

      // Step 2 — best-effort photo upload. The coffee is already saved, so a failure
      // here must not invite a duplicate resubmit: report it and move on.
      let photoFailed = false;
      const file = this.photoFile();
      if (file) {
        try {
          await firstValueFrom(this.api.uploadPhoto(savedId, file));
        } catch {
          photoFailed = true;
        }
      }

      this.store.reload(); // keep the grid in sync with the new/edited coffee
      if (photoFailed) {
        this.toast.show('Coffee saved, but the photo failed to upload. You can retry it from Edit.', 'error');
      } else {
        this.toast.show(existingId != null ? 'Coffee updated.' : 'Coffee added.', 'success');
      }
      await this.router.navigate(['/coffees', savedId]);
    } finally {
      this.submitting.set(false);
    }
  }
}
