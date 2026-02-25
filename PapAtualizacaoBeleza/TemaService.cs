namespace PapAtualizacaoBeleza
{
    public class TemaService
    {
        public bool ModoEscuro { get; private set; } = false;

        public event Action? OnChange;

        public void Alternar()
        {
            ModoEscuro = !ModoEscuro;
            OnChange?.Invoke();
        }
    }
}
