import { Component, OnInit, inject, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ProductoService } from '../../shared/services/producto.service';
import { AuthService } from '../../core/auth.service';
import { Producto, Categoria, CrearProductoDto, ActualizarProductoDto } from '../../shared/models/producto.model';

type ModoModal = 'crear' | 'editar' | null;

@Component({
  selector: 'app-productos',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './productos.component.html'
})
export class ProductosComponent implements OnInit {
  private svc = inject(ProductoService);
  private cdr = inject(ChangeDetectorRef);

  // ── Estado de lista ───────────────────────────────────────────
  productos:  Producto[]  = [];
  categorias: Categoria[] = [];
  loading     = true;
  error       = '';
  busqueda    = '';
  filtroCat   = 0;

  // ── Estado del modal ──────────────────────────────────────────
  modoModal:    ModoModal = null;
  guardando     = false;
  errorModal    = '';
  productoEdit: Producto | null = null;

  form = {
    codigo:       '',
    nombre:       '',
    categoriaId:  0,
    precioCompra: 0,
    precioVenta:  0,
    stockActual:  0,
    stockMinimo:  0
  };

  // Solo el administrador modifica el catálogo; el vendedor no ve el costo de compra
  private auth = inject(AuthService);
  esAdmin    = this.auth.isAdmin();
  esVendedor = this.auth.tieneRol('Vendedor');

  get esEditar(): boolean {
    return this.modoModal === 'editar';
  }

  // ── Confirmar eliminar ─────────────────────────────────────────
  productoAEliminar: Producto | null = null;
  eliminando = false;

  ngOnInit() {
    this.cargarCategorias();
    this.cargarProductos();
  }

  cargarProductos() {
    this.loading  = true;
    this.error    = '';
    this.productos = [];
    this.cdr.detectChanges();

    this.svc.getAll(
      this.busqueda || undefined,
      this.filtroCat || undefined
    ).subscribe({
      next: p => {
        this.productos = p;
        this.loading   = false;
        this.cdr.detectChanges();
      },
      error: () => {
        this.error   = 'Error al cargar productos';
        this.loading = false;
        this.cdr.detectChanges();
      }
    });
  }

  cargarCategorias() {
    this.svc.getCategorias().subscribe({
      next: c => {
        this.categorias = c;
        this.cdr.detectChanges();
      }
    });
  }

  buscar() { this.cargarProductos(); }

  limpiarFiltros() {
    this.busqueda  = '';
    this.filtroCat = 0;
    this.cargarProductos();
  }

  // ── Modal Crear ───────────────────────────────────────────────
  abrirCrear() {
    this.modoModal  = 'crear';
    this.errorModal = '';
    this.form = { codigo: '', nombre: '', categoriaId: 0, precioCompra: 0, precioVenta: 0, stockActual: 0, stockMinimo: 0 };
    this.cdr.detectChanges();
  }

  // ── Modal Editar ──────────────────────────────────────────────
  abrirEditar(p: Producto) {
    this.modoModal    = 'editar';
    this.productoEdit = p;
    this.errorModal   = '';
    this.form = {
      codigo:       p.codigo,
      nombre:       p.nombre,
      categoriaId:  p.categoriaId,
      precioCompra: p.precioCompra,
      precioVenta:  p.precioVenta,
      stockActual:  p.stockActual,
      stockMinimo:  p.stockMinimo
    };
    this.cdr.detectChanges();
  }

  cerrarModal() {
    this.modoModal    = null;
    this.productoEdit = null;
    this.errorModal   = '';
    this.guardando    = false;
    this.cdr.detectChanges();
  }

  guardar() {
    if (!this.form.nombre || !this.form.categoriaId) {
      this.errorModal = 'Completa todos los campos obligatorios';
      this.cdr.detectChanges();
      return;
    }
    this.guardando  = true;
    this.errorModal = '';
    this.cdr.detectChanges();

    if (this.modoModal === 'crear') {
      if (!this.form.codigo) {
        this.errorModal = 'El código es obligatorio';
        this.guardando  = false;
        this.cdr.detectChanges();
        return;
      }
      const dto: CrearProductoDto = { ...this.form };
      this.svc.create(dto).subscribe({
        next: () => {
          this.cerrarModal();
          this.cargarProductos();
        },
        error: (e) => {
          this.errorModal = e.error?.message ?? 'Error al crear producto';
          this.guardando  = false;
          this.cdr.detectChanges();
        }
      });

    } else if (this.modoModal === 'editar' && this.productoEdit) {
      const dto: ActualizarProductoDto = {
        nombre:       this.form.nombre,
        categoriaId:  this.form.categoriaId,
        precioCompra: this.form.precioCompra,
        precioVenta:  this.form.precioVenta,
        stockMinimo:  this.form.stockMinimo
      };
      this.svc.update(this.productoEdit.id, dto).subscribe({
        next: () => {
          this.cerrarModal();
          this.cargarProductos();
        },
        error: (e) => {
          this.errorModal = e.error?.message ?? 'Error al actualizar producto';
          this.guardando  = false;
          this.cdr.detectChanges();
        }
      });
    }
  }

  // ── Eliminar ──────────────────────────────────────────────────
  confirmarEliminar(p: Producto) {
    this.productoAEliminar = p;
    this.cdr.detectChanges();
  }

  cancelarEliminar() {
    this.productoAEliminar = null;
    this.cdr.detectChanges();
  }

  eliminar() {
    if (!this.productoAEliminar) return;
    this.eliminando = true;
    this.cdr.detectChanges();

    this.svc.delete(this.productoAEliminar.id).subscribe({
      next: () => {
        this.productoAEliminar = null;
        this.eliminando        = false;
        this.cargarProductos();
      },
      error: () => {
        this.eliminando = false;
        this.cdr.detectChanges();
      }
    });
  }

  getTextoBoton(): string {
    if (this.guardando) return 'Guardando...';
    return this.modoModal === 'crear' ? 'Crear producto' : 'Guardar cambios';
  }
}