# Especificación Técnica y de Negocio
## Bot de Automatización BBVA – Integración con DocLM

**Versión:** 0.7 (flujo y autenticación de DocLM incorporados)
**Cliente final:** BBVA (a través de Michael Page)
**Plazo estimado:** 1 mes a 1 mes y medio
**Estado:** lógica de negocio definida; pendiente confirmar credenciales de DocLM, detalles del contrato de la API (ver sección 11) y responsable de los procedimientos almacenados

---

## 1. Resumen ejecutivo

Solución para Windows compuesta por un **servicio de Windows** (el motor del bot) y un **panel de control** de escritorio (`.exe`), que procesa de forma continua un backlog histórico de documentos (registros desde 2017, potencialmente millones). Por cada registro:

1. Obtiene la ruta del documento desde SQL Server.
2. Recupera el archivo desde un servidor de archivos.
3. Lo envía a la API de **DocLM** (servicio externo de IA que extrae datos).
4. Recibe un JSON con los datos extraídos.
5. Actualiza la base de datos **solo** con los campos que vengan con valor.
6. Deja trazabilidad en dos logs independientes (técnico y operativo para BBVA).

El bot corre como **una sola instancia en un servidor dedicado** de BBVA. El Excel operativo se deposita en una carpeta de un **servidor de archivos**, para que el equipo de operación de BBVA solo tenga que revisar esa ruta. El bot trabaja de forma autónoma: una vez iniciado, sigue funcionando aunque se cierre la ventana de control o la sesión remota del servidor.

---

## 2. Alcance

### Incluido
- Servicio de Windows que ejecuta el motor del bot sin depender de ninguna sesión de usuario.
- Panel de control de escritorio (Iniciar / Detener y log en vivo) para operar y monitorear el servicio.
- Ciclo continuo: barrido de backlog y luego modo escucha.
- Integración con SQL Server (procedimientos almacenados), servidor de archivos y API DocLM.
- Bloqueo defensivo a nivel de fila en el SP de selección (buena práctica aunque hoy sea una sola instancia).
- Log técnico diario y Excel operativo diario.

### Fuera de alcance (por ahora)
- Pausar / Reanudar (se usa Detener + Iniciar).
- Reintentos automáticos ante error.
- Ejecución en varias instancias simultáneas (hoy el bot corre como una sola instancia en un servidor).
- Reprocesamiento automático de registros en error.

---

## 3. Lógica de negocio

### 3.1 Ciclo de vida del bot

El servicio de Windows arranca solo junto con el servidor y permanece activo de forma independiente del panel. El motor de procesamiento se activa cuando un operador presiona **Iniciar bot** en el panel; abrir el panel no inicia el bot.

**Iniciar bot significa activar el bot para que respete la ventana de ejecución configurada.** Si el bot se inicia fuera de dicha ventana, no procesa documentos inmediatamente: permanece en estado activo/espera hasta que se abra la ventana. Por tanto, se distinguen dos conceptos:

- **Estado del bot:** `INICIADO` o `DETENIDO`.
- **Estado de ejecución:** `FUERA_DE_HORARIO`, `EJECUTANDO` o `DETENIENDO`.

La ventana de ejecución se configura en formato `HH:mm` de 24 horas. La hora de inicio y la hora de fin no pueden ser iguales. Se admiten ventanas que crucen medianoche; por ejemplo, `20:00–06:00` representa una única ventana continua desde las 20:00 hasta las 06:00 del día siguiente.

Si el bot está iniciado y llega la hora de fin, deja de tomar nuevos lotes, termina el lote que esté en curso, genera/consolida el Excel Log correspondiente y posteriormente queda **fuera de horario**, esperando la siguiente apertura de la ventana. El servicio de Windows continúa activo.

Al presionar **Detener bot**, si existe un lote en procesamiento, la orden no interrumpe abruptamente las operaciones. Se completa el lote actual, se genera/consolida el Excel Log y solo después el bot pasa a `DETENIDO` y se habilita nuevamente **Iniciar bot**. Mientras se ejecuta esta parada ordenada, **Iniciar bot** permanece deshabilitado.

El último estado del bot y la configuración operativa deben persistirse de forma que, ante un reinicio del servicio o del servidor, se pueda recuperar el estado y continuar según las reglas definidas. El comportamiento exacto de reanudación automática tras reinicio queda sujeto a confirmación final con el equipo.

Una vez iniciado y dentro de horario opera en dos modos:

| Modo | Cuándo | Comportamiento |
|---|---|---|
| **Backlog** | Hay registros pendientes | Procesa lotes de registros sin pausa hasta agotar los pendientes o hasta que corresponda detener la toma de nuevos lotes. |
| **Escucha** | No quedan pendientes | Consulta cada X tiempo (configurable) esperando registros nuevos dentro de la ventana de ejecución. |

Cuando aparecen nuevos pendientes en modo escucha, vuelve a modo backlog automáticamente.

### 3.2 Fase 1: obtención, validación y agrupación de documentos

1. Ejecutar el SP de selección, que devuelve **hasta 5 registros pendientes por consulta** y los marca como `EN_PROCESO` en la misma operación atómica. El lote SQL de 5 registros es independiente del tamaño de los lotes que posteriormente se envían a DocLM.
2. Procesar los registros obtenidos de forma individual para recuperar cada archivo desde el servidor de archivos.
3. Si un registro no tiene ruta de archivo, se marca como `SIN_RUTA` y se continúa con los demás registros del lote.
4. Para cada archivo recuperado, contar el número de páginas **antes de enviarlo a DocLM**.
5. Si un documento tiene **más de 200 páginas**, se excluye individualmente del procesamiento DocLM, se registra el motivo y no se envía a DocLM. Esta exclusión no impide continuar con los demás registros válidos del mismo lote SQL.
6. Los documentos procesables se clasifican en rangos fijos:
   - `1–50` páginas.
   - `51–100` páginas.
   - `101–200` páginas.
   - `>200` páginas: exclusión fija, `0` archivos por lote DocLM.
7. Para cada uno de los tres rangos procesables, agrupar documentos del **mismo rango** según la configuración `Archivos por lote`, cuyo valor permitido es de **1 a 5**. No se mezclan documentos de rangos diferentes en una misma solicitud DocLM.
8. Obtener un token de acceso de DocLM (ver 3.2.1) y preparar cada archivo según el formato que exige la operación *Create Document* (**a confirmar**: Base64, multipart u otro).
9. Enviar cada lote a la API de DocLM siguiendo el flujo de procesamiento descrito en 3.2.2 y esperar los resultados. Una consulta SQL de 5 registros puede generar una o varias solicitudes a DocLM dependiendo de la clasificación por páginas y de la configuración de archivos por lote.

### 3.2.1 Autenticación con DocLM

DocLM usa **OAuth 2.0 con client credentials** sobre Microsoft Entra ID. Antes de llamar a la API, el bot obtiene un token de acceso:

