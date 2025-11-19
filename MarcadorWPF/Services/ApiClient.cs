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
        }

        public async Task<List<HuellaRespuesta>> ListarHuellasAsync()
        {
            var resp = await _http.GetAsync("api/huellas/listar");
            resp.EnsureSuccessStatusCode();
            string json = await resp.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<List<HuellaRespuesta>>(json) ?? new List<HuellaRespuesta>();
        }

        public async Task<AsistenciaRegistrarResultadoDTO> CrearAsistenciaAsync(AsistenciaCrearDTO asistencia)
        {
            string json = JsonConvert.SerializeObject(asistencia);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var resp = await _http.PostAsync("api/asistencias", content);

            Console.WriteLine($"POST asistencia → Status: {resp.StatusCode}");

            string body = await resp.Content.ReadAsStringAsync();
            Console.WriteLine($"Respuesta backend: {body}");

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
