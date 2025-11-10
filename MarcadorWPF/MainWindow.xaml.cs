using DPUruNet;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace MarcadorWPF
{
    public partial class MainWindow : Window
    {
        private const int DPFJ_PROBABILITY_ONE = 0x7fffffff;
        private Reader _reader = null;
        private Fmd _ultimoTemplate;
        private HttpClient _httpClient;

        // Umbral típico de aceptación
        private readonly int _targetFmr = DPFJ_PROBABILITY_ONE / 100000;

        public MainWindow()
        {
            InitializeComponent();
            _httpClient = new HttpClient();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Log("Iniciando aplicacion...");
            RefreshReaders();
        }

        private void RefreshReaders()
        {
            try
            {
                ReaderCollection readers = ReaderCollection.GetReaders();
                if (readers.Count == 0)
                {
                    Log("No se encontraron lectores de huella.");
                    return;
                }

                _reader = readers[0];
                var rc = _reader.Open(Constants.CapturePriority.DP_PRIORITY_COOPERATIVE);
                if (rc != Constants.ResultCode.DP_SUCCESS)
                {
                    Log("Error al abrir lector: " + rc);
                    return;
                }

                _reader.On_Captured += Reader_OnCaptured;
                _reader.CaptureAsync(Constants.Formats.Fid.ANSI,
                    Constants.CaptureProcessing.DP_IMG_PROC_DEFAULT,
                    _reader.Capabilities.Resolutions[0]);

                Log("Lector abierto: " + _reader.Description.SerialNumber);
                Log("Captura automática iniciada. Coloca el dedo en el lector...");
            }
            catch (Exception ex)
            {
                Log("Error al enumerar lectores: " + ex.Message);
            }
        }

        private void Reader_OnCaptured(CaptureResult captureResult)
        {
            if (captureResult.ResultCode != Constants.ResultCode.DP_SUCCESS || captureResult.Data == null)
            {
                Log("Error en captura: " + captureResult.ResultCode);
                return;
            }

            ShowFidImage(captureResult.Data);

            var resIso = FeatureExtraction.CreateFmdFromFid(captureResult.Data, Constants.Formats.Fmd.ANSI);
            if (resIso.ResultCode != Constants.ResultCode.DP_SUCCESS || resIso.Data == null)
            {
                Log("Error extrayendo FMD ISO: " + resIso.ResultCode);
                return;
            }

            _ultimoTemplate = resIso.Data;
            Task.Run(async () => await VerificarHuellaAsync());
        }

        private async Task VerificarHuellaAsync()
        {
            if (_ultimoTemplate == null) return;

            try
            {
                string url = "https://localhost:7084/api/Huellas/listar";
                var json = await _httpClient.GetStringAsync(url);
                var huellas = JsonConvert.DeserializeObject<List<HuellaRespuestaDTO>>(json);

                if (huellas == null)
                {
                    Log("No se pudo obtener la lista de huellas desde la API.");
                    return;
                }

                bool matchFound = false;

                foreach (var h in huellas)
                {
                    try
                    {
                        if (string.IsNullOrWhiteSpace(h.TemplateXml)) continue;

                        Fmd dbFmd;
                        try
                        {
                            dbFmd = Fmd.DeserializeXml(h.TemplateXml);
                        }
                        catch
                        {
                            Log($"❌ Huella IdPersona={h.IdPersona} no tiene un XML válido.");
                            continue;
                        }

                        CompareResult cmp = Comparison.Compare(_ultimoTemplate, 0, dbFmd, 0);
                        if (cmp.ResultCode == Constants.ResultCode.DP_SUCCESS)
                        {
                            bool match = cmp.Score < _targetFmr;
                            if (match)
                            {
                                matchFound = true;
                                Log($"✅ Huella identificada: IdPersona={h.IdPersona} → {h.PrimerNombre} {h.SegundoNombre} {h.ApellidoPaterno} {h.ApellidoMaterno} → score={cmp.Score}");
                                break;
                            }
                        }
                        else
                        {
                            Log($"Error comparando huella IdPersona={h.IdPersona}: {cmp.ResultCode}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Log($"Error procesando huella IdPersona={h.IdPersona}: {ex.Message}");
                    }
                }

                if (!matchFound)
                {
                    Log("❌ Huella no encontrada en la base de datos.");
                }
            }
            catch (Exception ex)
            {
                Log("Error verificando huella: " + ex.Message);
            }
        }

        private void ShowFidImage(Fid fid)
        {
            try
            {
                Fid.Fiv view = fid.Views[0];
                int w = view.Width;
                int h = view.Height;
                Bitmap bmp = new Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format8bppIndexed);

                var pal = bmp.Palette;
                for (int i = 0; i < 256; i++) pal.Entries[i] = System.Drawing.Color.FromArgb(i, i, i);
                bmp.Palette = pal;

                var data = bmp.LockBits(new System.Drawing.Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, bmp.PixelFormat);
                for (int y = 0; y < h; y++)
                {
                    IntPtr row = IntPtr.Add(data.Scan0, y * data.Stride);
                    Marshal.Copy(view.RawImage, y * w, row, w);
                }
                bmp.UnlockBits(data);

                Dispatcher.Invoke(() =>
                {
                    imgFinger.Source = ConvertBitmapToImageSource(bmp);
                });
            }
            catch (Exception ex)
            {
                Log("Error renderizando imagen: " + ex.Message);
            }
        }

        private BitmapImage ConvertBitmapToImageSource(Bitmap bitmap)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                bitmap.Save(ms, ImageFormat.Bmp);
                ms.Position = 0;
                BitmapImage bi = new BitmapImage();
                bi.BeginInit();
                bi.CacheOption = BitmapCacheOption.OnLoad;
                bi.StreamSource = ms;
                bi.EndInit();
                return bi;
            }
        }

        private void Log(string msg)
        {
            Dispatcher.Invoke(() =>
            {
                txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {msg}\r\n");
                txtLog.ScrollToEnd();
            });
        }
        // DTO para deserializar JSON de la API
        public class HuellaRespuestaDTO
        {
            public int IdPersona { get; set; }
            public string PrimerNombre { get; set; } = string.Empty;
            public string SegundoNombre { get; set; } = string.Empty;
            public string ApellidoPaterno { get; set; } = string.Empty;
            public string ApellidoMaterno { get; set; } = string.Empty;
            public string TemplateXml { get; set; } = string.Empty; // XML del template
        }
    }
}
