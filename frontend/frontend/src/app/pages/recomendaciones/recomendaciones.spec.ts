import { TestBed } from '@angular/core/testing';
import { LOCALE_ID } from '@angular/core';
import { registerLocaleData } from '@angular/common';
import localeEs from '@angular/common/locales/es';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController, TestRequest } from '@angular/common/http/testing';
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

const rec = (id: number, tipo: string, extra: any = {}) => ({
  id, tipo, productoId: id, productoCodigo: 'P-' + id, productoNombre: 'Producto ' + id, titulo: `Título ${tipo} ${id}`,
  motivos: ['Motivo uno del sistema', 'Motivo dos del sistema'], prioridad: 'Alta', cantidad: tipo === 'Compra' ? 64 : null,
  proveedorNombre: tipo === 'Compra' ? 'Ferretera Norte' : null, montoEstimado: tipo === 'Compra' ? 640 : null,
  estado: 'Pendiente', fechaGeneracion: '2026-11-10T12:00:00', fechaGestion: null, gestionadaPor: null, ...extra });

const lista = (items: any[]) => ({ pendientes: 3, pendientesCompra: 1, pendientesPromocion: 1, pendientesDescontinuar: 1,
  ultimaGeneracion: '2026-11-10T12:00:00', items });

const peticiones: string[] = [];
function responder(http: HttpTestingController, extra: (r: TestRequest) => boolean = () => false) {
  for (const r of http.match(() => true)) {
    const u = r.request.url, m = r.request.method;
    peticiones.push(`${m} ${u.replace('https://localhost:7091/api', '')}`);
    if (extra(r)) continue;
    if (u.endsWith('/auth/me')) r.flush({ usuarioId: 1, nombre: 'Prueba', rol: rolActual });
    else if (u.endsWith('/recomendaciones/generar')) r.flush({ nuevas: 2, actualizadas: 1, vencidas: 0, mensaje: '2 recomendaciones nuevas, 1 actualizadas y 0 vencidas.' });
    else if (/\/recomendaciones\/\d+\/(aplicar|descartar)$/.test(u)) r.flush(rec(1, 'Compra', { estado: u.endsWith('aplicar') ? 'Aplicada' : 'Descartada' }));
    else if (u.includes('/recomendaciones/configuracion') && m === 'GET') r.flush({
      valores: { 'Compra.DiasCobertura': 30, 'Compra.NivelServicio': 90, 'Promocion.Cantidad': 5, 'Descontinuar.UmbralUnidades': 3, 'Descontinuar.DiasVentana': 180, 'Descontinuar.DiasSinMovimiento': 90 },
      limites: { 'Compra.DiasCobertura': { defecto: 30, min: 7, max: 180 }, 'Compra.NivelServicio': { defecto: 90, min: 80, max: 99 }, 'Promocion.Cantidad': { defecto: 5, min: 1, max: 20 },
        'Descontinuar.UmbralUnidades': { defecto: 3, min: 0, max: 1000 }, 'Descontinuar.DiasVentana': { defecto: 180, min: 30, max: 730 }, 'Descontinuar.DiasSinMovimiento': { defecto: 90, min: 30, max: 730 } },
      nivelesServicio: [80, 85, 90, 95, 97, 99] });
    else if (u.includes('/recomendaciones/configuracion') && m === 'PUT') r.flush({ valores: r.request.body.valores, limites: {}, nivelesServicio: [] });
    else if (u.includes('/recomendaciones/producto/')) r.flush({ productoId: 1, recomendarCompra: true, cantidad: 64, prioridad: 'Alta', montoEstimado: 640,
      motivos: ['Demanda estimada: 2 u/día', 'Plazo de entrega: 5 días'], aviso: null,
      proveedores: [
        { proveedorId: 1, proveedor: 'Ferretera Norte', precio: 10, precioEsReferencia: false, diasEntrega: 5, entregaEsReal: true, calificacion: 4.2, puntajePct: 88, esRecomendado: true, justificacion: ['Es de los más rápidos: entrega en 5 días', 'Puntaje ponderado: precio 50 %'] },
        { proveedorId: 2, proveedor: 'Importaciones Andina', precio: 9, precioEsReferencia: true, diasEntrega: 12, entregaEsReal: false, calificacion: 3.5, puntajePct: 71, esRecomendado: false, justificacion: [] }] });
    else if (u.includes('/recomendaciones/complementarios')) r.flush([{ productoId: 2, codigo: 'P-2', nombre: 'Clavos 2"', precioVenta: 4.5, stockActual: 40, vecesJuntos: 12, confianzaPct: 60, motivo: 'Se vendió junto a Martillo en 12 de 20 ventas (60 %).' }]);
    else if (u.endsWith('/predicciones/estado')) r.flush({ ultima: null, ultimaExitosa: null, permitirDatosDemo: false, hayDatosDemo: false });
    else if (u.endsWith('/predicciones/productos')) r.flush({ productosEnRiesgo: 0, productosSinHistorial: 0, demandaTotal30d: 60, estacionalesDetectados: 0, productos: [{
      productoId: 1, codigo: 'P-1', nombre: 'Martillo', categoria: 'Herramientas', stockActual: 20, stockMinimo: 5, demandaDiaria: 2, demanda30d: 60, minima30d: 50, maxima30d: 70,
      confianzaPct: 85, nivelConfianza: 'Alta', diasHistorial: 200, diasHastaQuiebre: 10, fechaQuiebre: '2026-11-21', esEstacional: false, mesPico: null, explicacion: 'Promedio ponderado...' }] });
    else if (/\/predicciones\/productos\/\d+$/.test(u)) r.flush({ prediccion: { productoId: 1, codigo: 'P-1', nombre: 'Martillo', categoria: 'Herramientas', stockActual: 20, stockMinimo: 5, demandaDiaria: 2, demanda30d: 60, minima30d: 50, maxima30d: 70,
      confianzaPct: 85, nivelConfianza: 'Alta', diasHistorial: 200, diasHastaQuiebre: 10, fechaQuiebre: '2026-11-21', esEstacional: false, mesPico: null, explicacion: 'Promedio ponderado...' }, serie: [] });
    else if (u.endsWith('/productos')) r.flush([
      { id: 1, codigo: 'P-1', nombre: 'Martillo', categoriaId: 1, categoriaNombre: 'Herr', precioCompra: 8, precioVenta: 12, stockActual: 20, stockMinimo: 5, activo: true, esCritico: false },
      { id: 2, codigo: 'P-2', nombre: 'Clavos 2"', categoriaId: 1, categoriaNombre: 'Herr', precioCompra: 3, precioVenta: 4.5, stockActual: 40, stockMinimo: 5, activo: true, esCritico: false }]);
    else if (u.endsWith('/dashboard')) r.flush({ kpis: { totalProductos: 7, stockCritico: 0, entradasHoy: 0, salidasHoy: 0, deltaEntradas: 0, deltaSalidas: 0 }, movRecientes: [], alertas: [], categorias: [], topProductos: [], grafico: { dias: [], entradas: [], salidas: [] } });
    else if (u.includes('/analisis/ventas')) r.flush({ desde: '2026-10-12', hasta: '2026-11-10', totalVendido: 0, cantidadVentas: 0, ticketPromedio: 0, totalDescuentos: 0, unidadesVendidas: 0, porDia: [], porCategoria: [], topProductos: [] });
    else if (u.endsWith('/ventas/configuracion')) r.flush({ descuentoMaximoPorcentaje: 10 });
    else if (u.endsWith('/ventas/vendedores')) r.flush([]);
    else if (u.includes('/ventas')) r.flush({ cantidad: 0, cantidadCompletadas: 0, totalVendido: 0, totalDescuentos: 0, ventas: [] });
    else r.flush({});
  }
}

