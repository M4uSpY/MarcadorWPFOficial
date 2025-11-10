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
    }
}
