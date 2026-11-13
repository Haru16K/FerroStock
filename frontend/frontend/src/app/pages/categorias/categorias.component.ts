import { Component, OnInit, inject, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { CategoriaService } from '../../shared/services/categoria.service';
import { Categoria } from '../../shared/models/producto.model';

type ModoModal = 'crear' | 'editar' | null;

@Component({
  selector: 'app-categorias',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './categorias.component.html'
})
export class CategoriasComponent implements OnInit {
  private svc = inject(CategoriaService);
  private cdr = inject(ChangeDetectorRef);

  categorias: Categoria[] = [];
  loading = true;
  error   = '';

  modoModal: ModoModal = null;
  categoriaEdit: Categoria | null = null;
  guardando  = false;
  errorModal = '';
  form = { nombre: '', descripcion: '' };

  ngOnInit() { this.cargar(); }

  cargar() {
    this.loading = true;
    this.error   = '';
    this.cdr.detectChanges();

    this.svc.getAll().subscribe({
      next: c => {
        this.categorias = c;
        this.loading = false;
        this.cdr.detectChanges();
      },
      error: () => {
        this.error   = 'Error al cargar las categorías';
        this.loading = false;
        this.cdr.detectChanges();
      }
    });
  }

  abrirCrear() {
    this.modoModal     = 'crear';
    this.categoriaEdit = null;
    this.errorModal    = '';
    this.form = { nombre: '', descripcion: '' };
    this.cdr.detectChanges();
  }

  abrirEditar(c: Categoria) {
    this.modoModal     = 'editar';
    this.categoriaEdit = c;
    this.errorModal    = '';
    this.form = { nombre: c.nombre, descripcion: c.descripcion ?? '' };
    this.cdr.detectChanges();
  }

  cerrarModal() {
    this.modoModal     = null;
    this.categoriaEdit = null;
    this.guardando     = false;
    this.cdr.detectChanges();
  }

  guardar() {
    const nombre = this.form.nombre.trim();
    if (!nombre) {
      this.errorModal = 'El nombre de la categoría es obligatorio';
      this.cdr.detectChanges();
      return;
    }

    this.guardando  = true;
    this.errorModal = '';
    this.cdr.detectChanges();

    const dto = { nombre, descripcion: this.form.descripcion.trim() || null };
    const peticion = this.modoModal === 'editar' && this.categoriaEdit
      ? this.svc.update(this.categoriaEdit.id, dto)
      : this.svc.create(dto);

    peticion.subscribe({
      next: () => {
        this.cerrarModal();
        this.cargar();
      },
      error: e => {
        this.errorModal = e.error?.message ?? 'Error al guardar la categoría';
        this.guardando  = false;
        this.cdr.detectChanges();
      }
    });
  }
}
