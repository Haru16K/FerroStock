import { Component, OnInit, inject, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ReporteService } from '../../shared/services/reporte.service';
import { ResumenMovimientos } from '../../shared/models/reporte.model';
import { hoyLima, primerDiaMesLima, formatearFechaLima, fechaCorta } from '../../shared/utils/fecha-lima';
import { ExportarBotonesComponent } from '../../shared/components/exportar-botones.component';
import { exportarReporte, FormatoExport, ReporteExport } from '../../shared/utils/exportar';

@Component({
  selector: 'app-reportes-historial',
  standalone: true,
  imports: [CommonModule, FormsModule, ExportarBotonesComponent],
  templateUrl: './reportes-historial.component.html'
})
export class ReportesHistorialComponent implements OnInit {
  private svc = inject(ReporteService);
  private cdr = inject(ChangeDetectorRef);

  resumen: ResumenMovimientos | null = null;
  loading = true;
  error   = '';

  fechaDesde = this.primerDiaMes();
  fechaHasta = this.hoy();

  ngOnInit() {
    this.generar();
  }

  generar() {
    this.loading = true;
    this.error   = '';
    this.resumen = null;
    this.cdr.detectChanges();

    this.svc.getMovimientos(this.fechaDesde, this.fechaHasta).subscribe({
      next: r => {
        this.resumen = r;
        this.loading = false;
        this.cdr.detectChanges();
      },
      error: () => {
        this.error   = 'Error al generar el reporte de historial';
        this.loading = false;
        this.cdr.detectChanges();
      }
    });
  }

  exportar(formato: FormatoExport) {
    const r = this.resumen;
    if (!r) return;
    const reporte: ReporteExport = {
      titulo: 'Reporte de historial de movimientos',
      filtros: [
        `Período: ${fechaCorta(this.fechaDesde)} al ${fechaCorta(this.fechaHasta)}`,
        `Entradas: ${r.totalEntradas} unidades · Salidas: ${r.totalSalidas} unidades`
      ],
      columnas: [
        { titulo: 'Fecha' }, { titulo: 'Producto' }, { titulo: 'Código' }, { titulo: 'Tipo' },
        { titulo: 'Cantidad', formato: 'entero' }, { titulo: 'Proveedor / motivo' },
        { titulo: 'Precio unit.', formato: 'moneda' }, { titulo: 'Usuario' }
      ],
      filas: r.movimientos.map(m => [
        formatearFechaLima(m.fecha, true), m.productoNombre, m.productoCodigo, m.tipo,
        m.cantidad, m.proveedor || m.motivo || '', m.precioUnitario ?? null, m.usuarioNombre
      ]),
      nombreArchivo: `historial_${this.fechaDesde}_${this.fechaHasta}`
    };
    exportarReporte(formato, reporte);
  }

  primerDiaMes(): string {
    return primerDiaMesLima();
  }

  hoy(): string {
    return hoyLima();
  }

  // Ajusta UTC a hora peruana (UTC-5)
  formatearFecha(fechaStr: string): string {
    return formatearFechaLima(fechaStr);
  }
}