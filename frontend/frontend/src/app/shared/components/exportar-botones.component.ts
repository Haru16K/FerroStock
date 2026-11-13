import { Component, input, output } from '@angular/core';
import { FormatoExport } from '../utils/exportar';

// Botones CSV / Excel / PDF reutilizables. La pantalla escucha (exportar) y arma el reporte.
@Component({
  selector: 'app-exportar',
  standalone: true,
  template: `
    <style>
      :host { display: inline-flex; gap: 6px; }
      button { display: inline-flex; align-items: center; gap: 6px; padding: 8px 12px; font-size: 13px; font-family: inherit; font-weight: 600; border-radius: 8px; cursor: pointer; border: 1px solid #E2E0D8; background: #fff; color: #1A1916; white-space: nowrap; }
      button:hover:not(:disabled) { background: #F4F3EF; }
      button:disabled { opacity: .5; cursor: not-allowed; }
      svg { width: 15px; height: 15px; stroke: currentColor; fill: none; stroke-width: 2; stroke-linecap: round; stroke-linejoin: round; }
    </style>
    <button type="button" [disabled]="disabled()" (click)="exportar.emit('csv')" title="Texto separado por comas">
      <svg viewBox="0 0 24 24"><path d="M12 3v12m0 0l-4-4m4 4l4-4M4 21h16"/></svg> CSV
    </button>
    <button type="button" [disabled]="disabled()" (click)="exportar.emit('excel')" title="Libro de Excel">
      <svg viewBox="0 0 24 24"><path d="M12 3v12m0 0l-4-4m4 4l4-4M4 21h16"/></svg> Excel
    </button>
    <button type="button" [disabled]="disabled()" (click)="exportar.emit('pdf')" title="Documento PDF">
      <svg viewBox="0 0 24 24"><path d="M12 3v12m0 0l-4-4m4 4l4-4M4 21h16"/></svg> PDF
    </button>
  `
})
export class ExportarBotonesComponent {
  disabled = input(false);
  exportar = output<FormatoExport>();
}
