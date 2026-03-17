namespace PapAtualizacaoBeleza
{
    public class EstadoApp
    {
        public Usuario? UsuarioAtual { get; set; }

        public bool EstadoLogado => UsuarioAtual != null;

        public bool EhAdmin => UsuarioAtual?.Permissao >= NivelPermissao.AdminComum;

        public EstadoCofre EstadoCofreAtual { get; set; } = EstadoCofre.Fechado;

        public void Logout() => UsuarioAtual = null;

        // ── Verificação de segurança para ações críticas ──

        // tipo da ação pendente de verificação
        public AcaoCritica? AcaoPendente { get; set; }

        // Dia 23: flag para o Cadastro saber que é um re-treino autorizado
        public bool RetreinarBiometria { get; set; } = false;

        // id do novo master (só usado quando AcaoPendente == TransferirMaster)
        public int IdNovoMasterPendente { get; set; }

        // nome do alvo que o utilizador terá de escrever no step 2
        // reset → nome do próprio master | transferência → nome do novo master
        public string NomeAlvoVerificacao { get; set; } = "";

        // código gerado pelo EmailService — guardado aqui para comparar
        public string CodigoVerificacaoPendente { get; set; } = "";

        // quando foi gerado — expira em 10 minutos
        public DateTime CodigoGeradoEm { get; set; }

        public bool CodigoExpirado =>
            (DateTime.Now - CodigoGeradoEm).TotalMinutes > 10;

        // limpa tudo após conclusão ou cancelamento
        public void LimparVerificacao()
        {
            AcaoPendente = null;
            IdNovoMasterPendente = 0;
            NomeAlvoVerificacao = "";
            CodigoVerificacaoPendente = "";
            CodigoGeradoEm = default;
        }
    }

    public enum EstadoCofre { Fechado, AAbrir, Aberto, AFechar }

    // ações que requerem verificação dupla
    public enum AcaoCritica { ResetarSistema, TransferirMaster }
}
