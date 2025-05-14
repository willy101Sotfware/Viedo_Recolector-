namespace VIDEO_RECOLECTOR.Models
{
    public class CameraSettings
    {
        public string CameraUrl { get; set; } = "http://localhost";
        public int CameraPort { get; set; } = 0; 
        public string VideoStoragePath { get; set; } = "videos";
        public bool UseDirectShow { get; set; } = true;  
        public int MaxRetries { get; set; } = 3; 
    }
}
