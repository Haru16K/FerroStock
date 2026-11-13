import { Movimiento } from './movimiento.model';

export interface ProductoCritico {
  id:              number;
  codigo:          string;
  nombre:          string;
  categoriaNombre: string;
  stockActual:     number;
  stockMinimo:     number;
  deficit:         number;
}

export interface ResumenMovimientos {
  desde:         string;
  hasta:         string;
  totalEntradas: number;
  totalSalidas:  number;
  movimientos:   Movimiento[];
}