<p align="center">
  <img src="design/FinderCast.svg" alt="Icono de FinderCast" width="128" height="128">
</p>

<h1 align="center">FinderCast</h1>

<p align="center">
  Lanzador rápido de aplicaciones para Windows 11, al estilo de Raycast y Tinycast en macOS.<br>
  Pulsa el atajo, escribe y pulsa Enter.
</p>

---

## Uso

| Acción | Atajo |
| --- | --- |
| Abrir o cerrar el lanzador | `Alt` `Espacio` (se cambia en los ajustes) |
| Moverse por los resultados | `↑` / `↓` |
| Abrir el resultado elegido | `Enter` o clic |
| Cerrar | `Esc`, o hacer clic fuera |

Busca entre los accesos directos del menú Inicio (los del usuario y los de todos los usuarios) y
entre sus propios comandos: *Ajustes de FinderCast* y *Salir de FinderCast*. No hace falta escribir
el nombre entero ni con tildes: `vsc` encuentra *Visual Studio Code* y `configuracion` encuentra
*Configuración*. Las apps que más abres suben en la lista.

Un doble clic en el logo y el nombre del pie del lanzador abre los ajustes.

La app se queda en segundo plano con un icono en el área de notificación: un clic abre los
ajustes y el clic derecho muestra *Abrir FinderCast* y *Salir*. Si ocultas el icono, volver a
abrir FinderCast desde el menú Inicio muestra los ajustes.

## Ajustes

- Abrir al iniciar Windows (activado la primera vez).
- Mostrar el icono en la bandeja.
- Idioma: español o inglés (por defecto, el de Windows). Cambia al momento, sin reiniciar.
- Atajo para abrir: haz clic en las teclas y pulsa la nueva combinación (`Esc` cancela). Si otra
  app ya lo usa, FinderCast lo avisa y conserva el anterior.

> PowerToys Run también usa `Alt` `Espacio` por defecto. Si lo tienes activo, cambia el atajo de
> uno de los dos.

## Descargar

1. Descarga `FinderCast-<versión>-portable.zip` desde [Releases](../../releases).
2. Descomprímelo donde quieras y abre `FinderCast\FinderCast.exe`.

No hace falta instalar nada: la carpeta lleva dentro .NET y el Windows App SDK. Para quitarlo, usa
**Salir** en el menú de la bandeja y borra la carpeta. Como el ejecutable no está firmado, la
primera vez Windows SmartScreen puede pedir confirmación (*Más información → Ejecutar de todas formas*).

**Requisitos:** Windows 10 (versión 2004 o posterior) u 11, de 64 bits. Acrylic y Mica se ven en
Windows 11.

## Compilar

Requisitos: SDK de .NET 10, Windows 10 2004 o posterior, x64. El material Acrylic del lanzador y
el Mica de los ajustes se ven en Windows 11.

```bash
dotnet build
```

El ejecutable queda en `src/FinderCast/bin/Debug/net10.0-windows10.0.19041.0/win-x64/FinderCast.exe`.
Lleva el Windows App SDK dentro, así que no hace falta instalar nada más.

Paquete de una versión (`artifacts/FinderCast-<versión>-portable.zip`; la versión sale de
`Directory.Build.props`):

```bash
powershell -ExecutionPolicy Bypass -File scripts/publicar.ps1
```

## Estructura

```
src/FinderCast/
  Program.cs              Entrada: una sola instancia; la segunda pide mostrar los ajustes.
  App.xaml(.cs)           Arranque: ajustes, índice, ventanas, atajo global y bandeja.
  MainWindow.xaml(.cs)    El lanzador: Acrylic, sin bordes, búsqueda y resultados.
  SettingsWindow.xaml(.cs) Ajustes con la construcción de la ventana de ShutterGrid, sobre Mica.
  Controls/               Controles de los ajustes dibujados a mano como en ShutterGrid: interruptor
                          de shadcn/ui con muelle, botón y menú desplegable de Arc UI, información
                          emergente de shadcn/ui y selector de atajo con teclas sueltas.
  Themes/                 Paleta, tipografía, teclas y las curvas y muelles de las animaciones
                          (sistema de diseño de ShutterGrid).
  Services/               Índice del menú Inicio, búsqueda, atajo global y su grabador,
                          bandeja, inicio con Windows, textos e idioma, ajustes en JSON.
  Interop/                Win32: ventana de mensajes, iconos del Shell y funciones nativas.
design/FinderCast.svg     Icono original (Assets/ tiene el .ico y el .png generados a partir de él).
```

Los ajustes se guardan en `%APPDATA%\FinderCast\settings.json`.

## Licencia

FinderCast se distribuye bajo la [licencia MIT](LICENSE).