- **Petición:** `POST https://login.microsoftonline.com/{tenant_id}/oauth2/v2.0/token`
- **Cuerpo:** `x-www-form-urlencoded` con `client_id`, `client_secret`, `scope` y `grant_type`.
- **`scope`:** `api://wtw-crbra-doclm-p/.default`
- **`grant_type`:** `client_credentials`

El bot debe obtener el token **antes** de iniciar el flujo de procesamiento de un lote: la obtención (o reutilización, si sigue vigente) del token es siempre el primer paso, ya que ninguna llamada a la API de DocLM puede hacerse sin él.

Reglas de uso:

- El `tenant_id`, el `client_id` y el `client_secret` se entregan por configuración segura (sección 8); **nunca** se escriben en el código, en el repositorio ni en esta especificación.
- El token se mantiene solo en memoria, se reutiliza mientras esté vigente y se renueva antes de expirar o ante una respuesta de no autorizado.
- **Duración y renovación:** la respuesta del endpoint de token incluye `expires_in`, la cantidad de segundos que dura el token. El bot guarda ese valor, calcula el momento exacto de expiración y lo reutiliza hasta un poco antes de ese momento (margen de seguridad configurable, por ejemplo 1 minuto), sin necesidad de que nadie le indique de antemano cuánto dura.
- **Renovación en caliente, no al final del lote:** antes de cada llamada a la API de DocLM (no solo al iniciar un lote), el bot verifica si el token sigue vigente. Si ya expiró o está por expirar, lo renueva en ese momento y continúa con la llamada pendiente, sin esperar a que termine el lote en curso. Esto evita que un lote largo (por ejemplo, 5 archivos con varias esperas de 20 segundos) se quede con un token vencido a mitad de proceso.
- Como respaldo adicional, si pese a esta verificación llega una respuesta de no autorizado, el bot renueva el token una vez y reintenta esa misma llamada (regla ya definida en 3.2.2).
- Ni el token ni el secreto se registran en ningún log.

### 3.2.2 Flujo de procesamiento en DocLM

Base de la API: `https://api.doclm.ai.wtwco.com/v2/COLDOCSPROD`. El procesamiento de un documento sigue estos pasos:

| Paso | Operación | Método y ruta | Resultado |
|---|---|---|---|
| 1 | Create Document (subida del documento) | `POST /documents` | `id_document` |
| 2 | Esperar 20 segundos | — | — |
| 3 | Get Document | `GET /documents/{id_document}` | `state` |
| 4 | Si `state == pending` | Volver al paso 2 | — |
| 5 | Si `state == processed`: Run Assessment | `POST /documents/{id_document}/assessments?assessment_id={id_assesment}&batch=true` | `id_run` y `state` (inicialmente `pending`) |
| 6 | Esperar 20 segundos | — | — |
| 7 | Get Document Assessment | `GET /documents/{id_document}/assessments/{id_run}` | `state` y, al terminar, los resultados |
| 8 | Si `state == pending` | Volver al paso 6 | — |
| 9 | `state == processed` | Entregar los **resultados** al flujo de la fase 2 | JSON de resultados |

Estados de DocLM: tanto el documento como el assessment solo devuelven dos valores de `state`: **`pending`** (todavía en proceso) y **`processed`** (terminado). Un `processed` es la condición para avanzar.

Por qué se espera tras crear el documento: *Create Document* devuelve el `id_document` apenas se recibe la solicitud, pero el documento puede no estar cargado del todo. Por eso se espera y se consulta su estado antes de ejecutar el assessment. El `id_document` y el `id_run` se conservan en memoria durante todo el flujo, porque cada consulta posterior los necesita.

Notas de diseño:

- Hay **dos ciclos de consulta** (documento y assessment), cada uno con espera de 20 segundos. El intervalo es configurable (sección 7.5), con 20 segundos como valor inicial.
- Mientras se esperan los ciclos, el panel muestra las etapas *Enviando a DocLM* y *Esperando respuesta de DocLM* (sección 5.2).
- Los ciclos deben respetar la **parada ordenada**: la espera es cancelable, y la parada termina el lote actual según la sección 3.3.2.
- El flujo **no define** un tiempo máximo de espera, y como solo existen `pending` y `processed`, un documento que se quede en `pending` indefinidamente nunca terminaría solo. Propuesta: un tiempo máximo configurable por ciclo y, al superarlo, marcar el registro como `ERROR` con el detalle (sección 3.5).
- *Create Document* siempre devuelve un `id_document` al crear el documento. Si la petición falla por completo (error de red, de servidor, etc.), el registro se marca como `ERROR` con el detalle (sección 3.5).
- Una respuesta de no autorizado durante el flujo provoca la renovación del token y un único reintento de esa llamada.

### 3.3 Fase 2: actualización

1. Con cada respuesta JSON de DocLM, identificar los registros originales correspondientes y ejecutar el SP de actualización usando sus respectivos IDs.
2. Se actualiza columna por columna **solo** si la llave del JSON trae valor. Una llave vacía nunca sobrescribe el dato existente.
3. Marcar cada registro con su estado final.
4. Registrar en el Excel operativo el resultado de cada documento procesado, excluido o con error.

### 3.3.1 Regla de rangos y configuración de lotes DocLM

Los rangos de páginas son **fijos y protegidos**; el operador no puede editarlos ni eliminarlos. Solo puede modificar `Archivos por lote` para los tres rangos procesables.

| Rango de páginas | Archivos por lote | Procesamiento |
|---|---:|---|
| `1–50` | 1–5 | Configurable |
| `51–100` | 1–5 | Configurable |
| `101–200` | 1–5 | Configurable |
| `>200` | 0 | Exclusión fija; nunca se envía a DocLM |

Ejemplo: dos documentos de 150 y 180 páginas pertenecen al rango `101–200` y pueden enviarse juntos a DocLM si `Archivos por lote = 2`. Si quedan más documentos del mismo rango, se continúa creando lotes hasta agotar los documentos disponibles.

### 3.3.2 Parada ordenada durante el procesamiento

Si el operador presiona **Detener bot** mientras se ejecuta cualquier etapa de un lote —consulta al SP, obtención del archivo, validación de páginas, envío a DocLM, espera de respuesta o actualización de la base de datos— la orden de detención se registra pero **no cancela abruptamente el lote en curso**.

El comportamiento es:

1. Mostrar al operador un mensaje claro, por ejemplo: **“Detención solicitada. El bot finalizará el lote actual antes de detenerse.”**
2. No tomar nuevos lotes.
3. Finalizar correctamente el lote que ya estaba en ejecución.
4. Generar/consolidar el **Excel Log** con los resultados de lo procesado.
5. Cambiar el estado del bot a `DETENIDO`.
6. Habilitar nuevamente **Iniciar bot**.

