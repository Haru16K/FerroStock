import { Component, OnInit, inject, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AnalisisService } from '../../shared/services/analisis.service';
import { ReporteRotacion } from '../../shared/models/analisis.model';
import { ExportarBotonesComponent } from '../../shared/components/exportar-botones.component';
import { exportarReporte, FormatoExport, ReporteExport } from '../../shared/utils/exportar';
import { hoyLima, sumarDias, fechaCorta } from '../../shared/utils/fecha-lima';
import { mensajeError } from '../../shared/utils/errores';

@Component({
  selector: 'app-reportes-rotacion',
  standalone: true,
  imports: [CommonModule, FormsModule, ExportarBotonesComponent],
  templateUrl: './reportes-rotacion.component.html'
})
export class ReportesRotacionComponent implements OnInit {
  private svc = inject(AnalisisService);
  private cdr = inject(ChangeDetectorRef);

  desde = sumarDias(hoyLima(), -29);
  hasta = hoyLima();
  top   = 20;          // 0 = todos

  data: ReporteRotacion | null = null;
  loading = true;
  error   = '';

  ngOnInit() { this.generar(); }

  atajo(dias: number) {
    this.hasta = hoyLima();
    this.desde = sumarDias(this.hasta, -(dias - 1));
    this.generar();
  }

  generar() {
    if (!this.desde || !this.hasta) {
      this.error = 'Indica las fechas del período';
      this.cdr.detectChanges();
      return;
    }
    if (this.desde > this.hasta) {
      this.error = "La fecha 'desde' no puede ser posterior a 'hasta'";
      this.cdr.detectChanges();
      return;
    }

    this.loading = true;
    this.error   = '';
    this.cdr.detectChanges();

    this.svc.getRotacion(this.desde, this.hasta, Number(this.top) || undefined).subscribe({
      next: d => {
        this.data    = d;
        this.loading = false;
        this.cdr.detectChanges();
      },
      error: e => {
        this.error   = mensajeError(e, 'Error al generar el reporte de rotación');
        this.loading = false;
        this.cdr.detectChanges();
      }
    });
  }

  exportar(formato: FormatoExport) {
    if (!this.data) return;
    const reporte: ReporteExport = {
      titulo: 'Reporte de rotación de productos',
      filtros: [
        `Período: ${fechaCorta(this.data.desde)} al ${fechaCorta(this.data.hasta)} (${this.data.diasPeriodo} días)`,
        `Productos mostrados: ${this.top ? 'los ' + this.top + ' de mayor rotación' : 'todos'}`
      ],
      columnas: [
        { titulo: 'N°', formato: 'entero' },
        { titulo: 'Código' }, { titulo: 'Producto' }, { titulo: 'Categoría' },
        { titulo: 'Unid. vendidas', formato: 'entero' },
        { titulo: 'Stock actual', formato: 'entero' },
        { titulo: 'Stock promedio', formato: 'decimal' },
        { titulo: 'Rotación', formato: 'decimal' },
        { titulo: 'Cobertura (días)', formato: 'decimal' }
      ],
      filas: this.data.productos.map((p, i) => [
        i + 1, p.codigo, p.nombre, p.categoria, p.unidadesVendidas, p.stockActual,
        p.stockPromedio, p.rotacion, p.diasCobertura ?? 'Sin ventas'
      ]),
      nombreArchivo: `rotacion_${this.data.desde}_${this.data.hasta}`
    };
    exportarReporte(formato, reporte);
  }
}
