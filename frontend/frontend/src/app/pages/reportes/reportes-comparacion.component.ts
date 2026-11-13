import { Component, OnInit, OnDestroy, inject, ChangeDetectorRef, viewChild, ElementRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AnalisisService } from '../../shared/services/analisis.service';
import { CategoriaService } from '../../shared/services/categoria.service';
import { ComparacionMensual } from '../../shared/models/analisis.model';
import { Categoria } from '../../shared/models/producto.model';
import { ExportarBotonesComponent } from '../../shared/components/exportar-botones.component';
import { exportarReporte, FormatoExport, ReporteExport } from '../../shared/utils/exportar';
import { mesActualLima, fechaCorta, nombreMes } from '../../shared/utils/fecha-lima';
import { mensajeError } from '../../shared/utils/errores';

declare const Chart: any;   // Chart.js se carga por CDN en index.html

@Component({
  selector: 'app-reportes-comparacion',
  standalone: true,
  imports: [CommonModule, FormsModule, ExportarBotonesComponent],
  templateUrl: './reportes-comparacion.component.html'
})
export class ReportesComparacionComponent implements OnInit, OnDestroy {
  private svc    = inject(AnalisisService);
  private catSvc = inject(CategoriaService);
  private cdr    = inject(ChangeDetectorRef);
  private lienzo = viewChild<ElementRef<HTMLCanvasElement>>('cCmp');

  readonly mesMaximo = mesActualLima();
  readonly fechaCorta = fechaCorta;
  readonly nombreMes  = nombreMes;

  mes = mesActualLima();
  categoriaId = 0;
  categorias: Categoria[] = [];

  data: ComparacionMensual | null = null;
  loading = true;
  error   = '';
  private chart: any = null;

  ngOnInit() {
    this.catSvc.getAll().subscribe({
      next: c => { this.categorias = c; this.cdr.detectChanges(); }
    });
    this.generar();
  }

  ngOnDestroy() { this.chart?.destroy(); }

  generar() {
    if (!this.mes) {
      this.error = 'Selecciona un mes';
      this.cdr.detectChanges();
      return;
    }
    this.loading = true;
    this.error   = '';
    this.cdr.detectChanges();

    this.svc.getComparacionMensual(this.mes, Number(this.categoriaId) || undefined).subscribe({
      next: d => {
        this.data    = d;
        this.loading = false;
        this.cdr.detectChanges();
        setTimeout(() => this.dibujar(), 0);
      },
      error: e => {
        this.error   = mensajeError(e, 'Error al generar la comparación');
        this.loading = false;
        this.cdr.detectChanges();
      }
    });
  }

  private dibujar() {
    this.chart?.destroy();
    const canvas = this.lienzo()?.nativeElement;
    if (!canvas || !this.data) return;

    this.chart = new Chart(canvas.getContext('2d'), {
      type: 'bar',
      data: {
        labels: this.data.porCategoria.map(c => c.categoria),
        datasets: [
          { label: 'Mes anterior', data: this.data.porCategoria.map(c => c.montoAnterior), backgroundColor: '#D3D1C7', borderRadius: 4 },
          { label: 'Mes actual',   data: this.data.porCategoria.map(c => c.montoActual),   backgroundColor: '#D95F2B', borderRadius: 4 }
        ]
      },
      options: {
        responsive: true, maintainAspectRatio: false,
        plugins: {
          legend: { labels: { font: { size: 11 }, boxWidth: 10, boxHeight: 10 } },
          tooltip: { callbacks: { label: (ctx: any) => `${ctx.dataset.label}: S/ ${Number(ctx.parsed.y).toFixed(2)}` } }
        },
        scales: {
          x: { grid: { display: false }, ticks: { font: { size: 10 }, color: '#6B6963' } },
          y: { grid: { color: '#E2E0D8' }, ticks: { font: { size: 10 }, color: '#6B6963' }, beginAtZero: true }
        }
      }
    });
  }

  // Variación porcentual: null = el período anterior no tuvo ventas (no hay base)
  textoVar(v: number | null): string {
    if (v === null) return 'Sin base';
    if (v === 0)    return '0%';
    return (v > 0 ? '▲ +' : '▼ ') + v.toFixed(1) + '%';
  }

  claseVar(v: number | null): string {
    if (v === null || v === 0) return 'var-igual';
    return v > 0 ? 'var-sube' : 'var-baja';
  }

  exportar(formato: FormatoExport) {
    const d = this.data;
    if (!d) return;

    const textoVar = (v: number | null) => v;
    const reporte: ReporteExport = {
      titulo: 'Comparación mensual de ventas',
      filtros: [
        `Mes analizado: ${nombreMes(d.mes)} (${fechaCorta(d.actual.desde)} al ${fechaCorta(d.actual.hasta)})`,
        `Comparado con: ${fechaCorta(d.anterior.desde)} al ${fechaCorta(d.anterior.hasta)}`,
        `Categoría: ${d.categoriaNombre ?? 'Todas'}`,
        `Resumen: S/ ${d.actual.monto.toFixed(2)} vs S/ ${d.anterior.monto.toFixed(2)} - ` +
          (d.variacionMonto === null ? 'sin base de comparación' : `${d.variacionMonto > 0 ? '+' : ''}${d.variacionMonto.toFixed(1)}%`),
        'Importes netos de descuento'
      ],
      columnas: [
        { titulo: 'Categoría' },
        { titulo: 'Mes actual', formato: 'moneda' },
        { titulo: 'Mes anterior', formato: 'moneda' },
        { titulo: 'Variación', formato: 'porcentaje' }
      ],
      filas: d.porCategoria.map(c => [c.categoria, c.montoActual, c.montoAnterior, textoVar(c.variacionMonto) ?? 'Sin base']),
      nombreArchivo: `comparacion-mensual_${d.mes}`
    };
    exportarReporte(formato, reporte);
  }
}