No se establece un tiempo fijo de dos minutos para la detención, porque la duración real depende de la etapa en curso y de las respuestas de los sistemas externos.

La misma regla de finalización ordenada se aplica cuando termina la ventana de ejecución: se dejan de tomar nuevos lotes, se termina el lote actual, se genera/consolida el Excel Log y el bot queda fuera de horario si continúa en estado `INICIADO`.

### 3.4 Estados de un registro

| Estado | Significado | ¿El bot lo vuelve a tomar? |
|---|---|---|
| `PENDIENTE` | Sin procesar | Sí |
| `EN_PROCESO` | Tomado por una instancia | No |
| `ACTUALIZADO` | Datos actualizados correctamente | No |
| `SIN_DATOS` | DocLM respondió sin ningún valor útil | No |
| `SIN_RUTA` | El registro no tiene ruta de archivo | No |
| `ERROR` | Falló algún paso (archivo, red, DocLM, BD) | No (reproceso manual) |

> **Decisión a confirmar:** un registro sin ruta, si se dejara como `PENDIENTE`, sería devuelto una y otra vez por el SP de selección (bucle infinito). Por eso se propone estado propio `SIN_RUTA` o filtrar `RutaArchivo IS NOT NULL` en el SP.

### 3.5 Manejo de errores (versión inicial)

- Ante cualquier falla (archivo inexistente, red, timeout, error de DocLM, error de BD) se marca `ERROR` con un mensaje y el bot continúa con el siguiente registro.
- Sin reintentos automáticos en esta versión.
- Los registros en `ERROR` quedan fuera del flujo automático. Reprocesar implica devolverlos manualmente a `PENDIENTE` en la base de datos.
- Una falla masiva (por ejemplo, DocLM caído) no debe marcar millones de registros como error. Ver mejora propuesta en la sección 10.

### 3.6 Concurrencia (nota de diseño)

**Cambio de alcance:** el bot ya no se distribuye a varias máquinas de usuario; corre como **una única instancia en un servidor dedicado**. Esto elimina el riesgo de que dos instancias tomen el mismo registro al mismo tiempo.

Aun así, se mantiene el bloqueo atómico en el SP de selección (`UPDLOCK, ROWLOCK, READPAST`, marcando `EN_PROCESO`) como buena práctica defensiva: protege ante un eventual reinicio del servicio mientras hay un proceso a medias, y deja la puerta abierta si en el futuro se decide escalar a más de una instancia.

- El SP de selección solo devuelve registros en estado `PENDIENTE`.
- No se requiere lógica adicional de coordinación entre instancias por ahora.

> **Pendiente:** definir si el SP lo construye este equipo o el área de base de datos de BBVA. Los scripts de la sección 6 son una propuesta de referencia.

### 3.7 Recuperación de registros huérfanos

Si el bot se detiene o el servidor se reinicia con un registro en `EN_PROCESO`, ese registro quedaría bloqueado. Propuesta:

- Al presionar **Detener**, el bot termina el registro actual y luego se detiene (parada ordenada).
- Un SP de mantenimiento devuelve a `PENDIENTE` los registros `EN_PROCESO` cuya `FechaInicioProceso` supere un umbral configurable (por ejemplo, 30 minutos). El bot lo ejecuta al iniciar y periódicamente.
- Si el servicio se reinicia (por una caída o por un reinicio del servidor), al levantar ejecuta primero este SP y luego reanuda el ciclo según el último estado guardado.

---

## 4. Interfaz de usuario (panel de control)

La solución se divide en dos piezas (ver sección 7.2): el **servicio de Windows**, que ejecuta el motor del bot, y el **panel de control**, una ventana visual que solo sirve para operar y monitorear el servicio. Cerrar completamente el panel WPF o cerrar la sesión remota del servidor **no afecta** al servicio ni al proceso en curso. Al volver a abrir el panel, este consulta al servicio y muestra el estado actual, sin iniciar una nueva instancia ni reiniciar el procesamiento.

- Panel **WPF** con aspecto cuidado, moderno y simple.
- Navegación lateral dentro de la misma aplicación, con las opciones **Panel principal** y **Configuración**.
- La opción **Configuración** abre una vista interna, no una ventana independiente, y dispone de un botón **Atrás** para regresar al panel principal sin cerrar la aplicación ni perder el contexto operativo.
- Controles:
  - **Iniciar bot**: activa el bot para que respete la ventana de ejecución. Si se inicia fuera de horario, permanece activo esperando la apertura de la ventana.
  - **Detener bot**: solicita una parada ordenada. Si existe un lote en curso, se termina antes de detener el bot. Durante la detención, **Iniciar bot** permanece deshabilitado.
  - **Abrir Excel de logs**: abre la carpeta del servidor de archivos donde se deposita el Excel del día.
- **Indicadores principales**: estado del servicio, estado del bot, estado de ejecución, ID, tiempos, total procesado, procesado del día, pendientes, documentos en procesamiento, última ejecución y demás contadores operativos.
- **Sección visual de estado y progreso**, separada del log y ubicada debajo de los indicadores principales, con las 10 etapas definidas en la sección 5.
- **Panel de log en vivo**: tabla o consola estilizada con hora, nivel, ID de registro/tarea, documento, estado y mensaje de resultado. Coloreado por nivel y con autoscroll.
- **Dependencias**: indicadores para SQL Server/BD, servidor de archivos y DocLM, mostrando disponibilidad y un botón **Probar conexión** para cada dependencia.
- Al abrirse, el panel muestra el estado actual y las últimas líneas del log técnico, de modo que se puede abrir y cerrar cuantas veces se necesite sin perder contexto.
- Si el servicio no responde, el panel lo indica de forma clara y no intenta ejecutar el proceso por su cuenta.
- Se descartó Pausar/Reanudar: Detener + Iniciar cumple la misma función porque el motor siempre retoma el siguiente registro pendiente.

### 4.1 Configuración operativa

La vista de configuración debe ser interactiva y mantener la navegación lateral visible para permitir volver al Panel principal mediante **Atrás**. Los cambios se gestionan dentro de la misma aplicación WPF y no requieren abrir una segunda ventana.

El panel incluye una sección **Configuración** para los parámetros operativos que pueden ser administrados por el operador. La configuración es persistida por el servicio en un archivo JSON local; el panel no modifica directamente dicho archivo, sino que solicita los cambios al servicio mediante el canal Named Pipes.

#### Ventana de ejecución

- Hora de inicio y hora de fin en formato `HH:mm` de 24 horas.
- Las horas no pueden ser iguales.
- Se permiten ventanas que crucen medianoche.
- Si el bot está iniciado fuera de la ventana, permanece activo esperando.
- Mientras el bot esté iniciado o ejecutándose, los campos de horario permanecen bloqueados.
- Para editar el horario, el operador debe detener el bot y esperar a que finalice la parada ordenada y la generación/consolidación del Excel Log.
- Mensaje de ayuda de la interfaz: **“Para editar la hora de ejecución del bot, por favor detenga su ejecución.”**

