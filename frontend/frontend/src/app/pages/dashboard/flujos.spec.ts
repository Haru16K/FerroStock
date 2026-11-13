import { TestBed } from '@angular/core/testing';
import { LOCALE_ID } from '@angular/core';
import { registerLocaleData } from '@angular/common';
import localeEs from '@angular/common/locales/es';
registerLocaleData(localeEs);
import { provideHttpClient, withInterceptors, HttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { provideRouter, Router } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { routes } from '../../app.routes';
import { authInterceptor } from '../../core/auth.interceptor';
import { MovimientosComponent } from '../movimientos/movimientos.component';

function sesion(rol: string) {
  rolActual = rol;
  const payload = btoa(JSON.stringify({ exp: Math.floor(Date.now() / 1000) + 3600 }));
  localStorage.setItem('ferrostock_token', `x.${payload}.y`);
  localStorage.setItem('ferrostock_user', JSON.stringify({ nombre: 'Prueba', rol, usuarioId: 1 }));
}
const cfg = () => ({ providers: [{ provide: LOCALE_ID, useValue: 'es' }, provideHttpClient(withInterceptors([authInterceptor])), provideHttpClientTesting(), provideRouter(routes)] });
const dashboard = { kpis: { totalProductos: 7, stockCritico: 1, entradasHoy: 0, salidasHoy: 0, deltaEntradas: 0, deltaSalidas: 0 },
  movRecientes: [], alertas: [], categorias: [], topProductos: [], grafico: { dias: [], entradas: [], salidas: [] } };

let rolActual = 'Administrador';
function responder(http: HttpTestingController) {
  for (const r of http.match(() => true)) {
    const u = r.request.url;
    if (u.endsWith('/dashboard')) r.flush(dashboard);
    else if (u.includes('/productos')) r.flush([]);
    else if (u.includes('/movimientos')) r.flush([]);
    else if (u.includes('/analisis/ventas')) r.flush({ desde: '2026-10-09', hasta: '2026-11-07', totalVendido: 0, cantidadVentas: 0, ticketPromedio: 0, totalDescuentos: 0, unidadesVendidas: 0, porDia: [], porCategoria: [], topProductos: [] });
    else if (u.includes('/analisis/rotacion')) r.flush({ desde: '2026-10-09', hasta: '2026-11-07', diasPeriodo: 30, productos: [] });
    else if (u.includes('/analisis/comparacion-mensual')) r.flush({ mes: '2026-11', mesEnCurso: true, categoriaId: null, categoriaNombre: null,
      actual: { desde: '2026-11-01', hasta: '2026-11-07', monto: 0, unidades: 0, ventas: 0 }, anterior: { desde: '2026-10-01', hasta: '2026-10-07', monto: 0, unidades: 0, ventas: 0 },
      variacionMonto: 0, variacionUnidades: 0, variacionVentas: 0, porCategoria: [] });
    else if (u.includes('/categorias')) r.flush([]);
    else if (u.endsWith('/auth/me')) r.flush({ usuarioId: 1, nombre: 'Prueba', rol: rolActual });
    else r.flush({});
  }
}

describe('Flujos del dashboard y sesión', () => {
it('Administrador: "Nueva entrada" del dashboard abre el formulario en Movimientos', async () => {
  (globalThis as any).Chart = class { destroy() {} };
  sesion('Administrador');
  TestBed.configureTestingModule(cfg());
  const http = TestBed.inject(HttpTestingController);
  const harness = await RouterTestingHarness.create();
  await harness.navigateByUrl('/dashboard');
  responder(http);
  await harness.fixture.whenStable();
  harness.detectChanges();
  const boton = Array.from(document.querySelectorAll('button')).find(b => (b.textContent ?? '').includes('Nueva entrada')) as HTMLElement;
  boton.click();
  await harness.fixture.whenStable();
  await new Promise(r => setTimeout(r, 80));
  const router = TestBed.inject(Router);
  responder(http);
  await harness.fixture.whenStable();
  harness.detectChanges();
  const texto = document.body.textContent ?? '';
  expect(router.url.startsWith('/movimientos')).toBe(true);
});

it('Gerencia no ve el botón "Nueva entrada"', async () => {
  (globalThis as any).Chart = class { destroy() {} };
  sesion('Gerencia');
  TestBed.configureTestingModule(cfg());
  const http = TestBed.inject(HttpTestingController);
  const harness = await RouterTestingHarness.create();
  await harness.navigateByUrl('/dashboard');
  responder(http);
  await harness.fixture.whenStable();
  harness.detectChanges();
  const boton = Array.from(document.querySelectorAll('button')).find(b => (b.textContent ?? '').includes('Nueva entrada'));
  expect(boton).toBeUndefined();
});

it('Movimientos abre el modal de entrada con ?nuevo=entrada', async () => {
  sesion('Almacenero');
  TestBed.configureTestingModule(cfg());
  const http = TestBed.inject(HttpTestingController);
  const harness = await RouterTestingHarness.create();
  await harness.navigateByUrl('/movimientos?nuevo=entrada');
  responder(http);
  await harness.fixture.whenStable();
  harness.detectChanges();
  const modal = document.querySelector('.modal-title')?.textContent ?? '(sin modal)';
  expect(modal).toContain('entrada');
});

it('Una respuesta 401 (sesión cerrada por cambio de contraseña) saca al usuario al login', async () => {
  sesion('Administrador');
  TestBed.configureTestingModule(cfg());
  const http = TestBed.inject(HttpTestingController);
  const cliente = TestBed.inject(HttpClient);
  const router = TestBed.inject(Router);
  cliente.get('/api/productos').subscribe({ error: () => {} });
  http.expectOne('/api/productos').flush({}, { status: 401, statusText: 'Unauthorized' });
  await new Promise(r => setTimeout(r, 80));
  expect(localStorage.getItem('ferrostock_token')).toBeNull();
});

it('Una ruta que no existe avisa en consola y redirige al inicio', async () => {
  (globalThis as any).Chart = class { destroy() {} };
  sesion('Administrador');
  const aviso = vi.spyOn(console, 'warn').mockImplementation(() => {});
  TestBed.configureTestingModule(cfg());
  const http = TestBed.inject(HttpTestingController);
  const harness = await RouterTestingHarness.create();
  await harness.navigateByUrl('/reportes/ruta-inexistente');
  responder(http);
  expect(TestBed.inject(Router).url).toBe('/dashboard');
  expect(aviso.mock.calls.some(c => String(c[0]).includes('no existe en app.routes.ts'))).toBe(true);
  aviso.mockRestore();
});

it('Un rol sin permiso avisa en consola con el motivo', async () => {
  (globalThis as any).Chart = class { destroy() {} };
  sesion('Almacenero');   // el almacenero no puede ver Rotación
  const aviso = vi.spyOn(console, 'warn').mockImplementation(() => {});
  TestBed.configureTestingModule(cfg());
  const http = TestBed.inject(HttpTestingController);
  const harness = await RouterTestingHarness.create();
  await harness.navigateByUrl('/reportes/rotacion');
  responder(http);
  expect(TestBed.inject(Router).url).toBe('/dashboard');
  expect(aviso.mock.calls.some(c => String(c[0]).includes('Acceso denegado') && String(c[0]).includes('Almacenero'))).toBe(true);
  aviso.mockRestore();
});

it('Administrador y Gerencia SÍ entran a Rotación y Comparación (sin redirección)', async () => {
  (globalThis as any).Chart = class { destroy() {} };
  for (const rol of ['Administrador', 'Gerencia']) {
    for (const ruta of ['/reportes/rotacion', '/reportes/comparacion']) {
      TestBed.resetTestingModule();
      sesion(rol);
      TestBed.configureTestingModule(cfg());
      const http = TestBed.inject(HttpTestingController);
      const harness = await RouterTestingHarness.create();
      await harness.navigateByUrl(ruta);
      responder(http);
      expect(TestBed.inject(Router).url).toBe(ruta);
    }
  }
});
});
