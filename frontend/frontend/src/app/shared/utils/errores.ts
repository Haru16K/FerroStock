// Texto de error para mostrar al usuario según lo que respondió (o no respondió) el servidor.
// Evita el mensaje genérico cuando se puede decir qué pasó realmente.
export function mensajeError(e: any, porDefecto: string): string {
  const msg = e?.error?.message;
  if (typeof msg === 'string' && msg) return msg;

  switch (e?.status) {
    case 0:
      return 'No se pudo conectar con el servidor. Verifica que el backend esté en ejecución.';
    case 401:
      return 'Tu sesión no es válida. Vuelve a iniciar sesión.';
    case 403:
      return 'No tienes permiso para ver esta información.';
    case 404:
      return `${porDefecto}: el servidor no tiene este servicio. Reinicia el backend con la última versión.`;
    default:
      return e?.status >= 500
        ? `${porDefecto}: error interno del servidor (HTTP ${e.status}). Revisa la consola donde corre dotnet run.`
        : `${porDefecto} (HTTP ${e?.status ?? '?'}).`;
  }
}
