# Panel de control — Fase 1 (v0.6)

## Objetivo
Panel de control WPF (Fluent, WPF UI) que cumple la **Fase 1 de la especificación v0.6**
(`especificacion-bot-bbva-doclm-v0.6-2.md`): navegación lateral, panel principal completo
y vista de configuración, todo con motor simulado (mock).

## Alcance (completado)
- Navegación lateral: **Panel principal** y **Configuración** (vista interna + Atrás).
- Panel principal: indicadores operativos, contadores del día, sección de progreso por
  las **10 etapas**, contexto del lote en curso, log detallado y dependencias.
- Configuración: ventana de ejecución (HH:mm, cruza medianoche, bloqueo en ejecución) y
  archivos por lote por rango de páginas (1–5, >200 fijo en 0), con guardado explícito y
  confirmación "¿Guardar cambios?".
- Motor simulado v2: lotes de hasta 5 registros, 10 etapas, exclusión >200 páginas,
  ventana de ejecución, parada ordenada (DETENIENDO) y estados bot/ejecución.

## Checklist
- [x] Modelos de dominio v0.6 (EstadoBot, EstadoEjecucion, EtapaProceso, RangoPaginas, ConfigOperativa, LogEntry+Documento/Estado, Contadores+Excluidos).
- [x] Motor simulado v2 (lotes, etapas, ventana, parada ordenada, >200 págs).
- [x] MainViewModel (indicadores + 10 etapas + contexto + dependencias + navegación).
- [x] ConfiguracionViewModel (ventana + archivos por lote + guardar/atrás).
- [x] Vista Panel principal.
- [x] Vista Configuración.
- [x] Navegación lateral.
- [x] Pruebas (rangos, ventana cruza medianoche, contadores, motor).
- [x] Build + verificación visual.

## Verificación
- `dotnet build BotBBVA.sln` → 0 errores, 0 advertencias.
- `dotnet test` → 20/20 en verde.
- `BotBBVA.Panel.exe` compila y arranca (proceso verificado).

## Ruta elegida
Inline (el parent construye directamente). Motivo: coherencia visual de un único entregable
UI con WPF UI v4 + paleta BBVA; delegar habría exigido transferir todo ese contexto.

## Próximo paso
Feedback visual del usuario; luego Fase 2 (servicio de Windows + Named Pipes reemplazando el mock).