#### Configuración de archivos por lote

La tabla muestra los cuatro rangos fijos de páginas y el valor `Archivos por lote`. Los rangos no se pueden editar ni eliminar. Solo los valores de los tres rangos procesables pueden modificarse y deben mantenerse entre `1` y `5`. El rango `>200` mantiene siempre `0`.

Cada fila tiene una acción visual de **editar** (por ejemplo, icono de lápiz). Al editar se habilita únicamente el valor de `Archivos por lote`. Si el valor no cambia, no se requiere actualización. Si cambia, debe guardarse explícitamente.

Si existen cambios sin guardar y el operador intenta salir, el panel pregunta **“¿Guardar cambios?”**. Si confirma, el panel solicita al servicio guardar la nueva configuración en JSON. Si no confirma, los cambios se descartan.

La misma lógica de cambios sin guardar aplica a la configuración de horario.

#### Configuración de endpoints

El panel incluye una sección **Endpoints** dentro de Configuración, con la lista de URLs externas que usa el bot: el endpoint de token de autenticación de DocLM y los endpoints de la API de DocLM (crear documento, consultar documento, ejecutar assessment, consultar assessment).

Cada endpoint se muestra en modo lectura con una acción de **editar** (icono de lápiz). Al editar, el campo de la URL se vuelve editable. El botón **Guardar** permanece deshabilitado mientras el valor no cambie respecto al guardado, y se habilita únicamente cuando el valor editado es distinto al original. Al guardar, el panel solicita al servicio escribir el nuevo valor en el JSON de configuración (sección 7.5), sin necesidad de recompilar ni redesplegar el bot.

Las credenciales (`client_id`, `client_secret`, tenant) no forman parte de esta sección: se gestionan como se describe en la sección 8 y nunca se muestran ni se editan en texto plano desde el panel.

La misma sección incluye un campo para el **tiempo de espera entre consultas**: un campo de texto numérico con el valor `20` por defecto y la palabra "segundos" junto al campo, acompañado de un mensaje explicativo indicando que es el tiempo que el bot espera antes de volver a consultar el estado de un documento o de un assessment mientras su `state` siga en `pending`. Sigue la misma lógica de edición con lápiz y guardado solo ante cambios.

Valores como `id_document`, `id_run` y `state` son generados y leídos automáticamente por el bot durante la ejecución de cada registro; no son parámetros de configuración y no aparecen como campos editables en el panel.

---

## 5. Estado y progreso visual del procesamiento

El panel debe mostrar una sección visual de **estado y progreso del procesamiento separada del log detallado**. Esta sección se ubica debajo de los indicadores principales y permanece visible mientras el panel está abierto, permitiendo identificar inmediatamente en qué etapa se encuentra el bot.

### 5.1 Indicadores principales

La parte superior del panel mantiene los indicadores operativos solicitados para monitoreo, incluyendo como mínimo:

- Estado del servicio.
- Estado del bot: `INICIADO` / `DETENIDO`.
- Estado de ejecución: `FUERA_DE_HORARIO` / `EJECUTANDO` / `DETENIENDO`.
- ID de la tarea/registro o lote actualmente procesado.
- Hora de inicio.
- Hora de última ejecución.
- Tiempo de ejecución.
- Total de documentos procesados.
- Documentos procesados durante el día.
- Documentos pendientes por procesar.
- Documentos actualmente en procesamiento.
- Última ejecución y su resultado.
- Contadores operativos del día: actualizados, sin datos, sin ruta, excluidos por páginas y errores.

### 5.2 Etapas visibles del proceso

Debajo de los indicadores principales se presenta un componente visual de progreso por etapas. Debe mostrar claramente la etapa actual, las etapas completadas y las etapas pendientes. Las etapas y su orden son:

1. **Consultando SP**
2. **Obteniendo archivo del servidor**
3. **Validación de archivos**
4. **Enviando a DocLM**
5. **Esperando respuesta de DocLM**
6. **Leyendo información de DocLM para actualizar la base de datos**
7. **Base de datos actualizada**
8. **Generando Excel**
9. **Excel generado**
10. **Fin de proceso**

El componente debe permitir identificar visualmente algo equivalente a:

- **Completado:** la etapa ya terminó.
- **En curso:** la etapa actual del bot.
- **Pendiente:** aún no se ejecuta.

La finalidad es que el operador pueda responder rápidamente a la pregunta: **“¿En qué paso está el bot ahora?”** sin tener que revisar todo el log.

### 5.3 Contexto del procesamiento actual

Cuando exista un lote en ejecución, la sección visual debe mostrar, cuando aplique:

- ID de tarea/registro.
- Documento o documentos del lote.
- Rango de páginas aplicable.
- Cantidad de archivos del lote y capacidad configurada.
- Tiempo transcurrido.
- Mensaje contextual de la etapa actual.

### 5.4 Log detallado independiente

El log permanece como un componente separado y está orientado a trazabilidad y diagnóstico. Cada entrada debe incluir, como mínimo:

- Hora.
- Nivel: información, advertencia o error.
- ID de tarea/registro.
- Documento.
- Estado.
- Mensaje de resultado o detalle técnico.

El estado visual responde **“¿en qué paso está el bot?”**, mientras que el log responde **“¿qué ocurrió?”**. Ambos deben coexistir sin sustituirse entre sí.

### 5.5 Comportamiento visual durante una parada ordenada

Mientras el bot está finalizando el lote actual, la sección de estado debe mostrar claramente `DETENIENDO` y la etapa en curso. El botón **Iniciar bot** permanece deshabilitado hasta que el lote termine y el Excel Log haya sido generado/consolidado.

## 5. Logging

### 5.1 Log técnico (equipo de desarrollo)

- Se transmite en vivo al panel de control (el servicio envía cada entrada) y se guarda en archivo de texto. Si el panel está cerrado, el servicio sigue escribiendo el archivo con normalidad.
- **Un archivo por día** (`bot-AAAA-MM-DD.log`), rotación automática a medianoche para que nunca crezca demasiado.
- Contenido: ciclo de vida, llamadas a SP con duración, respuesta de DocLM (sin datos sensibles), excepciones con stack trace.
- Carpeta: `Logs/Tecnico/` junto al ejecutable del servicio, en el propio servidor donde corre el bot (decisión confirmada; no se envía al servidor de archivos). El Excel operativo para BBVA es lo único que se deposita en el servidor de archivos. Las rutas relativas se resuelven desde el directorio de la aplicación y no desde el directorio de trabajo de Windows, que en un servicio es `System32`.

### 5.2 Excel operativo (BBVA)

- Orientado a auditoría por parte de personal de BBVA.
- **Un archivo por día**: `AAAA-MM-DD.xlsx`, depositado en una **carpeta del servidor de archivos** (ruta a confirmar), de modo que el equipo de operación solo entra a esa ubicación a revisar el resultado del día.
- Columnas sugeridas:

