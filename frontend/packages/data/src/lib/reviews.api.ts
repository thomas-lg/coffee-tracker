import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import type { Review, ReviewCreate, ReviewUpdate } from './models';

/** Reviews are nested under a coffee. Each POST creates a new dated entry. */
@Injectable({ providedIn: 'root' })
export class ReviewsApi {
  private readonly http = inject(HttpClient);
  private base(coffeeId: number): string {
    return `/api/coffees/${coffeeId}/reviews`;
  }

  /** A coffee's reviews, newest-first (ordering enforced server-side). */
  listForCoffee(coffeeId: number): Observable<Review[]> {
    return this.http.get<Review[]>(this.base(coffeeId));
  }

  get(coffeeId: number, id: number): Observable<Review> {
    return this.http.get<Review>(`${this.base(coffeeId)}/${id}`);
  }

  create(coffeeId: number, dto: ReviewCreate): Observable<Review> {
    return this.http.post<Review>(this.base(coffeeId), dto);
  }

  update(coffeeId: number, id: number, dto: ReviewUpdate): Observable<Review> {
    return this.http.put<Review>(`${this.base(coffeeId)}/${id}`, dto);
  }

  delete(coffeeId: number, id: number): Observable<void> {
    return this.http.delete<void>(`${this.base(coffeeId)}/${id}`);
  }
}
