import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from './auth.service';

/**
 * Restringe una ruta a ciertos roles. Se configura en la ruta:
 *   { path: 'ventas', canActivate: [roleGuard], data: { roles: ['Administrador', 'Vendedor'] } }
 * Si el rol no está permitido, redirige a la pantalla de inicio de ese rol.
 */
export const roleGuard: CanActivateFn = (route, state) => {
  const auth   = inject(AuthService);
  const router = inject(Router);
  const roles  = (route.data?.['roles'] as string[] | undefined) ?? [];

  // Sesión sin un rol reconocido: se cierra, en vez de redirigir en círculo entre pantallas protegidas
  if (!auth.tieneRolValido()) {
    auth.logout();
    return false;
  }

  if (auth.tieneRol(...roles)) return true;

  // Se avisa en consola: una redirección silenciosa es muy difícil de diagnosticar
  console.warn(`[roleGuard] Acceso denegado a ${state.url}. Rol: ${auth.getUser()?.rol}. Roles permitidos: ${roles.join(', ') || '(ninguno definido en la ruta)'}.`);
  return router.createUrlTree([auth.rutaInicio()]);
};

/** Ruta que no existe: avisa en consola y manda a la pantalla de inicio. */
export const rutaNoEncontradaGuard: CanActivateFn = (_route, state) => {
  console.warn(`[router] La ruta ${state.url} no existe en app.routes.ts. Se redirige al inicio.`);
  return inject(Router).createUrlTree(['/']);
};

/** Ruta raíz: envía a cada rol a su pantalla de inicio. */
export const inicioGuard: CanActivateFn = () => {
  const auth   = inject(AuthService);
  const router = inject(Router);

  if (!auth.tieneRolValido()) {
    auth.logout();
    return false;
  }
  return router.createUrlTree([auth.rutaInicio()]);
};