async function abrir(rol: string, url: string) {
  (globalThis as any).Chart = class { destroy() {} };
  peticiones.length = 0;
  sesion(rol);
  TestBed.configureTestingModule(config());
  const http = TestBed.inject(HttpTestingController);
  const harness = await RouterTestingHarness.create();
  await harness.navigateByUrl(url);
  return { http, harness, texto: () => document.body.textContent ?? '' };
}
const asentar = async (h: RouterTestingHarness) => { await h.fixture.whenStable(); await new Promise(r => setTimeout(r, 20)); h.detectChanges(); };
const boton = (txt: string) => Array.from(document.querySelectorAll('button')).find(b => b.textContent?.trim().includes(txt)) as HTMLElement | undefined;

describe('Recomendaciones (MOD-07)', () => {
  it('Gerencia ve las recomendaciones con su motivo, cantidad y proveedor', async () => {
    const { http, harness, texto } = await abrir('Gerencia', '/recomendaciones');
    responder(http, r => { if (r.request.url.endsWith('/recomendaciones')) { r.flush(lista([rec(1, 'Compra'), rec(2, 'Promocion'), rec(3, 'Descontinuar')])); return true; } return false; });
    await asentar(harness);
    expect(texto()).toContain('Título Compra 1');
    expect(texto()).toContain('Motivo uno del sistema');
    expect(texto()).toContain('Cantidad sugerida');
    expect(texto()).toContain('Ferretera Norte');
    expect(texto()).toContain('Título Descontinuar 3');
  });

  it('Marcar como aplicada llama a la API y recarga la lista (HU-35)', async () => {
    const { http, harness } = await abrir('Administrador', '/recomendaciones');
    const lis = (r: TestRequest) => { if (r.request.url.endsWith('/recomendaciones')) { r.flush(lista([rec(1, 'Compra')])); return true; } return false; };
    responder(http, lis); await asentar(harness);
    boton('Marcar como aplicada')!.click();
    await harness.fixture.whenStable();
    responder(http, lis); await asentar(harness);   // responde el POST; este pide recargar la lista
    responder(http, lis); await asentar(harness);   // responde la recarga
    expect(peticiones.some(p => p === 'POST /recomendaciones/1/aplicar')).toBe(true);
    expect(peticiones.filter(p => p === 'GET /recomendaciones').length).toBe(2);
  });

  it('Descartar usa su propio endpoint', async () => {
    const { http, harness } = await abrir('Administrador', '/recomendaciones');
    const lis = (r: TestRequest) => { if (r.request.url.endsWith('/recomendaciones')) { r.flush(lista([rec(1, 'Compra')])); return true; } return false; };
    responder(http, lis); await asentar(harness);
    boton('Descartar')!.click();
    await harness.fixture.whenStable(); responder(http, lis); await asentar(harness);
    expect(peticiones.some(p => p === 'POST /recomendaciones/1/descartar')).toBe(true);
  });

  it('Actualizar recomendaciones muestra el resultado', async () => {
    const { http, harness, texto } = await abrir('Administrador', '/recomendaciones');
    const lis = (r: TestRequest) => { if (r.request.url.endsWith('/recomendaciones')) { r.flush(lista([])); return true; } return false; };
    responder(http, lis); await asentar(harness);
    boton('Actualizar recomendaciones')!.click();
    await harness.fixture.whenStable(); responder(http, lis); await asentar(harness);
    expect(texto()).toContain('2 recomendaciones nuevas');
  });

  it('Configurar umbrales: carga los valores y los guarda', async () => {
    const { http, harness, texto } = await abrir('Gerencia', '/recomendaciones');
    const lis = (r: TestRequest) => { if (r.request.url.endsWith('/recomendaciones')) { r.flush(lista([])); return true; } return false; };
    responder(http, lis); await asentar(harness);
    boton('Configurar umbrales')!.click();
    await harness.fixture.whenStable(); responder(http, lis); await asentar(harness);
    expect(texto()).toContain('Días de venta que debe cubrir cada compra');
    boton('Guardar')!.click();
    await harness.fixture.whenStable(); responder(http, lis); await asentar(harness);
    expect(peticiones.some(p => p === 'PUT /recomendaciones/configuracion')).toBe(true);
  });

  it('Almacenero y Vendedor no pueden entrar a Recomendaciones', async () => {
    for (const rol of ['Almacenero', 'Vendedor']) {
      TestBed.resetTestingModule();
      const { http, harness } = await abrir(rol, '/recomendaciones');
      responder(http); await asentar(harness);
      expect(TestBed.inject(Router).url).not.toBe('/recomendaciones');
    }
  });
});

