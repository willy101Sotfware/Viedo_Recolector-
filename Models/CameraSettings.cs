namespace VIDEO_RECOLECTOR.Models
{
    public class CameraSettings
    {
        public string CameraUrl { get; set; } = "http://localhost";
        public int CameraPort { get; set; } = 0;  // Puerto de la cámara (0 para la primera cámara)
        public string VideoStoragePath { get; set; } = "videos";
        public bool UseDirectShow { get; set; } = true;  // Usar DirectShow o no
        public int MaxRetries { get; set; } = 3;  // Intentos máximos de reconexión
    }
}
