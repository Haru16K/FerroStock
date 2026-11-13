import { Component, OnInit, OnDestroy, inject, ChangeDetectorRef, viewChild, ElementRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { PrediccionService } from '../../shared/services/prediccion.service';
import { RecomendacionService } from '../../shared/services/recomendacion.service';
import { RecomendacionProducto } from '../../shared/models/recomendacion.model';
import { AuthService } from '../../core/auth.service';
import {
  ResumenPredicciones, PrediccionProducto, DetallePrediccion, TendenciaResultado,
  Precision, EjecucionModelo, EstadoModelo
} from '../../shared/models/prediccion.model';
import { formatearFechaLima, fechaCorta } from '../../shared/utils/fecha-lima';
import { mensajeError } from '../../shared/utils/errores';

declare const Chart: any;   // Chart.js se carga por CDN en index.html

type Pestana = 'productos' | 'categorias' | 'precision';
const COLORES = ['#185FA5', '#0F6E56', '#854F0B', '#993C1D', '#534AB7', '#D95F2B', '#6B6963', '#3B6D11'];

@Component({
  selector: 'app-predicciones',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './predicciones.component.html'
})
export class PrediccionesComponent implements OnInit, OnDestroy {
  private svc  = inject(PrediccionService);
  private recSvc = inject(RecomendacionService);
  private auth = inject(AuthService);
  private cdr  = inject(ChangeDetectorRef);

  private lienzoFicha = viewChild<ElementRef<HTMLCanvasElement>>('cFicha');
  private lienzoTend  = viewChild<ElementRef<HTMLCanvasElement>>('cTend');

  esAdmin = this.auth.isAdmin();
  veTodo  = this.auth.tieneRol('Administrador', 'Gerencia');   // el almacenero solo ve productos
  puedeVerCompras = this.auth.tieneRol('Administrador', 'Gerencia');   // la compra la deciden ellos
  readonly fechaCorta = fechaCorta;

  pestana: Pestana = 'productos';
  estado: EstadoModelo | null = null;

  // Productos
  resumen: ResumenPredicciones | null = null;
  loading = true;
  error   = '';
  filtro  = '';
  soloRiesgo = false;

  // Ficha
  ficha: DetallePrediccion | null = null;
  cargandoFicha = false;
  errorFicha = '';
  reco: RecomendacionProducto | null = null;
  cargandoReco = false;
  errorReco = '';
  private chartFicha: any = null;

  // Categorías
  horizonte: 'semanal' | 'mensual' = 'semanal';
  tendencia: TendenciaResultado | null = null;
  cargandoTend = false;
  errorTend = '';
  private chartTend: any = null;

  // Precisión
  precision: Precision | null = null;
  ejecuciones: EjecucionModelo[] = [];
  cargandoPrec = false;
  errorPrec = '';

  // Acciones del administrador
  trabajando = '';
  mensaje = '';
  errorAccion = '';

  ngOnInit() {
    this.cargarEstado();
    this.cargarProductos();
  }

  ngOnDestroy() {
    this.chartFicha?.destroy();
    this.chartTend?.destroy();
  }

  // ── Estado del modelo (HU-29) ─────────────────────────────────
  cargarEstado() {
    this.svc.getEstado().subscribe({
      next: e => { this.estado = e; this.cdr.detectChanges(); }
    });
  }

  get falloModelo(): boolean {
    return this.estado?.ultima?.estado === 'Error';
  }

  // ── Productos (HU-24, HU-25, HU-27) ───────────────────────────
  cargarProductos() {
    this.loading = true;
    this.error   = '';
    this.cdr.detectChanges();

    this.svc.getProductos().subscribe({
      next: r => {
        this.resumen = r;
        this.loading = false;
        this.cdr.detectChanges();
      },
      error: e => {
        this.error   = mensajeError(e, 'No se pudieron cargar las predicciones');
        this.loading = false;
        this.cdr.detectChanges();
      }
    });
  }

  get productosFiltrados(): PrediccionProducto[] {
    const q = this.filtro.trim().toLowerCase();
    return (this.resumen?.productos ?? []).filter(p =>
      (!q || p.nombre.toLowerCase().includes(q) || p.codigo.toLowerCase().includes(q) || p.categoria.toLowerCase().includes(q))
      && (!this.soloRiesgo || (p.diasHastaQuiebre !== null && p.diasHastaQuiebre <= 7)));
  }

  claseQuiebre(p: PrediccionProducto): string {
    if (p.diasHastaQuiebre === null) return 'q-na';
    if (p.diasHastaQuiebre <= 7)  return 'q-rojo';
    if (p.diasHastaQuiebre <= 15) return 'q-ambar';
    return 'q-ok';
  }

  textoQuiebre(p: PrediccionProducto): string {
    if (p.diasHastaQuiebre === null) return p.diasHistorial === 0 ? 'Sin ventas' : 'Sin consumo reciente';
    if (p.diasHastaQuiebre === 0) return p.stockActual <= 0 ? 'Sin stock' : 'Hoy';
    return `en ${p.diasHastaQuiebre} día${p.diasHastaQuiebre === 1 ? '' : 's'}`;
  }

  claseConfianza(n: string): string { return 'conf-' + n.toLowerCase(); }

  // ── Ficha del producto ────────────────────────────────────────
  abrirFicha(p: PrediccionProducto) {
    this.ficha = null;
    this.reco = null;
    this.errorReco = '';
    this.errorFicha = '';
    this.cargandoFicha = true;
    this.cdr.detectChanges();

    this.svc.getProducto(p.productoId).subscribe({
      next: d => {
        this.ficha = d;
        this.cargandoFicha = false;
        this.cdr.detectChanges();
        setTimeout(() => this.dibujarFicha(), 0);
        if (this.puedeVerCompras) this.cargarReco(p.productoId);
      },
      error: e => {
        this.errorFicha = mensajeError(e, 'No se pudo cargar la ficha');
        this.cargandoFicha = false;
        this.cdr.detectChanges();
      }
    });
  }

  private cargarReco(productoId: number) {
    this.cargandoReco = true;
    this.cdr.detectChanges();
    this.recSvc.getProducto(productoId).subscribe({
      next: r => { this.reco = r; this.cargandoReco = false; this.cdr.detectChanges(); },
      error: e => {
        this.errorReco = mensajeError(e, 'No se pudo calcular la recomendación de compra');
        this.cargandoReco = false;
        this.cdr.detectChanges();
      }
    });
  }

  cerrarFicha() {
    this.reco = null;
    this.cargandoReco = false;
    this.chartFicha?.destroy();
    this.chartFicha = null;
    this.ficha = null;
    this.cargandoFicha = false;
    this.errorFicha = '';
    this.cdr.detectChanges();
  }

  private dibujarFicha() {
    this.chartFicha?.destroy();
    const canvas = this.lienzoFicha()?.nativeElement;
    if (!canvas || !this.ficha) return;

    const serie = this.ficha.serie;
    this.chartFicha = new Chart(canvas.getContext('2d'), {
      type: 'bar',
      data: {
        labels: serie.map(s => s.etiqueta),
        datasets: [{
          label: 'Unidades por semana',
          data: serie.map(s => s.unidades),
          backgroundColor: serie.map(s => s.proyectado ? '#F5C4B3' : '#B5D4F4'),
          borderRadius: 4
        }]
      },
      options: {
        responsive: true, maintainAspectRatio: false,
        plugins: { legend: { display: false } },
        scales: {
          x: { grid: { display: false }, ticks: { font: { size: 10 }, color: '#6B6963' } },
          y: { grid: { color: '#E2E0D8' }, ticks: { font: { size: 10 }, color: '#6B6963' }, beginAtZero: true }
        }
      }
    });
  }

  // ── Pestañas ──────────────────────────────────────────────────
  cambiarPestana(p: Pestana) {
    this.pestana = p;
    this.cdr.detectChanges();
    if (p === 'categorias' && !this.tendencia) this.cargarTendencia();
    if (p === 'categorias' && this.tendencia) setTimeout(() => this.dibujarTendencia(), 0);
    if (p === 'precision' && !this.precision) this.cargarPrecision();
  }

  // ── Tendencia por categoría (HU-26) ───────────────────────────
  cargarTendencia() {
    this.cargandoTend = true;
    this.errorTend = '';
    this.cdr.detectChanges();

    this.svc.getCategorias(this.horizonte).subscribe({
      next: t => {
        this.tendencia = t;
        this.cargandoTend = false;
        this.cdr.detectChanges();
        setTimeout(() => this.dibujarTendencia(), 0);
      },
      error: e => {
        this.errorTend = mensajeError(e, 'No se pudo cargar la tendencia por categoría');
        this.cargandoTend = false;
        this.cdr.detectChanges();
      }
    });
  }

  cambiarHorizonte(h: 'semanal' | 'mensual') {
    this.horizonte = h;
    this.cargarTendencia();
  }

  private dibujarTendencia() {
    this.chartTend?.destroy();
    const canvas = this.lienzoTend()?.nativeElement;
    const t = this.tendencia;
    if (!canvas || !t || t.categorias.length === 0) return;

    const nH = t.etiquetasHistoricas.length;
    const nP = t.etiquetasProyeccion.length;
    const datasets: any[] = [];

    t.categorias.forEach((c, i) => {
      const color = COLORES[i % COLORES.length];
      datasets.push({
        label: c.categoria, data: [...c.historico, ...Array(nP).fill(null)],
        borderColor: color, backgroundColor: color, tension: 0.25, pointRadius: 2
      });
      // La proyección arranca en el último punto real para que la línea sea continua
      datasets.push({
        label: `${c.categoria} (proy.)`,
        data: [...Array(nH - 1).fill(null), c.historico[nH - 1], ...c.proyeccion],
        borderColor: color, borderDash: [6, 4], tension: 0.25, pointRadius: 2
      });
    });

    this.chartTend = new Chart(canvas.getContext('2d'), {
      type: 'line',
      data: { labels: [...t.etiquetasHistoricas, ...t.etiquetasProyeccion], datasets },
      options: {
        responsive: true, maintainAspectRatio: false,
        plugins: {
          legend: { labels: { font: { size: 11 }, boxWidth: 10, boxHeight: 10, filter: (item: any) => !item.text.includes('(proy.)') } },
          tooltip: { callbacks: { label: (ctx: any) => `${ctx.dataset.label}: S/ ${Number(ctx.parsed.y).toFixed(2)}` } }
        },
        scales: {
          x: { grid: { display: false }, ticks: { font: { size: 10 }, color: '#6B6963' } },
          y: { grid: { color: '#E2E0D8' }, ticks: { font: { size: 10 }, color: '#6B6963' }, beginAtZero: true }
        }
      }
    });
  }

  sumar(a: number[]): number { return a.reduce((x, y) => x + y, 0); }

  claseTendencia(t: string): string {
    return t === 'Sube' ? 'var-sube' : t === 'Baja' ? 'var-baja' : 'var-igual';
  }

  textoTendencia(c: { tendencia: string; variacionPct: number | null }): string {
    if (c.variacionPct === null) return 'Sin datos';
    const signo = c.variacionPct > 0 ? '▲ +' : c.variacionPct < 0 ? '▼ ' : '';
    return `${signo}${c.variacionPct.toFixed(1)}%`;
  }

  // ── Precisión (HU-28) ─────────────────────────────────────────
  cargarPrecision() {
    this.cargandoPrec = true;
    this.errorPrec = '';
    this.cdr.detectChanges();

    this.svc.getPrecision().subscribe({
      next: p => { this.precision = p; this.cargarEjecuciones(); },
      error: e => {
        this.errorPrec = mensajeError(e, 'No se pudo calcular la precisión');
        this.cargandoPrec = false;
        this.cdr.detectChanges();
      }
    });
  }

  private cargarEjecuciones() {
    this.svc.getEjecuciones().subscribe({
      next: l => { this.ejecuciones = l; this.cargandoPrec = false; this.cdr.detectChanges(); },
      error: () => { this.cargandoPrec = false; this.cdr.detectChanges(); }
    });
  }

  // ── Acciones del administrador ────────────────────────────────
  recalcular(simularFallo = false) {
    this.ejecutar(simularFallo ? 'Simulando un fallo...' : 'Actualizando el modelo...',
      this.svc.recalcular(simularFallo),
      (e: EjecucionModelo) => e.estado === 'Ok'
        ? `Modelo actualizado: ${e.productosProcesados} productos, ${e.estacionalesDetectados} estacionales detectados.`
        : '');
  }

  generarDemo() {
    this.ejecutar('Generando 14 meses de ventas de demostración...', this.svc.generarDemo(), r => r.message);
  }

  eliminarDemo() {
    this.ejecutar('Eliminando datos de demostración...', this.svc.eliminarDemo(), r => r.message);
  }

  private ejecutar<T>(texto: string, peticion: import('rxjs').Observable<T>, mensajeOk: (r: T) => string) {
    this.trabajando = texto;
    this.mensaje = '';
    this.errorAccion = '';
    this.cdr.detectChanges();

    peticion.subscribe({
      next: r => {
        this.trabajando = '';
        this.mensaje = mensajeOk(r);
        this.refrescarTodo();
      },
      error: e => {
        this.trabajando = '';
        this.errorAccion = mensajeError(e, 'No se pudo completar la acción');
        this.cdr.detectChanges();
      }
    });
  }

  private refrescarTodo() {
    this.cargarEstado();
    this.cargarProductos();
    this.precision = null;
    this.tendencia = null;
    if (this.pestana === 'categorias') this.cargarTendencia();
    if (this.pestana === 'precision') this.cargarPrecision();
  }

  fecha(f: string | null): string { return formatearFechaLima(f, true); }
}
