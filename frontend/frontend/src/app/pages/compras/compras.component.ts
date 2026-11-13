import { Component, OnInit, inject, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Observable } from 'rxjs';
import { OrdenCompraService } from '../../shared/services/orden-compra.service';
import { ProveedorService } from '../../shared/services/proveedor.service';
import { ProductoService } from '../../shared/services/producto.service';
import { AuthService } from '../../core/auth.service';
import {
  OrdenCompra, EstadoOrdenCompra, CrearOrdenCompraDto
} from '../../shared/models/orden-compra.model';
import { Proveedor } from '../../shared/models/proveedor.model';
import { Producto } from '../../shared/models/producto.model';

interface LineaForm {
  productoId:     number;
  cantidad:       number;
  precioUnitario: number;
}

interface LineaRecepcionForm {
  detalleId:      number;
  productoCodigo: string;
  productoNombre: string;
  cantidad:       number;
  recibida:       number;
}

type TipoAccion = 'aprobar' | 'rechazar' | 'cancelar';

@Component({
  selector: 'app-compras',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './compras.component.html'
})
export class ComprasComponent implements OnInit {
  private svc     = inject(OrdenCompraService);
  private provSvc = inject(ProveedorService);
  private prodSvc = inject(ProductoService);
  private auth    = inject(AuthService);
  private cdr     = inject(ChangeDetectorRef);

  esAdmin = this.auth.isAdmin();

  readonly estados: EstadoOrdenCompra[] =
    ['Pendiente', 'Aprobada', 'Rechazada', 'Recibida', 'Cancelada'];

  // ── Lista e historial ─────────────────────────────────────────
  ordenes: OrdenCompra[] = [];
  cantidad = 0;
  montoComprado = 0;
  loading = true;
  error   = '';

  filtroProveedor = 0;
  filtroEstado: EstadoOrdenCompra | '' = '';
  filtroDesde = '';
  filtroHasta = '';

  proveedores: Proveedor[] = [];
  productos:   Producto[]  = [];

  // ── Modal nueva orden ─────────────────────────────────────────
  nuevaAbierta = false;
  guardando    = false;
  errorNueva   = '';
  formNueva    = { proveedorId: 0, observaciones: '' };
  lineas: LineaForm[] = [];
  asociados: number[] = [];       // productos asociados al proveedor elegido
  mostrarTodos = false;

  // ── Modal detalle ─────────────────────────────────────────────
  detalle: OrdenCompra | null = null;

  // ── Modal aprobar / rechazar / cancelar ───────────────────────
  accion: TipoAccion | null = null;
  ordenAccion: OrdenCompra | null = null;
  motivo = '';
  errorAccion = '';
  procesando  = false;

  // ── Modal recepción ───────────────────────────────────────────
  ordenRecibir: OrdenCompra | null = null;
  lineasRecepcion: LineaRecepcionForm[] = [];
  errorRecepcion = '';

  ngOnInit() {
    this.provSvc.getAll().subscribe({
      next: p => { this.proveedores = p; this.cdr.detectChanges(); }
    });
    this.prodSvc.getAll().subscribe({
      next: p => { this.productos = p; this.cdr.detectChanges(); }
    });
    this.cargar();
  }

  cargar() {
    this.loading = true;
    this.error   = '';
    this.cdr.detectChanges();

    this.svc.getAll({
      proveedorId: this.filtroProveedor || undefined,
      estado:      this.filtroEstado    || undefined,
      desde:       this.filtroDesde     || undefined,
      hasta:       this.filtroHasta     || undefined
    }).subscribe({
      next: r => {
        this.ordenes       = r.ordenes;
        this.cantidad      = r.cantidad;
        this.montoComprado = r.montoTotalComprado;
        this.loading       = false;
        this.cdr.detectChanges();
      },
      error: e => {
        this.error   = e.error?.message ?? 'Error al cargar las órdenes de compra';
        this.loading = false;
        this.cdr.detectChanges();
      }
    });
  }

  limpiarFiltros() {
    this.filtroProveedor = 0;
    this.filtroEstado    = '';
    this.filtroDesde     = '';
    this.filtroHasta     = '';
    this.cargar();
  }

  get hayFiltros(): boolean {
    return !!(this.filtroProveedor || this.filtroEstado || this.filtroDesde || this.filtroHasta);
  }

  get pendientes(): number {
    return this.ordenes.filter(o => o.estado === 'Pendiente').length;
  }

  // ── Permisos por estado y rol ─────────────────────────────────
  puedeAprobarORechazar(o: OrdenCompra) { return this.esAdmin && o.estado === 'Pendiente'; }
  puedeCancelar(o: OrdenCompra)         { return this.esAdmin && (o.estado === 'Pendiente' || o.estado === 'Aprobada'); }
  puedeRecibir(o: OrdenCompra)          { return o.estado === 'Aprobada'; }   // Admin y Almacenero

