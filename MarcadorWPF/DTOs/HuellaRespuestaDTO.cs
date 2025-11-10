using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarcadorWPF.DTOs
{
    public class HuellaRespuesta
    {
        public int IdPersona { get; set; }
        public byte[] Template { get; set; } = Array.Empty<byte>();
    }
}