| Columna | Descripción |
|---|---|
| Fecha y hora | Momento del procesamiento |
| ID registro | Identificador en la tabla |
| Ruta archivo | Ruta original |
| Resultado | Actualizado / Pendiente / Sin ruta / Error |
| Campos actualizados | Lista de columnas modificadas |
| Detalle | Motivo del error o de que no se actualizó |
| Instancia | Nombre del servidor donde corrió el bot |

- Escritura en lotes (memoria y volcado cada N registros o cada X minutos, y siempre al detener) para no abrir y cerrar el Excel millones de veces.
- Si el servidor de archivos no está disponible al momento de escribir, se guarda una copia temporal local y se reintenta hasta que la carpeta vuelva a estar accesible, sin perder resultados.
- **Formato exacto de columnas y presentación: pendiente de definir con el cliente.**

---

## 6. Integración con procedimientos almacenados

> Los procedimientos almacenados son proporcionados y mantenidos por el equipo de base de datos de BBVA. El alcance del bot contempla **consumir** estos procedimientos, no construir ni modificar tablas, índices o procedimientos almacenados.

El bot utilizará los procedimientos almacenados como contratos de integración con la base de datos. Los nombres definitivos, parámetros, tipos de datos y estructuras de respuesta serán los proporcionados por BBVA.

### 6.1 Procedimiento de consulta de pendientes

El bot necesita un procedimiento almacenado que permita obtener un lote de hasta **5 registros pendientes** para procesamiento.

Como mínimo, el resultado debe proporcionar al bot la información necesaria para:

- Identificar de forma única el registro.
- Obtener la ruta del documento en el servidor de archivos.
- Conocer el estado necesario para iniciar el procesamiento.
- Reservar o marcar los registros como `EN_PROCESO`, según la implementación definida por BBVA.

La lógica interna utilizada para seleccionar, ordenar y bloquear los registros pertenece al procedimiento almacenado y no forma parte de la implementación del bot.

### 6.2 Procedimiento de actualización de datos

El bot necesita un procedimiento almacenado para enviar los datos extraídos por DocLM y solicitar la actualización del registro correspondiente.

El bot deberá proporcionar, como mínimo:

- Identificador del registro.
- Datos extraídos que correspondan al contrato definido para DocLM/BBVA.
- Estado o resultado del procesamiento cuando el procedimiento lo requiera.

La regla de negocio del bot es que los valores vacíos recibidos desde DocLM **no deben utilizarse para sobrescribir información existente**. La forma en que esta regla se materialice dentro de la base de datos será responsabilidad del procedimiento proporcionado por BBVA, salvo que BBVA indique otro contrato.

### 6.3 Procedimiento para marcar estados

El bot necesita disponer de una operación proporcionada por BBVA para registrar estados finales o incidencias que no puedan resolverse mediante el procedimiento de actualización.

Entre los estados utilizados por la lógica del bot se encuentran:

- `ACTUALIZADO`
- `SIN_DATOS`
- `SIN_RUTA`
- `ERROR`

Los nombres definitivos y valores aceptados deberán coincidir con el contrato real de BBVA.

### 6.4 Procedimiento para recuperación de registros en proceso

Si BBVA proporciona un procedimiento para liberar registros que hayan quedado en `EN_PROCESO` después de una interrupción inesperada, el bot deberá poder invocarlo durante el arranque y/o según la periodicidad definida.

El umbral de tiempo, parámetros y reglas de recuperación serán los establecidos por BBVA.

### 6.5 Información requerida a BBVA

Para integrar los procedimientos almacenados, BBVA deberá proporcionar:

- Nombre de cada procedimiento.
- Parámetros de entrada y sus tipos.
- Resultado esperado y columnas devueltas, cuando corresponda.
- Estados válidos.
- Reglas para reservar registros.
- Reglas para actualizar datos.
- Reglas para liberar registros en proceso, si aplica.
- Restricciones o consideraciones de seguridad para la cuenta de servicio.

**El bot no requiere conocer ni modificar la estructura completa de las tablas para ejecutar su lógica; requiere conocer el contrato de los procedimientos que BBVA exponga para la integración.**

---

## 7. Especificación técnica

### 7.1 Stack

| Elemento | Decisión |
|---|---|
| Lenguaje | C# |
| Plataforma | .NET 8 (LTS), versión fijada en el `.csproj` |
| Motor del bot | Worker Service de .NET hospedado como servicio de Windows (`Microsoft.Extensions.Hosting.WindowsServices`) |
| Panel de control | WPF (patrón MVVM ligero), con la librería **WPF UI** (paquete NuGet, gratuita y de código abierto) para una estética moderna tipo Fluent Design, sin necesidad de instalar nada fuera del proyecto ni permisos de administrador |
| Comunicación panel ↔ servicio | Named Pipes locales con mensajes JSON y acceso restringido |
| BD | SQL Server (`Microsoft.Data.SqlClient`, opcional Dapper) |
| Logging técnico | Serilog (archivo con rotación diaria + envío en vivo al panel) |
| Excel | ClosedXML (o EPPlus/OpenXML SDK) |
| Resiliencia HTTP | `HttpClient` con timeouts y Polly (cuando se definan reintentos) |
| Configuración | `appsettings.json` + credenciales protegidas |
| Inyección de dependencias | `Microsoft.Extensions.DependencyInjection` / Generic Host |

**Motivo de elegir C# sobre Python:** ejecutable nativo de Windows, más liviano y con menos riesgo de falsos positivos de antivirus corporativo que un `.exe` empaquetado con PyInstaller, algo relevante en un entorno bancario.

**Compatibilidad de IDE:** el proyecto se crea o edita en Visual Studio 2022 (equipo corporativo) y 2026 (equipo personal). Se fija el SDK con un archivo `global.json` y el *target framework* en el `.csproj` para evitar diferencias al compilar en cada máquina.

### 7.2 Arquitectura en capas

Diseño simple, sin sobreingeniería: capas + inyección de dependencias + patrón repositorio. El motor vive en un servicio de Windows (no puede tener interfaz porque los servicios corren aislados de la sesión del usuario), y la interfaz es una aplicación aparte que se comunica con él.

El código debe seguir principios **SOLID** y buenas prácticas de diseño orientado a objetos (responsabilidad única por clase, dependencia de interfaces y no de implementaciones concretas, clases abiertas a extensión sin modificar lo existente), manteniendo la solución ordenada, legible y elegante, sin capas ni patrones innecesarios que no aporten valor real al problema.

