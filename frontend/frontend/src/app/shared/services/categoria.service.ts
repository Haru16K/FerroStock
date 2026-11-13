import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { Categoria } from '../models/producto.model';

@Injectable({ providedIn: 'root' })
export class CategoriaService {
  private http = inject(HttpClient);
  private base = `${environment.apiUrl}/categorias`;

  getAll() {
    return this.http.get<Categoria[]>(this.base);
  }

  create(dto: { nombre: string; descripcion: string | null }) {
    return this.http.post<Categoria>(this.base, dto);
  }

  update(id: number, dto: { nombre: string; descripcion: string | null }) {
    return this.http.put<Categoria>(`${this.base}/${id}`, dto);
  }
}
