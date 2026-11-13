import { Component, OnInit, inject, ChangeDetectorRef } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { ProductoService } from '../../shared/services/producto.service';
import { Producto } from '../../shared/models/producto.model';
import { ExportarBotonesComponent } from '../../shared/components/exportar-botones.component';
import { exportarReporte, FormatoExport, ReporteExport } from '../../shared/utils/exportar';
import { hoyLima } from '../../shared/utils/fecha-lima';

interface GrupoCategoria {
  nombre:      string;
  productos:   Producto[];
  valorTotal:  number;
  unidades:    number;
}

@Component({
  selector: 'app-reportes-stock',
  standalone: true,
  imports: [CommonModule, DatePipe, ExportarBotonesComponent],
  templateUrl: './reportes-stock.component.html'
})
export class ReportesStockComponent implements OnInit {
  private svc = inject(ProductoService);
  private cdr = inject(ChangeDetectorRef);

  productos: Producto[] = [];
  grupos:    GrupoCategoria[] = [];
  loading = true;
  error   = '';
  generadoEl = new Date();

  ngOnInit() {
    this.cargar();
  }

  cargar() {
    this.loading = true;
    this.error   = '';
    this.svc.getAll().subscribe({
      next: p => {
        this.productos  = p;
        this.grupos     = this.agruparPorCategoria(p);
        this.generadoEl = new Date();
        this.loading    = false;
        this.cdr.detectChanges();
      },
      error: () => {
        this.error   = 'Error al generar el reporte de inventario';
        this.loading = false;
        this.cdr.detectChanges();
      }
    });
  }

  private agruparPorCategoria(productos: Producto[]): GrupoCategoria[] {
    const mapa = new Map<string, Producto[]>();
    for (const p of productos) {
      const lista = mapa.get(p.categoriaNombre) ?? [];
      lista.push(p);
      mapa.set(p.categoriaNombre, lista);
    }
    return Array.from(mapa.entries())
      .map(([nombre, productos]) => ({
        nombre,
        productos,
        valorTotal: productos.reduce((sum, p) => sum + p.stockActual * p.precioCompra, 0),
        unidades:   productos.reduce((sum, p) => sum + p.stockActual, 0)
      }))
      .sort((a, b) => b.valorTotal - a.valorTotal);
  }

  get valorTotalInventario(): number {
    return this.productos.reduce((sum, p) => sum + p.stockActual * p.precioCompra, 0);
  }

  get totalUnidades(): number {
    return this.productos.reduce((sum, p) => sum + p.stockActual, 0);
  }

  get totalCriticos(): number {
    return this.productos.filter(p => p.esCritico).length;
  }

  exportar(formato: FormatoExport) {
    const reporte: ReporteExport = {
      titulo: 'Reporte de inventario',
      filtros: [
        `Productos: ${this.productos.length} · En stock crítico: ${this.totalCriticos}`,
        'Valorizado a precio de compra'
      ],
      columnas: [
        { titulo: 'Código' }, { titulo: 'Producto' }, { titulo: 'Categoría' },
        { titulo: 'Stock actual', formato: 'entero' }, { titulo: 'Stock mínimo', formato: 'entero' },
        { titulo: 'Precio compra', formato: 'moneda' }, { titulo: 'Valor stock', formato: 'moneda' },
        { titulo: 'Estado' }
      ],
      filas: this.productos.map(p => [
        p.codigo, p.nombre, p.categoriaNombre, p.stockActual, p.stockMinimo,
        p.precioCompra, p.stockActual * p.precioCompra, p.esCritico ? 'Crítico' : 'Normal'
      ]),
      totales: ['TOTAL', '', '', this.totalUnidades, '', '', this.valorTotalInventario, ''],
      nombreArchivo: `inventario_${hoyLima()}`
    };
    exportarReporte(formato, reporte);
  }
}