```
BotBBVA.sln
│
├── src/
│   ├── BotBBVA.Servicio/            # Worker Service (servicio de Windows)
│   │   ├── Hosting/                 # Arranque, DI, BackgroundService
│   │   └── Control/                 # Servidor de Named Pipe (comandos y eventos)
│   │
│   ├── BotBBVA.Panel/               # WPF: panel de control
│   │   ├── Views/
│   │   ├── ViewModels/
│   │   └── Recursos/
│   │
│   ├── BotBBVA.Contratos/           # Mensajes compartidos panel <-> servicio
│   │                                # (comandos, estados, eventos de log)
│   │
│   ├── BotBBVA.Aplicacion/          # Lógica de negocio y orquestación
│   │   ├── Servicios/               # OrquestadorBot, ProcesadorRegistro
│   │   ├── Interfaces/              # Contratos (repositorios, clientes)
│   │   └── Modelos/                 # DTOs, estados, resultados
│   │
│   ├── BotBBVA.Datos/               # Acceso a datos
│   │   ├── Repositorios/            # DocumentoRepositorio (llama a los SP)
│   │   └── Conexion/                # Fábrica de conexiones
│   │
│   ├── BotBBVA.Integraciones/       # Sistemas externos
│   │   ├── DocLM/                   # Cliente HTTP de la API
│   │   └── Archivos/                # Lectura desde servidor de archivos
│   │
│   └── BotBBVA.Infraestructura/     # Transversal
│       ├── Logging/                 # Serilog, envío al panel, Excel operativo
│       ├── Configuracion/           # Opciones tipadas y estado persistente
│       └── Seguridad/               # Manejo de credenciales
│
├── tests/
│   └── BotBBVA.Pruebas/
├── deploy/                          # Scripts de instalación y desinstalación del servicio
├── docs/                            # Esta especificación
└── global.json
```

Reglas de dependencias:

- `Servicio → Aplicacion → (Datos | Integraciones | Infraestructura)`. La capa de aplicación depende de **interfaces**, no de implementaciones concretas.
- `Panel → Contratos`. El panel no conoce la lógica de negocio ni la base de datos; solo envía comandos y recibe estados y eventos.

### 7.3 Componentes principales

| Componente | Responsabilidad |
|---|---|
| `BotWorker` | `BackgroundService` que hospeda el orquestador y mantiene vivo el motor dentro del servicio de Windows. |
| `OrquestadorBot` | Ciclo continuo, alternancia backlog/escucha, control de horario, estados de ejecución, progreso por etapas y parada ordenada con `CancellationToken`. |
| `ProcesadorRegistro` | Ejecuta las fases 1 y 2 para un registro y devuelve el resultado. |
| `CanalControlServidor` | Named Pipe dentro del servicio: recibe comandos (Iniciar, Detener, ObtenerEstado, actualizar configuración) y emite estados, progreso por etapas y eventos de log. |
| `CanalControlCliente` | Lado del panel: se conecta al servicio, envía comandos y recibe eventos; se reconecta solo si el servicio se reinicia. |
| `EstadoPersistenteServicio` | Guarda el estado del bot y la configuración operativa persistente para recuperar el funcionamiento después de un reinicio. |
| `DocumentoRepositorio` | Invoca los SP (selección, actualización, marcar estado, liberar huérfanos). |
| `DocLmCliente` | Ejecuta el flujo de la sección 3.2.2 (crear documento, consultar estado, ejecutar assessment, consultar resultados) y deserializa el JSON de resultados. |
| `DocLmAutenticador` | Obtiene y renueva el token OAuth2 de DocLM (sección 3.2.1), lo guarda solo en memoria y lo entrega al cliente. |
| `ServidorArchivosServicio` | Lee el archivo desde la ruta de red. |
| `LogTecnicoServicio` | Logging estructurado con salida a archivo y al panel. |
| `ExcelOperativoServicio` | Acumula resultados y escribe/consolida el Excel diario por lotes en el servidor de archivos, incluyendo el cierre ordenado. |

### 7.4 Ejecución en segundo plano

- El motor corre dentro de un `BackgroundService` con `async/await`. Windows lo mantiene activo desde el arranque del servidor, sin sesión de usuario y sin que nadie se conecte.
- **Iniciar** habilita el ciclo de trabajo; **Detener** solicita una parada ordenada mediante `CancellationToken`, evita tomar nuevos lotes, espera a terminar el lote actual y consolida el Excel Log antes de dejar el bot detenido. El servicio sigue vivo para recibir la siguiente orden.
- El último estado se persiste (por ejemplo, en un archivo `estado.json` en la carpeta de datos de la aplicación) y se aplica al arrancar el servicio.
- El panel recibe estados y eventos de log por el canal de control. Si el panel se cierra o se desconecta, el servicio no se ve afectado: el log continúa escribiéndose en archivo y el Excel operativo sigue generándose.
- Manejo global de excepciones: un error en un registro nunca detiene el bot; un error fatal (por ejemplo, pérdida de conexión a BD) genera reintentos con espera creciente y queda en el log.
- Conexiones a BD de corta vida (una por operación) aprovechando el *connection pool*.
- Recuperación automática configurada en Windows: si el proceso del servicio falla, Windows lo reinicia.

### 7.5 Configuración (`appsettings.json`)

```json
{
  "Bot": {
    "IntervaloEscuchaSegundos": 60,
    "ReanudarUltimoEstado": true,
    "MinutosLimiteHuerfanos": 30,
    "NombreInstancia": ""          // Vacío = nombre del servidor
  },
  "DocLM": {
    "UrlBase": "https://api.doclm.ai.wtwco.com/v2/COLDOCSPROD",
    "TimeoutSegundos": 120,          // Por petición HTTP
    "IntervaloConsultaSegundos": 20, // Espera entre consultas de estado
    "TiempoMaximoEsperaSegundos": 0, // 0 = sin definir; a confirmar con DocLM
    "AssessmentId": "",              // Valor a confirmar con DocLM
    "Autenticacion": {
      "UrlToken": "https://login.microsoftonline.com/{TenantId}/oauth2/v2.0/token",
      "TenantId": "",                // Por entorno; no se versiona
      "ClientId": "",                // Por entorno; no se versiona
      "Scope": "api://wtw-crbra-doclm-p/.default"
    }
  },
  "Logs": {
    "CarpetaBase": "Logs",
    "RutaExcelOperativo": "",      // Carpeta del servidor de archivos (ruta UNC), a confirmar
    "LoteExcel": 50
  }
}
```

Las credenciales (incluido el `client_secret` de DocLM) **no** van en este archivo (ver sección 8). La configuración operativa de ventana de ejecución y archivos por lote se mantiene en un JSON administrado por el servicio, separado de `appsettings.json`.

### 7.6 Convenciones de código

- Programación **orientada a objetos**, aplicando principios **SOLID**, con estilo funcional puntual (transformaciones de datos, LINQ).
- **Máximo 400 líneas por archivo.** Si un archivo crece, se divide por responsabilidad.
- **Comentarios en cada función y en todo código relevante**, redactados en forma natural e impersonal, sin primera persona. Ejemplos:
  - `// Se valida que la ruta exista antes de intentar leer el archivo.`
  - `// Se refactoriza el nombre de la variable para reflejar que contiene la ruta de red.`
  - `// Se omiten las llaves vacías para no sobrescribir datos existentes.`
