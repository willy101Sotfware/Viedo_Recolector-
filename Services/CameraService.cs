using OpenCvSharp;
using Microsoft.Extensions.Configuration;
using System.Globalization;
using Microsoft.AspNetCore.Hosting;
using VIDEO_RECOLECTOR.Models;

namespace VIDEO_RECOLECTOR.Services
{
    public interface ICameraService
    {
        Task StartCamera();
        Task StopCamera();
        bool IsRecording { get; }
    }

    public class CameraService : ICameraService, IDisposable
    {
        private VideoCapture? _capture;
        private VideoWriter? _writer;
        private bool _isRecording;
        private string _currentVideoPath = "";
        private readonly string _baseVideoDirectory;
        private readonly ILogger<CameraService> _logger;
        private int _selectedCameraPort = -1;
        private int frameCount = 0;
        private static readonly object _cameraLock = new object();
        private static HashSet<int> _busyCameras = new HashSet<int>();
        private readonly IConfiguration _configuration;
        private bool _disposed;

        public bool IsRecording => _isRecording;

        public CameraService(IConfiguration configuration, IWebHostEnvironment env, ILogger<CameraService> logger)
        {
            _configuration = configuration;
            var videoPath = _configuration.GetValue<string>("CameraSettings:VideoStoragePath") ?? "videos";
            _baseVideoDirectory = Path.Combine(env.WebRootPath, videoPath);
            _logger = logger;
            _disposed = false;
        }

        private bool IsCameraBusy(int port)
        {
            lock (_cameraLock)
            {
                return _busyCameras.Contains(port);
            }
        }

        private void MarkCameraAsBusy(int port)
        {
            lock (_cameraLock)
            {
                _busyCameras.Add(port);
            }
        }

        private void MarkCameraAsAvailable(int port)
        {
            lock (_cameraLock)
            {
                _busyCameras.Remove(port);
            }
        }

        private (int width, int height, double fps) GetCameraCapabilities(VideoCapture capture)
        {
            int width = (int)capture.Get(VideoCaptureProperties.FrameWidth);
            int height = (int)capture.Get(VideoCaptureProperties.FrameHeight);
            double fps = capture.Get(VideoCaptureProperties.Fps);
            return (width, height, fps);
        }

        private void DetectCamera()
        {
            // Si no, buscar una cámara disponible
            for (int port = 0; port < 10; port++)
            {
                if (IsCameraBusy(port))
                {
                    _logger.LogInformation($"Cámara {port} está ocupada, continuando búsqueda...");
                    continue;
                }

                try
                {
                    using var testCapture = new VideoCapture(port, VideoCaptureAPIs.ANY);
                    if (testCapture.IsOpened())
                    {
                        var capabilities = GetCameraCapabilities(testCapture);
                        _logger.LogInformation($"Cámara encontrada en puerto {port}. Resolución: {capabilities.width}x{capabilities.height}, FPS: {capabilities.fps}");
                        _selectedCameraPort = port;
                        break;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Error al probar cámara en puerto {port}: {ex.Message}");
                }
            }

            if (_selectedCameraPort == -1)
            {
                throw new Exception("No se encontró ninguna cámara disponible");
            }
        }

        private string CreateVideoDirectory()
        {
            var now = DateTime.Now;
            var yearDir = Path.Combine(_baseVideoDirectory, now.Year.ToString());
            var monthDir = Path.Combine(yearDir, now.ToString("MMMM", CultureInfo.InvariantCulture));
            var weekDir = Path.Combine(monthDir, $"Semana{now.Day / 7 + 1}");
            var dayDir = Path.Combine(weekDir, now.ToString("dddd", CultureInfo.InvariantCulture));

            Directory.CreateDirectory(dayDir);
            return dayDir;
        }

        public async Task StartCamera()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(CameraService));
            }

            if (_isRecording)
            {
                _logger.LogWarning("La cámara ya está grabando");
                return;
            }

            try
            {
                DetectCamera();
                MarkCameraAsBusy(_selectedCameraPort);
                _capture = new VideoCapture(_selectedCameraPort, VideoCaptureAPIs.ANY);
                if (!_capture.IsOpened())
                {
                    throw new Exception($"No se pudo abrir la cámara en puerto {_selectedCameraPort}");
                }

                var capabilities = GetCameraCapabilities(_capture);
                var videoDir = CreateVideoDirectory();
                var timestamp = DateTime.Now.ToString("HH_mm_ss");
                _currentVideoPath = Path.Combine(videoDir, $"video_{timestamp}.avi");

                _writer = new VideoWriter(_currentVideoPath, FourCC.XVID, capabilities.fps, new Size(capabilities.width, capabilities.height));
                _isRecording = true;

                await Task.Run(() =>
                {
                    try
                    {
                        using var frame = new Mat();
                        while (_isRecording && !_disposed)
                        {
                            if (_capture.Read(frame))
                            {
                                if (!frame.Empty())
                                {
                                    _writer.Write(frame);
                                    frameCount++;
                                    if (frameCount % 30 == 0)
                                    {
                                        _logger.LogInformation($"Frames grabados: {frameCount}");
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Error durante la grabación: {ex.Message}");
                        _isRecording = false;
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error al iniciar la grabación: {ex.Message}");
                CleanupResources();
                throw;
            }
        }

        public async Task StopCamera()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(CameraService));
            }

            _isRecording = false;
            await Task.Delay(100); // Dar tiempo para que el bucle de grabación termine
            CleanupResources();
        }

        private void CleanupResources()
        {
            if (_writer != null)
            {
                _writer.Dispose();
                _writer = null;
            }

            if (_capture != null)
            {
                _capture.Dispose();
                _capture = null;
            }

            if (_selectedCameraPort != -1)
            {
                MarkCameraAsAvailable(_selectedCameraPort);
                _selectedCameraPort = -1;
            }

            frameCount = 0;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                if (_isRecording)
                {
                    StopCamera().Wait();
                }
                CleanupResources();
            }
        }
    }
}
