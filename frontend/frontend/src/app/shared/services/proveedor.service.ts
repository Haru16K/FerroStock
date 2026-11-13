import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import {
  Proveedor, CrearProveedorDto, ActualizarProveedorDto, ProveedorProducto
} from '../models/proveedor.model';

@Injectable({ providedIn: 'root' })
export class ProveedorService {
  private http = inject(HttpClient);
  private base = `${environment.apiUrl}/proveedores`;

  getAll(busqueda?: string) {
    let params = new HttpParams();
    if (busqueda) params = params.set('busqueda', busqueda);
    return this.http.get<Proveedor[]>(this.base, { params });
  }

  getById(id: number) {
    return this.http.get<Proveedor>(`${this.base}/${id}`);
  }

  getByProducto(productoId: number) {
    return this.http.get<Proveedor[]>(`${this.base}/por-producto/${productoId}`);
  }

  create(dto: CrearProveedorDto) {
    return this.http.post<Proveedor>(this.base, dto);
  }

  update(id: number, dto: ActualizarProveedorDto) {
    return this.http.put<Proveedor>(`${this.base}/${id}`, dto);
  }

  delete(id: number) {
    return this.http.delete(`${this.base}/${id}`);
  }

  getProductos(id: number) {
    return this.http.get<ProveedorProducto[]>(`${this.base}/${id}/productos`);
  }

  // Reemplaza el conjunto completo de productos asociados
  asociarProductos(id: number, productoIds: number[]) {
    return this.http.put<ProveedorProducto[]>(`${this.base}/${id}/productos`, { productoIds });
  }
}
