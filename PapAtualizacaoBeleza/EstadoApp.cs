namespace PapAtualizacaoBeleza
{
    public class EstadoApp
    {
        public Usuario? UsuarioAtual {  get; set; }

        public bool EstadoLogado => UsuarioAtual != null;

        public bool EhAdmin => UsuarioAtual?.Permissao >= NivelPermissao.AdminComum;

        public void Logout() => UsuarioAtual = null;
    }
}