  // ── Nueva orden ───────────────────────────────────────────────
  abrirNueva() {
    this.nuevaAbierta = true;
    this.errorNueva   = '';
    this.guardando    = false;
    this.formNueva    = { proveedorId: 0, observaciones: '' };
    this.lineas       = [];
    this.asociados    = [];
    this.mostrarTodos = false;
    this.cdr.detectChanges();
  }

  cerrarNueva() {
    this.nuevaAbierta = false;
    this.cdr.detectChanges();
  }

  onProveedorChange() {
    this.lineas    = this.formNueva.proveedorId ? [this.lineaVacia()] : [];
    this.asociados = [];
    this.mostrarTodos = false;
    this.cdr.detectChanges();

    if (!this.formNueva.proveedorId) return;
    this.provSvc.getProductos(this.formNueva.proveedorId).subscribe({
      next: ps => { this.asociados = ps.map(p => p.productoId); this.cdr.detectChanges(); }
    });
  }

  private lineaVacia(): LineaForm {
    return { productoId: 0, cantidad: 1, precioUnitario: 0 };
  }

  agregarLinea() { this.lineas.push(this.lineaVacia()); }

  quitarLinea(i: number) { this.lineas.splice(i, 1); }

  // Si el proveedor tiene productos asociados se ofrecen solo esos (salvo "mostrar todos")
  get productosDisponibles(): Producto[] {
    if (this.mostrarTodos || this.asociados.length === 0) return this.productos;
    return this.productos.filter(p => this.asociados.includes(p.id));
  }

  get proveedorSinAsociados(): boolean {
    return !!this.formNueva.proveedorId && this.asociados.length === 0;
  }

  productoUsado(id: number, linea: LineaForm): boolean {
    return this.lineas.some(l => l !== linea && l.productoId === id);
  }

  onProductoChange(l: LineaForm) {
    const p = this.productos.find(x => x.id === Number(l.productoId));
    if (p) l.precioUnitario = p.precioCompra;
  }

  subtotal(l: LineaForm): number {
    return (Number(l.cantidad) || 0) * (Number(l.precioUnitario) || 0);
  }

  get totalNueva(): number {
    return this.lineas.reduce((acc, l) => acc + this.subtotal(l), 0);
  }

  crearOrden() {
    if (!this.formNueva.proveedorId) {
      this.errorNueva = 'Selecciona un proveedor';
      this.cdr.detectChanges();
      return;
    }
    if (this.lineas.length === 0 || this.lineas.some(l => !l.productoId)) {
      this.errorNueva = 'Selecciona un producto en cada línea (o quita las líneas vacías)';
      this.cdr.detectChanges();
      return;
    }
    if (this.lineas.some(l => !Number.isInteger(Number(l.cantidad)) || Number(l.cantidad) <= 0)) {
      this.errorNueva = 'Las cantidades deben ser números enteros mayores a cero';
      this.cdr.detectChanges();
      return;
    }
    if (this.lineas.some(l => !(Number(l.precioUnitario) > 0))) {
      this.errorNueva = 'Los precios unitarios deben ser mayores a cero';
      this.cdr.detectChanges();
      return;
    }

    this.guardando  = true;
    this.errorNueva = '';
    this.cdr.detectChanges();

    const dto: CrearOrdenCompraDto = {
      proveedorId:   Number(this.formNueva.proveedorId),
      observaciones: this.formNueva.observaciones.trim() || null,
      lineas: this.lineas.map(l => ({
        productoId:     Number(l.productoId),
        cantidad:       Number(l.cantidad),
        precioUnitario: Number(l.precioUnitario)
      }))
    };

    this.svc.crear(dto).subscribe({
      next: () => {
        this.nuevaAbierta = false;
        this.guardando    = false;
        this.cargar();
      },
      error: e => {
        this.errorNueva = e.error?.message ?? 'Error al crear la orden de compra';
        this.guardando  = false;
        this.cdr.detectChanges();
      }
    });
  }

  // ── Detalle ───────────────────────────────────────────────────
  abrirDetalle(o: OrdenCompra) {
    this.detalle = o;
    this.cdr.detectChanges();
  }

  cerrarDetalle() {
    this.detalle = null;
    this.cdr.detectChanges();
  }

  // ── Aprobar / rechazar / cancelar ─────────────────────────────
  abrirAccion(tipo: TipoAccion, o: OrdenCompra) {
    this.accion      = tipo;
    this.ordenAccion = o;
    this.motivo      = '';
    this.errorAccion = '';
    this.procesando  = false;
    this.cdr.detectChanges();
  }

  cerrarAccion() {
    this.accion      = null;
    this.ordenAccion = null;
    this.cdr.detectChanges();
  }

