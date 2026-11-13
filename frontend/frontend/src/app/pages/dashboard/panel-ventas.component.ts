import { Component, OnInit, OnDestroy, inject, ChangeDetectorRef, viewChild, ElementRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AnalisisService } from '../../shared/services/analisis.service';
import { ProductoService } from '../../shared/services/producto.service';
import { AnalisisVentas } from '../../shared/models/analisis.model';
import {
  hoyLima, sumarDias, primerDiaMesLima, primerDiaMesAnteriorLima, ultimoDiaMesAnteriorLima, fechaCorta
} from '../../shared/utils/fecha-lima';
import { mensajeError } from '../../shared/utils/errores';

declare const Chart: any;   // Chart.js se carga por CDN en index.html

type Periodo = '7' | '30' | '90' | 'mes' | 'mes-anterior';

const COLORES = ['#185FA5', '#0F6E56', '#854F0B', '#993C1D', '#534AB7', '#D95F2B', '#6B6963', '#3B6D11'];

@Component({
  selector: 'app-panel-ventas',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './panel-ventas.component.html'
})
export class PanelVentasComponent implements OnInit, OnDestroy {
  private svc     = inject(AnalisisService);
  private prodSvc = inject(ProductoService);
  private cdr     = inject(ChangeDetectorRef);

  private cDia  = viewChild<ElementRef<HTMLCanvasElement>>('cDia');
  private cCat  = viewChild<ElementRef<HTMLCanvasElement>>('cCat');
  private cTop  = viewChild<ElementRef<HTMLCanvasElement>>('cTop');
  private cInv  = viewChild<ElementRef<HTMLCanvasElement>>('cInv');

  periodo: Periodo = '30';
  desde = '';
  hasta = '';
  data: AnalisisVentas | null = null;
  loading = true;
  error   = '';
  inventario: { categoria: string; valor: number; unidades: number }[] = [];

  private chartsVentas: any[] = [];
  private chartInv: any = null;

  ngOnInit() {
    this.cargar();
    this.prodSvc.getAll().subscribe({
      next: ps => {
        const mapa = new Map<string, { valor: number; unidades: number }>();
        for (const p of ps.filter(x => x.activo)) {
          const g = mapa.get(p.categoriaNombre) ?? { valor: 0, unidades: 0 };
          g.valor    += p.stockActual * p.precioCompra;
          g.unidades += p.stockActual;
          mapa.set(p.categoriaNombre, g);
        }
        this.inventario = [...mapa.entries()]
          .map(([categoria, g]) => ({ categoria, ...g }))
          .sort((a, b) => b.valor - a.valor);
        this.cdr.detectChanges();
        setTimeout(() => this.dibujarInventario(), 0);
      }
    });
  }

  ngOnDestroy() {
    this.chartsVentas.forEach(c => c.destroy());
    this.chartInv?.destroy();
  }

  private rango(): [string, string] {
    const hoy = hoyLima();
    switch (this.periodo) {
      case '7':            return [sumarDias(hoy, -6), hoy];
      case '90':           return [sumarDias(hoy, -89), hoy];
      case 'mes':          return [primerDiaMesLima(), hoy];
      case 'mes-anterior': return [primerDiaMesAnteriorLima(), ultimoDiaMesAnteriorLima()];
      default:             return [sumarDias(hoy, -29), hoy];
    }
  }

  cargar() {
    [this.desde, this.hasta] = this.rango();
    this.loading = true;
    this.error   = '';
    this.cdr.detectChanges();

    this.svc.getVentas(this.desde, this.hasta).subscribe({
      next: d => {
        this.data    = d;
        this.loading = false;
        this.cdr.detectChanges();
        setTimeout(() => this.dibujarVentas(), 0);
      },
      error: e => {
        this.error   = mensajeError(e, 'No se pudieron cargar las ventas del período');
        this.loading = false;
        this.cdr.detectChanges();
      }
    });
  }

  get textoPeriodo(): string {
    return `${fechaCorta(this.desde)} al ${fechaCorta(this.hasta)}`;
  }

