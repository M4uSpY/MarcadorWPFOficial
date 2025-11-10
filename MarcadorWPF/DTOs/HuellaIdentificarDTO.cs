using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarcadorWPF.DTOs
{
    public class HuellaIdentificarDTO
    {
        public int IdPersona { get; set; }
        public string ApellidoPaterno { get; set; } = string.Empty;
        public string ApellidoMaterno { get; set; } = string.Empty;
        public string PrimerNombre { get; set; } = string.Empty;
        public string SegundoNombre { get; set; } = string.Empty;
        public DateTime FechaNacimiento { get; set; }
        public byte[] Foto { get; set; }
    }
}
