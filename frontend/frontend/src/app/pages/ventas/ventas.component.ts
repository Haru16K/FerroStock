import { Component, OnInit, OnDestroy, inject, ChangeDetectorRef } from '@angular/core';
import { Subscription } from 'rxjs';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { VentaService } from '../../shared/services/venta.service';
import { ProductoService } from '../../shared/services/producto.service';
import { RecomendacionService } from '../../shared/services/recomendacion.service';
import { Complementario } from '../../shared/models/recomendacion.model';
import { AuthService } from '../../core/auth.service';
import { Venta, VentaListado, EstadoVenta, Vendedor, CrearVentaDto } from '../../shared/models/venta.model';
import { Producto } from '../../shared/models/producto.model';
import { formatearFechaLima, fechaCorta } from '../../shared/utils/fecha-lima';
import { ExportarBotonesComponent } from '../../shared/components/exportar-botones.component';
import { exportarReporte, FormatoExport, ReporteExport } from '../../shared/utils/exportar';

interface LineaForm {
  productoId: number;
  cantidad:   number;
}

@Component({
  selector: 'app-ventas',
  standalone: true,
  imports: [CommonModule, FormsModule, ExportarBotonesComponent],
  templateUrl: './ventas.component.html'
})
export class VentasComponent implements OnInit, OnDestroy {
  private svc     = inject(VentaService);
  private prodSvc = inject(ProductoService);
  private auth    = inject(AuthService);
  private recSvc  = inject(RecomendacionService);
  private cdr     = inject(ChangeDetectorRef);

  puedeVender  = this.auth.tieneRol('Administrador', 'Vendedor');
  esAdmin      = this.auth.isAdmin();
  filtraVendedor = this.auth.tieneRol('Administrador', 'Gerencia');   // el vendedor solo ve lo suyo
  nombreUsuario  = this.auth.getUser()?.nombre ?? '';

  readonly estados: EstadoVenta[] = ['Completada', 'Anulada'];

  // ── Historial ─────────────────────────────────────────────────
  ventas: VentaListado[] = [];
  limite = 200;
  abriendoId: number | null = null;
  private subLista?: Subscription;
  cantidad = 0;
  cantidadCompletadas = 0;
  totalVendido = 0;
  totalDescuentos = 0;
  loading = true;
  error   = '';

  filtroVendedor = 0;
  filtroProducto = 0;
  filtroEstado: EstadoVenta | '' = '';
  filtroDesde = '';
  filtroHasta = '';

  vendedores: Vendedor[] = [];
  productos:  Producto[] = [];

  // ── Nueva venta ───────────────────────────────────────────────
  nuevaAbierta = false;
  guardando    = false;
  errorNueva   = '';
  lineas: LineaForm[] = [];
  clienteNombre    = '';
  clienteDocumento = '';
  descuento        = 0;
  descuentoMax     = 0;
  sugerencias: Complementario[] = [];
  private tokenSugerencias = 0;

  // ── Detalle / comprobante ─────────────────────────────────────
  detalle: Venta | null = null;
  avisoImpresion = '';

  // ── Anular ────────────────────────────────────────────────────
  ventaAnular: Pick<Venta, 'id' | 'numero' | 'total'> | null = null;
  motivo = '';
  errorAnular = '';
  procesando = false;

  ngOnInit() {
    this.prodSvc.getAll().subscribe({
      next: p => { this.productos = p; this.cdr.detectChanges(); }
    });
    if (this.filtraVendedor) {
      this.svc.getVendedores().subscribe({
        next: v => { this.vendedores = v; this.cdr.detectChanges(); }
      });
    }
    this.cargar();
  }

  ngOnDestroy() {
    // Al salir de la pantalla se cancela la consulta en curso: si no, el servidor sigue
    // trabajando para nadie y hace más lentas las demás pantallas.
    this.subLista?.unsubscribe();
  }

