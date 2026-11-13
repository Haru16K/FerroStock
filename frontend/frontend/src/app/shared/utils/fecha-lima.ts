// El backend guarda las fechas en UTC y filtra por día calendario de Lima.
// Estas funciones usan siempre la zona America/Lima, sin depender de la zona
// horaria del navegador.

const ZONA = 'America/Lima';

function partes(fecha: Date, opciones: Intl.DateTimeFormatOptions) {
  const mapa: Record<string, string> = {};
  for (const p of new Intl.DateTimeFormat('es-PE', { timeZone: ZONA, hour12: false, ...opciones }).formatToParts(fecha)) {
    mapa[p.type] = p.value;
  }
  return mapa;
}

/** Día de hoy en Lima, formato yyyy-MM-dd (el que espera el backend en los filtros). */
export function hoyLima(): string {
  const p = partes(new Date(), { year: 'numeric', month: '2-digit', day: '2-digit' });
  return `${p['year']}-${p['month']}-${p['day']}`;
}

/** Primer día del mes actual en Lima, formato yyyy-MM-dd. */
export function primerDiaMesLima(): string {
  return hoyLima().slice(0, 8) + '01';
}

/**
 * Convierte una fecha del backend (UTC, con o sin 'Z') a texto en hora de Lima.
 * Por defecto "dd/MM HH:mm"; con conAnio = true, "dd/MM/yyyy HH:mm".
 */
export function formatearFechaLima(fechaStr: string | null | undefined, conAnio = false): string {
  if (!fechaStr) return '—';
  const fechaUtc = fechaStr.endsWith('Z') ? fechaStr : fechaStr + 'Z';
  const p = partes(new Date(fechaUtc), {
    day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit'
  });
  const dia = conAnio ? `${p['day']}/${p['month']}/${p['year']}` : `${p['day']}/${p['month']}`;
  return `${dia} ${p['hour']}:${p['minute']}`;
}

/** Suma (o resta) días a una fecha yyyy-MM-dd y devuelve otra fecha yyyy-MM-dd. */
export function sumarDias(fecha: string, dias: number): string {
  const d = new Date(`${fecha}T00:00:00Z`);
  d.setUTCDate(d.getUTCDate() + dias);
  return d.toISOString().slice(0, 10);
}

/** Mes actual en Lima, formato yyyy-MM. */
export function mesActualLima(): string {
  return hoyLima().slice(0, 7);
}

/** Primer día del mes anterior, formato yyyy-MM-dd. */
export function primerDiaMesAnteriorLima(): string {
  const d = new Date(`${primerDiaMesLima()}T00:00:00Z`);
  d.setUTCMonth(d.getUTCMonth() - 1);
  return d.toISOString().slice(0, 10);
}

/** Último día del mes anterior, formato yyyy-MM-dd. */
export function ultimoDiaMesAnteriorLima(): string {
  return sumarDias(primerDiaMesLima(), -1);
}

/** Momento actual como texto en hora de Lima: dd/MM/yyyy HH:mm. */
export function ahoraLimaTexto(): string {
  return formatearFechaLima(new Date().toISOString(), true);
}

/** yyyy-MM-dd -> dd/MM/yyyy */
export function fechaCorta(fecha: string): string {
  const [a, m, d] = fecha.split('-');
  return `${d}/${m}/${a}`;
}

/** yyyy-MM -> "noviembre de 2026" */
export function nombreMes(mes: string): string {
  return new Date(`${mes}-01T12:00:00`).toLocaleDateString('es-PE', { month: 'long', year: 'numeric' });
}
