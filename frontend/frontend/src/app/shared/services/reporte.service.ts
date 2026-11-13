import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { ProductoCritico, ResumenMovimientos } from '../models/reporte.model';

@Injectable({ providedIn: 'root' })
export class ReporteService {
  private http = inject(HttpClient);
  private base = `${environment.apiUrl}/reportes`;

  getCriticos() {
    return this.http.get<ProductoCritico[]>(`${this.base}/criticos`);
  }

  getMovimientos(desde: string, hasta: string) {
    const params = new HttpParams()
      .set('desde', desde)
      .set('hasta', hasta);
    return this.http.get<ResumenMovimientos>(`${this.base}/movimientos`, { params });
  }
}