  cargar() {
    this.subLista?.unsubscribe();   // un filtro nuevo cancela la consulta anterior
    this.loading = true;
    this.error   = '';
    this.cdr.detectChanges();

    this.subLista = this.svc.getAll({
      vendedorId: this.filtroVendedor || undefined,
      productoId: this.filtroProducto || undefined,
      estado:     this.filtroEstado   || undefined,
      desde:      this.filtroDesde    || undefined,
      hasta:      this.filtroHasta    || undefined
    }).subscribe({
      next: r => {
        this.ventas              = r.ventas;
        this.cantidad            = r.cantidad;
        this.cantidadCompletadas = r.cantidadCompletadas;
        this.totalVendido        = r.totalVendido;
        this.totalDescuentos     = r.totalDescuentos;
        this.limite              = r.limite;
        this.loading             = false;
        this.cdr.detectChanges();
      },
      error: e => {
        this.error   = e.error?.message ?? 'Error al cargar las ventas';
        this.loading = false;
        this.cdr.detectChanges();
      }
    });
  }

  // Hay más ventas que las que se muestran en la tabla
  get truncado(): boolean { return this.cantidad > this.ventas.length; }

  get hayFiltros(): boolean {
    return !!(this.filtroVendedor || this.filtroProducto || this.filtroEstado
      || this.filtroDesde || this.filtroHasta);
  }

  limpiarFiltros() {
    this.filtroVendedor = 0;
    this.filtroProducto = 0;
    this.filtroEstado   = '';
    this.filtroDesde    = '';
    this.filtroHasta    = '';
    this.cargar();
  }

  // ── Nueva venta ───────────────────────────────────────────────
  abrirNueva() {
    this.nuevaAbierta = true;
    this.guardando    = false;
    this.errorNueva   = '';
    this.lineas       = [this.lineaVacia()];
    this.clienteNombre = '';
    this.clienteDocumento = '';
    this.descuento    = 0;
    this.sugerencias  = [];
    this.cdr.detectChanges();

    // Stock y precios en tiempo real (HU-12): se vuelven a consultar al abrir
    this.prodSvc.getAll().subscribe({
      next: p => { this.productos = p; this.cdr.detectChanges(); }
    });
    this.svc.getConfiguracion().subscribe({
      next: c => { this.descuentoMax = c.descuentoMaximoPorcentaje; this.cdr.detectChanges(); }
    });
  }

  cerrarNueva() {
    this.nuevaAbierta = false;
    this.cdr.detectChanges();
  }

  private lineaVacia(): LineaForm { return { productoId: 0, cantidad: 1 }; }

  agregarLinea() { this.lineas.push(this.lineaVacia()); }

  quitarLinea(i: number) {
    this.lineas.splice(i, 1);
    this.cargarSugerencias();
  }

  // Productos que suelen venderse junto a los de esta venta (HU-33). Son opcionales:
  // si no se pueden calcular, la venta sigue su curso sin mostrar nada.
  cargarSugerencias() {
    const ids = this.lineas.map(l => Number(l.productoId)).filter(id => id > 0);
    const token = ++this.tokenSugerencias;   // evita que una respuesta lenta pise a una más nueva
    if (ids.length === 0) {
      this.sugerencias = [];
      this.cdr.detectChanges();
      return;
    }
    this.recSvc.getComplementarios(ids).subscribe({
      next: s => {
        if (token !== this.tokenSugerencias) return;
        this.sugerencias = s;
        this.cdr.detectChanges();
      },
      error: () => {
        if (token !== this.tokenSugerencias) return;
        this.sugerencias = [];
        this.cdr.detectChanges();
      }
    });
  }

  agregarSugerido(s: Complementario) {
    const vacia = this.lineas.find(l => !Number(l.productoId));
    if (vacia) { vacia.productoId = s.productoId; vacia.cantidad = 1; }
    else this.lineas.push({ productoId: s.productoId, cantidad: 1 });
    this.cargarSugerencias();
    this.cdr.detectChanges();
  }

  get productosActivos(): Producto[] {
    return this.productos.filter(p => p.activo);
  }

  producto(l: LineaForm): Producto | undefined {
    return this.productos.find(p => p.id === Number(l.productoId));
  }

