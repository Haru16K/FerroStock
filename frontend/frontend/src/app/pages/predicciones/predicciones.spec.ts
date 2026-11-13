import { TestBed } from '@angular/core/testing';
import { LOCALE_ID } from '@angular/core';
import { registerLocaleData } from '@angular/common';
import localeEs from '@angular/common/locales/es';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { provideRouter, Router } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { routes } from '../../app.routes';
registerLocaleData(localeEs);

let rolActual = 'Administrador';
function sesion(rol: string) {
  rolActual = rol;
  const payload = btoa(JSON.stringify({ exp: Math.floor(Date.now() / 1000) + 3600 }));
  localStorage.setItem('ferrostock_token', `x.${payload}.y`);
  localStorage.setItem('ferrostock_user', JSON.stringify({ nombre: 'Prueba', rol, usuarioId: 1 }));
}
const config = () => ({ providers: [{ provide: LOCALE_ID, useValue: 'es' }, provideHttpClient(), provideHttpClientTesting(), provideRouter(routes)] });

const prod = (id: number, nombre: string, extra: any = {}) => ({
  productoId: id, codigo: 'P-' + id, nombre, categoria: 'Herramientas', stockActual: 20, stockMinimo: 5,
  demandaDiaria: 2, demanda30d: 60, minima30d: 50, maxima30d: 70, confianzaPct: 85, nivelConfianza: 'Alta',
  diasHistorial: 200, diasHastaQuiebre: 10, fechaQuiebre: '2026-11-18', esEstacional: false, mesPico: null,
  explicacion: 'Promedio ponderado de ventas...', ...extra });

const ejecucion = (estado: string, mensaje: string | null = null) => ({
  id: 1, inicio: '2026-11-01T10:00:00', fin: '2026-11-01T10:00:02', origen: 'Automática', mes: '2026-11', estado, mensaje,
  productosProcesados: 7, mae: 1.2, mape: 9.5, estacionalesDetectados: 1 });

function responder(http: HttpTestingController, estado: any) {
  for (const r of http.match(() => true)) {
    const u = r.request.url;
    if (u.endsWith('/predicciones/estado')) r.flush(estado);
    else if (u.endsWith('/predicciones/productos')) r.flush({ productosEnRiesgo: 1, productosSinHistorial: 1, demandaTotal30d: 120, estacionalesDetectados: 1,
      productos: [ prod(1, 'Martillo', { diasHastaQuiebre: 3, fechaQuiebre: '2026-11-11', stockActual: 6 }), prod(2, 'Taladro', { esEstacional: true, mesPico: 'enero' }),
                   prod(3, 'Sin ventas', { diasHistorial: 0, diasHastaQuiebre: null, fechaQuiebre: null, confianzaPct: 0, nivelConfianza: 'Baja', demanda30d: 0, demandaDiaria: 0, minima30d: 0, maxima30d: 0 }) ] });
    else if (/\/predicciones\/productos\/\d+$/.test(u)) r.flush({ prediccion: prod(1, 'Martillo', { diasHastaQuiebre: 3, fechaQuiebre: '2026-11-11' }), serie: Array.from({ length: 16 }, (_, i) => ({ etiqueta: 'S' + i, unidades: 10, proyectado: i >= 12 })) });
    else if (u.includes('/predicciones/categorias')) r.flush({ horizonte: 'semanal', etiquetasHistoricas: ['a', 'b'], etiquetasProyeccion: ['c'], categorias: [{ categoriaId: 1, categoria: 'Pinturas', historico: [10, 20], proyeccion: [30], variacionPct: 50, tendencia: 'Sube' }] });
    else if (u.endsWith('/predicciones/precision')) r.flush({ mae: 1.8, mape: 9.6, wape: 9.4, observaciones: 20, productosEvaluados: 5, semanasEvaluadas: 4, interpretacion: 'El modelo se desvía en promedio 1.8 unidades', peores: [] });
    else if (u.endsWith('/predicciones/ejecuciones')) r.flush([ejecucion('Ok')]);
    else if (u.endsWith('/auth/me')) r.flush({ usuarioId: 1, nombre: 'Prueba', rol: rolActual });
    else r.flush({});
  }
}
const estadoOk = { ultima: ejecucion('Ok'), ultimaExitosa: ejecucion('Ok'), permitirDatosDemo: true, hayDatosDemo: false };

