import { Component, OnInit, inject, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ReporteService } from '../../shared/services/reporte.service';
import { MovimientoService } from '../../shared/services/movimiento.service';
import { ProductoCritico } from '../../shared/models/reporte.model';

@Component({
  selector: 'app-alertas',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './alertas.component.html'
})
export class AlertasComponent implements OnInit {
  private svc    = inject(ReporteService);
  private movSvc = inject(MovimientoService);
  private cdr    = inject(ChangeDetectorRef);

  criticos: ProductoCritico[] = [];
  loading = true;
  error   = '';

  // ── Modal de reposición rápida ───────────────────────────────
  productoReponer: ProductoCritico | null = null;
  formReponer = { cantidad: 0, proveedor: '', precioUnitario: 0 };
  guardando  = false;
  errorModal = '';

  ngOnInit() {
    this.cargar();
  }

  cargar() {
    this.loading = true;
    this.error   = '';
    this.cdr.detectChanges();

    this.svc.getCriticos().subscribe({
      next: c => {
        this.criticos = [...c].sort((a, b) => b.deficit - a.deficit);
        this.loading  = false;
        this.cdr.detectChanges();
      },
      error: () => {
        this.error   = 'Error al cargar las alertas de stock';
        this.loading = false;
        this.cdr.detectChanges();
      }
    });
  }

  get deficitTotal(): number {
    return this.criticos.reduce((sum, p) => sum + p.deficit, 0);
  }

  // ── Reposición rápida ─────────────────────────────────────────
  abrirReponer(p: ProductoCritico) {
    this.productoReponer = p;
    this.errorModal = '';
    // Sugiere cubrir el déficit + un pequeño colchón sobre el mínimo
    this.formReponer = { cantidad: p.deficit, proveedor: '', precioUnitario: 0 };
    this.cdr.detectChanges();
  }

  cerrarModal() {
    this.productoReponer = null;
    this.guardando  = false;
    this.errorModal = '';
    this.cdr.detectChanges();
  }

  confirmarReposicion() {
    if (!this.productoReponer) return;
    if (!this.formReponer.cantidad || !this.formReponer.proveedor) {
      this.errorModal = 'Completa cantidad y proveedor';
      this.cdr.detectChanges();
      return;
    }
    this.guardando  = true;
    this.errorModal = '';
    this.cdr.detectChanges();

    this.movSvc.registrarEntrada({
      productoId:     this.productoReponer.id,
      cantidad:       this.formReponer.cantidad,
      proveedor:      this.formReponer.proveedor,
      precioUnitario: this.formReponer.precioUnitario
    }).subscribe({
      next: () => {
        this.cerrarModal();
        this.cargar(); // el producto repuesto desaparece solo si ya no es crítico
      },
      error: e => {
        this.errorModal = e.error?.message ?? 'Error al registrar la entrada';
        this.guardando  = false;
        this.cdr.detectChanges();
      }
    });
  }
}