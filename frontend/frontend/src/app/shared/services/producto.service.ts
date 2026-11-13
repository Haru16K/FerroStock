import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { Producto, CrearProductoDto, ActualizarProductoDto, Categoria } from '../models/producto.model';

@Injectable({ providedIn: 'root' })
export class ProductoService {
  private http = inject(HttpClient);
  private base = `${environment.apiUrl}`;

  getAll(busqueda?: string, categoriaId?: number) {
    let params = new HttpParams();
    if (busqueda)    params = params.set('busqueda', busqueda);
    if (categoriaId) params = params.set('categoriaId', categoriaId);
    return this.http.get<Producto[]>(`${this.base}/productos`, { params });
  }

  getById(id: number) {
    return this.http.get<Producto>(`${this.base}/productos/${id}`);
  }

  getCriticos() {
    return this.http.get<Producto[]>(`${this.base}/productos/criticos`);
  }

  create(dto: CrearProductoDto) {
    return this.http.post<Producto>(`${this.base}/productos`, dto);
  }

  update(id: number, dto: ActualizarProductoDto) {
    return this.http.put<Producto>(`${this.base}/productos/${id}`, dto);
  }

  delete(id: number) {
    return this.http.delete(`${this.base}/productos/${id}`);
  }

  getCategorias() {
    return this.http.get<Categoria[]>(`${this.base}/categorias`);
  }
}