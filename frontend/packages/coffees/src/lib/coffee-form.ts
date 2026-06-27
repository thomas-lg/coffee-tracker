import { ChangeDetectionStrategy, Component, computed, effect, inject, input, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { FormField, FormRoot, form, min, required } from '@angular/forms/signals';
import { firstValueFrom } from 'rxjs';
import { HttpErrorResponse } from '@angular/common/http';
import { Button, ToastService } from '@coffee-tracker/ui';
import { CoffeesApi, ScanApi, type CoffeeCreate } from '@coffee-tracker/data';

/** Flat, all-required editable shape (Signal Forms binds cleanly to non-optional fields). */
interface CoffeeFormModel {
  name: string;
  roaster: string;
  origin: string;
  roastLevel: string;
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
  imports: [FormField, FormRoot, RouterLink, Button],
  templateUrl: './coffee-form.html',
})
export class CoffeeForm {
  private readonly api = inject(CoffeesApi);
  private readonly scanApi = inject(ScanApi);
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
  });

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
      roastLevel: m.roastLevel,
      price: m.price,
      dateBought: m.dateBought,
      shopName: m.shopName || null,
      purchaseUrl: m.purchaseUrl || null,
    };
  }

  private async loadExisting(id: number): Promise<void> {
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
      if (c.photoPath) this.photoPreview.set('/' + c.photoPath);
    } catch {
      this.toast.show('Could not load that coffee.', 'error');
    }
  }

  protected onPhotoSelected(event: Event): void {
    const file = (event.target as HTMLInputElement).files?.[0];
    if (!file) return;
    this.photoFile.set(file);
    this.photoPreview.set(URL.createObjectURL(file));
  }

  /** Snap-to-fill: scan the bag, pre-fill fields, keep the file as the coffee photo. */
  protected async onSnap(event: Event): Promise<void> {
    const file = (event.target as HTMLInputElement).files?.[0];
    if (!file) return;
    this.photoFile.set(file);
    this.photoPreview.set(URL.createObjectURL(file));
    this.scanning.set(true);
    try {
      const { parsed } = await firstValueFrom(this.scanApi.scan(file));
      this.model.update((m) => ({
        ...m,
        name: parsed.name ?? m.name,
        roaster: parsed.roaster ?? m.roaster,
        origin: parsed.origin ?? m.origin,
        roastLevel: parsed.roastLevel ?? m.roastLevel,
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
    if (this.submitting() || this.f().invalid()) return;
    this.submitting.set(true);
    try {
      const existingId = this.coffeeId();
      const dto = this.toDto(this.model());
      let savedId: number;
      if (existingId != null) {
        await firstValueFrom(this.api.update(existingId, dto));
        savedId = existingId;
      } else {
        const created = await firstValueFrom(this.api.create(dto));
        savedId = created.id;
      }
      const file = this.photoFile();
      if (file) await firstValueFrom(this.api.uploadPhoto(savedId, file));
      this.toast.show(existingId != null ? 'Coffee updated.' : 'Coffee added.', 'success');
      await this.router.navigate(['/coffees', savedId]);
    } catch {
      this.toast.show('Could not save the coffee.', 'error');
    } finally {
      this.submitting.set(false);
    }
  }
}
