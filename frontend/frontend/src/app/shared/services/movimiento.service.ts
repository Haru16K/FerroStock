import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { Movimiento, RegistrarEntradaDto, RegistrarSalidaDto } from '../models/movimiento.model';

@Injectable({ providedIn: 'root' })
export class MovimientoService {
  private http = inject(HttpClient);
  private base = `${environment.apiUrl}/movimientos`;

  getByFechas(desde: string, hasta: string) {
    const params = new HttpParams()
      .set('desde', desde)
      .set('hasta', hasta);
    return this.http.get<Movimiento[]>(this.base, { params });
  }

  getByProducto(productoId: number) {
    return this.http.get<Movimiento[]>(`${this.base}/producto/${productoId}`);
  }

  registrarEntrada(dto: RegistrarEntradaDto) {
    return this.http.post<Movimiento>(`${this.base}/entrada`, dto);
  }

  registrarSalida(dto: RegistrarSalidaDto) {
    return this.http.post<Movimiento>(`${this.base}/salida`, dto);
  }
}