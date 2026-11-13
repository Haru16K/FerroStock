export type EstadoVenta = 'Completada' | 'Anulada';

export interface DetalleVenta {
  id:             number;
  productoId:     number;
  productoCodigo: string;
  productoNombre: string;
  cantidad:       number;
  precioUnitario: number;
  subtotal:       number;
}

export interface Venta {
  id:                   number;
  numero:               string;
  fecha:                string;
  estado:               EstadoVenta;
  vendedorId:           number;
  vendedorNombre:       string;
  clienteNombre:        string | null;
  clienteDocumento:     string | null;
  subtotal:             number;
  descuentoPorcentaje:  number;
  descuentoMonto:       number;
  total:                number;
  descuentoAplicadoPor: string | null;
  anuladoPor:           string | null;
  fechaAnulacion:       string | null;
  motivoAnulacion:      string | null;
  detalles:             DetalleVenta[];
}

// Fila del historial (liviana). El detalle completo se pide aparte con getById.
export interface VentaListado {
  id:                  number;
  numero:              string;
  fecha:               string;
  estado:              EstadoVenta;
  vendedorId:          number;
  vendedorNombre:      string;
  clienteNombre:       string | null;
  clienteDocumento:    string | null;
  subtotal:            number;
  descuentoPorcentaje: number;
  descuentoMonto:      number;
  total:               number;
  unidades:            number;
  lineas:              number;
  esDemo:              boolean;
}

export interface ResumenVentas {
  cantidad:            number;
  cantidadCompletadas: number;
  totalVendido:        number;
  totalDescuentos:     number;
  ventas:              VentaListado[];   // las más recientes, hasta 'limite'
  limite:              number;
}

export interface FiltrosVentas {
  vendedorId?: number;
  productoId?: number;
  estado?:     EstadoVenta | '';
  desde?:      string;
  hasta?:      string;
}

export interface LineaVentaDto {
  productoId: number;
  cantidad:   number;
}

export interface CrearVentaDto {
  clienteNombre:       string | null;
  clienteDocumento:    string | null;
  descuentoPorcentaje: number;
  lineas:              LineaVentaDto[];
}

export interface ConfiguracionVentas {
  descuentoMaximoPorcentaje: number;
}

export interface Vendedor {
  id:     number;
  nombre: string;
  activo: boolean;
}
