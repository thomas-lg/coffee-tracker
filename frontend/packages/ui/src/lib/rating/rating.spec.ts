import { describe, expect, it } from 'vitest';
import { TestBed } from '@angular/core/testing';
import { provideZonelessChangeDetection } from '@angular/core';
import { Rating } from './rating';

describe('Rating', () => {
  function create() {
    TestBed.configureTestingModule({ providers: [provideZonelessChangeDetection()] });
    return TestBed.createComponent(Rating);
  }

  it('fills three stars out of five for a rating of 3', () => {
    const fixture = create();
    fixture.componentRef.setInput('value', 3);
    fixture.detectChanges();

    // The rendered width is the user-visible fact; fillPct() is how it is computed.
    const fill = (fixture.nativeElement as HTMLElement).querySelector<HTMLElement>(
      '[style*="width"]',
    );
    expect(fill?.style.width).toBe('60%');
  });

  it('emits the picked star in interactive mode', () => {
    const fixture = create();
    fixture.componentRef.setInput('interactive', true);
    fixture.detectChanges();

    let picked = 0;
    fixture.componentInstance.rated.subscribe((v) => (picked = v));
    const stars = (fixture.nativeElement as HTMLElement).querySelectorAll('button');
    expect(stars.length).toBe(5);
    (stars[3] as HTMLButtonElement).click();
    expect(picked).toBe(4);
  });
});
