import { Component, OnInit, ChangeDetectorRef, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth.service';

@Component({
  selector: 'app-restablecer',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './restablecer.component.html'
})
export class RestablecerComponent implements OnInit {
  token     = '';
  nueva     = '';
  confirmar = '';
  error     = '';
  exito     = '';
  loading   = false;

  private cdr = inject(ChangeDetectorRef);

  constructor(
    private auth: AuthService,
    private route: ActivatedRoute,
    private router: Router
  ) {}

  ngOnInit() {
    this.token = this.route.snapshot.queryParamMap.get('token') ?? '';
    if (this.token) {
      // El token se guarda solo en memoria: se quita de la barra de direcciones y del historial
      this.router.navigate([], { relativeTo: this.route, queryParams: {}, replaceUrl: true });
    }
  }

  guardar() {
    if (this.nueva.length < 8) {
      this.error = 'La contraseña debe tener al menos 8 caracteres';
      this.cdr.detectChanges();
      return;
    }
    if (this.nueva !== this.confirmar) {
      this.error = 'Las contraseñas no coinciden';
      this.cdr.detectChanges();
      return;
    }

    this.loading = true;
    this.error   = '';
    this.cdr.detectChanges();

    this.auth.restablecerPassword(this.token, this.nueva).subscribe({
      next: r => {
        this.exito   = r.message;
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
