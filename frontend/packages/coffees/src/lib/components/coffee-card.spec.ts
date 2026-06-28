import { describe, expect, it } from 'vitest';
import { TestBed } from '@angular/core/testing';
import { provideZonelessChangeDetection } from '@angular/core';
import { provideRouter } from '@angular/router';
import type { Coffee } from '@coffee-tracker/data';
import { CoffeeCard } from './coffee-card';

const SAMPLE: Coffee = {
  id: 7,
  name: 'Geisha',
  roaster: 'Onyx',
  origin: 'Panama',
  roastLevel: 'Dark',
  price: 30,
  dateBought: '2026-06-01',
  photoPath: null,
  shopName: null,
  purchaseUrl: null,
  createdAt: '2026-06-01T00:00:00Z',
  averageRating: 4.5,
  reviewCount: 2,
  flavorTags: ['Citrus', 'Fruity', 'Nutty', 'Berry'],
};

describe('CoffeeCard', () => {
  function create(coffee: Coffee) {
    TestBed.configureTestingModule({
      providers: [provideZonelessChangeDetection(), provideRouter([])],
    });
    const fixture = TestBed.createComponent(CoffeeCard);
    fixture.componentRef.setInput('coffee', coffee);
    fixture.detectChanges();
    return fixture;
  }

  it('renders the coffee name, roaster, origin and roast tag', () => {
    const el = create(SAMPLE).nativeElement as HTMLElement;
    expect(el.querySelector('h3')?.textContent?.trim()).toBe('Geisha');
    expect(el.textContent).toContain('Onyx');
    expect(el.textContent).toContain('Panama');
    expect(el.textContent).toContain('Dark'); // roast bucket tag
  });

  it('shows at most three flavour chips', () => {
    const el = create(SAMPLE).nativeElement as HTMLElement;
    expect(el.querySelectorAll('ct-tag-chip').length).toBe(3);
  });

  it('links to the coffee detail route', () => {
    const el = create(SAMPLE).nativeElement as HTMLElement;
    expect(el.querySelector('a')?.getAttribute('href')).toBe('/coffees/7');
  });

  it('omits the flavour row when there are no tags', () => {
    const el = create({ ...SAMPLE, flavorTags: [] }).nativeElement as HTMLElement;
    expect(el.querySelectorAll('ct-tag-chip').length).toBe(0);
  });
});
