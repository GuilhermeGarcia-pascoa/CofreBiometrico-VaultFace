namespace PapAtualizacaoBeleza
{
    public class EstadoApp
    {
        public Usuario? UsuarioAtual { get; set; }

        public bool EstadoLogado => UsuarioAtual != null;

        public bool EhAdmin => UsuarioAtual?.Permissao >= NivelPermissao.AdminComum;

        public EstadoCofre EstadoCofreAtual { get; set; } = EstadoCofre.Fechado;

        public void Logout() => UsuarioAtual = null;
    }

    public enum EstadoCofre { Fechado, AAbrir, Aberto, AFechar }
}
