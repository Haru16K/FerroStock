import { ahoraLimaTexto } from './fecha-lima';

// Exportación común de reportes (HU-15 y HU-21). Cada pantalla arma un ReporteExport con
// lo que el usuario ve (ya filtrado) y se genera el archivo en el navegador.
// Excel y PDF incluyen la fecha de generación en hora de Lima y los filtros aplicados.

export type FormatoExport = 'csv' | 'excel' | 'pdf';
export type FormatoColumna = 'texto' | 'entero' | 'decimal' | 'moneda' | 'porcentaje';
export type Celda = string | number | null;

export interface ColumnaExport {
  titulo:   string;
  formato?: FormatoColumna;   // por defecto: texto
}

export interface ReporteExport {
  titulo:        string;
  filtros?:      string[];     // una línea por filtro, p. ej. "Período: 01/11/2026 al 07/11/2026"
  columnas:      ColumnaExport[];
  filas:         Celda[][];
  totales?:      Celda[];      // fila final opcional
  nombreArchivo: string;       // sin extensión
}

export async function exportarReporte(formato: FormatoExport, r: ReporteExport): Promise<void> {
  switch (formato) {
    case 'csv':   return exportarCsv(r);
    case 'excel': return exportarExcel(r);
    case 'pdf':   return exportarPdf(r);
  }
}

// ── Utilidades ──────────────────────────────────────────────────────────────

function descargar(blob: Blob, nombre: string) {
  const url = URL.createObjectURL(blob);
  const a   = document.createElement('a');
  a.href     = url;
  a.download = nombre;
  a.click();
  URL.revokeObjectURL(url);
}

const NUM_ES = (n: number, dec: number) =>
  n.toLocaleString('es-PE', { minimumFractionDigits: dec, maximumFractionDigits: dec });

// Texto legible de una celda (para PDF)
function textoCelda(v: Celda, f: FormatoColumna = 'texto'): string {
  if (v === null || v === undefined || v === '') return '';
  if (typeof v === 'string') return v;
  switch (f) {
    case 'entero':     return NUM_ES(v, 0);
    case 'decimal':    return NUM_ES(v, 2);
    case 'moneda':     return 'S/ ' + NUM_ES(v, 2);
    case 'porcentaje': return (v > 0 ? '+' : '') + NUM_ES(v, 1) + '%';
    default:           return String(v);
  }
}

// Las fuentes estándar del PDF no tienen todos los caracteres Unicode
function paraPdf(s: string): string {
  return s
    .replace(/[\u2013\u2014]/g, '-')
    .replace(/[\u201C\u201D]/g, '"')
    .replace(/[\u2018\u2019]/g, "'")
    .replace(/[^\x00-\xFF]/g, '?');
}

// ── CSV ─────────────────────────────────────────────────────────────────────

function exportarCsv(r: ReporteExport) {
  const celda = (v: Celda, f: FormatoColumna = 'texto') => {
    if (v === null || v === undefined) return '';
    if (typeof v === 'number') return f === 'moneda' || f === 'decimal' ? v.toFixed(2) : String(v);
    let t = v;
    // Evita que Excel interprete un texto como fórmula (=, +, -, @)
    if (/^[=+\-@]/.test(t)) t = "'" + t;
    return `"${t.replace(/"/g, '""')}"`;
  };

  const lineas = [r.columnas.map(c => `"${c.titulo}"`).join(',')];
  for (const fila of r.filas) {
    lineas.push(fila.map((v, i) => celda(v, r.columnas[i]?.formato)).join(','));
  }
  if (r.totales) lineas.push(r.totales.map((v, i) => celda(v, r.columnas[i]?.formato)).join(','));

  // BOM para que Excel abra bien las tildes
  descargar(new Blob(['\ufeff' + lineas.join('\r\n')], { type: 'text/csv;charset=utf-8;' }), `${r.nombreArchivo}.csv`);
}

// ── Excel ───────────────────────────────────────────────────────────────────

const FORMATO_EXCEL: Record<FormatoColumna, string | undefined> = {
  texto:      undefined,
  entero:     '#,##0',
  decimal:    '#,##0.00',
  moneda:     '"S/" #,##0.00',
  porcentaje: '+0.0"%";-0.0"%";0.0"%"'
};

