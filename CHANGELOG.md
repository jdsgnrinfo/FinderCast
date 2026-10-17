# Cambios

Todas las versiones de FinderCast. El formato sigue [Keep a Changelog](https://keepachangelog.com/es-ES/1.1.0/)
y la numeración, [versionado semántico](https://semver.org/lang/es/).

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

[0.2.0]: https://github.com/jdsgnrinfo/FinderCast/releases/tag/v0.2.0
[0.1.0]: https://github.com/jdsgnrinfo/FinderCast/releases/tag/v0.1.0
