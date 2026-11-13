import { Component, OnInit, inject, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { UsuarioService } from '../../shared/services/usuario.service';
import { Usuario, CrearUsuarioDto } from '../../shared/models/usuario.model';
import { AuthService } from '../../core/auth.service';

@Component({
  selector: 'app-usuarios',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './usuarios.component.html',
})
export class UsuariosComponent implements OnInit {
  private svc  = inject(UsuarioService);
  private auth = inject(AuthService);
  private cdr  = inject(ChangeDetectorRef);

  readonly roles = ['Administrador', 'Almacenero', 'Vendedor', 'Gerencia'];
  errorRol = '';

  usuarios:  Usuario[] = [];
  loading    = true;
  error      = '';

  // Modal crear
  modalAbierto = false;
  guardando    = false;
  errorModal   = '';
  mostrarPass  = false;

  form: CrearUsuarioDto = {
    nombre:   '',
    email:    '',
    password: '',
    rol:      'Almacenero'
  };

  // Confirmar desactivar
  usuarioADesactivar: Usuario | null = null;
  desactivando = false;

  // ID del usuario actual (no puede desactivarse a sí mismo)
  usuarioActualId = this.auth.getUser()?.usuarioId ?? 0;

  ngOnInit() { this.cargar(); }

  cargar() {
    this.loading  = true;
    this.error    = '';
    this.usuarios = [];
    this.cdr.detectChanges();

    this.svc.getAll().subscribe({
      next: u => {
        this.usuarios = u;
        this.loading  = false;
        this.cdr.detectChanges();
      },
      error: () => {
        this.error   = 'Error al cargar usuarios';
        this.loading = false;
        this.cdr.detectChanges();
      }
    });
  }

  // ── Modal crear ───────────────────────────────────────────────
  abrirModal() {
    this.modalAbierto = true;
    this.errorModal   = '';
    this.mostrarPass  = false;
    this.form = { nombre: '', email: '', password: '', rol: 'Almacenero' };
    this.cdr.detectChanges();
  }

  cerrarModal() {
    this.modalAbierto = false;
    this.guardando    = false;
    this.errorModal   = '';
    this.cdr.detectChanges();
  }

  guardar() {
    if (!this.form.nombre || !this.form.email || !this.form.password) {
      this.errorModal = 'Completa todos los campos';
      this.cdr.detectChanges();
      return;
    }
    if (this.form.password.length < 6) {
      this.errorModal = 'La contraseña debe tener al menos 6 caracteres';
      this.cdr.detectChanges();
      return;
    }

    this.guardando  = true;
    this.errorModal = '';
    this.cdr.detectChanges();

    this.svc.create(this.form).subscribe({
      next: () => { this.cerrarModal(); this.cargar(); },
      error: e => {
        this.errorModal = e.error?.message ?? 'Error al crear usuario';
        this.guardando  = false;
        this.cdr.detectChanges();
      }
    });
  }

  // ── Cambiar rol (HU-17) ───────────────────────────────────────
  cambiarRol(u: Usuario, nuevoRol: string) {
    if (nuevoRol === u.rol) return;
    this.errorRol = '';

    this.svc.cambiarRol(u.id, nuevoRol).subscribe({
      next: actualizado => {
        u.rol = actualizado.rol;
        this.cdr.detectChanges();
      },
      error: e => {
        this.errorRol = e.error?.message ?? 'No se pudo cambiar el rol';
        this.cargar();   // restaura el valor real en la lista
      }
    });
  }

  // ── Desactivar ────────────────────────────────────────────────
  confirmarDesactivar(u: Usuario) {
    this.usuarioADesactivar = u;
    this.cdr.detectChanges();
  }

  cancelarDesactivar() {
    this.usuarioADesactivar = null;
    this.cdr.detectChanges();
  }

  desactivar() {
    if (!this.usuarioADesactivar) return;
    this.desactivando = true;
    this.cdr.detectChanges();

    this.svc.desactivar(this.usuarioADesactivar.id).subscribe({
      next: () => {
        this.usuarioADesactivar = null;
        this.desactivando       = false;
        this.cargar();
      },
      error: () => {
        this.desactivando = false;
        this.cdr.detectChanges();
      }
    });
  }

  // ── Helpers ───────────────────────────────────────────────────
  getIniciales(nombre: string): string {
    return nombre.split(' ').map(n => n[0]).join('').substring(0, 2).toUpperCase();
  }

  get totalAdmins()      { return this.usuarios.filter(u => u.rol === 'Administrador' && u.activo).length; }
  get totalAlmaceneros() { return this.usuarios.filter(u => u.rol === 'Almacenero'    && u.activo).length; }
  get totalVendedores()  { return this.usuarios.filter(u => u.rol === 'Vendedor'      && u.activo).length; }
  get totalGerencia()    { return this.usuarios.filter(u => u.rol === 'Gerencia'      && u.activo).length; }
  get totalActivos()     { return this.usuarios.filter(u => u.activo).length; }
}