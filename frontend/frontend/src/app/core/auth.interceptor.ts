import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { AuthService } from './auth.service';

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth  = inject(AuthService);
  const token = auth.getToken();

  if (token) {
    req = req.clone({
      setHeaders: { Authorization: `Bearer ${token}` }
    });
  }

  return next(req).pipe(
    catchError(err => {
      // Sesión vencida o cuenta desactivada: se cierra la sesión.
      // (El login también responde 401 con credenciales malas, por eso se excluye.)
      if (err.status === 401 && token && !req.url.includes('/auth/login')) {
        auth.logout();
      }
      return throwError(() => err);
    })
  );
};
