export interface Movimiento {
  id:              number;
  productoId:      number;
  productoNombre:  string;
  productoCodigo:  string;
  tipo:            'Entrada' | 'Salida';
  cantidad:        number;
  fecha:           string;
  motivo:          string | null;
  proveedor:       string | null;
  precioUnitario:  number | null;
  usuarioNombre:   string;
}

export interface RegistrarEntradaDto {
  productoId:     number;
  cantidad:       number;
  proveedor:      string;
  precioUnitario: number;
}

export interface RegistrarSalidaDto {
  productoId: number;
  cantidad:   number;
  motivo:     string;
}