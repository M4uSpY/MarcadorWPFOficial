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
        public string TipoMarcacion { get; set; } = "";
        public DateTime? HoraEntrada { get; set; }
        public DateTime? HoraSalida { get; set; }
        public string HorasTrabajadas { get; set; }
        public string Mensaje { get; set; } = "";
    }
}
