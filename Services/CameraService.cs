using OpenCvSharp;
using VIDEO_RECOLECTOR.Models;
using Microsoft.Extensions.Options;
using System.Globalization;
using Microsoft.AspNetCore.Hosting;

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
        private readonly CameraSettings _settings;
        private VideoCapture? _capture;
        private VideoWriter? _writer;
        private bool _isRecording;
        private string _currentVideoPath = "";
        private readonly string _baseVideoDirectory;

        public bool IsRecording => _isRecording;

        public CameraService(IOptions<CameraSettings> settings, IWebHostEnvironment env)
        {
            _settings = settings.Value;
            _baseVideoDirectory = Path.Combine(env.WebRootPath, "videos");
        }

        private string GetVideoDirectory(DateTime date)
        {
            var year = date.Year.ToString();
            var month = CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(date.Month);
            var weekNumber = CultureInfo.CurrentCulture.Calendar.GetWeekOfYear(
                date, 
                CultureInfo.CurrentCulture.DateTimeFormat.CalendarWeekRule, 
                CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek);
            var day = date.ToString("dddd", CultureInfo.CurrentCulture); // Nombre del día

            var path = Path.Combine(_baseVideoDirectory, year, month, $"Semana{weekNumber}", day);
            Directory.CreateDirectory(path);
            return path;
        }

        private string GetVideoFileName(DateTime date)
        {
            return $"video_{date:HH_mm_ss}.{_settings.VideoFormat}";
        }

        public async Task StartCamera()
        {
            if (_isRecording)
                return;

            try
            {
                _capture = new VideoCapture(_settings.Port);
                if (!_capture.IsOpened())
                    throw new Exception("No se pudo abrir la cámara.");

                _capture.Set(VideoCaptureProperties.FrameWidth, _settings.Resolution.Width);
                _capture.Set(VideoCaptureProperties.FrameHeight, _settings.Resolution.Height);
                _capture.Set(VideoCaptureProperties.Fps, _settings.FrameRate);

                var now = DateTime.Now;
                var videoDirectory = GetVideoDirectory(now);
                var videoFileName = GetVideoFileName(now);
                _currentVideoPath = Path.Combine(videoDirectory, videoFileName);

                _writer = new VideoWriter(_currentVideoPath, 
                    FourCC.MP4V,
                    _settings.FrameRate,
                    new Size(_settings.Resolution.Width, _settings.Resolution.Height));

                _isRecording = true;

                await Task.Run(RecordVideo);
            }
            catch (Exception)
            {
                _capture?.Dispose();
                _writer?.Dispose();
                _capture = null;
                _writer = null;
                _isRecording = false;
                throw;
            }
        }

        private async Task RecordVideo()
        {
            using var frame = new Mat();
            while (_isRecording && _capture != null && _writer != null)
            {
                if (_capture.Read(frame))
                {
                    if (!frame.Empty())
                    {
                        _writer.Write(frame);
                    }
                }
                await Task.Delay(1000 / _settings.FrameRate);
            }
        }

        public async Task StopCamera()
        {
            _isRecording = false;
            await Task.Delay(100); // Esperar a que termine el último frame

            _writer?.Dispose();
            _writer = null;

            _capture?.Dispose();
            _capture = null;
        }

        public void Dispose()
        {
            _writer?.Dispose();
            _capture?.Dispose();
        }
    }
}
