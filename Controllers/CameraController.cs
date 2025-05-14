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

        [HttpPost("forcestop")]
        public async Task<IActionResult> ForceStopCamera()
        {
            try
            {
                _logger.LogWarning("Forzando detención de la cámara");
                
            
                try
                {
                    await _cameraService.StopCamera();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error en el primer intento de detener la cámara durante forcestop");
                }
                
              
                if (_cameraService is CameraService cameraService)
                {
                    try
                    {
                        cameraService.Dispose();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error al forzar la liberación de recursos");
                    }
                }
                
                return Ok(new { message = "Cámara forzada a detenerse" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al forzar la detención de la cámara");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpGet("status")]
        public IActionResult GetStatus()
        {
            var isRecording = _cameraService.IsRecording;
            return Ok(new { isRecording });
        }
    }
}