- Nombres descriptivos en español o inglés, pero consistentes en toda la solución.
- La integración con procedimientos almacenados se documenta mediante contratos de entrada/salida; su implementación interna no forma parte del código del bot.
- Comentarios XML (`///`) en interfaces y métodos públicos.

---

## 8. Seguridad

Aspectos críticos por tratarse de un entorno bancario:

- **Credenciales fuera del código y del repositorio.** Cadena de conexión y credenciales de DocLM (incluido el `client_secret` de OAuth2) se almacenan cifradas con DPAPI, ligadas al equipo o a la cuenta de servicio (no a la sesión de una persona). Preferible autenticación integrada de Windows con la cuenta de servicio contra SQL Server si BBVA lo permite.
- **Token de DocLM:** se mantiene solo en memoria y nunca se registra. Si alguna vez se compartió un `client_secret` por un canal no seguro (capturas, chats, correos), debe rotarse.
- **Mínimo privilegio:** el usuario de BD del bot solo ejecuta los SP necesarios, sin permisos directos de lectura/escritura sobre tablas.
- **Comunicaciones cifradas:** HTTPS/TLS con DocLM y conexión cifrada a SQL Server.
- **Datos sensibles fuera de los logs:** no registrar contenido de documentos, credenciales ni datos personales en el log técnico. El Excel operativo solo lleva lo necesario para auditoría.
- **Protección de las carpetas de logs** con permisos del sistema operativo. El Excel operativo se escribe con una cuenta de servicio con permiso de escritura solo sobre su carpeta en el servidor de archivos, y el equipo de operación con permiso de solo lectura.
- **Firma digital del ejecutable** (Authenticode) para facilitar la aprobación del equipo de seguridad y reducir alertas de antivirus.
- Validación de rutas de archivo para evitar lectura fuera de los servidores esperados.
- **Cuenta de servicio dedicada** con mínimo privilegio (idealmente una cuenta administrada de grupo, gMSA, si BBVA la ofrece) para ejecutar el servicio, sin contraseñas de personas.
- **Canal panel ↔ servicio restringido:** Named Pipe solo local, con permisos limitados a los grupos autorizados; no se abren puertos de red.
- Dependencias actualizadas y sin paquetes innecesarios.
- `.gitignore` estricto para no versionar secretos, `appsettings` con credenciales ni logs.

---

## 9. Despliegue

- Se publican dos artefactos *self-contained* (incluyen el runtime de .NET, sin instalar nada adicional): `BotBBVA.Servicio` y `BotBBVA.Panel`.
- **Servicio:** se instala una sola vez en el servidor con un script (`deploy/instalar-servicio.ps1`) que lo registra con inicio **Automático (retrasado)**, cuenta de servicio dedicada y recuperación ante fallos (reiniciar automáticamente). Un script equivalente lo desinstala.
- Tras un reinicio del servidor el servicio arranca solo, sin necesidad de que alguien inicie sesión, y retoma el trabajo según el último estado guardado.
- **Panel:** se copia en el servidor y se abre desde cualquier sesión (escritorio remoto de Windows, AnyDesk, etc.). Cerrarlo o cerrar la sesión remota no detiene el bot.
- **Logs:** el log técnico se guarda en la carpeta de la aplicación (`Logs/Tecnico`, junto al ejecutable del servicio); el Excel operativo se escribe directamente en la carpeta designada del servidor de archivos (ruta a confirmar).
- **Requisitos del servidor:** Windows Server, acceso de red a SQL Server, al servidor de archivos (lectura de documentos y escritura del Excel operativo) y a la API de DocLM; cuenta de servicio con esos permisos; sin suspensión automática.
- **Actualizaciones:** detener el servicio, reemplazar los binarios e iniciarlo de nuevo; el estado persistido evita perder el ciclo de trabajo.

---

## 10. Riesgos y mejoras propuestas

| Tema | Riesgo | Propuesta |
|---|---|---|
| Caída de DocLM o de red | Se marcarían miles de registros como `ERROR` seguidos | Interruptor de seguridad: tras N errores consecutivos, el bot entra en espera y avisa en el log en lugar de seguir marcando errores. |
| Registros huérfanos | Quedan `EN_PROCESO` tras un apagado | SP de liberación (sección 6.4). |
| Excel muy grande | Un archivo diario con muchos registros se vuelve lento | Escritura en lotes; evaluar CSV o partición por horas si el volumen lo exige. |
| Servidor de archivos no disponible al escribir el log | El Excel operativo no se podría guardar | Reintentar la escritura y, si persiste, guardar una copia temporal local hasta que el servidor de archivos vuelva a estar disponible. |
| Límites de la API | DocLM podría limitar peticiones por minuto; los ciclos de consulta cada 20 segundos multiplican las llamadas | Confirmar cuotas y agregar control de tasa. |
| Espera indefinida en DocLM | Un documento que nunca llega a `processed` bloquearía el avance | Tiempo máximo de espera configurable por ciclo; al superarlo se marca `ERROR` con el detalle y se continúa. |
| Token expirado o secreto rotado | Fallas de autenticación en cascada | Renovación automática del token, un reintento ante no autorizado y, si persiste, el interruptor de seguridad. |
| Servicio caído sin que nadie lo note | El backlog se detiene sin aviso | Recuperación automática del servicio y monitoreo, por ejemplo una alerta si no hay actividad en el log durante N minutos. |
| Panel sin servicio | El panel no puede operar si el servicio está detenido | El panel detecta y muestra el estado del servicio; el servicio se reinicia solo ante fallos. |
| Sin reintentos | Errores transitorios quedan como definitivos | Fase posterior: reintentos con espera para fallas transitorias. |

---

## 11. Preguntas abiertas

