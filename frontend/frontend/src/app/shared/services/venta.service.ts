import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import {
  Venta, ResumenVentas, FiltrosVentas, CrearVentaDto, ConfiguracionVentas, Vendedor
} from '../models/venta.model';

@Injectable({ providedIn: 'root' })
export class VentaService {
  private http = inject(HttpClient);
  private base = `${environment.apiUrl}/ventas`;

  getAll(f: FiltrosVentas = {}) {
    let params = new HttpParams();
    if (f.vendedorId) params = params.set('vendedorId', f.vendedorId);
    if (f.productoId) params = params.set('productoId', f.productoId);
    if (f.estado)     params = params.set('estado', f.estado);
    if (f.desde)      params = params.set('desde', f.desde);
    if (f.hasta)      params = params.set('hasta', f.hasta);
    return this.http.get<ResumenVentas>(this.base, { params });
  }

  getById(id: number) {
    return this.http.get<Venta>(`${this.base}/${id}`);
  }

  getConfiguracion() {
    return this.http.get<ConfiguracionVentas>(`${this.base}/configuracion`);
  }

  getVendedores() {
    return this.http.get<Vendedor[]>(`${this.base}/vendedores`);
  }

  registrar(dto: CrearVentaDto) {
    return this.http.post<Venta>(this.base, dto);
  }

  anular(id: number, motivo: string) {
    return this.http.post<Venta>(`${this.base}/${id}/anular`, { motivo });
  }
}
