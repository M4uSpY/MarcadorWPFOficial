using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarcadorWPF.DTOs
{
    public class AsistenciaRegistrarResultadoDTO
    {
        public bool Registrado { get; set; }
        public bool EsEntrada { get; set; }
        public bool FaltaGenerada { get; set; }
        public string Mensaje { get; set; } = string.Empty;
    }
}
