using OpenCvSharp;
using Microsoft.Extensions.Configuration;
using System.Globalization;
using Microsoft.AspNetCore.Hosting;
using VIDEO_RECOLECTOR.Models;
using System.IO;
using System.Diagnostics;

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
        private readonly IConfiguration _configuration;
        private bool _disposed;
        private static SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
        private Task? _recordingTask;
        private CancellationTokenSource? _recordingCts;
        private int _frameCount = 0;

        public bool IsRecording => _isRecording;

        public CameraService(IConfiguration configuration, IWebHostEnvironment env, ILogger<CameraService> logger)
        {
            _configuration = configuration;
            var videoPath = _configuration.GetValue<string>("CameraSettings:VideoStoragePath") ?? "videos";
            
            
            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string wwwrootPath = Path.Combine(baseDirectory, "wwwroot");
            
           
            if (!Directory.Exists(wwwrootPath))
            {
                Directory.CreateDirectory(wwwrootPath);
                logger.LogInformation($"Creado directorio wwwroot: {wwwrootPath}");
            }
            
            _baseVideoDirectory = Path.Combine(wwwrootPath, videoPath);
            
            if (!Directory.Exists(_baseVideoDirectory))
            {
                Directory.CreateDirectory(_baseVideoDirectory);
                logger.LogInformation($"Creado directorio base de videos: {_baseVideoDirectory}");
            }
            
            _logger = logger;
            _disposed = false;
            
            _logger.LogInformation($"Directorio base para videos configurado en: {_baseVideoDirectory}");
        }

        private string CreateVideoDirectory()
        {
            try
            {
                var now = DateTime.Now;
                
                string yearStr = now.Year.ToString();
                string monthStr = now.Month.ToString("00");
                string dayStr = now.Day.ToString("00");
                
                string yearPath = Path.Combine(_baseVideoDirectory, yearStr);
                string monthPath = Path.Combine(yearPath, monthStr);
                string dayPath = Path.Combine(monthPath, dayStr);
                
                if (!Directory.Exists(yearPath))
                {
                    Directory.CreateDirectory(yearPath);
                    _logger.LogInformation($"Creado directorio de año: {yearPath}");
                }
                
                if (!Directory.Exists(monthPath))
                {
                    Directory.CreateDirectory(monthPath);
                    _logger.LogInformation($"Creado directorio de mes: {monthPath}");
                }
                
                if (!Directory.Exists(dayPath))
                {
                    Directory.CreateDirectory(dayPath);
                    _logger.LogInformation($"Creado directorio de día: {dayPath}");
                }
                
                _logger.LogInformation($"Ruta para guardar videos: {dayPath}");
                return dayPath;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error al crear directorios: {ex.Message}");
                
                if (!Directory.Exists(_baseVideoDirectory))
                {
                    try
                    {
                        Directory.CreateDirectory(_baseVideoDirectory);
                    }
                    catch
                    {
                        return Directory.GetCurrentDirectory();
                    }
                }
                
                return _baseVideoDirectory;
            }
        }

        public async Task StartCamera()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(CameraService));
            }

            await _semaphore.WaitAsync();

            try
            {
                if (_isRecording)
                {
                    _logger.LogWarning("La cámara ya está grabando");
                    return;
                }

                await StopCameraInternal();

                _logger.LogInformation("Iniciando grabación...");

                try
                {
                    _capture = null;
                    
                    try
                    {
                        _capture = new VideoCapture(0);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Error al crear VideoCapture: {ex.Message}");
                        throw new Exception("No se pudo abrir la cámara");
                    }

                    if (_capture == null || !_capture.IsOpened())
                    {
                        throw new Exception("No se pudo abrir la cámara");
                    }

                    int width = 640;
                    int height = 480;
                    double fps = 30;

                    try
                    {
                        width = (int)_capture.Get(VideoCaptureProperties.FrameWidth);
                        height = (int)_capture.Get(VideoCaptureProperties.FrameHeight);
                        fps = _capture.Get(VideoCaptureProperties.Fps);
                        
                        if (width <= 0) width = 640;
                        if (height <= 0) height = 480;
                        if (fps <= 0) fps = 30;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"Error al obtener propiedades de la cámara: {ex.Message}. Usando valores predeterminados.");
                    }

                    var videoDir = CreateVideoDirectory();
                    var timestamp = DateTime.Now.ToString("HH_mm_ss");
                    _currentVideoPath = Path.Combine(videoDir, $"video_{timestamp}.avi");

                    _writer = null;
                    
                    try
                    {
                        _writer = new VideoWriter(_currentVideoPath, FourCC.XVID, fps, new Size(width, height));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Error al crear VideoWriter con XVID: {ex.Message}. Intentando con MJPG.");
                        
                        try
                        {
                            _writer = new VideoWriter(_currentVideoPath, FourCC.MJPG, fps, new Size(width, height));
                        }
                        catch (Exception ex2)
                        {
                            _logger.LogError($"Error al crear VideoWriter con MJPG: {ex2.Message}");
                            throw new Exception("No se pudo crear el archivo de video");
                        }
                    }

                    if (_writer == null || !_writer.IsOpened())
                    {
                        throw new Exception("No se pudo crear el archivo de video");
                    }

                    _isRecording = true;
                    _frameCount = 0;

                    _recordingCts = new CancellationTokenSource();
                    var token = _recordingCts.Token;

                    _recordingTask = Task.Run(() =>
                    {
                        try
                        {
                            using var frame = new Mat();
                            int errorCount = 0;
                            
                            while (!token.IsCancellationRequested && _isRecording && !_disposed && errorCount < 10)
                            {
                                try
                                {
                                    if (_capture != null && _writer != null)
                                    {
                                        bool readSuccess = false;
                                        
                                        try
                                        {
                                            readSuccess = _capture.Read(frame);
                                        }
                                        catch (Exception ex)
                                        {
                                            _logger.LogError($"Error al leer frame: {ex.Message}");
                                            errorCount++;
                                            Thread.Sleep(100);
                                            continue;
                                        }
                                        
                                        if (readSuccess && !frame.Empty())
                                        {
                                            try
                                            {
                                                _writer.Write(frame);
                                                _frameCount++;
                                                if (_frameCount % 30 == 0)
                                                {
                                                    _logger.LogInformation($"Frames grabados: {_frameCount}");
                                                }
                                                errorCount = 0; 
                                            }
                                            catch (Exception ex)
                                            {
                                                _logger.LogError($"Error al escribir frame: {ex.Message}");
                                                errorCount++;
                                            }
                                        }
                                    }
                                    
                                    Thread.Sleep(10);
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError($"Error en el bucle de grabación: {ex.Message}");
                                    errorCount++;
                                    Thread.Sleep(100); 
                                }
                            }
                            
                            if (errorCount >= 10)
                            {
                                _logger.LogError("Demasiados errores consecutivos. Deteniendo grabación.");
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError($"Error durante la grabación: {ex.Message}");
                        }
                    }, token);

                    _logger.LogInformation($"Grabación iniciada. Archivo: {_currentVideoPath}");
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error al iniciar la grabación: {ex.Message}");
                    CleanupResources();
                    throw;
                }
            }
            finally
            {
                _semaphore.Release();
            }
        }

        private async Task StopCameraInternal()
        {
            try
            {
                if (_recordingCts != null)
                {
                    _recordingCts.Cancel();
                }
                
                if (_recordingTask != null)
                {
                   
                    await Task.WhenAny(_recordingTask, Task.Delay(2000));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error al detener la grabación: {ex.Message}");
            }
            finally
            {
                CleanupResources();
            }
        }

        public async Task StopCamera()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(CameraService));
            }

      
            await _semaphore.WaitAsync();

            try
            {
                if (!_isRecording)
                {
                    _logger.LogWarning("La cámara no está grabando");
                    return;
                }

                _isRecording = false;
                _logger.LogInformation($"Deteniendo grabación. Total frames: {_frameCount}");
                
                await StopCameraInternal();
            }
            finally
            {
                _semaphore.Release();
            }
        }

        private void CleanupResources()
        {
            try
            {
                if (_writer != null)
                {
                    try { _writer.Release(); } catch { }
                    try { _writer.Dispose(); } catch { }
                    _writer = null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error al liberar writer: {ex.Message}");
            }

            try
            {
                if (_capture != null)
                {
                    try { _capture.Release(); } catch { }
                    try { _capture.Dispose(); } catch { }
                    _capture = null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error al liberar capture: {ex.Message}");
            }

            _recordingCts = null;
            _recordingTask = null;
            _frameCount = 0;
            _isRecording = false;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                
                if (_isRecording)
                {
                    try
                    {
                        StopCamera().Wait(3000);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Error al detener la grabación durante Dispose: {ex.Message}");
                    }
                }
                
                CleanupResources();
            }
        }
    }
}