  get tituloAccion(): string {
    switch (this.accion) {
      case 'aprobar':  return 'Aprobar orden de compra';
      case 'rechazar': return 'Rechazar orden de compra';
      case 'cancelar': return 'Cancelar orden de compra';
      default:         return '';
    }
  }

  get textoBotonAccion(): string {
    if (this.procesando) return 'Procesando...';
    switch (this.accion) {
      case 'aprobar':  return 'Sí, aprobar';
      case 'rechazar': return 'Sí, rechazar';
      default:         return 'Sí, cancelar orden';
    }
  }

  confirmarAccion() {
    if (!this.accion || !this.ordenAccion) return;

    const motivo = this.motivo.trim();
    if (this.accion === 'cancelar' && !motivo) {
      this.errorAccion = 'Debes indicar el motivo de la cancelación';
      this.cdr.detectChanges();
      return;
    }

    const id = this.ordenAccion.id;
    let peticion: Observable<OrdenCompra>;
    switch (this.accion) {
      case 'aprobar':  peticion = this.svc.aprobar(id); break;
      case 'rechazar': peticion = this.svc.rechazar(id, motivo || null); break;
      default:         peticion = this.svc.cancelar(id, motivo); break;
    }

    this.procesando  = true;
    this.errorAccion = '';
    this.cdr.detectChanges();

    peticion.subscribe({
      next: actualizada => this.alActualizar(actualizada, () => this.cerrarAccion()),
      error: e => {
        this.errorAccion = e.error?.message ?? 'No se pudo completar la acción';
        this.procesando  = false;
        this.cdr.detectChanges();
      }
    });
  }

  // ── Recepción ─────────────────────────────────────────────────
  abrirRecibir(o: OrdenCompra) {
    this.ordenRecibir = o;
    this.errorRecepcion = '';
    this.procesando = false;
    // Por defecto se asume que llegó todo lo pedido
    this.lineasRecepcion = o.detalles.map(d => ({
      detalleId:      d.id,
      productoCodigo: d.productoCodigo,
      productoNombre: d.productoNombre,
      cantidad:       d.cantidad,
      recibida:       d.cantidad
    }));
    this.cdr.detectChanges();
  }

  cerrarRecibir() {
    this.ordenRecibir = null;
    this.cdr.detectChanges();
  }

  get hayDiferencia(): boolean {
    return this.lineasRecepcion.some(l => Number(l.recibida) !== l.cantidad);
  }

  confirmarRecepcion() {
    if (!this.ordenRecibir) return;

    for (const l of this.lineasRecepcion) {
      const r = Number(l.recibida);
      if (!Number.isInteger(r) || r < 0 || r > l.cantidad) {
        this.errorRecepcion =
          `${l.productoCodigo}: la cantidad recibida debe ser un entero entre 0 y ${l.cantidad}`;
        this.cdr.detectChanges();
        return;
      }
    }
    if (this.lineasRecepcion.every(l => Number(l.recibida) === 0)) {
      this.errorRecepcion = 'Debes recibir al menos una unidad. Si no llegó nada, cancela la orden';
      this.cdr.detectChanges();
      return;
    }

    this.procesando     = true;
    this.errorRecepcion = '';
    this.cdr.detectChanges();

    this.svc.recibir(
      this.ordenRecibir.id,
      this.lineasRecepcion.map(l => ({ detalleId: l.detalleId, cantidadRecibida: Number(l.recibida) }))
    ).subscribe({
      next: actualizada => this.alActualizar(actualizada, () => this.cerrarRecibir()),
      error: e => {
        this.errorRecepcion = e.error?.message ?? 'No se pudo registrar la recepción';
        this.procesando     = false;
        this.cdr.detectChanges();
      }
    });
  }

  private alActualizar(actualizada: OrdenCompra, cerrar: () => void) {
    this.procesando = false;
    cerrar();
    if (this.detalle?.id === actualizada.id) this.detalle = actualizada;
    this.cargar();
  }

  // ── Formato ───────────────────────────────────────────────────
  estadoClass(e: string): string {
    return 'badge-' + e.toLowerCase();
  }

  // Muestra la fecha en hora peruana (el backend guarda UTC)
  formatearFecha(fechaStr: string | null): string {
    if (!fechaStr) return '—';
    const fechaUtc = fechaStr.endsWith('Z') ? fechaStr : fechaStr + 'Z';
    const partes = new Intl.DateTimeFormat('es-PE', {
      timeZone: 'America/Lima',
      day: '2-digit', month: '2-digit', year: 'numeric',
      hour: '2-digit', minute: '2-digit', hour12: false
    }).formatToParts(new Date(fechaUtc));

    const get = (t: string) => partes.find(p => p.type === t)?.value ?? '';
    return `${get('day')}/${get('month')}/${get('year')} ${get('hour')}:${get('minute')}`;
  }
}
