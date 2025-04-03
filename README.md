# Video Recolector API

API para grabación y gestión de videos desde cámara web con organización automática por fecha.

## Características

- 📹 Grabación de video desde cámara web
- 📁 Organización automática de videos por año/mes/semana/día
- 🎥 Formato MP4 compatible con Windows
- 🌐 API RESTful para control remoto
- 📊 Gestión jerárquica de archivos
- ⚙️ Configuración flexible de cámara

## Requisitos

- .NET 8.0 SDK
- Windows (para soporte de cámara web)
- OpenCV Sharp 4.8.0

## Instalación

1. Clonar el repositorio:
```bash
git clone https://github.com/willy101Sotfware/Viedo_Recolector-.git
cd Viedo_Recolector-
```

2. Restaurar dependencias:
```bash
dotnet restore
```

3. Compilar el proyecto:
```bash
dotnet build
```

4. Ejecutar la aplicación:
```bash
dotnet run
```

## Configuración

Ajustar la configuración en `appsettings.json`:

```json
{
  "CameraSettings": {
    "Port": 0,              // Puerto de la cámara (0 para la primera cámara)
    "VideoFormat": "mp4",   // Formato de video
    "FrameRate": 30,        // Frames por segundo
    "Resolution": {
      "Width": 1280,       // Ancho del video
      "Height": 720        // Alto del video
    }
  }
}
```

## Estructura de Carpetas

Los videos se organizan automáticamente en la siguiente estructura:
```
wwwroot/videos/
├── 2025/
│   ├── Abril/
│   │   ├── Semana14/
│   │   │   ├── Jueves/
│   │   │   │   ├── video_09_30_00.mp4
│   │   │   │   └── video_09_35_15.mp4
```

## Endpoints API

### Control de Cámara

- **Iniciar Grabación**
  ```http
  POST /api/camera/start
  ```

- **Detener Grabación**
  ```http
  POST /api/camera/stop
  ```

- **Estado de Grabación**
  ```http
  GET /api/camera/status
  ```

### Gestión de Videos

- **Listar Videos**
  ```http
  GET /api/camera/videos
  ```
  Respuesta:
  ```json
  {
    "videos": [
      {
        "name": "2025",
        "type": "directory",
        "path": "2025",
        "items": [
          {
            "name": "Abril",
            "type": "directory",
            "items": [...]
          }
        ]
      }
    ]
  }
  ```

- **Obtener Video**
  ```http
  GET /api/camera/videos/{año}/{mes}/{semana}/{dia}/{nombre_video}.mp4
  ```

## Ejemplo de Uso (C#)

```csharp
using System.Net.Http;

public class VideoClient
{
    private readonly HttpClient _client;
    private readonly string _baseUrl;

    public VideoClient(string baseUrl)
    {
        _client = new HttpClient();
        _baseUrl = baseUrl;
    }

    public async Task StartRecording()
    {
        await _client.PostAsync($"{_baseUrl}/api/camera/start", null);
    }

    public async Task StopRecording()
    {
        await _client.PostAsync($"{_baseUrl}/api/camera/stop", null);
    }

    public async Task<List<VideoInfo>> GetVideos()
    {
        var response = await _client.GetFromJsonAsync<VideoListResponse>($"{_baseUrl}/api/camera/videos");
        return response.Videos;
    }
}
```

## Ejemplo de Uso (JavaScript)

```javascript
class VideoClient {
    constructor(baseUrl) {
        this.baseUrl = baseUrl;
    }

    async startRecording() {
        await fetch(`${this.baseUrl}/api/camera/start`, { method: 'POST' });
    }

    async stopRecording() {
        await fetch(`${this.baseUrl}/api/camera/stop`, { method: 'POST' });
    }

    async getVideos() {
        const response = await fetch(`${this.baseUrl}/api/camera/videos`);
        return await response.json();
    }
}
```

## Contribuir

1. Fork el proyecto
2. Crear una rama para tu característica (`git checkout -b feature/AmazingFeature`)
3. Commit tus cambios (`git commit -m 'Add some AmazingFeature'`)
4. Push a la rama (`git push origin feature/AmazingFeature`)
5. Abrir un Pull Request

## Licencia

Este proyecto está bajo la Licencia MIT. Ver el archivo `LICENSE` para más detalles.
