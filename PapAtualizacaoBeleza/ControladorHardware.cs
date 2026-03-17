using System.IO.Ports;

namespace SProjetoPapAtualizacao
{
    public class ControladorHardware : IDisposable
    {
        private SerialPort _serialPort;
        private Timer? _heartbeatTimer;
        private bool _disposed = false;

        // Intervalo entre heartbeats: 2s
        // O Arduino tem timeout de 5s — margem de 2.5x garante tolerância a um heartbeat perdido
        private const int HEARTBEAT_INTERVALO_MS = 2000;

        // ── Verificação rápida de uma porta já conhecida ─────────────────────────
        // Usado pelo monitor de ligação para confirmar que o Arduino ainda responde.
        // Mais rápido que DetectarPortaArduino() porque já sabe qual porta testar.
        public static bool PingPorta(string porta)
        {
            SerialPort? teste = null;
            try
            {
                teste = new SerialPort(porta, 9600)
                {
                    ReadTimeout = 500,
                    WriteTimeout = 300,
                };
                teste.Open();
                Thread.Sleep(350);
                teste.DiscardInBuffer();
                teste.Write("P");
                string resposta = teste.ReadLine().Trim();
                return resposta == "VAULTFACE_OK";
            }
            catch
            {
                return false;
            }
            finally
            {
                try { teste?.Close(); teste?.Dispose(); } catch { }
            }
        }

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
                        ReadTimeout = 500,
                        WriteTimeout = 300,
                    };
                    teste.Open();

                    // O Arduino reinicia ao abrir a porta série — aguardar ~350ms
                    Thread.Sleep(350);

                    teste.DiscardInBuffer();
                    teste.Write("P");

                    string resposta = teste.ReadLine().Trim();

                    if (resposta == "VAULTFACE_OK")
                        return porta;
                }
                catch
                {
                    // Porta ocupada, sem resposta ou dispositivo errado — continua
                }
                finally
                {
                    try { teste?.Close(); teste?.Dispose(); } catch { }
                }
            }

            return null;
        }

        // ── Construtor ───────────────────────────────────────────────────────────
        public ControladorHardware(string portaCOM)
        {
            _serialPort = new SerialPort(portaCOM, 9600);

            try
            {
                if (!_serialPort.IsOpen)
                    _serialPort.Open();

                // Pausa após abrir — Arduino reinicia ao estabelecer ligação
                Thread.Sleep(350);
            }
            catch (Exception ex)
            {
                throw new Exception($"Erro ao conectar no Arduino: {ex.Message}");
            }
        }

        // ── Controlo do cofre ────────────────────────────────────────────────────

        // ── Dia 24: Sensor de porta ──────────────────────────────────────────────
        // Evento disparado quando o estado da porta muda (true=aberta, false=fechada)
        public event Action<bool>? PortaEstadoMudou;

        // Pede ao Arduino o estado atual da porta
        public bool? LerEstadoPorta()
        {
            try
            {
                if (!_serialPort.IsOpen) return null;
                _serialPort.DiscardInBuffer();
                _serialPort.Write("D");
                string resp = _serialPort.ReadLine().Trim();
                return resp == "PORTA_ABERTA";
            }
            catch { return null; }
        }

        // Subscreve eventos assíncronos do Arduino (porta sensor)
        public void SubscreverEventos()
        {
            _serialPort.DataReceived -= OnDadosRecebidos;
            _serialPort.DataReceived += OnDadosRecebidos;
        }

        private void OnDadosRecebidos(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                string linha = _serialPort.ReadLine().Trim();
                if (linha == "PORTA_ABERTA") PortaEstadoMudou?.Invoke(true);
                else if (linha == "PORTA_FECHADA") PortaEstadoMudou?.Invoke(false);
            }
            catch { }
        }

        // Abre o cofre — envia 'A' e inicia o heartbeat
        public void Abrir()
        {
            if (!_serialPort.IsOpen) return;
            _serialPort.Write("A");
            IniciarHeartbeat();
        }

        // Fecha o cofre — para o heartbeat e envia 'F'
        public void Fechar()
        {
            PararHeartbeat();
            if (!_serialPort.IsOpen) return;
            _serialPort.Write("F");
        }

        // ── Heartbeat ────────────────────────────────────────────────────────────

        // Inicia o timer que envia 'H' ao Arduino a cada 2 segundos.
        // Enquanto o cofre estiver aberto e o programa a correr, o Arduino
        // recebe este sinal e reseta o seu watchdog interno.
        private void IniciarHeartbeat()
        {
            PararHeartbeat(); // garantir que não há timer duplo

            _heartbeatTimer = new Timer(_ =>
            {
                try
                {
                    if (_serialPort.IsOpen)
                        _serialPort.Write("H");
                }
                catch
                {
                    // Se a porta falhar durante o heartbeat, para o timer silenciosamente
                    PararHeartbeat();
                }
            },
            state: null,
            dueTime: HEARTBEAT_INTERVALO_MS,   // primeiro disparo após 2s
            period: HEARTBEAT_INTERVALO_MS);   // repetir a cada 2s
        }

        // Para o timer de heartbeat — chamado ao fechar o cofre ou desconectar
        private void PararHeartbeat()
        {
            _heartbeatTimer?.Dispose();
            _heartbeatTimer = null;
        }

        // ── Limpeza ──────────────────────────────────────────────────────────────

        // Fecha a porta série — chamado no Dispose ou manualmente após operação simples
        public void Desconectar()
        {
            PararHeartbeat();
            if (_serialPort != null && _serialPort.IsOpen)
                _serialPort.Close();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Desconectar();
            _serialPort?.Dispose();
        }
    }
}
