using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarcadorWPF.DTOs
{
    public class HuellaRegistrarDTO
    {
        public string NombreUsuario { get; set; } = string.Empty;
        public string TemplateISO { get; set; } = string.Empty; // base64
    }
}