  private dibujarVentas() {
    const d = this.data;
    this.chartsVentas.forEach(c => c.destroy());
    this.chartsVentas = [];
    if (!d) return;

    const moneda = (ctx: any) => 'S/ ' + Number(ctx.parsed.y ?? ctx.parsed.x ?? ctx.parsed).toFixed(2);
    const ejes = {
      x: { grid: { color: '#E2E0D8' }, ticks: { font: { size: 10 }, color: '#6B6963' } },
      y: { grid: { color: '#E2E0D8' }, ticks: { font: { size: 10 }, color: '#6B6963' }, beginAtZero: true }
    };

    const dia = this.cDia()?.nativeElement;
    if (dia) {
      this.chartsVentas.push(new Chart(dia.getContext('2d'), {
        type: 'bar',
        data: {
          labels: d.porDia.map(p => p.fecha.slice(8, 10) + '/' + p.fecha.slice(5, 7)),
          datasets: [{ label: 'Ventas (S/)', data: d.porDia.map(p => p.monto), backgroundColor: '#B5D4F4', borderRadius: 4 }]
        },
        options: {
          responsive: true, maintainAspectRatio: false,
          plugins: { legend: { display: false }, tooltip: { callbacks: { label: moneda } } },
          scales: ejes
        }
      }));
    }

    const cat = this.cCat()?.nativeElement;
    if (cat && d.porCategoria.length > 0) {
      this.chartsVentas.push(new Chart(cat.getContext('2d'), {
        type: 'doughnut',
        data: {
          labels: d.porCategoria.map(c => c.categoria),
          datasets: [{ data: d.porCategoria.map(c => c.monto), backgroundColor: COLORES, borderWidth: 1 }]
        },
        options: {
          responsive: true, maintainAspectRatio: false,
          plugins: {
            legend: { position: 'right', labels: { font: { size: 11 }, boxWidth: 10, boxHeight: 10 } },
            tooltip: { callbacks: { label: (ctx: any) => `${ctx.label}: S/ ${Number(ctx.parsed).toFixed(2)}` } }
          }
        }
      }));
    }

    const top = this.cTop()?.nativeElement;
    const top5 = d.topProductos.slice(0, 5);
    if (top && top5.length > 0) {
      this.chartsVentas.push(new Chart(top.getContext('2d'), {
        type: 'bar',
        data: {
          labels: top5.map(p => p.nombre),
          datasets: [{ label: 'Ventas (S/)', data: top5.map(p => p.monto), backgroundColor: '#F5C4B3', borderRadius: 4 }]
        },
        options: {
          indexAxis: 'y', responsive: true, maintainAspectRatio: false,
          plugins: { legend: { display: false }, tooltip: { callbacks: { label: moneda } } },
          scales: { x: ejes.y, y: { ticks: { font: { size: 10 }, color: '#6B6963' }, grid: { display: false } } }
        }
      }));
    }
  }

  private dibujarInventario() {
    this.chartInv?.destroy();
    const canvas = this.cInv()?.nativeElement;
    if (!canvas || this.inventario.length === 0) return;

    this.chartInv = new Chart(canvas.getContext('2d'), {
      type: 'bar',
      data: {
        labels: this.inventario.map(i => i.categoria),
        datasets: [{ label: 'Valor del stock (S/)', data: this.inventario.map(i => i.valor), backgroundColor: '#C0DD97', borderRadius: 4 }]
      },
      options: {
        responsive: true, maintainAspectRatio: false,
        plugins: {
          legend: { display: false },
          tooltip: { callbacks: { label: (ctx: any) => 'S/ ' + Number(ctx.parsed.y).toFixed(2) } }
        },
        scales: {
          x: { grid: { display: false }, ticks: { font: { size: 10 }, color: '#6B6963' } },
          y: { grid: { color: '#E2E0D8' }, ticks: { font: { size: 10 }, color: '#6B6963' }, beginAtZero: true }
        }
      }
    });
  }
}
