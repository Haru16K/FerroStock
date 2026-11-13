export interface Usuario {
  id:     number;
  nombre: string;
  email:  string;
  rol:    'Administrador' | 'Almacenero' | 'Vendedor' | 'Gerencia';
  activo: boolean;
}

export interface CrearUsuarioDto {
  nombre:   string;
  email:    string;
  password: string;
  rol:      string;
}