async function abrir(rol: string, estado: any) {
  (globalThis as any).Chart = class { destroy() {} };
  sesion(rol);
  TestBed.configureTestingModule(config());
  const http = TestBed.inject(HttpTestingController);
  const harness = await RouterTestingHarness.create();
  await harness.navigateByUrl('/predicciones');
  responder(http, estado);
  await harness.fixture.whenStable();
  harness.detectChanges();
  return { http, harness, texto: () => document.body.textContent ?? '' };
}

describe('Pantalla de Predicciones', () => {
  it('Administrador: muestra productos ordenados, estacional, riesgo y botones', async () => {
    const { texto } = await abrir('Administrador', estadoOk);
    expect(texto()).toContain('Martillo');
    expect(texto()).toContain('en 3 días');
    expect(texto()).toContain('Estacional');
    expect(texto()).toContain('Sin ventas');
    expect(texto()).toContain('Actualizar modelo');
    expect(texto()).toContain('Cargar datos de demostración');
    expect(texto()).toContain('Precisión del modelo');
  });

  it('Almacenero: ve productos pero no categorías, precisión ni botones de administración', async () => {
    const { texto } = await abrir('Almacenero', { ...estadoOk, permitirDatosDemo: true });
    expect(texto()).toContain('Martillo');
    expect(texto()).not.toContain('Por categoría');
    expect(texto()).not.toContain('Precisión del modelo');
    expect(texto()).not.toContain('Actualizar modelo');
  });

  it('Vendedor no puede entrar (se redirige a Ventas)', async () => {
    sesion('Vendedor');
    TestBed.configureTestingModule(config());
    const http = TestBed.inject(HttpTestingController);
    const harness = await RouterTestingHarness.create();
    await harness.navigateByUrl('/predicciones');
    http.match(() => true).forEach(r => r.flush(
      r.request.url.endsWith('/auth/me') ? { usuarioId: 1, nombre: 'Prueba', rol: 'Vendedor' }
      : r.request.url.includes('/ventas') ? { cantidad: 0, cantidadCompletadas: 0, totalVendido: 0, totalDescuentos: 0, ventas: [] }
      : []));
    expect(TestBed.inject(Router).url).toBe('/ventas');
  });

  it('Muestra el aviso cuando la actualización del modelo falló (HU-29)', async () => {
    const { texto } = await abrir('Administrador', { ...estadoOk, ultima: ejecucion('Error', 'Fallo simulado') });
    expect(texto()).toContain('La actualización del modelo falló');
    expect(texto()).toContain('Fallo simulado');
  });

  it('Abre la ficha de un producto y la cierra', async () => {
    const { http, harness, texto } = await abrir('Administrador', estadoOk);
    const boton = document.querySelector('button[title="Ver ficha de predicción"]') as HTMLElement;
    boton.click();
    await harness.fixture.whenStable();
    responder(http, estadoOk);
    await new Promise(r => setTimeout(r, 20));
    harness.detectChanges();
    expect(texto()).toContain('Demanda a 30 días');
    expect(texto()).toContain('Promedio ponderado de ventas');
    (Array.from(document.querySelectorAll('button')).find(b => b.textContent?.trim() === 'Cerrar') as HTMLElement).click();
    harness.detectChanges();
    expect(texto()).not.toContain('Demanda a 30 días');
  });

  it('Cambia a la pestaña de categorías y a la de precisión', async () => {
    const { http, harness, texto } = await abrir('Gerencia', estadoOk);
    const tabs = () => Array.from(document.querySelectorAll('button.tab')) as HTMLElement[];
    tabs().find(t => t.textContent?.includes('categoría'))!.click();
    await harness.fixture.whenStable(); responder(http, estadoOk); await harness.fixture.whenStable(); harness.detectChanges();
    expect(texto()).toContain('Pinturas');
    expect(texto()).toContain('+50.0%');
    tabs().find(t => t.textContent?.includes('Precisión'))!.click();
    await harness.fixture.whenStable(); responder(http, estadoOk); responder(http, estadoOk); await harness.fixture.whenStable(); harness.detectChanges();
    expect(texto()).toContain('MAE');
    expect(texto()).toContain('El modelo se desvía en promedio');
  });
});