async function exportarExcel(r: ReporteExport) {
  const mod: any = await import('exceljs');
  const Workbook = (mod.default ?? mod).Workbook;

  const wb = new Workbook();
  wb.creator = 'FerroStock';
  const ws = wb.addWorksheet(r.titulo.replace(/[\\/?*[\]:]/g, '').substring(0, 31) || 'Reporte');

  ws.addRow([r.titulo]).font = { bold: true, size: 14 };
  ws.addRow([`Generado el ${ahoraLimaTexto()} (hora de Lima)`]).font = { color: { argb: 'FF6B6963' } };
  for (const f of r.filtros ?? []) ws.addRow([f]).font = { color: { argb: 'FF6B6963' } };
  ws.addRow([]);

  const filaEncabezado = ws.addRow(r.columnas.map(c => c.titulo));
  filaEncabezado.font = { bold: true };
  filaEncabezado.eachCell((c: any) => {
    c.fill   = { type: 'pattern', pattern: 'solid', fgColor: { argb: 'FFF4F3EF' } };
    c.border = { bottom: { style: 'thin', color: { argb: 'FFE2E0D8' } } };
  });
  const numFilaEnc = filaEncabezado.number;

  const agregar = (valores: Celda[], negrita = false) => {
    const fila = ws.addRow(valores);
    fila.eachCell({ includeEmpty: true }, (c: any, col: number) => {
      const fmt = FORMATO_EXCEL[r.columnas[col - 1]?.formato ?? 'texto'];
      if (fmt && typeof c.value === 'number') c.numFmt = fmt;
      if (negrita) c.font = { bold: true };
    });
    return fila;
  };
  for (const f of r.filas) agregar(f);
  if (r.totales) {
    const t = agregar(r.totales, true);
    t.eachCell({ includeEmpty: true }, (c: any) => {
      c.border = { top: { style: 'thin', color: { argb: 'FF1A1916' } } };
    });
  }

  // Ancho de columnas según el contenido
  r.columnas.forEach((c, i) => {
    const largos = [c.titulo.length, ...r.filas.map(f => textoCelda(f[i], c.formato).length)];
    ws.getColumn(i + 1).width = Math.min(50, Math.max(11, Math.max(...largos) + 2));
  });

  ws.views = [{ state: 'frozen', ySplit: numFilaEnc }];
  ws.autoFilter = { from: { row: numFilaEnc, column: 1 }, to: { row: numFilaEnc, column: r.columnas.length } };

  const buffer = await wb.xlsx.writeBuffer();
  descargar(
    new Blob([buffer], { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' }),
    `${r.nombreArchivo}.xlsx`);
}

// ── PDF ─────────────────────────────────────────────────────────────────────

async function exportarPdf(r: ReporteExport) {
  const { jsPDF } = await import('jspdf');
  const autoTable = (await import('jspdf-autotable')).default;

  const doc = new jsPDF({
    orientation: r.columnas.length > 6 ? 'landscape' : 'portrait',
    unit: 'pt',
    format: 'a4'
  });
  const ancho = doc.internal.pageSize.getWidth();
  const alto  = doc.internal.pageSize.getHeight();

  let y = 40;
  doc.setFont('helvetica', 'bold');
  doc.setFontSize(14);
  doc.text(paraPdf(r.titulo), 40, y);

  doc.setFont('helvetica', 'normal');
  doc.setFontSize(9);
  doc.setTextColor(107, 105, 99);
  y += 16;
  doc.text(paraPdf(`Generado el ${ahoraLimaTexto()} (hora de Lima) - FerroTech S.A.C.`), 40, y);
  for (const f of r.filtros ?? []) {
    y += 12;
    doc.text(paraPdf(f), 40, y);
  }
  doc.setTextColor(26, 25, 22);

  const alineadas: Record<number, { halign: 'right' }> = {};
  r.columnas.forEach((c, i) => {
    if (c.formato && c.formato !== 'texto') alineadas[i] = { halign: 'right' };
  });
  const texto = (fila: Celda[]) => fila.map((v, i) => paraPdf(textoCelda(v, r.columnas[i]?.formato)));

  autoTable(doc, {
    startY: y + 14,
    head: [r.columnas.map(c => paraPdf(c.titulo))],
    body: r.filas.map(texto),
    foot: r.totales ? [texto(r.totales)] : undefined,
    styles: { fontSize: 8, cellPadding: 4 },
    headStyles: { fillColor: [217, 95, 43], textColor: 255 },
    footStyles: { fillColor: [244, 243, 239], textColor: [26, 25, 22], fontStyle: 'bold' },
    columnStyles: alineadas,
    margin: { left: 40, right: 40, bottom: 40 }
  });

  const paginas = doc.getNumberOfPages();
  doc.setFontSize(8);
  doc.setTextColor(107, 105, 99);
  for (let i = 1; i <= paginas; i++) {
    doc.setPage(i);
    doc.text(`Página ${i} de ${paginas}`, ancho - 40, alto - 20, { align: 'right' });
  }

  doc.save(`${r.nombreArchivo}.pdf`);
}
