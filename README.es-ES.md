# UniDesk

UniDesk es una barra lateral ligera, personalizable y limpia para el escritorio de Windows. Reúne hora y clima, monitorización de hardware, accesos directos, tareas, notas rápidas y textos frecuentes en un espacio de trabajo cómodo.

<p align="center">
  <a href="README.md">简体中文</a> ·
  <a href="README.en-US.md">English</a> ·
  <a href="README.ja-JP.md">日本語</a> ·
  Español
</p>

![Imagen de presentación de UniDesk](images/unidesk-hero.png)

## ✨ Funciones principales

### Hora y clima

- Muestra la hora actual, la fecha y la información del calendario lunar.
- Muestra clima, temperatura, calidad del aire, humedad y ciudad.
- Incluye un calendario de escritorio para consultar fechas solares y lunares rápidamente.

### Monitor de hardware

- Muestra en tiempo real el uso de CPU, memoria y GPU.
- Muestra la temperatura de CPU / GPU.
- Muestra la velocidad de subida y descarga de red del equipo.
- La temperatura de GPU se obtiene, cuando es posible, desde los controladores y fuentes de monitorización disponibles. Si no está disponible, se muestra `--` de forma segura.

### Accesos directos

- Permite añadir aplicaciones, archivos y carpetas frecuentes.
- Permite arrastrar aplicaciones, archivos o carpetas desde el escritorio o el Explorador de archivos.
- Permite ordenar libremente los accesos directos.
- Permite personalizar cuántos accesos se muestran en el panel principal.

### Tareas

- Permite crear, editar, completar y eliminar tareas.
- Muestra hora límite y prioridad.
- Guarda los datos localmente para el seguimiento diario.

### Notas rápidas

- Permite gestionar varias notas.
- Incluye guardado automático, fijar, copiar y eliminar.
- Sirve para ideas rápidas, borradores, notas de reuniones y recordatorios.

### Textos rápidos

- Soporta historial del portapapeles.
- Soporta frases y textos frecuentes.
- Permite copiar con un clic.
- Incluye filtrado de contenido sensible para reducir el guardado accidental de códigos de verificación, contraseñas, tokens, cookies y textos similares.

### Gestión de módulos

- Permite mostrar u ocultar módulos.
- Permite ordenar módulos libremente.
- Ayuda a crear un panel de escritorio adaptado a tu forma de trabajar.

### Personalización

- Permite ajustar colores de tema, transparencia, ancho del panel, alto del panel y tamaño de fuente.
- Permite personalizar el título superior.
- Soporta mantener encima, bloquear, contraer, iniciar con Windows y configurar el número de accesos directos.
- Permite restaurar el diseño o la configuración predeterminada.

### Copia de seguridad y restauración

- Soporta copia de seguridad local.
- Permite restaurar tareas, notas rápidas, historial del portapapeles y textos frecuentes.
- Facilita recuperar datos habituales después de reinstalar Windows o cambiar de equipo.

## 🖼️ Vista previa

### Funciones principales

![Resumen de funciones de UniDesk](images/unidesk-features.png)

### Personalización

![Vista de personalización de UniDesk](images/unidesk-customization.png)

## 🚀 ¿Para quién es?

UniDesk está pensado para usuarios de Windows que quieren mantener el escritorio limpio, pero necesitan consultar información, abrir herramientas, registrar tareas y tomar notas rápidamente.

Casos de uso habituales:

- Trabajo diario de oficina
- Productividad personal
- Inicio rápido desde el escritorio
- Consulta del estado del sistema
- Tareas y notas ligeras
- Copia rápida de textos frecuentes

## 📦 Instalación

