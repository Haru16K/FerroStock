import { Component, OnInit, inject, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RecomendacionService } from '../../shared/services/recomendacion.service';
import { ListaRecomendaciones, Recomendacion, ConfiguracionIa } from '../../shared/models/recomendacion.model';
import { formatearFechaLima } from '../../shared/utils/fecha-lima';
import { mensajeError } from '../../shared/utils/errores';

@Component({
  selector: 'app-recomendaciones',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './recomendaciones.component.html'
})
export class RecomendacionesComponent implements OnInit {
  private svc = inject(RecomendacionService);
  private cdr = inject(ChangeDetectorRef);

  readonly tipos = [
    { valor: '',             texto: 'Todas' },
    { valor: 'Compra',       texto: 'Compras' },
    { valor: 'Promocion',    texto: 'Promociones' },
    { valor: 'Descontinuar', texto: 'Descontinuar' }
  ];
  readonly estados = ['Pendiente', 'Aplicada', 'Descartada', 'Vencida', 'Todas'];

  lista: ListaRecomendaciones | null = null;
  tipo = '';
  estado = 'Pendiente';
  loading = true;
  error   = '';

  trabajando = false;
  mensaje = '';
  errorAccion = '';
  procesandoId: number | null = null;

  // Configuración
  configAbierta = false;
  config: ConfiguracionIa | null = null;
  valores: Record<string, number> = {};
  guardandoConfig = false;
  errorConfig = '';

  readonly etiquetasConfig: Record<string, string> = {
    'Compra.DiasCobertura':           'Días de venta que debe cubrir cada compra',
    'Compra.NivelServicio':           'Nivel de servicio: probabilidad de no quedarse sin stock (%)',
    'Promocion.Cantidad':             'Cantidad de productos a proponer para promoción',
    'Descontinuar.UmbralUnidades':    'Baja rotación: se vende menos de (unidades)…',
    'Descontinuar.DiasVentana':       '…en los últimos (días)',
    'Descontinuar.DiasSinMovimiento': 'Y lleva sin movimiento al menos (días)'
  };

  ngOnInit() { this.cargar(); }

  cargar() {
    this.loading = true;
    this.error   = '';
    this.cdr.detectChanges();

    this.svc.getAll(this.tipo, this.estado).subscribe({
      next: l => {
        this.lista   = l;
        this.loading = false;
        this.cdr.detectChanges();
      },
      error: e => {
        this.error   = mensajeError(e, 'No se pudieron cargar las recomendaciones');
        this.loading = false;
        this.cdr.detectChanges();
      }
    });
  }

  cambiarTipo(t: string) { this.tipo = t; this.cargar(); }
  cambiarEstado(e: string) { this.estado = e; this.cargar(); }

  textoTipo(t: string): string {
    return t === 'Compra' ? 'Compra' : t === 'Promocion' ? 'Promoción' : 'Descontinuar';
  }

  conteoTipo(t: string): number {
    const l = this.lista;
    if (!l) return 0;
    return t === 'Compra' ? l.pendientesCompra : t === 'Promocion' ? l.pendientesPromocion
         : t === 'Descontinuar' ? l.pendientesDescontinuar : l.pendientes;
  }

  fecha(f: string | null): string { return formatearFechaLima(f, true); }

  // ── Actualizar (HU-31) ────────────────────────────────────────
  generar() {
    this.trabajando = true;
    this.mensaje = '';
    this.errorAccion = '';
    this.cdr.detectChanges();

    this.svc.generar().subscribe({
      next: r => {
        this.trabajando = false;
        this.mensaje = r.mensaje;
        this.cargar();
      },
      error: e => {
        this.trabajando = false;
        this.errorAccion = mensajeError(e, 'No se pudieron generar las recomendaciones');
        this.cdr.detectChanges();
      }
    });
  }

  // ── Aplicar / descartar (HU-35) ───────────────────────────────
  gestionar(r: Recomendacion, accion: 'aplicar' | 'descartar') {
    this.procesandoId = r.id;
    this.errorAccion = '';
    this.cdr.detectChanges();

    const peticion = accion === 'aplicar' ? this.svc.aplicar(r.id) : this.svc.descartar(r.id);
    peticion.subscribe({
      next: () => { this.procesandoId = null; this.cargar(); },
      error: e => {
        this.procesandoId = null;
        this.errorAccion = mensajeError(e, 'No se pudo actualizar la recomendación');
        this.cdr.detectChanges();
      }
    });
  }

  // ── Configuración ─────────────────────────────────────────────
  abrirConfig() {
    this.configAbierta = true;
    this.errorConfig = '';
    this.cdr.detectChanges();

    this.svc.getConfiguracion().subscribe({
      next: c => {
        this.config = c;
        this.valores = { ...c.valores };
        this.cdr.detectChanges();
      },
      error: e => {
        this.errorConfig = mensajeError(e, 'No se pudo cargar la configuración');
        this.cdr.detectChanges();
      }
    });
  }

  cerrarConfig() { this.configAbierta = false; this.cdr.detectChanges(); }

  get clavesConfig(): string[] { return Object.keys(this.etiquetasConfig); }

  guardarConfig() {
    this.guardandoConfig = true;
    this.errorConfig = '';
    this.cdr.detectChanges();

    const valores: Record<string, number> = {};
    for (const k of this.clavesConfig) valores[k] = Number(this.valores[k]);

    this.svc.guardarConfiguracion(valores).subscribe({
      next: () => {
        this.guardandoConfig = false;
        this.configAbierta = false;
        this.mensaje = 'Configuración guardada. Se aplicará la próxima vez que se actualicen las recomendaciones.';
        this.cdr.detectChanges();
      },
      error: e => {
        this.errorConfig = mensajeError(e, 'No se pudo guardar la configuración');
        this.guardandoConfig = false;
        this.cdr.detectChanges();
      }
    });
  }
}
