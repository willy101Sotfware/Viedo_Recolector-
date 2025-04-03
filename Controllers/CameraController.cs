using Microsoft.AspNetCore.Mvc;
using VIDEO_RECOLECTOR.Services;
using System.IO;
using Microsoft.AspNetCore.Hosting;

namespace VIDEO_RECOLECTOR.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CameraController : ControllerBase
    {
        private readonly ICameraService _cameraService;
        private readonly ILogger<CameraController> _logger;
        private readonly IWebHostEnvironment _env;

        public CameraController(ICameraService cameraService, ILogger<CameraController> logger, IWebHostEnvironment env)
        {
            _cameraService = cameraService;
            _logger = logger;
            _env = env;
        }

        [HttpPost("start")]
        public async Task<IActionResult> StartCamera()
        {
            try
            {
                await _cameraService.StartCamera();
                return Ok(new { message = "Cámara iniciada exitosamente" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al iniciar la cámara");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpPost("stop")]
        public async Task<IActionResult> StopCamera()
        {
            try
            {
                await _cameraService.StopCamera();
                return Ok(new { message = "Cámara detenida exitosamente" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al detener la cámara");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpGet("status")]
        public IActionResult GetStatus()
        {
            return Ok(new { isRecording = _cameraService.IsRecording });
        }

        [HttpGet("videos")]
        public IActionResult GetVideos()
        {
            try
            {
                var videosPath = Path.Combine(_env.WebRootPath, "videos");
                if (!Directory.Exists(videosPath))
                {
                    return Ok(new { videos = new List<VideoInfo>() });
                }

                var videos = Directory.GetFiles(videosPath, "*.*", SearchOption.AllDirectories)
                    .Where(file => file.EndsWith(".avi", StringComparison.OrdinalIgnoreCase))
                    .Select(file =>
                    {
                        var fileInfo = new FileInfo(file);
                        var relativePath = Path.GetRelativePath(videosPath, file).Replace('\\', '/');
                        return new VideoInfo
                        {
                            FileName = fileInfo.Name,
                            FilePath = $"/videos/{relativePath}",
                            FileSize = fileInfo.Length,
                            CreatedAt = fileInfo.CreationTime,
                            DownloadUrl = $"/api/Camera/videos/{relativePath}"
                        };
                    })
                    .OrderByDescending(v => v.CreatedAt)
                    .ToList();

                return Ok(new { videos });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener la lista de videos");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpGet("videos/{**filePath}")]
        public IActionResult GetVideo(string filePath)
        {
            try
            {
                var videoPath = Path.Combine(_env.WebRootPath, "videos", filePath);
                if (!System.IO.File.Exists(videoPath))
                {
                    return NotFound(new { error = "Video no encontrado" });
                }

                // Verificar que el archivo esté dentro del directorio de videos
                var videosDirectory = Path.Combine(_env.WebRootPath, "videos");
                var fullPath = Path.GetFullPath(videoPath);
                if (!fullPath.StartsWith(Path.GetFullPath(videosDirectory)))
                {
                    return BadRequest(new { error = "Ruta de archivo no válida" });
                }

                // Devolver el archivo con el tipo MIME correcto para AVI
                return PhysicalFile(videoPath, "video/x-msvideo", enableRangeProcessing: true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener el video");
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }

    public class VideoInfo
    {
        public string FileName { get; set; } = "";
        public string FilePath { get; set; } = "";
        public string DownloadUrl { get; set; } = "";
        public long FileSize { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
