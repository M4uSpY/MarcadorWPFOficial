using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarcadorWPF.DTOs
{
    public class HuellaRespuestaDTO
    {
        public int IdPersona { get; set; }
        public int IdTrabajador { get; set; }
        public string PrimerNombre { get; set; } = string.Empty;
        public string SegundoNombre { get; set; } = string.Empty;
        public string ApellidoPaterno { get; set; } = string.Empty;
        public string ApellidoMaterno { get; set; } = string.Empty;
        public string CI { get; set; } = string.Empty;
        public string Cargo { get; set; } = string.Empty;
        public byte[] Foto { get; set; }
        public string TemplateXml { get; set; } = string.Empty;
    }
}
