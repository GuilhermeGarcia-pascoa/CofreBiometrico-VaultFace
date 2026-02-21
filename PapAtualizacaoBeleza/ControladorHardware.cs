using System.IO.Ports;

namespace SProjetoPapAtualizacao
{
    public class ControladorHardware
    {
        private SerialPort _serialPort;

        public ControladorHardware(string portaCOM)
        {
            _serialPort = new SerialPort(portaCOM, 9600);

            try
            {
                if (!_serialPort.IsOpen)
                {
                    _serialPort.Open();
                }
            }
            catch (Exception ex)
            {
                //lança o erro para quem chamou ou tratar
                throw new Exception($"Erro ao conectar no Arduino: {ex.Message}");
            }
        }

        // Função 1: ABRIR
        public void Abrir()
        {
            if (_serialPort.IsOpen)
            {
                _serialPort.Write("A"); // Manda 'A' para o Arduino
            }
        }

        // Função 2: FECHAR
        public void Fechar()
        {
            if (_serialPort.IsOpen)
            {
                _serialPort.Write("F"); // Manda 'F' para o Arduino
            }
        }

        // Função extra para limpar a memória quando fechar o programa
        public void Desconectar()
        {
            if (_serialPort != null && _serialPort.IsOpen)
            {
                _serialPort.Close();
            }
        }
    }
}