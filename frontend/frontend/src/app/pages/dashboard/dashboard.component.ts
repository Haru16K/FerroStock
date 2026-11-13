import { Component, OnInit, OnDestroy, inject, ChangeDetectorRef } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { Router } from '@angular/router';
import { AuthService } from '../../core/auth.service';
import { DashboardService } from '../../core/dashboard.service';
import { DashboardData } from '../../shared/models/dashboard.model';
import { PanelVentasComponent } from './panel-ventas.component';

declare const Chart: any;

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule, DatePipe, PanelVentasComponent],
  templateUrl: './dashboard.component.html'
})
export class DashboardComponent implements OnInit, OnDestroy {

  private auth         = inject(AuthService);
  private dashboardSvc = inject(DashboardService);
  private router       = inject(Router);
  private cdr          = inject(ChangeDetectorRef);

  data:    DashboardData | null = null;
  loading  = true;
  error    = '';
  hoy      = new Date();
  usuario  = this.auth.getUser();
  puedeVerVentas = this.auth.tieneRol('Administrador', 'Gerencia');
  puedeRegistrar = this.auth.tieneRol('Administrador', 'Almacenero');   // gerencia solo consulta
   // las ventas no las ve el almacenero

  private chart: any = null;

  ngOnInit() {
    this.cargarDashboard();
  }

  ngOnDestroy() {
    this.chart?.destroy();
  }

  cargarDashboard() {
    this.loading = true;
    this.dashboardSvc.getDashboard().subscribe({
      next: (data) => {
        this.data    = data;
        this.loading = false;
        this.cdr.detectChanges();
        setTimeout(() => this.initChart(), 50);
      },
      error: () => {
        this.error   = 'No se pudo cargar el dashboard. Verifica que el backend esté activo.';
        this.loading = false;
        this.cdr.detectChanges();
      }
    });
  }

  initChart() {
    if (!this.data) return;
    const canvas = document.getElementById('movChart') as HTMLCanvasElement;
    if (!canvas) return;

    this.chart?.destroy();

    this.chart = new Chart(canvas.getContext('2d'), {
      type: 'bar',
      data: {
        labels: this.data.grafico.dias,
        datasets: [
          { label: 'Entradas', data: this.data.grafico.entradas, backgroundColor: '#B5D4F4', borderRadius: 4 },
          { label: 'Salidas',  data: this.data.grafico.salidas,  backgroundColor: '#F5C4B3', borderRadius: 4 }
        ]
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        plugins: {
          legend: { labels: { font: { size: 11, family: "'Plus Jakarta Sans', sans-serif" }, boxWidth: 10, boxHeight: 10 } }
        },
        scales: {
          x: { grid: { color: '#E2E0D8' }, ticks: { font: { size: 10 }, color: '#6B6963' } },
          y: { grid: { color: '#E2E0D8' }, ticks: { font: { size: 10 }, color: '#6B6963' }, beginAtZero: true }
        }
      }
    });
  }

  filtrarChart(event: Event) {
    if (!this.chart || !this.data) return;
    const v = (event.target as HTMLSelectElement).value;
    this.chart.data.datasets[0].data = v === 'salidas'  ? this.data.grafico.entradas.map(() => 0) : this.data.grafico.entradas;
    this.chart.data.datasets[1].data = v === 'entradas' ? this.data.grafico.salidas.map(()  => 0) : this.data.grafico.salidas;
    this.chart.update();
  }

  getDeltaClass(delta: number): string {
    return delta >= 0 ? 'delta-up' : 'delta-down';
  }

  getDeltaText(delta: number, label: string): string {
    return delta >= 0 ? `↑ +${delta} ${label}` : `↓ ${delta} ${label}`;
  }


  // Abre Movimientos con el formulario de entrada ya desplegado
  nuevaEntrada() {
    this.router.navigate(['/movimientos'], { queryParams: { nuevo: 'entrada' } });
  }
}
