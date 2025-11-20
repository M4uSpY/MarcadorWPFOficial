using DPUruNet;
using MarcadorWPF.DTOs;
using MarcadorWPF.Services;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;



namespace MarcadorWPF
{
    public partial class MainWindow : Window
    {
        private DispatcherTimer _timer;
        private const int DPFJ_PROBABILITY_ONE = 0x7fffffff;
        private Reader _reader = null;
        private Fmd _ultimoTemplate;
        private HttpClient _httpClient;
        private readonly ApiClient _apiClient;

        // Umbral típico de aceptación
        private readonly int _targetFmr = DPFJ_PROBABILITY_ONE / 100000;

        public MainWindow()
        {
            InitializeComponent();
            _apiClient = new ApiClient("https://localhost:7084/");
            _httpClient = new HttpClient();
            txtFechaActual.Text = DateTime.Now.ToString("dddd, d 'de' MMMM 'de' yyyy",
        new System.Globalization.CultureInfo("es-ES"));
            // Mostrar la hora inmediatamente al iniciar
            ActualizarHora();

            // Iniciar temporizador para actualizar cada segundo
            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.Tick += (s, ev) => ActualizarHora();
            _timer.Start();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            RefreshReaders();
        }
        private void ActualizarHora()
        {
            txtHoraActual.Text = DateTime.Now.ToString("hh:mm tt", CultureInfo.InvariantCulture);
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

                btnLector.Content = "LECTOR ACTIVO";
                //Log("Lector abierto: " + _reader.Description.SerialNumber);
                //Log("Captura automática iniciada. Coloca el dedo en el lector...");
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

                                Log($"✅ Huella identificada");

                                // 1️⃣ Calculamos todo fuera del Dispatcher
                                var ahora = DateTime.Now;

                                var asistencia = new AsistenciaCrearDTO
                                {
                                    IdTrabajador = h.IdTrabajador,
                                    Fecha = ahora.Date,
                                    Hora = ahora.TimeOfDay,
                                    esEntrada = true
                                };

                                BitmapImage bitmap = null;
                                if (h.Foto != null && h.Foto.Length > 0)
                                {
                                    using (MemoryStream ms = new MemoryStream(h.Foto))
                                    {
                                        var tmp = new BitmapImage();
                                        tmp.BeginInit();
                                        tmp.CacheOption = BitmapCacheOption.OnLoad;
                                        tmp.StreamSource = ms;
                                        tmp.EndInit();
                                        tmp.Freeze();              // Para que se pueda usar en otro hilo
                                        bitmap = tmp;
                                    }
                                }

                                // 2️⃣ Dispatcher SOLO para actualizar la UI
                                await Application.Current.Dispatcher.InvokeAsync(() =>
                                {
                                    imgPersona.Source = bitmap;    // Puede ser null, no hay problema

                                    txtNombreCompleto.Text = $"{h.PrimerNombre} {h.ApellidoPaterno}";
                                    txtCarnetIdentidad.Text = h.CI;
                                    txtFechaRegistrada.Text = ahora.ToString("dd/MM/yyyy HH:mm:ss");
                                    txtCargo.Text = h.Cargo;
                                    txtHoraRegistrada.Text = ahora.ToString("hh:mm tt", CultureInfo.InvariantCulture);
                                });


                                // 3️⃣ Llamada a la API FUERA del Dispatcher
                                var resultado = await _apiClient.CrearAsistenciaAsync(asistencia);

                                if (resultado == null)
                                {
                                    Log("❌ Error al registrar asistencia en la API.");
                                }
                                else
                                {
                                    await Application.Current.Dispatcher.InvokeAsync(() =>
                                    {
                                        switch (resultado.TipoMarcacion)
                                        {
                                            case "ENTRADA":
                                                txtTipoMarcacion.Text = "ENTRADA";
                                                txtTipoMarcacion.Foreground =
                                                    new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x0C, 0xC7, 0x69)); // verde
                                                txtTiempoTrabajado.Text = "-";   // aún no hay horas trabajadas
                                                break;

                                            case "SALIDA":
                                                txtTipoMarcacion.Text = "SALIDA";
                                                txtTipoMarcacion.Foreground =
                                                    new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFF, 0x4B, 0x5C)); // rojo
                                                txtTiempoTrabajado.Text = resultado.HorasTrabajadas ?? "-";
                                                break;

                                            case "EN_PROCESO":
                                            default:
                                                txtTipoMarcacion.Text = "EN PROCESO";
                                                txtTipoMarcacion.Foreground =
                                                    new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFF, 0xC1, 0x07)); // amarillo
                                                txtTiempoTrabajado.Text = "-";
                                                break;
                                        }
                                    });

                                    // Log en el recuadro verde
                                    if (resultado.Registrado)
                                    {
                                        Log($"✅ {resultado.Mensaje}");
                                    }
                                    else
                                    {
                                        Log($"ℹ {resultado.Mensaje}");
                                    }
                                }

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
                txtLog.Text = msg;
            });
        }
        
    }
}
