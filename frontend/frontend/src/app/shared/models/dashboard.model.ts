export interface LoginDto {
  email:    string;
  password: string;
}

export interface TokenResponse {
  token:      string;
  nombre:     string;
  rol:        string;
  usuarioId:  number;
}

export interface DashboardKpis {
  totalProductos: number;
  stockCritico:   number;
  entradasHoy:    number;
  salidasHoy:     number;
  deltaEntradas:  number;
  deltaSalidas:   number;
}

export interface MovimientoReciente {
  producto:  string;
  categoria: string;
  tipo:      string;
  cantidad:  number;
  fecha:     string;
  usuario:   string;
}

export interface AlertaStock {
  nombre:    string;
  categoria: string;
  stock:     number;
  minimo:    number;
}

export interface CategoriaStock {
  nombre: string;
  pct:    number;
}

export interface TopProducto {
  nombre:    string;
  categoria: string;
  movs:      number;
}

export interface GraficoData {
  dias:     string[];
  entradas: number[];
  salidas:  number[];
}

export interface DashboardData {
  kpis:          DashboardKpis;
  movRecientes:  MovimientoReciente[];
  alertas:       AlertaStock[];
  categorias:    CategoriaStock[];
  topProductos:  TopProducto[];
  grafico:       GraficoData;
}