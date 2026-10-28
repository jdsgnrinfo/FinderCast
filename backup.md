# Historial de trabajo

Registro de lo que se ha hecho en FinderCast, sesión a sesión, para retomar el trabajo desde
cualquier ordenador. Las versiones publicadas y sus cambios están en [CHANGELOG.md](CHANGELOG.md);
aquí van además las decisiones, lo que se probó y lo que queda pendiente.

## Normas del repositorio

- Autor único: **Jotadsgnr** (`Jotadsgnr <jdsgnr.info@gmail.com>`). Ningún commit, archivo,
  release ni metadato lleva coautorías, créditos ni menciones a herramientas de IA.
- Textos de la app, comentarios del código, commits y documentación en español.
- Cada versión: subir `<Version>` en `Directory.Build.props`, pasar la sección *Sin publicar* del
  CHANGELOG a la versión con su fecha, commit, etiqueta `vX.Y.Z`, generar el zip con
  `scripts/publicar.ps1` y publicarlo en GitHub Releases.
- Este archivo se actualiza al final de cada sesión de trabajo.

## 2026-10-15 · Versión 0.1.0

- Primera versión: lanzador sobre Acrylic con `Alt` `Espacio`, índice del menú Inicio, búsqueda sin
  tildes y por iniciales, ajustes con el diseño de ShutterGrid sobre Mica, icono en la bandeja.
- Versión portable en un único `FinderCast.exe` comprimido.
- En este ordenador PowerToys Run también usa `Alt` `Espacio`.

## 2026-10-17 · Versiones 0.2.0, 0.2.1 y 0.2.2

**Revisión del proyecto y de GitHub.** Había una sola release (0.1.0), sin issues ni PRs; el commit
del ejecutable único aún no estaba publicado.

**0.2.0**
- Apps de Microsoft Store en los resultados (`PackageManager`, se abren con `shell:AppsFolder\<AUMID>`).
- Ejecutar como administrador desde el lanzador.
- Limpieza del historial de uso de apps desinstaladas.

**0.2.1**
- El lanzador pasa a dos ventanas: `MainWindow` (barra de búsqueda) y `ResultsWindow` (resultados
  y pie). La de resultados no se activa nunca (`WS_EX_NOACTIVATE`) para que el teclado siga en la
  búsqueda; por eso usa `ActiveAcrylic`, que fuerza el aspecto de ventana activa, y las dos tienen
  un borde de color fijo.
- Sin texto escrito solo se ve la barra de búsqueda; los resultados muestran justo cinco filas.
- Medidas actuales: ancho 600, barra 56 de alto, texto 15, lupa y engranaje 18, filas 48, iconos 26,
  nombres 14, tipo 12. Texto de la barra: «Buscar aplicaciones…».
- Engranaje en la barra para abrir los ajustes (sustituye al doble clic en el logo del pie, que se quitó).
- Versión del programa junto al resultado elegido.
- Nombres traducidos de los accesos de Windows (`desktop.ini` → `[LocalizedFileNames]`): ahora se
  encuentra *Administrador de tareas*, también por *Task Manager*.
- Atajo para abrir como administrador configurable: se graba en los ajustes igual que el atajo para
  abrir (modificadoras + `Enter`, por defecto `Ctrl` `Enter`). Las dos filas forman un grupo sin
  línea entre ellas.

**Probado y descartado**
- Radio de esquinas propio (8 + 2 px): Windows 11 solo ofrece 8, 4 o 0 px; dibujarlas a mano
  pierde el desenfoque real o deja bordes dentados. Se quedan los 8 px del sistema.
- Mica más claro en los ajustes (Mica Alt y un Mica a medida gris azulado): se dejó el Mica
  estándar. El Mica a medida funcionaba extendiendo el contenido bajo la barra de título.

**0.2.2**
- Ajuste *Mostrar las 5 últimas apps abiertas* (`ShowRecentApps`, desactivado por defecto). Las
  claves de las últimas apps se guardan en `RecentApps` del `settings.json`, la más reciente
  primero, y se quitan las desinstaladas. Con la búsqueda vacía: activado muestra la tarjeta con
  esas apps (si hay alguna); desactivado, solo la barra. Probado con capturas en los dos estados.
- Captura del lanzador en el README (`docs/lanzador.webp`).

## 2026-10-19 · Versión 0.3.0: consola, búsqueda difusa y emojis

