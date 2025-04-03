# Video Recolector API

API para grabación y gestión de videos desde cámara web con organización automática por fecha.

## Características

- Grabación de video desde cámara web
- Organización automática de videos por año/mes/semana/día
- Formato AVI compatible con Windows
- API RESTful para control remoto
- Gestión jerárquica de archivos
- Configuración flexible de cámara

## Endpoints API

### Control de Cámara

- **Iniciar Grabación**
  ```http
  POST /api/Camera/start
  ```

- **Detener Grabación**
  ```http
  POST /api/Camera/stop
  ```

- **Estado de Grabación**
  ```http
  GET /api/Camera/status
  ```

### Gestión de Videos

- **Listar Videos**
  ```http
  GET /api/Camera/videos
  ```
  Respuesta:
  ```json
  {
    "videos": [
      {
        "fileName": "video_14_00_12.avi",
        "filePath": "2025/April/Semana1/Thursday/video_14_00_12.avi",
        "downloadUrl": "/api/Camera/videos/2025/April/Semana1/Thursday/video_14_00_12.avi",
        "fileSize": 4183736,
        "createdAt": "2025-04-03T14:00:12.7060936-05:00"
      }
    ]
  }
  ```

- **Descargar Video**
  ```http
  GET /api/Camera/videos/{filePath}
  ```
  Ejemplo:
  ```http
  GET /api/Camera/videos/2025/April/Semana1/Thursday/video_14_00_12.avi
  ```

## Ejemplo de Cliente WPF

Aquí tienes un ejemplo completo de cómo consumir la API desde una aplicación WPF:

```csharp
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text.Json;
using System.IO;
using System.Windows;
using Microsoft.Win32;

public class VideoRecolectorClient
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;

    public VideoRecolectorClient(string baseUrl = "http://localhost:5001")
    {
        _baseUrl = baseUrl;
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(baseUrl)
        };
    }

    // Modelo de datos
    public class VideoInfo
    {
        public string FileName { get; set; }
        public string FilePath { get; set; }
        public string DownloadUrl { get; set; }
        public long FileSize { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class VideoListResponse
    {
        public List<VideoInfo> Videos { get; set; }
    }

    // Control de cámara
    public async Task StartRecording()
    {
        var response = await _httpClient.PostAsync("/api/Camera/start", null);
        response.EnsureSuccessStatusCode();
    }

    public async Task StopRecording()
    {
        var response = await _httpClient.PostAsync("/api/Camera/stop", null);
        response.EnsureSuccessStatusCode();
    }

    public async Task<bool> GetRecordingStatus()
    {
        var response = await _httpClient.GetFromJsonAsync<dynamic>("/api/Camera/status");
        return response.isRecording;
    }

    // Gestión de videos
    public async Task<List<VideoInfo>> GetVideos()
    {
        var response = await _httpClient.GetFromJsonAsync<VideoListResponse>("/api/Camera/videos");
        return response.Videos;
    }

    public async Task DownloadVideo(VideoInfo video, string savePath)
    {
        // Usar el filePath directamente como viene de la API
        var response = await _httpClient.GetAsync($"/api/Camera/videos/{video.FilePath}");
        response.EnsureSuccessStatusCode();

        using (var fs = new FileStream(savePath, FileMode.Create))
        {
            await response.Content.CopyToAsync(fs);
        }
    }
}

// Ejemplo de uso en una ventana WPF
public partial class MainWindow : Window
{
    private readonly VideoRecolectorClient _client;

    public MainWindow()
    {
        InitializeComponent();
        _client = new VideoRecolectorClient("http://localhost:5001");
    }

    private async void BtnStartRecording_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await _client.StartRecording();
            MessageBox.Show("Grabación iniciada");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error: {ex.Message}");
        }
    }

    private async void BtnStopRecording_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await _client.StopRecording();
            MessageBox.Show("Grabación detenida");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error: {ex.Message}");
        }
    }

    private async void BtnListVideos_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var videos = await _client.GetVideos();
            listBoxVideos.ItemsSource = videos;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error: {ex.Message}");
        }
    }

    private async void BtnDownloadVideo_Click(object sender, RoutedEventArgs e)
    {
        if (listBoxVideos.SelectedItem is VideoInfo video)
        {
            var dialog = new SaveFileDialog
            {
                FileName = video.FileName,
                DefaultExt = ".avi",
                Filter = "AVI files (.avi)|*.avi"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    await _client.DownloadVideo(video, dialog.FileName);
                    MessageBox.Show("Video descargado exitosamente");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error al descargar: {ex.Message}");
                }
            }
        }
    }
}

### XAML de ejemplo
```xaml
<Window x:Class="VideoRecolectorApp.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="Video Recolector" Height="450" Width="800">
    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
        </Grid.RowDefinitions>
        
        <StackPanel Grid.Row="0" Orientation="Horizontal" Margin="10">
            <Button x:Name="BtnStartRecording" Content="Iniciar Grabación" 
                    Click="BtnStartRecording_Click" Margin="5"/>
            <Button x:Name="BtnStopRecording" Content="Detener Grabación" 
                    Click="BtnStopRecording_Click" Margin="5"/>
            <Button x:Name="BtnListVideos" Content="Listar Videos" 
                    Click="BtnListVideos_Click" Margin="5"/>
            <Button x:Name="BtnDownloadVideo" Content="Descargar Video" 
                    Click="BtnDownloadVideo_Click" Margin="5"/>
        </StackPanel>
        
        <ListBox x:Name="listBoxVideos" Grid.Row="1" Margin="10"
                 DisplayMemberPath="FileName"/>
    </Grid>
</Window>
```

## Notas Importantes

1. La API devuelve los videos en formato AVI
2. Los videos se organizan automáticamente por año/mes/semana/día
3. Para descargar un video, usa el `filePath` que viene en la respuesta del API
4. La API soporta streaming de video y descarga parcial (ranges)

## Contribuir

1. Fork el proyecto
2. Crea tu rama de características (`git checkout -b feature/AmazingFeature`)
3. Commit tus cambios (`git commit -m 'Add some AmazingFeature'`)
4. Push a la rama (`git push origin feature/AmazingFeature`)
5. Abre un Pull Request
