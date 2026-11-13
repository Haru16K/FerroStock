export interface PuntoDia        { fecha: string; monto: number; ventas: number; }
export interface VentaCategoria  { categoriaId: number; categoria: string; monto: number; unidades: number; }
export interface ProductoVendido { productoId: number; codigo: string; nombre: string; unidades: number; monto: number; }

export interface AnalisisVentas {
  desde:            string;
  hasta:            string;
  totalVendido:     number;
  cantidadVentas:   number;
  ticketPromedio:   number;
  totalDescuentos:  number;
  unidadesVendidas: number;
  porDia:           PuntoDia[];
  porCategoria:     VentaCategoria[];
  topProductos:     ProductoVendido[];
}

export interface RotacionProducto {
  productoId:       number;
  codigo:           string;
  nombre:           string;
  categoria:        string;
  unidadesVendidas: number;
  stockActual:      number;
  stockPromedio:    number;
  rotacion:         number;
  diasCobertura:    number | null;
}

export interface ReporteRotacion {
  desde:       string;
  hasta:       string;
  diasPeriodo: number;
  productos:   RotacionProducto[];
}

export interface PeriodoVentas { desde: string; hasta: string; monto: number; unidades: number; ventas: number; }

export interface ComparacionCategoria {
  categoriaId:    number;
  categoria:      string;
  montoActual:    number;
  montoAnterior:  number;
  variacionMonto: number | null;
}

export interface ComparacionMensual {
  mes:               string;
  mesEnCurso:        boolean;
  categoriaId:       number | null;
  categoriaNombre:   string | null;
  actual:            PeriodoVentas;
  anterior:          PeriodoVentas;
  variacionMonto:    number | null;
  variacionUnidades: number | null;
  variacionVentas:   number | null;
  porCategoria:      ComparacionCategoria[];
}