describe('Ficha de predicción: compra y proveedor (HU-30, HU-32)', () => {
  const abrirFicha = async (rol: string) => {
    const ctx = await abrir(rol, '/predicciones');
    responder(ctx.http); await asentar(ctx.harness);
    (document.querySelector('button[title="Ver ficha de predicción"]') as HTMLElement).click();
    await ctx.harness.fixture.whenStable(); responder(ctx.http); await asentar(ctx.harness);
    responder(ctx.http); await asentar(ctx.harness);
    return ctx;
  };

  it('Administrador ve cuánto comprar, proveedores comparados y la justificación', async () => {
    const { texto } = await abrirFicha('Administrador');
    expect(texto()).toContain('Recomendación de compra');
    expect(texto()).toContain('Se sugiere comprar');
    expect(texto()).toContain('64 unidades');
    expect(texto()).toContain('Recomendado');
    expect(texto()).toContain('Importaciones Andina');
    expect(texto()).toContain('Es de los más rápidos');
    expect(peticiones.some(p => p.startsWith('GET /recomendaciones/producto/1'))).toBe(true);
  });

  it('Almacenero no ve la recomendación de compra ni la pide al servidor', async () => {
    const { texto } = await abrirFicha('Almacenero');
    expect(texto()).toContain('Demanda a 30 días');
    expect(texto()).not.toContain('Recomendación de compra');
    expect(peticiones.some(p => p.includes('/recomendaciones/producto/'))).toBe(false);
  });
});

