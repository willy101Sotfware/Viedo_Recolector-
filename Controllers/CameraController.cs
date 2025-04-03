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
                return StatusCode(500, new { error = "Error al iniciar la cámara: " + ex.Message });
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
                return StatusCode(500, new { error = "Error al detener la cámara: " + ex.Message });
            }
        }

        [HttpGet("status")]
        public IActionResult GetStatus()
        {
            return Ok(new { isRecording = _cameraService.IsRecording });
        }

        private List<object> GetVideosFromDirectory(string directory, string relativePath = "")
        {
            var result = new List<object>();

            if (!Directory.Exists(directory))
                return result;

            // Obtener subdirectorios
            foreach (var dir in Directory.GetDirectories(directory))
            {
                var dirName = Path.GetFileName(dir);
                var dirRelativePath = Path.Combine(relativePath, dirName);
                
                var videos = GetVideosFromDirectory(dir, dirRelativePath);
                if (videos.Any())
                {
                    result.Add(new
                    {
                        name = dirName,
                        type = "directory",
                        path = dirRelativePath.Replace("\\", "/"),
                        items = videos
                    });
                }
            }

            // Obtener videos
            var videoFiles = Directory.GetFiles(directory, "*.mp4")
                .Select(file =>
                {
                    var fileName = Path.GetFileName(file);
                    return new
                    {
                        name = fileName,
                        type = "file",
                        url = $"/videos/{Path.Combine(relativePath, fileName).Replace("\\", "/")}",
                        createdAt = System.IO.File.GetCreationTime(file),
                        size = new FileInfo(file).Length
                    };
                })
                .OrderByDescending(v => v.createdAt)
                .ToList<object>();

            result.AddRange(videoFiles);
            return result;
        }

        [HttpGet("videos")]
        public IActionResult GetVideos()
        {
            try
            {
                var videosDirectory = Path.Combine(_env.WebRootPath, "videos");
                var videos = GetVideosFromDirectory(videosDirectory);
                return Ok(new { videos });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener la lista de videos");
                return StatusCode(500, new { error = "Error al obtener la lista de videos: " + ex.Message });
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

                var fileStream = new FileStream(videoPath, FileMode.Open, FileAccess.Read);
                return File(fileStream, "video/mp4", Path.GetFileName(filePath));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener el video");
                return StatusCode(500, new { error = "Error al obtener el video: " + ex.Message });
            }
        }
    }
}
