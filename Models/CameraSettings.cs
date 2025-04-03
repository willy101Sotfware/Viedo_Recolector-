namespace VIDEO_RECOLECTOR.Models
{
    public class CameraSettings
    {
        public int Port { get; set; }
        public string VideoFormat { get; set; } = "mp4";
        public int FrameRate { get; set; }
        public Resolution Resolution { get; set; } = new Resolution();
    }

    public class Resolution
    {
        public int Width { get; set; }
        public int Height { get; set; }
    }
}