  precio(l: LineaForm): number   { return this.producto(l)?.precioVenta ?? 0; }
  stock(l: LineaForm): number    { return this.producto(l)?.stockActual ?? 0; }
  subtotalLinea(l: LineaForm): number { return (Number(l.cantidad) || 0) * this.precio(l); }

  productoUsado(id: number, linea: LineaForm): boolean {
    return this.lineas.some(l => l !== linea && Number(l.productoId) === id);
  }

  excedeStock(l: LineaForm): boolean {
    return !!this.producto(l) && Number(l.cantidad) > this.stock(l);
  }

  etiquetaProducto(p: Producto): string {
    const base = `${p.codigo} — ${p.nombre} · stock ${p.stockActual}`;
    if (p.stockActual <= 0)  return `${base} (agotado)`;
    if (p.precioVenta <= 0)  return `${base} (sin precio)`;
    return base;
  }

  get subtotalVenta(): number {
    return this.lineas.reduce((acc, l) => acc + this.subtotalLinea(l), 0);
  }

  get descuentoMonto(): number {
    const pct = Number(this.descuento) || 0;
    return Math.round(this.subtotalVenta * pct) / 100;
  }

  get totalVenta(): number {
    return this.subtotalVenta - this.descuentoMonto;
  }

  registrar() {
    if (this.lineas.length === 0 || this.lineas.some(l => !Number(l.productoId))) {
      this.errorNueva = 'Selecciona un producto en cada línea (o quita las líneas vacías)';
      this.cdr.detectChanges();
      return;
    }
    for (const l of this.lineas) {
      const p = this.producto(l)!;
      const cant = Number(l.cantidad);
      if (!Number.isInteger(cant) || cant <= 0) {
        this.errorNueva = `${p.codigo}: la cantidad debe ser un entero mayor a cero`;
        this.cdr.detectChanges();
        return;
      }
      if (p.stockActual <= 0) {
        this.errorNueva = `${p.nombre} está agotado (stock 0)`;
        this.cdr.detectChanges();
        return;
      }
      if (cant > p.stockActual) {
        this.errorNueva = `Stock insuficiente de ${p.nombre}. Disponible: ${p.stockActual}`;
        this.cdr.detectChanges();
        return;
      }
      if (p.precioVenta <= 0) {
        this.errorNueva = `${p.codigo} no tiene precio de venta configurado`;
        this.cdr.detectChanges();
        return;
      }
    }
    const pct = Number(this.descuento) || 0;
    if (pct < 0 || pct > this.descuentoMax) {
      this.errorNueva = `El descuento debe estar entre 0% y ${this.descuentoMax}% (límite autorizado)`;
      this.cdr.detectChanges();
      return;
    }

    this.guardando  = true;
    this.errorNueva = '';
    this.cdr.detectChanges();

    const dto: CrearVentaDto = {
      clienteNombre:       this.clienteNombre.trim() || null,
      clienteDocumento:    this.clienteDocumento.trim() || null,
      descuentoPorcentaje: pct,
      lineas: this.lineas.map(l => ({ productoId: Number(l.productoId), cantidad: Number(l.cantidad) }))
    };

    this.svc.registrar(dto).subscribe({
      next: venta => {
        this.nuevaAbierta = false;
        this.guardando    = false;
        this.avisoImpresion = '';
        this.detalle = venta;          // se muestra el comprobante de la venta recién registrada
        this.cargar();
      },
      error: e => {
        this.errorNueva = e.error?.message ?? 'Error al registrar la venta';
        this.guardando  = false;
        // El stock pudo cambiar: se refresca para que el vendedor vea lo vigente
        this.prodSvc.getAll().subscribe({ next: p => { this.productos = p; this.cdr.detectChanges(); } });
        this.cdr.detectChanges();
      }
    });
  }

  // ── Detalle / comprobante ─────────────────────────────────────
  // El historial no trae las líneas: se piden al abrir el comprobante
  abrirDetalle(v: VentaListado) {
    this.abriendoId = v.id;
    this.error = '';
    this.cdr.detectChanges();

    this.svc.getById(v.id).subscribe({
      next: venta => {
        this.abriendoId = null;
        this.detalle = venta;
        this.avisoImpresion = '';
        this.cdr.detectChanges();
      },
      error: e => {
        this.abriendoId = null;
        this.error = e.error?.message ?? 'No se pudo cargar el comprobante';
        this.cdr.detectChanges();
      }
    });
  }

