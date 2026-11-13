import { Routes } from '@angular/router';
import { authGuard } from './core/auth.guard';
import { roleGuard, inicioGuard, rutaNoEncontradaGuard } from './core/role.guard';

// Roles con acceso a cada módulo (HU-17). El backend aplica las mismas reglas.
const TODOS     = ['Administrador', 'Almacenero', 'Vendedor', 'Gerencia'];
const ALMACEN   = ['Administrador', 'Almacenero'];
const VENTAS    = ['Administrador', 'Vendedor', 'Gerencia'];
const ANALISIS  = ['Administrador', 'Almacenero', 'Gerencia'];

export const routes: Routes = [
  {
    path: 'login',
    loadComponent: () =>
      import('./pages/login/login.component').then(m => m.LoginComponent)
  },
  {
    path: 'recuperar',
    loadComponent: () =>
      import('./pages/recuperar/recuperar.component').then(m => m.RecuperarComponent)
  },
  {
    path: 'restablecer',
    loadComponent: () =>
      import('./pages/restablecer/restablecer.component').then(m => m.RestablecerComponent)
  },
  {
    path: '',
    loadComponent: () =>
      import('./layout/shell.component').then(m => m.ShellComponent),
    canActivate: [authGuard],
    children: [
      {
        path: 'dashboard',
        loadComponent: () =>
          import('./pages/dashboard/dashboard.component').then(m => m.DashboardComponent),
        canActivate: [roleGuard], data: { roles: ANALISIS }
      },
      {
        path: 'productos',
        loadComponent: () =>
          import('./pages/productos/productos.component').then(m => m.ProductosComponent),
        canActivate: [roleGuard], data: { roles: TODOS }
      },
      {
        path: 'movimientos',
        loadComponent: () =>
          import('./pages/movimientos/movimientos.component').then(m => m.MovimientosComponent),
        canActivate: [roleGuard], data: { roles: ALMACEN }
      },
      {
        path: 'ventas',
        loadComponent: () =>
          import('./pages/ventas/ventas.component').then(m => m.VentasComponent),
        canActivate: [roleGuard], data: { roles: VENTAS }
      },
      {
        path: 'categorias',
        loadComponent: () =>
          import('./pages/categorias/categorias.component').then(m => m.CategoriasComponent),
        canActivate: [roleGuard], data: { roles: ['Administrador'] }
      },
      {
        path: 'usuarios',
        loadComponent: () =>
          import('./pages/usuarios/usuarios.component').then(m => m.UsuariosComponent),
        canActivate: [roleGuard], data: { roles: ['Administrador'] }
      },
      {
        path: 'proveedores',
        loadComponent: () =>
          import('./pages/proveedores/proveedores.component').then(m => m.ProveedoresComponent),
        canActivate: [roleGuard], data: { roles: ALMACEN }
      },
      {
        path: 'compras',
        loadComponent: () =>
          import('./pages/compras/compras.component').then(m => m.ComprasComponent),
        canActivate: [roleGuard], data: { roles: ALMACEN }
      },
      {
        path: 'alertas',
        loadComponent: () =>
          import('./pages/alertas/alertas.component').then(m => m.AlertasComponent),
        canActivate: [roleGuard], data: { roles: ALMACEN }
      },
      {
        path: 'recomendaciones',
        loadComponent: () =>
          import('./pages/recomendaciones/recomendaciones.component').then(m => m.RecomendacionesComponent),
        canActivate: [roleGuard], data: { roles: ['Administrador', 'Gerencia'] }
      },
      {
        path: 'predicciones',
        loadComponent: () =>
          import('./pages/predicciones/predicciones.component').then(m => m.PrediccionesComponent),
        canActivate: [roleGuard], data: { roles: ['Administrador', 'Gerencia', 'Almacenero'] }
      },
      {
        path: 'reportes/stock',
        loadComponent: () =>
          import('./pages/reportes/reportes-stock.component').then(m => m.ReportesStockComponent),
        canActivate: [roleGuard], data: { roles: ANALISIS }
      },
      {
        path: 'reportes/rotacion',
        loadComponent: () =>
          import('./pages/reportes/reportes-rotacion.component').then(m => m.ReportesRotacionComponent),
        canActivate: [roleGuard], data: { roles: ['Administrador', 'Gerencia'] }
      },
      {
        path: 'reportes/comparacion',
        loadComponent: () =>
          import('./pages/reportes/reportes-comparacion.component').then(m => m.ReportesComparacionComponent),
        canActivate: [roleGuard], data: { roles: ['Administrador', 'Gerencia'] }
      },
      {
        path: 'reportes/historial',
        loadComponent: () =>
          import('./pages/reportes/reportes-historial.component').then(m => m.ReportesHistorialComponent),
        canActivate: [roleGuard], data: { roles: ANALISIS }
      },
      // Cada rol entra a su pantalla de inicio (Vendedor -> ventas; resto -> dashboard)
      { path: '', pathMatch: 'full', canActivate: [inicioGuard], children: [] }
    ]
  },
  { path: '**', canActivate: [rutaNoEncontradaGuard], children: [] }
];