- Prefijo `>` en la búsqueda (`Services/ConsoleCommand.cs`): el resto del texto es un único
  resultado de tipo `LaunchKind.Console` («Ejecutar en terminal»). Con Enter se abre PowerShell
  (`pwsh.exe` si está en el PATH; si no, Windows PowerShell) dentro de Windows Terminal (`wt.exe`)
  si existe, con `-NoExit` para que se vea el resultado. El comando va en `-EncodedCommand`
  (Base64 UTF-16): sin problemas de comillas ni del `;` de Windows Terminal. Con el atajo de
  administrador se abre con `runas`. Probado con `> ipconfig /all`.
- Con solo `>` escrito, la tarjeta muestra la ayuda «Escribe un comando de consola tras >».
- Búsqueda difusa en `Search.TypoScore`: si no hay coincidencia exacta, por iniciales, parcial ni
  de letras en orden, se mide la distancia de Damerau-Levenshtein (sustitución, inserción,
  borrado y transposición de vecinas) entre lo escrito y el principio del nombre desde cada
  palabra. Límite: 1 error hasta 5 letras, 2 desde 6, nada con menos de 3. Puntúa 200 − 30 × coste,
  siempre por debajo de las demás. Probado: `discrod`, `fotoshop`, `goolge`, `chrme`.
- Emojis y símbolos con el prefijo `:` (`Services/EmojiCatalog.cs`). La tabla
  `Assets/Emoji.tsv` (1.967 filas, ~170 KB, va incrustada en el .exe) la genera
  `scripts/generar-emojis.py`: descarga emojibase-data 15.3.2 (es + en, MIT, aviso en
  `THIRD-PARTY-NOTICES.md`, que también va en el zip) y añade 70 símbolos a mano. Se busca por
  nombre y etiquetas en los dos idiomas con la misma `Search.Score` (errores de tecleo incluidos;
  las etiquetas puntúan 150 menos; a igualdad, el nombre más corto). `Enter` copia con
  `Clipboard.SetContent` + `Flush`; el pie pasa a «Copiar [Enter]». Probado `:corazon`,
  `:flecha`, `:euro` y la copia de `:fuego` (🔥 en el portapapeles).
- Los emojis se ven en cuadrícula (`EmojiGrid`, un `GridView` en `ResultsWindow`, casillas de
  64 × 62: nueve por fila y cuatro filas a la vista). `ResultsWindow.UseGrid` cambia entre la lista
  y la cuadrícula y solo la visible recibe los resultados; el lanzador usa `ActiveList`. En la
  cuadrícula ↑ ↓ saltan `GridColumns` y ← → una casilla (en modo emojis el cursor de la caja no
  se mueve con ← →). El pie muestra el nombre del elegido. Probado con `:corazon` + → ↓.

## 2026-10-27 · Versión 0.4.0: apps ancladas

- Fila de apps ancladas (hasta 6, `PinnedApps` en `settings.json`) sobre la búsqueda, en la
  tarjeta de arriba. Se anclan con el `+` (la elegida en los resultados) o con clic derecho; clic
  derecho en una anclada la quita. Casillas de 64 × 44, iconos de 24.
- Sin el pie de la tarjeta de resultados. Filas de 44 e iconos de 22.
- **Probado y descartado:** juntar ancladas, búsqueda y resultados en una sola ventana. Al
  cambiar de alto a la vista, Windows pinta de negro lo nuevo hasta que WinUI lo dibuja, y el
  Acrylic salía gris. Se vuelve a las dos ventanas, que nunca cambian de tamaño a la vista.
- Iconos leídos de antemano: `AppIndex.Scanned` → `ResultItem.Preload` guarda los píxeles de
  todas las apps en segundo plano.
- Cuadrícula de emojis: el ancho de casilla sale del panel (576) entre 9, menos 1 px en total; con
  64 exactos la novena no cabía por el redondeo de la escala.
- `MainWindow.WarmUp` al arrancar: lee la tabla de emojis en segundo plano y dibuja la cuadrícula
  un segundo fuera de la pantalla, para que el primer `:` no tarde ni salga negro.

## 2026-10-28 · Revisión de la 0.4.0 (sin publicar)

- Revisada la 0.4.0 hecha en otro ordenador: compila sin avisos; búsqueda, recientes, emojis y
  consola probados con capturas. Anclar y desanclar no se probó (cambiaría los ajustes reales).
- Corregido: los textos del pie quitado (`OpenAction`, `CopyAction`, `RunAsAdminAction`) ya no se
  usaban; `ResultItem.Preload` guardaba también los iconos que fallaban y ya no se reintentaban;
  la barra vuelve a «Buscar aplicaciones…», el texto elegido.

**Ideas pendientes**
- Más comandos propios: bloquear, suspender, reiniciar, apagar, vaciar la papelera, abrir carpetas.
- Volver a poder arrastrar el lanzador (se perdió al quitar el pie arrastrable).
- Tarjeta de resultados que se ajuste a la cantidad de resultados.
