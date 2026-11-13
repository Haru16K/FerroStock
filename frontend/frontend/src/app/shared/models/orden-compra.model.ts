export type EstadoOrdenCompra =
  'Pendiente' | 'Aprobada' | 'Rechazada' | 'Recibida' | 'Cancelada';

export interface DetalleOrdenCompra {
  id:               number;
  productoId:       number;
  productoCodigo:   string;
  productoNombre:   string;
  cantidad:         number;
  precioUnitario:   number;
  subtotal:         number;
  cantidadRecibida: number;
}

export interface OrdenCompra {
  id:                   number;
  numero:               string;
  proveedorId:          number;
  proveedorRazonSocial: string;
  estado:               EstadoOrdenCompra;
  fechaCreacion:        string;
  total:                number;
  observaciones:        string | null;
  creadoPor:            string;
  revisadoPor:          string | null;
  fechaRevision:        string | null;
  motivoRechazo:        string | null;
  recibidoPor:          string | null;
  fechaRecepcion:       string | null;
  tieneDiferencia:      boolean;
  canceladoPor:         string | null;
  fechaCancelacion:     string | null;
  motivoCancelacion:    string | null;
  detalles:             DetalleOrdenCompra[];
}

export interface ResumenOrdenes {
  cantidad:           number;
  montoTotalComprado: number;
  ordenes:            OrdenCompra[];
}

export interface FiltrosOrdenes {
  proveedorId?: number;
  estado?:      EstadoOrdenCompra | '';
  desde?:       string;
  hasta?:       string;
}

export interface LineaOrdenCompraDto {
  productoId:     number;
  cantidad:       number;
  precioUnitario: number;
}

export interface CrearOrdenCompraDto {
  proveedorId:   number;
  observaciones: string | null;
  lineas:        LineaOrdenCompraDto[];
}

export interface LineaRecepcionDto {
  detalleId:        number;
  cantidadRecibida: number;
}
