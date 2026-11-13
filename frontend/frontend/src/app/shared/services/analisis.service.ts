import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { AnalisisVentas, ReporteRotacion, ComparacionMensual } from '../models/analisis.model';

@Injectable({ providedIn: 'root' })
export class AnalisisService {
  private http = inject(HttpClient);
  private base = `${environment.apiUrl}/analisis`;

  getVentas(desde: string, hasta: string) {
    const params = new HttpParams().set('desde', desde).set('hasta', hasta);
    return this.http.get<AnalisisVentas>(`${this.base}/ventas`, { params });
  }

  getRotacion(desde: string, hasta: string, top?: number) {
    let params = new HttpParams().set('desde', desde).set('hasta', hasta);
    if (top) params = params.set('top', top);
    return this.http.get<ReporteRotacion>(`${this.base}/rotacion`, { params });
  }

  getComparacionMensual(mes: string, categoriaId?: number) {
    let params = new HttpParams().set('mes', mes);
    if (categoriaId) params = params.set('categoriaId', categoriaId);
    return this.http.get<ComparacionMensual>(`${this.base}/comparacion-mensual`, { params });
  }
}
