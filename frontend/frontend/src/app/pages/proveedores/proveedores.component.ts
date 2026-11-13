import { Component, OnInit, inject, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { forkJoin } from 'rxjs';
import { ProveedorService } from '../../shared/services/proveedor.service';
import { ProductoService } from '../../shared/services/producto.service';
import { AuthService } from '../../core/auth.service';
import { Proveedor } from '../../shared/models/proveedor.model';
import { Producto } from '../../shared/models/producto.model';

type ModoModal = 'crear' | 'editar' | null;

@Component({
  selector: 'app-proveedores',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './proveedores.component.html'
})
export class ProveedoresComponent implements OnInit {
  private svc     = inject(ProveedorService);
  private prodSvc = inject(ProductoService);
  private auth    = inject(AuthService);
  private cdr     = inject(ChangeDetectorRef);

  esAdmin = this.auth.isAdmin();

  // ── Lista ─────────────────────────────────────────────────────
  proveedores: Proveedor[] = [];
  loading  = true;
  error    = '';
  busqueda = '';

  // ── Modal crear / editar ──────────────────────────────────────
  modoModal: ModoModal = null;
  guardando  = false;
  errorModal = '';
  proveedorEdit: Proveedor | null = null;

  form = {
    ruc: '', razonSocial: '', contacto: '', telefono: '', email: '', direccion: '',
    diasEntrega: 7, calificacion: '' as string | number
  };

  // ── Desactivar ────────────────────────────────────────────────
  proveedorADesactivar: Proveedor | null = null;
  desactivando = false;

  // ── Modal productos asociados ─────────────────────────────────
  proveedorProd: Proveedor | null = null;
  todosProductos: Producto[] = [];
  seleccionados: number[] = [];
  busquedaProd = '';
  cargandoProd = false;
  guardandoProd = false;
  errorProd = '';

  ngOnInit() {
    this.cargar();
  }

  cargar() {
    this.loading = true;
    this.error   = '';
    this.proveedores = [];
    this.cdr.detectChanges();

    this.svc.getAll(this.busqueda || undefined).subscribe({
      next: p => {
        this.proveedores = p;
        this.loading = false;
        this.cdr.detectChanges();
      },
      error: () => {
        this.error   = 'Error al cargar proveedores';
        this.loading = false;
        this.cdr.detectChanges();
      }
    });
  }

  buscar() { this.cargar(); }

  limpiar() {
    this.busqueda = '';
    this.cargar();
  }

  // ── Crear / editar ────────────────────────────────────────────
  abrirCrear() {
    this.modoModal  = 'crear';
    this.errorModal = '';
    this.proveedorEdit = null;
    this.form = { ruc: '', razonSocial: '', contacto: '', telefono: '', email: '', direccion: '', diasEntrega: 7, calificacion: '' };
    this.cdr.detectChanges();
  }

  abrirEditar(p: Proveedor) {
    this.modoModal     = 'editar';
    this.proveedorEdit = p;
    this.errorModal    = '';
    this.form = {
      ruc:         p.ruc,
      razonSocial: p.razonSocial,
      contacto:    p.contacto  ?? '',
      telefono:    p.telefono  ?? '',
      email:       p.email     ?? '',
      direccion:   p.direccion ?? '',
      diasEntrega: p.diasEntrega,
      calificacion: p.calificacion ?? ''
    };
    this.cdr.detectChanges();
  }

  cerrarModal() {
    this.modoModal     = null;
    this.proveedorEdit = null;
    this.errorModal    = '';
    this.guardando     = false;
    this.cdr.detectChanges();
  }

  guardar() {
    const ruc = this.form.ruc.trim();
    if (!/^\d{11}$/.test(ruc)) {
      this.errorModal = 'El RUC debe tener exactamente 11 dígitos numéricos';
      this.cdr.detectChanges();
      return;
    }
    const dias = Number(this.form.diasEntrega);
    if (!Number.isInteger(dias) || dias < 1 || dias > 180) {
      this.errorModal = 'Los días de entrega deben ser un número entero entre 1 y 180';
      this.cdr.detectChanges();
      return;
    }
    const calif = this.form.calificacion === '' || this.form.calificacion === null ? null : Number(this.form.calificacion);
    if (calif !== null && (Number.isNaN(calif) || calif < 1 || calif > 5)) {
      this.errorModal = 'La calificación debe estar entre 1 y 5 (o déjala vacía si aún no lo calificas)';
      this.cdr.detectChanges();
      return;
    }
    if (!this.form.razonSocial.trim()) {
      this.errorModal = 'La razón social es obligatoria';
      this.cdr.detectChanges();
      return;
    }

    this.guardando  = true;
    this.errorModal = '';
    this.cdr.detectChanges();

    const dto = {
      ruc,
      razonSocial: this.form.razonSocial.trim(),
      contacto:    this.form.contacto.trim()  || null,
      telefono:    this.form.telefono.trim()  || null,
      email:       this.form.email.trim()     || null,
      direccion:   this.form.direccion.trim() || null,
      diasEntrega: dias,
      calificacion: calif
    };

    const peticion = this.modoModal === 'editar' && this.proveedorEdit
      ? this.svc.update(this.proveedorEdit.id, dto)
      : this.svc.create(dto);

    peticion.subscribe({
      next: () => {
        this.cerrarModal();
        this.cargar();
      },
      error: e => {
        this.errorModal = e.error?.message ?? 'Error al guardar el proveedor';
        this.guardando  = false;
        this.cdr.detectChanges();
      }
    });
  }

  // ── Desactivar ────────────────────────────────────────────────
  confirmarDesactivar(p: Proveedor) {
    this.proveedorADesactivar = p;
    this.cdr.detectChanges();
  }

  cancelarDesactivar() {
    this.proveedorADesactivar = null;
    this.cdr.detectChanges();
  }

  desactivar() {
    if (!this.proveedorADesactivar) return;
    this.desactivando = true;
    this.cdr.detectChanges();

    this.svc.delete(this.proveedorADesactivar.id).subscribe({
      next: () => {
        this.proveedorADesactivar = null;
        this.desactivando = false;
        this.cargar();
      },
      error: () => {
        this.desactivando = false;
        this.cdr.detectChanges();
      }
    });
  }

  // ── Productos asociados ───────────────────────────────────────
  abrirProductos(p: Proveedor) {
    this.proveedorProd = p;
    this.busquedaProd  = '';
    this.errorProd     = '';
    this.cargandoProd  = true;
    this.seleccionados = [];
    this.todosProductos = [];
    this.cdr.detectChanges();

    forkJoin({
      productos: this.prodSvc.getAll(),
      asociados: this.svc.getProductos(p.id)
    }).subscribe({
      next: ({ productos, asociados }) => {
        this.todosProductos = productos;
        this.seleccionados  = asociados.map(a => a.productoId);
        this.cargandoProd   = false;
        this.cdr.detectChanges();
      },
      error: () => {
        this.errorProd    = 'Error al cargar los productos';
        this.cargandoProd = false;
        this.cdr.detectChanges();
      }
    });
  }

  cerrarProductos() {
    this.proveedorProd = null;
    this.guardandoProd = false;
    this.errorProd     = '';
    this.cdr.detectChanges();
  }

  get productosFiltrados(): Producto[] {
    const q = this.busquedaProd.trim().toLowerCase();
    if (!q) return this.todosProductos;
    return this.todosProductos.filter(p =>
      p.nombre.toLowerCase().includes(q) || p.codigo.toLowerCase().includes(q));
  }

  estaSeleccionado(id: number): boolean {
    return this.seleccionados.includes(id);
  }

  toggleProducto(id: number) {
    if (!this.esAdmin) return;
    this.seleccionados = this.estaSeleccionado(id)
      ? this.seleccionados.filter(x => x !== id)
      : [...this.seleccionados, id];
    this.cdr.detectChanges();
  }

  guardarProductos() {
    if (!this.proveedorProd) return;
    this.guardandoProd = true;
    this.errorProd     = '';
    this.cdr.detectChanges();

    this.svc.asociarProductos(this.proveedorProd.id, this.seleccionados).subscribe({
      next: () => {
        this.cerrarProductos();
        this.cargar();
      },
      error: e => {
        this.errorProd     = e.error?.message ?? 'Error al guardar los productos';
        this.guardandoProd = false;
        this.cdr.detectChanges();
      }
    });
  }
}
