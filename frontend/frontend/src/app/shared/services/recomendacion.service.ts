import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import {
  ListaRecomendaciones, Recomendacion, ResultadoGeneracion, RecomendacionProducto,
  Complementario, ConfiguracionIa
} from '../models/recomendacion.model';

@Injectable({ providedIn: 'root' })
export class RecomendacionService {
  private http = inject(HttpClient);
  private base = `${environment.apiUrl}/recomendaciones`;

  getAll(tipo: string, estado: string) {
    let params = new HttpParams().set('estado', estado);
    if (tipo) params = params.set('tipo', tipo);
    return this.http.get<ListaRecomendaciones>(this.base, { params });
  }

  generar()              { return this.http.post<ResultadoGeneracion>(`${this.base}/generar`, {}); }
  aplicar(id: number)    { return this.http.post<Recomendacion>(`${this.base}/${id}/aplicar`, {}); }
  descartar(id: number)  { return this.http.post<Recomendacion>(`${this.base}/${id}/descartar`, {}); }
  getProducto(id: number) { return this.http.get<RecomendacionProducto>(`${this.base}/producto/${id}`); }

  getComplementarios(productoIds: number[]) {
    let params = new HttpParams();
    for (const id of productoIds) params = params.append('productoIds', id);
    return this.http.get<Complementario[]>(`${this.base}/complementarios`, { params });
  }

  getConfiguracion() { return this.http.get<ConfiguracionIa>(`${this.base}/configuracion`); }
  guardarConfiguracion(valores: Record<string, number>) {
    return this.http.put<ConfiguracionIa>(`${this.base}/configuracion`, { valores });
  }
}
