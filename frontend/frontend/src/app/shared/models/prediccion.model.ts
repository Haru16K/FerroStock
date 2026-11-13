export type NivelConfianza = 'Alta' | 'Media' | 'Baja';

export interface PrediccionProducto {
  productoId:       number;
  codigo:           string;
  nombre:           string;
  categoria:        string;
  stockActual:      number;
  stockMinimo:      number;
  demandaDiaria:    number;
  demanda30d:       number;
  minima30d:        number;
  maxima30d:        number;
  confianzaPct:     number;
  nivelConfianza:   NivelConfianza;
  diasHistorial:    number;
  diasHastaQuiebre: number | null;
  fechaQuiebre:     string | null;
  esEstacional:     boolean;
  mesPico:          string | null;
  explicacion:      string;
}

export interface ResumenPredicciones {
  productosEnRiesgo:      number;
  productosSinHistorial:  number;
  demandaTotal30d:        number;
  estacionalesDetectados: number;
  productos:              PrediccionProducto[];
}

export interface PuntoSemana { etiqueta: string; unidades: number; proyectado: boolean; }

export interface DetallePrediccion {
  prediccion: PrediccionProducto;
  serie:      PuntoSemana[];
}

export interface TendenciaCategoria {
  categoriaId:  number;
  categoria:    string;
  historico:    number[];
  proyeccion:   number[];
  variacionPct: number | null;
  tendencia:    'Sube' | 'Baja' | 'Estable' | 'Sin datos';
}

export interface TendenciaResultado {
  horizonte:            'semanal' | 'mensual';
  etiquetasHistoricas:  string[];
  etiquetasProyeccion:  string[];
  categorias:           TendenciaCategoria[];
}

export interface ProductoError { productoId: number; codigo: string; nombre: string; mae: number; mape: number | null; }

export interface Precision {
  mae:                number;
  mape:               number | null;
  wape:               number | null;
  observaciones:      number;
  productosEvaluados: number;
  semanasEvaluadas:   number;
  interpretacion:     string;
  peores:             ProductoError[];
}

export interface EjecucionModelo {
  id:                     number;
  inicio:                 string;
  fin:                    string | null;
  origen:                 string;
  mes:                    string;
  estado:                 'Ok' | 'Error';
  mensaje:                string | null;
  productosProcesados:    number;
  mae:                    number | null;
  mape:                   number | null;
  estacionalesDetectados: number;
}

export interface EstadoModelo {
  ultima:            EjecucionModelo | null;
  ultimaExitosa:     EjecucionModelo | null;
  permitirDatosDemo: boolean;
  hayDatosDemo:      boolean;
}