  cerrarDetalle() {
    this.detalle = null;
    this.cdr.detectChanges();
  }

  private esc(s: string | null | undefined): string {
    return (s ?? '').replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;').replace(/'/g, '&#39;');
  }

  // Comprobante interno de venta (no es un comprobante electrónico de SUNAT)
  imprimir(v: Venta) {
    const w = window.open('', '_blank', 'width=420,height=700');
    if (!w) {
      this.avisoImpresion = 'El navegador bloqueó la ventana. Permite las ventanas emergentes para imprimir.';
      this.cdr.detectChanges();
      return;
    }
    this.avisoImpresion = '';

    const money = (n: number) => 'S/ ' + n.toFixed(2);
    const filas = v.detalles.map(d => `
      <tr>
        <td>${this.esc(d.productoNombre)}<br><small>${this.esc(d.productoCodigo)}</small></td>
        <td class="r">${d.cantidad}</td>
        <td class="r">${d.precioUnitario.toFixed(2)}</td>
        <td class="r">${d.subtotal.toFixed(2)}</td>
      </tr>`).join('');

    w.document.write(`<!DOCTYPE html><html><head><meta charset="utf-8">
      <title>Comprobante ${this.esc(v.numero)}</title>
      <style>
        body { font-family: 'Courier New', monospace; font-size: 12px; width: 320px; margin: 12px auto; color: #000; }
        h1 { font-size: 15px; text-align: center; margin: 0; }
        .c { text-align: center; } .r { text-align: right; }
        table { width: 100%; border-collapse: collapse; margin: 8px 0; }
        th, td { padding: 3px 0; vertical-align: top; } th { border-bottom: 1px dashed #000; text-align: left; }
        .tot td { border-top: 1px dashed #000; } .anulada { text-align: center; font-weight: bold; border: 2px solid #000; padding: 4px; margin: 8px 0; }
        small { color: #444; }
      </style></head><body>
      <h1>FerroTech S.A.C.</h1>
      <div class="c">Comprobante de venta ${this.esc(v.numero)}</div>
      <div class="c">${this.esc(formatearFechaLima(v.fecha, true))}</div>
      ${v.estado === 'Anulada' ? '<div class="anulada">VENTA ANULADA</div>' : ''}
      <hr>
      <div>Vendedor: ${this.esc(v.vendedorNombre)}</div>
      <div>Cliente: ${this.esc(v.clienteNombre || '—')}</div>
      ${v.clienteDocumento ? `<div>Documento: ${this.esc(v.clienteDocumento)}</div>` : ''}
      <table>
        <thead><tr><th>Producto</th><th class="r">Cant.</th><th class="r">P.U.</th><th class="r">Importe</th></tr></thead>
        <tbody>${filas}</tbody>
        <tfoot>
          <tr class="tot"><td colspan="3">Subtotal</td><td class="r">${v.subtotal.toFixed(2)}</td></tr>
          ${v.descuentoMonto > 0 ? `<tr><td colspan="3">Descuento (${v.descuentoPorcentaje}%)</td><td class="r">-${v.descuentoMonto.toFixed(2)}</td></tr>` : ''}
          <tr><td colspan="3"><b>TOTAL</b></td><td class="r"><b>${money(v.total)}</b></td></tr>
        </tfoot>
      </table>
      <div class="c">¡Gracias por su compra!</div>
      </body></html>`);
    w.document.close();
    w.focus();
    setTimeout(() => w.print(), 250);
  }

  // ── Anular (HU-14) ────────────────────────────────────────────
  abrirAnular(v: Pick<Venta, 'id' | 'numero' | 'total'>) {
    this.ventaAnular = v;
    this.motivo      = '';
    this.errorAnular = '';
    this.procesando  = false;
    this.cdr.detectChanges();
  }

  cerrarAnular() {
    this.ventaAnular = null;
    this.cdr.detectChanges();
  }

  confirmarAnular() {
    if (!this.ventaAnular) return;
    const motivo = this.motivo.trim();
    if (!motivo) {
      this.errorAnular = 'Debes indicar la justificación de la anulación';
      this.cdr.detectChanges();
      return;
    }
    this.procesando  = true;
    this.errorAnular = '';
    this.cdr.detectChanges();

    this.svc.anular(this.ventaAnular.id, motivo).subscribe({
      next: anulada => {
        this.procesando  = false;
        this.ventaAnular = null;
        if (this.detalle?.id === anulada.id) this.detalle = anulada;
        // El stock se repuso: se refresca el catálogo
        this.prodSvc.getAll().subscribe({ next: p => { this.productos = p; this.cdr.detectChanges(); } });
        this.cargar();
      },
      error: e => {
        this.errorAnular = e.error?.message ?? 'No se pudo anular la venta';
        this.procesando  = false;
        this.cdr.detectChanges();
      }
    });
  }

  // ── Exportar (HU-15 y HU-21) ──────────────────────────────────
  // Exporta exactamente lo que se ve en pantalla (respeta los filtros aplicados).
  exportar(formato: FormatoExport) {
    const completadas = this.ventas.filter(v => v.estado === 'Completada');
    const nombreDe = (lista: { id: number; nombre?: string; codigo?: string }[], id: number) =>
      lista.find(x => x.id === id);

    const filtros: string[] = [];
    if (this.filtroDesde || this.filtroHasta)
      filtros.push(`Período: ${this.filtroDesde ? fechaCorta(this.filtroDesde) : 'inicio'} al ${this.filtroHasta ? fechaCorta(this.filtroHasta) : 'hoy'}`);
    if (this.filtroVendedor) filtros.push(`Vendedor: ${this.vendedores.find(v => v.id === Number(this.filtroVendedor))?.nombre ?? ''}`);
    if (this.filtroProducto) {
      const p = nombreDe(this.productos.map(x => ({ id: x.id, nombre: x.nombre, codigo: x.codigo })), Number(this.filtroProducto));
      filtros.push(`Producto: ${p?.codigo ?? ''} ${p?.nombre ?? ''}`.trim());
    }
    if (this.filtroEstado) filtros.push(`Estado: ${this.filtroEstado}`);
    if (!this.filtraVendedor) filtros.push(`Vendedor: ${this.nombreUsuario}`);
    if (filtros.length === 0) filtros.push('Sin filtros: todas las ventas');
    if (this.truncado)
      filtros.push(`Se exportan las ${this.ventas.length} ventas más recientes de ${this.cantidad}; usa los filtros para acotar el resto`);
    filtros.push('Los totales incluyen solo ventas completadas');

    const reporte: ReporteExport = {
      titulo: 'Reporte de ventas',
      filtros,
      columnas: [
        { titulo: 'N° venta' }, { titulo: 'Fecha' }, { titulo: 'Estado' }, { titulo: 'Vendedor' },
        { titulo: 'Cliente' }, { titulo: 'Unidades', formato: 'entero' },
        { titulo: 'Subtotal', formato: 'moneda' }, { titulo: 'Desc. %', formato: 'decimal' },
        { titulo: 'Total', formato: 'moneda' }
      ],
      filas: this.ventas.map(v => [
        v.numero, formatearFechaLima(v.fecha, true), v.estado, v.vendedorNombre, v.clienteNombre ?? '',
        v.unidades, v.subtotal, v.descuentoPorcentaje, v.total
      ]),
      totales: [
        `TOTAL (${completadas.length} completadas)`, '', '', '', '',
        completadas.reduce((a, v) => a + v.unidades, 0),
        completadas.reduce((a, v) => a + v.subtotal, 0), '', completadas.reduce((a, v) => a + v.total, 0)
      ],
      nombreArchivo: 'ventas'
    };
    exportarReporte(formato, reporte);
  }

  formatearFecha(f: string | null): string {
    return formatearFechaLima(f, true);
  }

  estadoClass(e: string): string {
    return 'badge-' + e.toLowerCase();
  }
}
