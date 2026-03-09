using System.IO.Ports;

namespace SProjetoPapAtualizacao
{
    public class ControladorHardware
    {
        private SerialPort _serialPort;

        // ── Deteção automática da porta ──────────────────────────────────────────
        // Itera todas as portas COM disponíveis no sistema, envia o ping 'P'
        // e aguarda a resposta "VAULTFACE_OK".
        // Devolve o nome da porta (ex: "COM4") ou null se não encontrar nenhuma.
        public static string? DetectarPortaArduino()
        {
            foreach (string porta in SerialPort.GetPortNames())
            {
                SerialPort? teste = null;
                try
                {
                    teste = new SerialPort(porta, 9600)
                    {
                        ReadTimeout = 500, // espera máxima pela resposta: 500ms
                        WriteTimeout = 300,
                    };
                    teste.Open();

                    // Pequena pausa — o Arduino reinicia ao abrir a porta série
                    // e leva ~300ms a ficar pronto para receber comandos
                    Thread.Sleep(350);

                    // Limpa o buffer de entrada antes de enviar o ping
                    teste.DiscardInBuffer();

                    teste.Write("P");

                    // Lê a linha de resposta (termina em \n graças ao Serial.println)
                    string resposta = teste.ReadLine().Trim();

                    if (resposta == "VAULTFACE_OK")
                        return porta; // porta certa encontrada
                }
                catch
                {
                    // Porta ocupada, sem resposta ou dispositivo errado — continua
                }
                finally
                {
                    // Fecha sempre a porta de teste — o construtor principal reabre depois
                    try { teste?.Close(); teste?.Dispose(); } catch { }
                }
            }

            return null; // nenhuma porta respondeu corretamente
        }

        // ── Construtor ───────────────────────────────────────────────────────────
        // Recebe a porta já detetada pelo método acima.
        public ControladorHardware(string portaCOM)
        {
            _serialPort = new SerialPort(portaCOM, 9600);

            try
            {
                if (!_serialPort.IsOpen)
                    _serialPort.Open();

                // Pausa após abrir — o Arduino reinicia ao estabelecer a ligação
                Thread.Sleep(350);
            }
            catch (Exception ex)
            {
                throw new Exception($"Erro ao conectar no Arduino: {ex.Message}");
            }
        }

        // Abre o cofre — envia 'A' ao Arduino
        public void Abrir()
        {
            if (_serialPort.IsOpen)
                _serialPort.Write("A");
        }

        // Fecha o cofre — envia 'F' ao Arduino
        public void Fechar()
        {
            if (_serialPort.IsOpen)
                _serialPort.Write("F");
        }

        // Fecha a porta série ao terminar — deve ser chamado após cada operação
        public void Desconectar()
        {
            if (_serialPort != null && _serialPort.IsOpen)
                _serialPort.Close();
        }
    }
}
