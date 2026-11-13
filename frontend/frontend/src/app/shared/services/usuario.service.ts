import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { Usuario, CrearUsuarioDto } from '../models/usuario.model';

@Injectable({ providedIn: 'root' })
export class UsuarioService {
  private http = inject(HttpClient);
  private base = `${environment.apiUrl}/usuarios`;

  getAll() {
    return this.http.get<Usuario[]>(this.base);
  }

  create(dto: CrearUsuarioDto) {
    return this.http.post<Usuario>(this.base, dto);
  }

  cambiarRol(id: number, rol: string) {
    return this.http.put<Usuario>(`${this.base}/${id}/rol`, { rol });
  }

  desactivar(id: number) {
    return this.http.delete(`${this.base}/${id}`);
  }
}