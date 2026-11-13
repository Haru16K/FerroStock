import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import {
  OrdenCompra, ResumenOrdenes, FiltrosOrdenes,
  CrearOrdenCompraDto, LineaRecepcionDto
} from '../models/orden-compra.model';

@Injectable({ providedIn: 'root' })
export class OrdenCompraService {
  private http = inject(HttpClient);
  private base = `${environment.apiUrl}/ordenes-compra`;

  getAll(f: FiltrosOrdenes = {}) {
    let params = new HttpParams();
    if (f.proveedorId) params = params.set('proveedorId', f.proveedorId);
    if (f.estado)      params = params.set('estado', f.estado);
    if (f.desde)       params = params.set('desde', f.desde);
    if (f.hasta)       params = params.set('hasta', f.hasta);
    return this.http.get<ResumenOrdenes>(this.base, { params });
  }

  getById(id: number) {
    return this.http.get<OrdenCompra>(`${this.base}/${id}`);
  }

  crear(dto: CrearOrdenCompraDto) {
    return this.http.post<OrdenCompra>(this.base, dto);
  }

  aprobar(id: number) {
    return this.http.post<OrdenCompra>(`${this.base}/${id}/aprobar`, {});
  }

  rechazar(id: number, motivo: string | null) {
    return this.http.post<OrdenCompra>(`${this.base}/${id}/rechazar`, { motivo });
  }

  cancelar(id: number, motivo: string) {
    return this.http.post<OrdenCompra>(`${this.base}/${id}/cancelar`, { motivo });
  }

  recibir(id: number, lineas: LineaRecepcionDto[]) {
    return this.http.post<OrdenCompra>(`${this.base}/${id}/recibir`, { lineas });
  }
}