Descarga el instalador más reciente desde [GitHub Releases](https://github.com/SuperDaddyV/UniDesk/releases/latest).

Ejemplo del instalador actual:

```powershell
UniDesk_Setup_2.1.0.exe
```

Se recomienda cerrar UniDesk antes de instalar o actualizar.

Haz doble clic en el instalador normalmente y aprueba la solicitud UAC de Windows; un usuario estándar debe proporcionar credenciales de administrador, sin necesidad de usar **Ejecutar como administrador**. El acceso directo del escritorio y la supervisión completa de hardware están seleccionados de forma predeterminada. El instalador muestra claramente que instalará el controlador compartido PawnIO y un servicio de Windows de solo lectura ejecutado como `LocalSystem`. `UniDesk.exe` se mantiene con permisos de usuario normal. La página final selecciona iniciar UniDesk de forma predeterminada y lo abre con la identidad del usuario normal original. Si desmarcas el componente o su instalación falla, el clima, las notas, los accesos directos y las demás funciones básicas seguirán disponibles, pero métricas como la temperatura de la CPU pueden no mostrarse; Configuración permite exportar diagnósticos y reintentar la reparación.

Requisitos del sistema:

- Una versión compatible de Windows 11 x64
- Windows 10 Enterprise o IoT Enterprise LTSC x64 compatible con .NET 10

El proyecto conserva la base de compatibilidad de API de Windows 10 versión 1903, pero las versiones de consumo sin soporte ya no forman parte del soporte oficial.

## 🛠️ Compilar desde el código fuente

Requisitos:

- .NET 10 SDK
- Un entorno de desarrollo compatible con Windows 11 o Windows 10 LTSC
- Visual Studio 2022, JetBrains Rider u otro entorno compatible con .NET / WPF
- Inno Setup 6, solo necesario para crear el instalador

Compilar y ejecutar:

```powershell
git clone https://github.com/SuperDaddyV/UniDesk.git
cd UniDesk

dotnet restore UniDesk.sln
dotnet build UniDesk.sln -c Release
dotnet run --project UniDesk\UniDesk.csproj
```

Crear un candidato local sin firmar desde un árbol de trabajo limpio:

```powershell
.\scripts\Build-Release.ps1 -Version 2.1.0
```

El script publica la aplicación, el servicio de hardware y la herramienta de reparación en un directorio nuevo con versión, y compila el instalador únicamente desde esas entradas. Los artefactos públicos deben firmarse con SignPath mediante el flujo manual `Build and sign release candidate` de GitHub Actions y superar `Test-ReleaseReadiness.ps1`; el flujo no crea una GitHub Release automáticamente.

## 🧰 Tecnología

| Tecnología | Uso |
| --- | --- |
| .NET 10 LTS | Entorno de ejecución de la aplicación |
| WPF | Interfaz de escritorio para Windows |
| SQLite | Almacenamiento local |
| CommunityToolkit.Mvvm | Ayudas para UI y enlace de datos |
| LibreHardwareMonitorLib | Lectura de información de hardware |
| Hardcodet.NotifyIcon.Wpf | Soporte para bandeja del sistema |
| Inno Setup | Instalador para Windows |

## 🔐 Datos y privacidad

UniDesk prioriza el almacenamiento local. Los datos de usuario, como configuración, accesos directos, tareas, notas rápidas, textos rápidos y caché de iconos, se guardan en el equipo local.

El historial del portapapeles incluye filtrado de contenido sensible para reducir el guardado accidental de códigos de verificación, contraseñas, tokens, cookies y textos similares. Esta función reduce riesgos, pero no debe considerarse una garantía absoluta de seguridad. Si trabajas con contenido muy sensible, considera desactivar el historial del portapapeles o limpiarlo con frecuencia.

## Code signing policy

Los paquetes públicos siguen la [política de firma de código](CODE_SIGNING_POLICY.md) y la [política de privacidad](PRIVACY.md). Cada candidato firmado debe proceder de una revisión pública, recibir aprobación manual y superar las comprobaciones de versión de origen, Authenticode y SHA-256.

Free code signing provided by SignPath.io, certificate by SignPath Foundation.

## 🆕 Novedades

Las versiones recientes incluyen:

- Gestión de módulos con mostrar / ocultar y ordenación.
- Arrastrar para añadir accesos directos y ordenación libre.
- Notas rápidas con varias notas, guardado automático, fijar y copiar.
- Textos rápidos con historial del portapapeles, textos frecuentes y filtrado de contenido sensible.
- Mejor diseño del monitor de hardware para CPU, memoria, GPU, temperatura y velocidad RX / TX.
- Mejor lectura de temperatura de GPU en más entornos de hardware y controladores.
- Mejoras en la personalización y el desplazamiento del panel principal.

## 📌 Próximos pasos

- Más temas predefinidos.
- Información de hardware más completa.
- Opciones de extensión de módulos más flexibles.
- Mejor experiencia de instalación y actualización.

## 🙏 Créditos

UniDesk está desarrollado sobre [Happyeveryweek/LumiDesk](https://github.com/Happyeveryweek/LumiDesk). Gracias al autor original por la idea, la base y la experiencia de widget de escritorio.

## 📄 Licencia

Este proyecto está publicado bajo la [MIT License](LICENSE). Respeta también las licencias y avisos de copyright del proyecto original y de las dependencias de terceros.
