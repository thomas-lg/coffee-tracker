import {
  ChangeDetectionStrategy,
  Component,
  Injector,
  afterNextRender,
  computed,
  inject,
  linkedSignal,
  signal,
} from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { FormField, form } from '@angular/forms/signals';
import { firstValueFrom } from 'rxjs';
import { AdminScanSettingsApi, type OcrEngine, type ScanSettings } from '@coffee-tracker/data';
import { ToastService } from '@coffee-tracker/ui';

/** What each engine is for, in the terms someone choosing between them needs. */
interface EngineCopy {
  engine: OcrEngine;
  name: string;
  summary: string;
  goodAt: string;
  costs: string;
}

@Component({
  selector: 'ct-scan-settings',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormField],
  templateUrl: './scan-settings.html',
})
export class ScanSettingsScreen {
  private readonly api = inject(AdminScanSettingsApi);
  private readonly toast = inject(ToastService);
  private readonly injector = inject(Injector);

  private readonly settingsRes = rxResource({ stream: () => this.api.get() });

  /**
   * Reading value() on an errored resource throws, and the template reads this
   * directly, so a failed GET would take the screen down instead of reaching its own
   * "could not load" branch. Same guard as the account settings screen.
   */
  protected readonly settings = computed(() =>
    this.settingsRes.error() ? undefined : this.settingsRes.value(),
  );
  protected readonly loading = this.settingsRes.isLoading;
  protected readonly saving = signal(false);

  /**
   * The numbers come from the benchmark in this repo, scored over nine photographs of
   * real bags plus thirty rendered ones. They are here rather than in a wiki because
   * this is the screen where someone has to weigh them.
   */
  protected readonly copy: readonly EngineCopy[] = [
    {
      engine: 'RapidOcr',
      name: 'RapidOCR',
      summary: 'Neural models built for text in photographs.',
      goodAt:
        'Reads a bag held in the hand, in kitchen light, with the label at an angle. ' +
        'Scores 83% on the benchmark against Tesseract’s 74%, and finds the coffee’s ' +
        'name 72% of the time against 58%. It reads scripts and stylised logos the ' +
        'other engine does not see at all.',
      costs: 'Roughly a second per scan, and most of the container image.',
    },
    {
      engine: 'Tesseract',
      name: 'Tesseract',
      summary: 'The classic document engine.',
      goodAt:
        'Flat, evenly lit, high-contrast labels, and it is about five times faster. ' +
        'It still reads roast level and weight slightly better than RapidOCR does.',
      costs: 'Struggles with angles, glare and stylised type: it returns fragments.',
    },
    {
      engine: 'Disabled',
      name: 'Off',
      summary: 'No scanning.',
      goodAt:
        'A host where neither engine is installed, or when you would rather type the ' +
        'fields yourself. Everything else in the app is unaffected.',
      costs: 'The camera button answers “scanning is off” instead of reading the bag.',
    },
  ];

  /** The engine actually in force, as the server last reported it. */
  protected readonly current = computed(() => this.settings()?.engine);

  /**
   * The radio group's value. A linkedSignal rather than an effect because the selection
   * *is* a view of the settings until someone clicks: every answer the server gives
   * reseeds it, and the click in between is the only thing that moves it on its own.
   */
  private readonly model = linkedSignal<ScanSettings | undefined, { engine: OcrEngine | '' }>({
    source: this.settings,
    computation: (settings) => ({ engine: settings?.engine ?? '' }),
  });

  protected readonly f = form(this.model);

  /** Whether an engine can run here; a build may ship without one. */
  protected available(engine: OcrEngine): boolean {
    return this.settings()?.options.find((o) => o.engine === engine)?.available ?? false;
  }

  protected async choose(engine: OcrEngine): Promise<void> {
    if (engine === this.current() || this.saving()) {
      return;
    }

    this.saving.set(true);
    // Let the group render with the click applied before the request can answer.
    //
    // A radio is written back from the model only when the model's *value* changes, and
    // that is true of Signal Forms' own binding as much as it was of the hand-rolled
    // one. A refusal sets the model back to where it started, so if nothing has rendered
    // in between there is no change to write and the browser keeps the click: the
    // administrator is left looking at Tesseract selected on an instance still scanning
    // with RapidOCR. One frame here makes the revert a real change every time, instead
    // of only when the request happens to be slower than the scheduler.
    await new Promise<void>((resolve) =>
      afterNextRender(() => resolve(), { injector: this.injector }),
    );

    try {
      const updated = await firstValueFrom(this.api.setEngine(engine));
      this.settingsRes.set(updated satisfies ScanSettings);
      this.toast.show('Scanning engine changed.', 'success');
    } catch {
      this.toast.show('Could not change the scanning engine.', 'error');
      // The click has already moved the model and a refusal leaves the settings exactly
      // where they were, so nothing else would put it back: a linkedSignal only
      // recomputes when its source changes. Writing the engine still in force is what
      // unchecks the refused radio.
      this.model.set({ engine: this.current() ?? '' });
    } finally {
      this.saving.set(false);
    }
  }
}
