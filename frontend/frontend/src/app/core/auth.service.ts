import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { tap } from 'rxjs/operators';
import { LoginDto, TokenResponse } from '../shared/models/dashboard.model';
import { environment } from '../../environments/environment';

export interface Perfil {
  usuarioId: number;
  nombre:    string;
  rol:       string;
}

@Injectable({ providedIn: 'root' })
export class AuthService {

  private readonly TOKEN_KEY  = 'ferrostock_token';
  private readonly USER_KEY   = 'ferrostock_user';

  constructor(private http: HttpClient, private router: Router) {}

  login(dto: LoginDto) {
    return this.http.post<TokenResponse>(`${environment.apiUrl}/auth/login`, dto)
      .pipe(
        tap(res => {
          localStorage.setItem(this.TOKEN_KEY, res.token);
          localStorage.setItem(this.USER_KEY, JSON.stringify({
            nombre:    res.nombre,
            rol:       res.rol,
            usuarioId: res.usuarioId
          }));
        })
      );
  }

  logout() {
    localStorage.removeItem(this.TOKEN_KEY);
    localStorage.removeItem(this.USER_KEY);
    this.router.navigate(['/login']);
  }

  getToken(): string | null {
    return localStorage.getItem(this.TOKEN_KEY);
  }

  getUser(): { nombre: string; rol: string; usuarioId: number } | null {
    const u = localStorage.getItem(this.USER_KEY);
    return u ? JSON.parse(u) : null;
  }

  isLoggedIn(): boolean {
    const token = this.getToken();
    if (!token) return false;
    // Verificar que el token no haya expirado
    try {
      const payload = JSON.parse(atob(token.split('.')[1]));
      return payload.exp * 1000 > Date.now();
    } catch {
      return false;
    }
  }

  isAdmin(): boolean {
    return this.getUser()?.rol === 'Administrador';
  }

  /** Pide el enlace de recuperación (la respuesta es la misma exista o no el correo) */
  olvidePassword(email: string) {
    return this.http.post<{ message: string }>(`${environment.apiUrl}/auth/olvide-password`, { email });
  }

  /** Cambia la contraseña con el token recibido por correo (un solo uso) */
  restablecerPassword(token: string, nuevaPassword: string) {
    return this.http.post<{ message: string }>(`${environment.apiUrl}/auth/restablecer-password`, { token, nuevaPassword });
  }

  /** true si hay sesión y su rol es uno de los que conoce el sistema */
  tieneRolValido(): boolean {
    const rol = this.getUser()?.rol;
    return !!rol && ['Administrador', 'Almacenero', 'Vendedor', 'Gerencia'].includes(rol);
  }

  /** true si el usuario actual tiene alguno de los roles indicados */
  tieneRol(...roles: string[]): boolean {
    const rol = this.getUser()?.rol;
    return !!rol && roles.includes(rol);
  }

  /** Pantalla a la que va cada rol al entrar */
  rutaInicio(): string {
    switch (this.getUser()?.rol) {
      case 'Vendedor': return '/ventas';
      case 'Administrador':
      case 'Almacenero':
      case 'Gerencia': return '/dashboard';
      default:         return '/productos';
    }
  }

  /**
   * Consulta el perfil vigente en el servidor (el rol pudo cambiar después del login)
   * y actualiza los datos guardados.
   */
  refrescarPerfil() {
    return this.http.get<Perfil>(`${environment.apiUrl}/auth/me`).pipe(
      tap(p => {
        if (!p?.rol) return;
        localStorage.setItem(this.USER_KEY, JSON.stringify({
          nombre:    p.nombre,
          rol:       p.rol,
          usuarioId: p.usuarioId
        }));
      })
    );
  }
}
