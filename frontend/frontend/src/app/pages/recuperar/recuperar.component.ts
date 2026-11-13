import { Component, ChangeDetectorRef, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth.service';

@Component({
  selector: 'app-recuperar',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './recuperar.component.html'
})
export class RecuperarComponent {
  email   = '';
  error   = '';
  mensaje = '';
  loading = false;

  private cdr = inject(ChangeDetectorRef);

  constructor(private auth: AuthService) {}

  enviar() {
    if (!this.email.trim()) {
      this.error = 'Ingresa tu correo electrónico';
      this.cdr.detectChanges();
      return;
    }
    this.loading = true;
    this.error   = '';
    this.cdr.detectChanges();

    this.auth.olvidePassword(this.email.trim()).subscribe({
      next: r => {
        this.mensaje = r.message;   // misma respuesta exista o no la cuenta
        this.loading = false;
        this.cdr.detectChanges();
      },
      error: e => {
        this.error   = e.error?.message ?? 'Error al conectar con el servidor';
        this.loading = false;
        this.cdr.detectChanges();
      }
    });
  }
}