describe('Sugerencias durante la venta (HU-33)', () => {
  it('Al elegir un producto sugiere complementarios y permite agregarlos', async () => {
    const { http, harness, texto } = await abrir('Vendedor', '/ventas');
    responder(http); await asentar(harness);
    boton('Nueva venta')!.click();
    await harness.fixture.whenStable(); responder(http); await asentar(harness);

    const select = document.querySelector('.linea select') as HTMLSelectElement;
    select.value = select.options[1].value;
    select.dispatchEvent(new Event('change'));
    await harness.fixture.whenStable(); responder(http); await asentar(harness);

    expect(peticiones.some(p => p.includes('/recomendaciones/complementarios'))).toBe(true);
    expect(texto()).toContain('Los clientes suelen llevar también');
    expect(texto()).toContain('Se vendió junto a Martillo en 12 de 20 ventas');

    (document.querySelector('button.sug') as HTMLElement).click();
    await harness.fixture.whenStable(); responder(http); await asentar(harness);
    const lineas = document.querySelectorAll('.linea:not(.head)');
    expect(lineas.length).toBe(2);   // la que ya había + la sugerida
  });

  it('Si las sugerencias fallan, la venta sigue sin mostrar error', async () => {
    const { http, harness, texto } = await abrir('Vendedor', '/ventas');
    responder(http); await asentar(harness);
    boton('Nueva venta')!.click();
    await harness.fixture.whenStable(); responder(http); await asentar(harness);
    const select = document.querySelector('.linea select') as HTMLSelectElement;
    select.value = select.options[1].value;
    select.dispatchEvent(new Event('change'));
    await harness.fixture.whenStable();
    responder(http, r => { if (r.request.url.includes('/complementarios')) { r.flush({}, { status: 500, statusText: 'Error' }); return true; } return false; });
    await asentar(harness);
    expect(texto()).not.toContain('Los clientes suelen llevar también');
    expect(document.querySelector('.error-msg')).toBeNull();
  });
});
