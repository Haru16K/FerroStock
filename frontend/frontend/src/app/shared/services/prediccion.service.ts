import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import {
  ResumenPredicciones, DetallePrediccion, TendenciaResultado, Precision, EjecucionModelo, EstadoModelo
} from '../models/prediccion.model';

@Injectable({ providedIn: 'root' })
export class PrediccionService {
  private http = inject(HttpClient);
  private base = `${environment.apiUrl}/predicciones`;

  getEstado()        { return this.http.get<EstadoModelo>(`${this.base}/estado`); }
  getProductos()     { return this.http.get<ResumenPredicciones>(`${this.base}/productos`); }
  getProducto(id: number) { return this.http.get<DetallePrediccion>(`${this.base}/productos/${id}`); }

  getCategorias(horizonte: 'semanal' | 'mensual') {
    return this.http.get<TendenciaResultado>(`${this.base}/categorias`, { params: new HttpParams().set('horizonte', horizonte) });
  }

  getPrecision()     { return this.http.get<Precision>(`${this.base}/precision`); }
  getEjecuciones()   { return this.http.get<EjecucionModelo[]>(`${this.base}/ejecuciones`); }

  recalcular(simularFallo = false) {
    return this.http.post<EjecucionModelo>(`${this.base}/recalcular`, {},
      { params: new HttpParams().set('simularFallo', simularFallo) });
  }

  generarDemo()  { return this.http.post<{ message: string; ventas: number }>(`${this.base}/demo`, {}); }
  eliminarDemo() { return this.http.delete<{ message: string; ventas: number }>(`${this.base}/demo`); }
}
