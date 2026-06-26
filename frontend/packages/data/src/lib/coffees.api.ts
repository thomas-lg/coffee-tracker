import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import type { Coffee, CoffeeCreate, CoffeeUpdate } from './models';

/** Thin HTTP access to the coffee catalog endpoints. */
@Injectable({ providedIn: 'root' })
export class CoffeesApi {
  private readonly http = inject(HttpClient);
  private readonly base = '/api/coffees';

  list(): Observable<Coffee[]> {
    return this.http.get<Coffee[]>(this.base);
  }

  get(id: number): Observable<Coffee> {
    return this.http.get<Coffee>(`${this.base}/${id}`);
  }

  create(dto: CoffeeCreate): Observable<Coffee> {
    return this.http.post<Coffee>(this.base, dto);
  }

  update(id: number, dto: CoffeeUpdate): Observable<void> {
    return this.http.put<void>(`${this.base}/${id}`, dto);
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/${id}`);
  }

  /** Multipart upload; returns the coffee with its new photoPath. */
  uploadPhoto(id: number, file: File): Observable<Coffee> {
    const form = new FormData();
    form.append('file', file);
    return this.http.post<Coffee>(`${this.base}/${id}/photo`, form);
  }
}
