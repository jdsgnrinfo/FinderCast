# Cambios

Todas las versiones de FinderCast. El formato sigue [Keep a Changelog](https://keepachangelog.com/es-ES/1.1.0/)
y la numeración, [versionado semántico](https://semver.org/lang/es/).

## [Sin publicar]

### Añadido

- En los ajustes, junto a los atajos, cómo ejecutar un comando de consola (`>` `comando`) y cómo
  buscar emojis y símbolos (`:` `nombre`).

## [0.4.1] - 2026-10-29

### Añadido

- **Instalador** (`FinderCast-<versión>-setup.exe`), además del portable: se instala para el
  usuario actual sin permisos de administrador, con acceso en el menú Inicio y desinstalador en
  *Configuración → Aplicaciones*. Al desinstalar se cierra FinderCast y se quita del inicio de
  Windows; los ajustes se conservan.

### Cambiado

- **Icono de la bandeja:** una lupa que se ve blanca o negra según el tema de la barra de tareas
  (y cambia al cambiarlo), del tamaño de los iconos de Wi-Fi y volumen. Un clic abre el lanzador;
  el clic derecho muestra *Ajustes* y *Salir*.
- Márgenes de 12 px en las dos tarjetas y fila de ancladas más compacta (casillas de 52 × 40).

### Corregido

- Si un icono no se podía leer al arrancar, ya no se queda sin icono: se vuelve a pedir al mostrarlo.
- El texto de la barra vuelve a ser «Buscar aplicaciones…».

## [0.4.0] - 2026-10-27

### Añadido

- **Apps ancladas** sobre la búsqueda, hasta seis: se anclan con el `+` (la elegida en los
  resultados) o con clic derecho en un resultado, y se quitan con clic derecho en la anclada.

### Cambiado

- La tarjeta de arriba lleva las ancladas y la búsqueda; los resultados siguen en la de abajo.
- Iconos de los resultados algo más pequeños (22 px) y filas de 44.
- Sin el pie de atajos; `Ctrl` `Enter` sigue abriendo como administrador.
- La cuadrícula de emojis ocupa todo el ancho de la tarjeta.

### Corregido

- Los iconos de las apps y la tabla de emojis se preparan al arrancar: los resultados salen al
  momento y la tarjeta ya no se ve negra la primera vez que se escribe `:`.

## [0.3.0] - 2026-10-19

### Añadido

- **Comandos de consola** con el prefijo `>`: `> ipconfig /all` o `> winget upgrade --all` se
  ejecutan en PowerShell (PowerShell 7 si está instalado), dentro de Windows Terminal si lo hay,
  en una ventana que se queda abierta con el resultado. Con el atajo de administrador, la consola
  se abre elevada.
- **Emojis y símbolos** con el prefijo `:`: `:corazon`, `:flecha`, `:euro` buscan por nombre y
  etiquetas, en español e inglés, entre casi 1.900 emojis y 70 símbolos (©, →, ≠, €, ½…). Se
  muestran en una cuadrícula de nueve por fila que se recorre con las cuatro flechas; el pie
  muestra el nombre del elegido y `Enter` lo copia al portapapeles.
- **Búsqueda tolerante a errores de tecleo:** letras cambiadas, de más, de menos o dos al revés
  (`discrod` → Discord, `fotoshop` → Photoshop, `goolge` → Google Chrome). Un error hasta 5 letras
  y dos a partir de 6; estas coincidencias quedan por debajo de las exactas.

## [0.2.2] - 2026-10-17

### Añadido

- Ajuste *Mostrar las 5 últimas apps abiertas* (desactivado por defecto). Activado, al abrir el
  lanzador sin escribir nada se ven las cinco últimas apps abiertas; desactivado, solo la barra de
  búsqueda hasta que se escribe algo.

## [0.2.1] - 2026-10-17

### Añadido

- Engranaje en la barra de búsqueda para abrir los ajustes.
- Ajuste para grabar el atajo de *Abrir como administrador*, igual que el atajo para abrir: una o
  más modificadoras (`Ctrl`, `Alt`, `Shift`) con `Enter`. El pie del lanzador muestra el elegido.
- Versión del programa junto al resultado elegido.

### Cambiado

- El lanzador son dos tarjetas: la búsqueda arriba y los resultados debajo. Sin nada escrito solo
  se ve la barra, y los resultados muestran cinco filas completas.
- `Ctrl` `Enter` abre la app como administrador (ya no abre su ubicación).
- El pie del lanzador muestra los atajos *Abrir como administrador* y *Abrir*, sin el logo ni el
  nombre. Los ajustes se abren con el engranaje, la bandeja o el comando *Ajustes de FinderCast*.

### Corregido

- Las apps de Windows salen con su nombre traducido (*Administrador de tareas*, no *Task Manager*)
  y se encuentran por los dos nombres.

## [0.2.0] - 2026-10-17

### Añadido

- **Apps de Microsoft Store** (Calculadora, Fotos, Configuración…) en los resultados, con su icono.
  Se vuelven a leer al abrir el lanzador si han pasado unos minutos.
- `Ctrl` `Enter` abre la ubicación del acceso directo en el Explorador y `Ctrl` `Mayús` `Enter`
  abre la app como administrador.

### Cambiado

- La versión portable es un único `FinderCast.exe` comprimido.
- Las apps desinstaladas dejan de contar en el historial de uso guardado en los ajustes.

## [0.1.0] - 2026-10-15

Primera versión.

### Añadido

- **Lanzador** sobre Acrylic que se abre con `Alt` `Espacio` en la pantalla del puntero: busca
  entre los accesos directos del menú Inicio mientras escribes, se maneja con `↑` `↓` `Enter` y
  `Esc`, y se oculta al abrir algo o al hacer clic fuera.
- **Búsqueda** sin tildes ni mayúsculas, por iniciales (`vsc` → Visual Studio Code) y con letras
  salteadas; las apps que más abres suben en la lista.
- **Iconos reales** de cada app, también los de los juegos de Steam y los enlaces web, sin la placa
  blanca que Windows pone a los iconos pequeños al ampliarlos.
- **Comandos propios:** *Ajustes de FinderCast* y *Salir de FinderCast*. Doble clic en el logo
  del pie del lanzador para abrir los ajustes.
- **Ajustes** con el sistema de diseño de ShutterGrid, sobre Mica: abrir al iniciar Windows,
  icono en la bandeja, idioma (español o inglés, cambia al momento) y atajo para abrir, que se
  graba pulsando la nueva combinación. Si otra app ya usa el atajo, FinderCast lo avisa.
- **Icono en la bandeja** con *Abrir FinderCast* y *Salir*. Abrir el `.exe` otra vez muestra los
  ajustes.

[0.4.1]: https://github.com/jdsgnrinfo/FinderCast/releases/tag/v0.4.1
[0.4.0]: https://github.com/jdsgnrinfo/FinderCast/releases/tag/v0.4.0
[0.3.0]: https://github.com/jdsgnrinfo/FinderCast/releases/tag/v0.3.0
[0.2.2]: https://github.com/jdsgnrinfo/FinderCast/releases/tag/v0.2.2
[0.2.1]: https://github.com/jdsgnrinfo/FinderCast/releases/tag/v0.2.1
[0.2.0]: https://github.com/jdsgnrinfo/FinderCast/releases/tag/v0.2.0
[0.1.0]: https://github.com/jdsgnrinfo/FinderCast/releases/tag/v0.1.0