1. Contrato de la API de DocLM, pendientes tras recibir el flujo y la autenticación: formato exacto de *Create Document* (Base64 / multipart), valor y origen del `assessment_id`, efecto de `batch=true`, si cada archivo de un lote DocLM requiere su propio *Create Document* o se envían juntos, si existe algún estado de fallo además de `pending` y `processed`, tiempo máximo de espera, vigencia del token, límites de tamaño y cuotas.
2. Contrato del JSON: lista definitiva de llaves y confirmación de que son siempre las mismas.
3. Contrato de los procedimientos almacenados que BBVA proporcionará: nombres, parámetros, tipos de datos, resultados y estados válidos.
4. Reglas de selección, actualización y liberación de registros definidas por los procedimientos de BBVA.
5. Intervalo de polling en modo escucha.
6. Formato definitivo del Excel de logs para BBVA (columnas, hojas, estilos).
7. Acceso al servidor de archivos: credenciales, ruta UNC, cuenta de servicio.
8. Política de seguridad de BBVA sobre ejecutables: firma, lista blanca, revisión previa.
9. ¿La carpeta del servidor de archivos donde se deposita el Excel operativo ya está definida, y con qué permisos la va a consultar el equipo de operación?
10. Volumen real de la tabla y tamaño promedio de los documentos, para estimar tiempos.
11. Política de retención de los logs técnicos locales (cuántos días se conservan y quién los limpia), dado que quedan en el servidor del bot.
12. Características del servidor: versión de Windows Server (el sistema operativo ya está confirmado como Windows Server), recursos, política de BBVA para instalar servicios de Windows, cuenta de servicio disponible (o gMSA) y quién tiene permiso para instalar y actualizar.
13. Comportamiento tras un reinicio: ¿el bot debe reanudarse solo si estaba iniciado (propuesto) o esperar a que un operador presione Iniciar?
14. ¿Se necesita abrir el panel desde otro equipo sin conectarse al servidor? Hoy el canal es solo local.
15. ¿Se requiere alguna alerta (correo u otro medio) si el servicio se detiene o falla?

---

## 12. Plan de trabajo por fases

El desarrollo arranca sin depender de la información externa que aún falta (servidor de archivos, base de datos real, procedimientos almacenados, documentación de DocLM). Por eso la primera fase es la parte visual, que se puede construir y dejar funcional de forma aislada, con datos simulados donde haga falta. Las fases siguientes se ajustarán en cuanto llegue el resto de la información (ver sección 11, Preguntas abiertas).

### Fase 1 — Parte visual (panel de control WPF)

- Primera tarea de desarrollo. No depende del servicio, la base de datos, el servidor de archivos ni DocLM.
- Construcción del panel de control: ventana principal, indicadores operativos, botones Iniciar/Detener, sección visual de estado y progreso por etapas, área de log en vivo tipo consola con autoscroll y coloreado por nivel, botón para abrir la ubicación del Excel de logs y sección de configuración.
- Diseño visual "bonito, profesional, escalable y simple", como se definió en la sección 4, construido con la librería **WPF UI** (ver sección 7.1) para lograr una estética moderna tipo Fluent Design sin partir de cero.
- En esta fase el panel puede trabajar con datos y eventos simulados (mock) para probar la interfaz y la experiencia de uso, ya que el canal real con el servicio (Named Pipes) todavía no existe.

### Fase 2 — Servicio de Windows (motor del bot)

- Estructura del Worker Service, configuración, seguridad básica (credenciales cifradas, cuenta de servicio).
- Integración con los procedimientos almacenados proporcionados por BBVA, una vez recibido su contrato técnico.
- Canal de comunicación panel ↔ servicio vía Named Pipes, reemplazando los datos simulados de la Fase 1 por el flujo real.

### Fase 3 — Integraciones y lógica de negocio

- Repositorios, integración con el servidor de archivos y cliente DocLM (con datos simulados si aún no hay credenciales ni documentación oficial).
- Orquestador (modo Backlog / modo Escucha), ventana de ejecución, lote SQL de hasta 5 registros, validación de páginas, agrupación por rangos y configuración de archivos por lote, fases 1 y 2 del procesamiento, manejo de errores y recuperación de huérfanos.

### Fase 4 — Logging

- Log técnico diario junto al ejecutable del servicio.
- Excel operativo en el servidor de archivos, con el formato definitivo que se acuerde con BBVA.

### Fase 5 — Pruebas y cierre

- Pruebas de volumen, parada ordenada, validación de escritura del Excel en el servidor de archivos.
- Ajustes finales, firma del ejecutable, empaquetado, documentación y entrega.

---

## 13. Historial de cambios

| Versión | Cambio |
|---|---|
| 0.1 | Primera versión: lógica de negocio, flujo de dos fases, logs, arquitectura y seguridad. |
| 0.2 | El bot corre como una sola instancia en un servidor dedicado (ya no en varias máquinas). El Excel operativo se deposita en una carpeta de un servidor de archivos para que operación lo revise ahí. Se ajustan concurrencia, despliegue, seguridad, riesgos y preguntas abiertas. |
| 0.3 | La solución se separa en un servicio de Windows (motor del bot, autónomo y sin depender de la sesión del servidor) y un panel de control WPF aparte para Iniciar/Detener y ver el log en vivo. Se ajustan arquitectura, componentes, comunicación, seguridad, despliegue, riesgos y preguntas abiertas. |
| 0.4 | Log técnico definido junto al ejecutable del servicio (Excel operativo sigue en el servidor de archivos). El plan de trabajo pasa a fases, con la parte visual (panel WPF con datos simulados) como primera fase. Se explicitan principios SOLID y el enfoque ordenado y sin sobreingeniería. El backlog se procesa de los registros más antiguos a los más recientes. |
| 0.5 | Se incorpora procesamiento por lotes SQL de hasta 5 registros, validación de páginas antes de DocLM, exclusión de documentos de más de 200 páginas, rangos fijos 1–50 / 51–100 / 101–200 / >200 y configuración de 1–5 archivos por lote DocLM. Se incorpora configuración operativa mediante JSON administrado por el servicio, edición visual con guardado explícito y monitoreo ampliado. |
| 0.6 | Se refina el ciclo de vida con ventana de ejecución, distinción entre estado del bot y estado de ejecución, espera fuera de horario, parada ordenada por lote, generación/consolidación del Excel antes de habilitar nuevamente Iniciar y una sección visual de progreso separada del log con las 10 etapas del procesamiento. Se incorporan indicadores operativos y pruebas de conectividad de dependencias. Además, la integración SQL queda definida únicamente como contrato de consumo de los procedimientos proporcionados por BBVA; se eliminan tablas, columnas, índices y scripts SQL ficticios. |
| 0.7 | Se incorpora la autenticación de DocLM (OAuth2 client credentials sobre Microsoft Entra ID) y el flujo de procesamiento de la API (crear documento, consulta de estado cada 20 segundos, assessment y consulta de resultados). Se agregan el componente `DocLmAutenticador`, parámetros de configuración, notas de seguridad del `client_secret`, riesgos asociados y se precisan las preguntas abiertas sobre el contrato de la API. |
| 0.7.1 | Se precisan los estados de DocLM (`pending` y `processed`), la razón de la espera tras crear el documento y el valor inicial `pending` del assessment. |
| 0.7.2 | Se agrega la sección de configuración de Endpoints (URLs editables con lápiz y guardado condicional) y el campo de tiempo de espera entre consultas (20 segundos por defecto). Se aclara que el token de DocLM se obtiene siempre antes de iniciar el flujo de procesamiento. |
| 0.7.3 | Se detalla la renovación del token de DocLM: uso de `expires_in` para calcular su vigencia y verificación antes de cada llamada (no solo por lote), para evitar que un lote largo se quede con un token vencido a mitad de proceso. |
