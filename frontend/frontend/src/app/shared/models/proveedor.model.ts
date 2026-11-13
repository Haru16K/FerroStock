export interface Proveedor {
  id:                number;
  ruc:               string;
  razonSocial:       string;
  contacto:          string | null;
  telefono:          string | null;
  email:             string | null;
  direccion:         string | null;
  activo:            boolean;
  cantidadProductos: number;
  diasEntrega:       number;
  calificacion:      number | null;
}

export interface CrearProveedorDto {
  ruc:         string;
  razonSocial: string;
  contacto:    string | null;
  telefono:    string | null;
  email:       string | null;
  direccion:   string | null;
  diasEntrega: number;
  calificacion: number | null;
}

export type ActualizarProveedorDto = CrearProveedorDto;

export interface ProveedorProducto {
  productoId: number;
  codigo:     string;
  nombre:     string;
}
