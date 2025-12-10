using MarcadorWPF.DTOs;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace MarcadorWPF.Services
{
    public class ApiClient
    {
        private readonly HttpClient _http;
        
        public ApiClient(string baseUrl)
        {
            _http = new HttpClient();
            _http.BaseAddress = new Uri(baseUrl);
            _http.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

            _http.DefaultRequestHeaders.Add("X-Marcador-Key", "MI_SUPER_CLAVE_MUY_LARGA_Y_RARA_123_ABC_XYZasdfasdfasdfadsfadsfadsfadsf");
        }

        public async Task<List<HuellaRespuestaDTO>> ListarHuellasAsync()
        {
            var resp = await _http.GetAsync("api/huellas/listar");

            if (!resp.IsSuccessStatusCode)
                return new List<HuellaRespuestaDTO>();

            string json = await resp.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<List<HuellaRespuestaDTO>>(json)
                   ?? new List<HuellaRespuestaDTO>();
        }

        public async Task<AsistenciaRegistrarResultadoDTO> CrearAsistenciaAsync(AsistenciaCrearDTO asistencia)
        {
            string json = JsonConvert.SerializeObject(asistencia);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var resp = await _http.PostAsync("api/asistencias", content);

            string body = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
            {
                return new AsistenciaRegistrarResultadoDTO
                {
                    Registrado = false,
                    Mensaje = $"Error al registrar asistencia. Status: {resp.StatusCode}"
                };
            }

            var resultado = JsonConvert.DeserializeObject<AsistenciaRegistrarResultadoDTO>(body);
            return resultado;
        }

    }
}
