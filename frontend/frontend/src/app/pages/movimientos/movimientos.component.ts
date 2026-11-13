import { Component, OnInit, inject, ChangeDetectorRef } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MovimientoService } from '../../shared/services/movimiento.service';
import { ProductoService } from '../../shared/services/producto.service';
import { Movimiento, RegistrarEntradaDto, RegistrarSalidaDto } from '../../shared/models/movimiento.model';
import { Producto } from '../../shared/models/producto.model';
import { hoyLima, formatearFechaLima } from '../../shared/utils/fecha-lima';

type TipoModal = 'entrada' | 'salida' | null;

@Component({
  selector: 'app-movimientos',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './movimientos.component.html',
})
export class MovimientosComponent implements OnInit {
  private svc     = inject(MovimientoService);
  private prodSvc = inject(ProductoService);
  private cdr     = inject(ChangeDetectorRef);
  private route   = inject(ActivatedRoute);
  private router  = inject(Router);

  // ── Lista (siempre del día actual) ──────────────────────────────
  movimientos: Movimiento[] = [];
  productos:   Producto[]   = [];
  loading  = true;
  error    = '';

  // ── Modal ─────────────────────────────────────────────────────
  tipoModal:  TipoModal = null;
  guardando   = false;
  errorModal  = '';

  formEntrada = {
    productoId:     0,
    cantidad:       1,
    proveedor:      '',
    precioUnitario: 0
  };

  formSalida = {
    productoId: 0,
    cantidad:   1,
    motivo:     ''
  };

  ngOnInit() {
    this.cargarProductos();
    this.cargarMovimientosDeHoy();

    // Viene del botón "Nueva entrada" del dashboard
    if (this.route.snapshot.queryParamMap.get('nuevo') === 'entrada') {
      this.abrirEntrada();
      // Se quita el parámetro para que recargar la página no vuelva a abrir el formulario
      this.router.navigate([], { relativeTo: this.route, queryParams: {}, replaceUrl: true });
    }
  }

  cargarMovimientosDeHoy() {
    this.loading     = true;
    this.error       = '';
    this.movimientos = [];
    this.cdr.detectChanges();

    const hoy = this.hoy();
    this.svc.getByFechas(hoy, hoy).subscribe({
      next: m => {
        this.movimientos = m;
        this.loading     = false;
        this.cdr.detectChanges();
      },
      error: () => {
        this.error   = 'Error al cargar los movimientos de hoy';
        this.loading = false;
        this.cdr.detectChanges();
      }
    });
  }

  cargarProductos() {
    this.prodSvc.getAll().subscribe({
      next: p => { this.productos = p; this.cdr.detectChanges(); }
    });
  }

  // ── Modal entrada ─────────────────────────────────────────────
  abrirEntrada() {
    this.tipoModal  = 'entrada';
    this.errorModal = '';
    this.formEntrada = { productoId: 0, cantidad: 1, proveedor: '', precioUnitario: 0 };
    this.cdr.detectChanges();
  }

  // ── Modal salida ──────────────────────────────────────────────
  abrirSalida() {
    this.tipoModal  = 'salida';
    this.errorModal = '';
    this.formSalida = { productoId: 0, cantidad: 1, motivo: '' };
    this.cdr.detectChanges();
  }

  cerrarModal() {
    this.tipoModal  = null;
    this.guardando  = false;
    this.errorModal = '';
    this.cdr.detectChanges();
  }

  guardar() {
    this.guardando  = true;
    this.errorModal = '';
    this.cdr.detectChanges();

    if (this.tipoModal === 'entrada') {
      if (!this.formEntrada.productoId || !this.formEntrada.proveedor) {
        this.errorModal = 'Completa todos los campos obligatorios';
        this.guardando  = false;
        this.cdr.detectChanges();
        return;
      }
      const dto: RegistrarEntradaDto = { ...this.formEntrada };
      this.svc.registrarEntrada(dto).subscribe({
        next: () => { this.cerrarModal(); this.cargarMovimientosDeHoy(); },
        error: e => {
          this.errorModal = e.error?.message ?? 'Error al registrar entrada';
          this.guardando  = false;
          this.cdr.detectChanges();
        }
      });

    } else if (this.tipoModal === 'salida') {
      if (!this.formSalida.productoId || !this.formSalida.motivo) {
        this.errorModal = 'Completa todos los campos obligatorios';
        this.guardando  = false;
        this.cdr.detectChanges();
        return;
      }
      const dto: RegistrarSalidaDto = { ...this.formSalida };
      this.svc.registrarSalida(dto).subscribe({
        next: () => { this.cerrarModal(); this.cargarMovimientosDeHoy(); },
        error: e => {
          this.errorModal = e.error?.message ?? 'Error al registrar salida';
          this.guardando  = false;
          this.cdr.detectChanges();
        }
      });
    }
  }

  getStockProducto(id: number): number {
    return this.productos.find(p => p.id === id)?.stockActual ?? 0;
  }

  hoy(): string {
    return hoyLima();
  }

  get totalEntradas() { return this.movimientos.filter(m => m.tipo === 'Entrada').length; }
  get totalSalidas()  { return this.movimientos.filter(m => m.tipo === 'Salida').length;  }

  // Formatea fecha ajustando UTC a hora peruana (UTC-5)
  formatearFecha(fechaStr: string): string {
    return formatearFechaLima(fechaStr);
  }

  esCriticoProducto(id: number): boolean {
    const p = this.productos.find(p => p.id === id);
    return p ? p.stockActual <= p.stockMinimo : false;
  }
}