export type TipoRecomendacion = 'Compra' | 'Promocion' | 'Descontinuar';
export type EstadoRecomendacion = 'Pendiente' | 'Aplicada' | 'Descartada' | 'Vencida';

export interface Recomendacion {
  id:              number;
  tipo:            TipoRecomendacion;
  productoId:      number;
  productoCodigo:  string;
  productoNombre:  string;
  titulo:          string;
  motivos:         string[];
  prioridad:       'Alta' | 'Media' | 'Baja';
  cantidad:        number | null;
  proveedorNombre: string | null;
  montoEstimado:   number | null;
  estado:          EstadoRecomendacion;
  fechaGeneracion: string;
  fechaGestion:    string | null;
  gestionadaPor:   string | null;
}

export interface ListaRecomendaciones {
  pendientes:             number;
  pendientesCompra:       number;
  pendientesPromocion:    number;
  pendientesDescontinuar: number;
  ultimaGeneracion:       string | null;
  items:                  Recomendacion[];
}

export interface ResultadoGeneracion { nuevas: number; actualizadas: number; vencidas: number; mensaje: string; }

export interface ProveedorRecomendado {
  proveedorId:        number;
  proveedor:          string;
  precio:             number;
  precioEsReferencia: boolean;
  diasEntrega:        number;
  entregaEsReal:      boolean;
  calificacion:       number | null;
  puntajePct:         number;
  esRecomendado:      boolean;
  justificacion:      string[];
}

export interface RecomendacionProducto {
  productoId:      number;
  recomendarCompra: boolean;
  cantidad:        number;
  prioridad:       string;
  motivos:         string[];
  montoEstimado:   number | null;
  proveedores:     ProveedorRecomendado[];
  aviso:           string | null;
}

export interface Complementario {
  productoId:   number;
  codigo:       string;
  nombre:       string;
  precioVenta:  number;
  stockActual:  number;
  vecesJuntos:  number;
  confianzaPct: number;
  motivo:       string;
}

export interface LimiteConfig { defecto: number; min: number; max: number; }

export interface ConfiguracionIa {
  valores:         Record<string, number>;
  limites:         Record<string, LimiteConfig>;
  nivelesServicio: number[];
}
