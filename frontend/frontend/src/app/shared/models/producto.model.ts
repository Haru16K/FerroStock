export interface Producto {
  id:             number;
  codigo:         string;
  nombre:         string;
  categoriaId:    number;
  categoriaNombre: string;
  precioCompra:   number;
  precioVenta:    number;
  esEstacional?:  boolean;
  stockActual:    number;
  stockMinimo:    number;
  activo:         boolean;
  esCritico:      boolean;
}

export interface CrearProductoDto {
  codigo:       string;
  nombre:       string;
  categoriaId:  number;
  precioCompra: number;
  precioVenta:  number;
  stockActual:  number;
  stockMinimo:  number;
}

export interface ActualizarProductoDto {
  nombre:       string;
  categoriaId:  number;
  precioCompra: number;
  precioVenta:  number;
  stockMinimo:  number;
}

export interface Categoria {
  id:          number;
  nombre:      string;
  descripcion: string | null;
  cantidadProductos?: number;
}