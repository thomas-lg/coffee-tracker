import { ComponentFixture, TestBed } from '@angular/core/testing';

import { Coffees } from './coffees';

describe('Coffees', () => {
  let component: Coffees;
  let fixture: ComponentFixture<Coffees>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [Coffees],
    }).compileComponents();

    fixture = TestBed.createComponent(Coffees);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
