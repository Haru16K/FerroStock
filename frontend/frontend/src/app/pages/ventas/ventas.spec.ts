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

function sesion(rol: string) {
  const payload = btoa(JSON.stringify({ exp: Math.floor(Date.now() / 1000) + 3600 }));
  localStorage.setItem('ferrostock_token', `x.${payload}.y`);
  localStorage.setItem('ferrostock_user', JSON.stringify({ nombre: 'Prueba', rol, usuarioId: 1 }));
}

const fila = (id: number, extra: any = {}) => ({
  id, numero: 'V-0000' + id, fecha: '2026-11-10T15:00:00', estado: 'Completada', vendedorId: 1, vendedorNombre: 'Ana',
  clienteNombre: 'Cliente ' + id, clienteDocumento: null, subtotal: 100, descuentoPorcentaje: 0, descuentoMonto: 0,
  total: 100, unidades: 7, lineas: 2, esDemo: false, ...extra });

const completa = (id: number) => ({ ...fila(id), descuentoAplicadoPor: null, anuladoPor: null, fechaAnulacion: null, motivoAnulacion: null,
  detalles: [{ id: 1, productoId: 1, productoCodigo: 'P-1', productoNombre: 'Martillo grande', cantidad: 3, precioUnitario: 10, subtotal: 30 },
             { id: 2, productoId: 2, productoCodigo: 'P-2', productoNombre: 'Clavos finos', cantidad: 4, precioUnitario: 5, subtotal: 20 }] });

const esListaReq = (req: { method: string; url: string }) => req.method === 'GET' && /\/ventas$/.test(req.url);
const esLista = (r: TestRequest) => esListaReq(r.request);

function responder(http: HttpTestingController, listado: any = { cantidad: 2, cantidadCompletadas: 2, totalVendido: 200, totalDescuentos: 0, limite: 200, ventas: [fila(1), fila(2)] }) {
  const peticiones: string[] = [];
  for (const r of http.match(() => true)) {
    const u = r.request.url;
    peticiones.push(`${r.request.method} ${u.replace('https://localhost:7091/api', '')}`);
    if (u.endsWith('/auth/me')) r.flush({ usuarioId: 1, nombre: 'Prueba', rol: 'Administrador' });
    else if (esLista(r)) r.flush(listado);
    else if (/\/ventas\/\d+$/.test(u)) r.flush(completa(Number(u.split('/').pop())));
    else if (u.endsWith('/ventas/vendedores')) r.flush([]);
    else if (u.endsWith('/dashboard')) r.flush({ kpis: { totalProductos: 0, stockCritico: 0, entradasHoy: 0, salidasHoy: 0, deltaEntradas: 0, deltaSalidas: 0 }, movRecientes: [], alertas: [], categorias: [], topProductos: [], grafico: { dias: [], entradas: [], salidas: [] } });
    else if (u.includes('/analisis/ventas')) r.flush({ desde: '', hasta: '', totalVendido: 0, cantidadVentas: 0, ticketPromedio: 0, totalDescuentos: 0, unidadesVendidas: 0, porDia: [], porCategoria: [], topProductos: [] });
    else if (u.endsWith('/productos')) r.flush([]);
    else if (u.includes('/proveedores')) r.flush([]);
    else r.flush({});
  }
  return peticiones;
}

async function abrir() {
  sesion('Administrador');
  TestBed.configureTestingModule({ providers: [{ provide: LOCALE_ID, useValue: 'es' }, provideHttpClient(), provideHttpClientTesting(), provideRouter(routes)] });
  const http = TestBed.inject(HttpTestingController);
  const harness = await RouterTestingHarness.create();
  await harness.navigateByUrl('/ventas');
  return { http, harness, texto: () => document.body.textContent ?? '' };
}
const asentar = async (h: RouterTestingHarness) => { await h.fixture.whenStable(); await new Promise(r => setTimeout(r, 20)); h.detectChanges(); };

describe('Historial de ventas liviano', () => {
  it('Muestra las filas del listado con sus totales', async () => {
    const { http, harness, texto } = await abrir();
    responder(http); await asentar(harness);
    expect(texto()).toContain('V-00001');
    expect(texto()).toContain('Cliente 2');
    expect(texto()).not.toContain('Mostrando las');   // se ven todas: no hay aviso
  });

  it('Avisa cuando hay más ventas de las que se muestran', async () => {
    const { http, harness, texto } = await abrir();
    responder(http, { cantidad: 1500, cantidadCompletadas: 1400, totalVendido: 98000, totalDescuentos: 300, limite: 200, ventas: [fila(1), fila(2)] });
    await asentar(harness);
    expect(texto()).toContain('Mostrando las 2 ventas más recientes de 1500');
  });

  it('El comprobante pide el detalle de la venta al abrirse', async () => {
    const { http, harness, texto } = await abrir();
    responder(http); await asentar(harness);
    (document.querySelector('button[title="Ver comprobante"]') as HTMLElement).click();
    await harness.fixture.whenStable();
    const pedidas = responder(http); await asentar(harness);
    expect(pedidas.some(p => p === 'GET /ventas/1')).toBe(true);
    expect(texto()).toContain('Martillo grande');
    expect(texto()).toContain('Clavos finos');
  });

  it('Cambiar un filtro CANCELA la consulta anterior en curso', async () => {
    const { http, harness } = await abrir();
    await harness.fixture.whenStable();
    const primera = http.match(esListaReq);            // la carga inicial, todavía sin respuesta
    expect(primera.length).toBe(1);
    expect(primera[0].cancelled).toBe(false);

    const select = Array.from(document.querySelectorAll('select')).find(s => s.textContent?.includes('Todos los estados')) as HTMLSelectElement;
    select.selectedIndex = 1;
    select.dispatchEvent(new Event('change'));
    await harness.fixture.whenStable();

    expect(primera[0].cancelled).toBe(true);        // la anterior se abortó
    const segunda = http.match(esListaReq);
    expect(segunda.length).toBe(1);                 // y solo hay una consulta viva
    expect(segunda[0].cancelled).toBe(false);
  });

  it('Salir de Ventas CANCELA la consulta pendiente (no deja trabajo en el servidor)', async () => {
    const { http, harness } = await abrir();
    await harness.fixture.whenStable();
    const pendiente = http.match(esListaReq);
    expect(pendiente[0].cancelled).toBe(false);

    await harness.navigateByUrl('/proveedores');
    expect(TestBed.inject(Router).url).toBe('/proveedores');
    expect(pendiente[0].cancelled).toBe(true);
  });
});
