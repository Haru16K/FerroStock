import { Component, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth.service';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './login.component.html'
})
export class LoginComponent {
  email    = '';
  password = '';
  error    = '';
  loading  = false;

  constructor(private auth: AuthService, private router: Router, private cdr: ChangeDetectorRef) {
    // Si ya está logueado, ir a la pantalla de inicio de su rol
    if (this.auth.isLoggedIn()) this.router.navigate(['/']);
  }

  onSubmit() {
    if (!this.email || !this.password) {
      this.error = 'Completa todos los campos';
      this.cdr.detectChanges();
      return;
    }
    this.loading = true;
    this.error   = '';
    this.cdr.detectChanges();

    this.auth.login({ email: this.email, password: this.password })
      .subscribe({
        next: () => this.router.navigate(['/']),
        error: (err) => {
          this.error   = err.status === 401
            ? 'Correo o contraseña incorrectos'
            : 'Error al conectar con el servidor';
          this.loading = false;
          this.cdr.detectChanges();
        }
      });
  }
}