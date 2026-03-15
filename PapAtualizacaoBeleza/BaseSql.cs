using System.Data;
using System.Data.SqlClient;
using System.Security.Cryptography;
using System.Runtime.Versioning;

namespace PapAtualizacaoBeleza
{
    public enum NivelPermissao
    {
        Basico = 1,
        AdminComum = 2,
        AdminSupremo = 3
    }

    public class Usuario
    {
        public int Id { get; set; }
        public string Nome { get; set; }
        public NivelPermissao Permissao { get; set; }
        public List<byte[]> Rostos { get; set; } = new List<byte[]>();
    }

    // ── KPIs para a página de relatórios ─────────────────────────────────────
    public class EstatisticasRelatorio
    {
        public int TotalAcessos { get; set; }
        public int TentativasFalhadas { get; set; }
        public string UtilizadorMaisAtivo { get; set; } = "—";
        public int HoraDePico { get; set; }
        public int TotalCadastros { get; set; }
    }

    public class BaseSql
    {
        private string _caminhoBancoAtual;
        private string _connectionStringAtual;
        private string _caminhoAppData;
        private string _connectionStringAppData;

        // ── Pasta segura em %APPDATA%\VaultFace ──
        // Fica em C:\Users\<user>\AppData\Roaming\VaultFace
        // Não é apagada ao remover o executável e requer permissões de utilizador para aceder.
        private static string PastaVaultFace
        {
            get
            {
                string pasta = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "VaultFace"
                );
                Directory.CreateDirectory(pasta); // cria se não existir, não faz nada se já existir
                return pasta;
            }
        }

        // ── Chave-mestra protegida com DPAPI do Windows ───────────────────────────
        // Na primeira execução, gera uma chave AES-256 aleatória, protege-a com
        // ProtectedData.Protect() (vinculada à conta Windows atual) e guarda em
        // master.key na pasta AppData\VaultFace.
        // Em execuções futuras, lê o ficheiro e desprotege com ProtectedData.Unprotect().
        // Mesmo com acesso físico ao ficheiro .key e ao código-fonte, sem a sessão
        // Windows que o criou é impossível recuperar a chave original.
        [SupportedOSPlatform("windows")]
        private static byte[] ObterChaveMestra()
        {
            string pasta = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "VaultFace");
            Directory.CreateDirectory(pasta);
            string caminhoKey = Path.Combine(pasta, "master.key");

            if (File.Exists(caminhoKey))
            {
                // Carrega e desprotege a chave existente
                byte[] protegida = File.ReadAllBytes(caminhoKey);
                return ProtectedData.Unprotect(protegida, null, DataProtectionScope.CurrentUser);
            }
            else
            {
                // Primeira execução: gera chave aleatória, protege com DPAPI e guarda
                byte[] novaChave = new byte[32];
                using (var rng = RandomNumberGenerator.Create())
                    rng.GetBytes(novaChave);

                byte[] protegida = ProtectedData.Protect(novaChave, null, DataProtectionScope.CurrentUser);
                File.WriteAllBytes(caminhoKey, protegida);
                return novaChave;
            }
        }

        // IV fixo derivado da chave — não precisa de ser secreto, apenas consistente
        private static readonly byte[] ivFixa = new byte[16]; // IV zero — simples e consistente

        public BaseSql()
        {
            IniciarBanco();
        }
        public void IniciarBanco()
        {
            _caminhoAppData = Path.Combine(PastaVaultFace, "AppData.mdf");
            _connectionStringAppData = $@"Data Source=(LocalDB)\MSSQLLocalDB;
                                        AttachDbFilename={_caminhoAppData};
                                        Integrated Security=True;";



            if (!File.Exists(_caminhoAppData))
                CriarAppData();

            SelecionarOuCriarBancoAtual();

            using (SqlConnection conexao = new(_connectionStringAtual))
            {
                conexao.Open();
            }
        }

        #region Criação AppData (Mantida)
        private void CriarAppData()
        {
            string masterConnection = @"Data Source=(LocalDB)\MSSQLLocalDB;Integrated Security=True;";
            using (SqlConnection conexao = new(masterConnection))
            {
                conexao.Open();

                string sqlCreate = $@"
                    CREATE DATABASE [AppData]
                    ON PRIMARY (
                        NAME = N'AppData',
                        FILENAME = '{_caminhoAppData}'
                    )
                    LOG ON (
                        NAME = N'AppData_log',
                        FILENAME = '{Path.ChangeExtension(_caminhoAppData, ".ldf")}'
                    );";

                using (SqlCommand cmd = new(sqlCreate, conexao))
                    cmd.ExecuteNonQuery();
            }

            CriarTabelasAppData();
        }

        private void CriarTabelasAppData()
        {
            using (SqlConnection conexao = new(_connectionStringAppData))
            {
                conexao.Open();

                string sql = @"
                    IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Databases' AND xtype='U')
                        CREATE TABLE Databases (
                            DatabaseId INT IDENTITY(1,1) PRIMARY KEY,
                            NomeBanco NVARCHAR(100) NOT NULL,
                            DataCriacao DATETIME NOT NULL
                        );

                    IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='ChavesFotos' AND xtype='U')
                        CREATE TABLE ChavesFotos (
                            ChaveId INT IDENTITY(1,1) PRIMARY KEY,
                            DatabaseId INT NOT NULL,
                            FotoId INT NOT NULL,
                            ChaveCriptografada VARBINARY(MAX) NOT NULL,
                            FOREIGN KEY(DatabaseId) REFERENCES Databases(DatabaseId)
                        );

                    IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Logs' AND xtype='U')
                            CREATE TABLE Logs (
                                LogId INT IDENTITY(1,1) PRIMARY KEY,
                                DatabaseId INT NOT NULL,
                                DataHora DATETIME NOT NULL,
                                Usuario NVARCHAR(100),
                                Acao NVARCHAR(100),
                                Detalhes NVARCHAR(MAX),
                                FOREIGN KEY(DatabaseId) REFERENCES Databases(DatabaseId)
                        );

                    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Databases') AND name = 'PinEmergencia')
                        ALTER TABLE Databases ADD PinEmergencia NVARCHAR(64) NULL;";

                using (SqlCommand cmd = new(sql, conexao))
                    cmd.ExecuteNonQuery();
            }
        }

        #endregion

        #region Logs

        public void RegistrarLog(string usuario, string acao, string detalhes)
        {
            try
            {
                using (SqlConnection conexao = new(_connectionStringAppData))
                {
                    conexao.Open();
                    string sql = @"INSERT INTO Logs (DatabaseId, DataHora, Usuario, Acao, Detalhes) 
                         VALUES ((SELECT TOP 1 DatabaseId FROM Databases ORDER BY DatabaseId DESC), @DataHora, @Usuario, @Acao, @Detalhes)";

                    using (SqlCommand cmd = new(sql, conexao))
                    {
                        cmd.Parameters.AddWithValue("@DataHora", DateTime.Now);
                        cmd.Parameters.AddWithValue("@Usuario", usuario ?? "Sistema");
                        cmd.Parameters.AddWithValue("@Acao", acao);
                        cmd.Parameters.AddWithValue("@Detalhes", detalhes);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch
            { }
        }

        public DataTable ObterLogs()
        {
            DataTable dt = new DataTable();

            string sql = @"SELECT LogId, DataHora, Usuario, Acao, Detalhes FROM Logs WHERE DatabaseId = 
                        (SELECT TOP 1 DatabaseId FROM Databases ORDER BY DatabaseId DESC) ORDER BY DataHora DESC 
                         ";

            using (SqlConnection conn = new(_connectionStringAppData))
            {
                SqlDataAdapter adapter = new(sql, conn);
                adapter.Fill(dt);
            }
            return dt;
        }

        #endregion

        #region Banco Dinâmico Atual (Mantida)
        private void SelecionarOuCriarBancoAtual()
        {
            int? ultimoDbId = null;
            string nomeUltimoDb = null;

            using (SqlConnection conexao = new(_connectionStringAppData))
            {
                conexao.Open();
                string sql = "SELECT TOP 1 DatabaseId, NomeBanco FROM Databases ORDER BY DatabaseId DESC";
                using (SqlCommand cmd = new(sql, conexao))
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        ultimoDbId = reader.GetInt32(0);
                        nomeUltimoDb = reader.GetString(1);
                    }
                }
            }

            if (ultimoDbId.HasValue)
            {
                string caminhoBanco = Path.Combine(PastaVaultFace, nomeUltimoDb + ".mdf");

                if (File.Exists(caminhoBanco))
                {
                    _caminhoBancoAtual = caminhoBanco;
                    _connectionStringAtual = $@"Data Source=(LocalDB)\MSSQLLocalDB;
                                         AttachDbFilename={_caminhoBancoAtual};
                                         Integrated Security=True;
                                         MultipleActiveResultSets=True;";
                    CriarTabelasBancoAtual();
                    return;
                }
            }

            CriarBancoDinamico();
        }

        private void CriarBancoDinamico()
        {
            string nomeBanco = "Rostos_" + Guid.NewGuid().ToString("N");
            _caminhoBancoAtual = Path.Combine(PastaVaultFace, nomeBanco + ".mdf");
            _connectionStringAtual = $@"Data Source=(LocalDB)\MSSQLLocalDB;
                                             AttachDbFilename={_caminhoBancoAtual};
                                             Integrated Security=True;";

            string masterConnection = @"Data Source=(LocalDB)\MSSQLLocalDB;Integrated Security=True;";
            using (SqlConnection conexao = new(masterConnection))
            {
                conexao.Open();

                string sqlCreate = $@"
                    CREATE DATABASE [{nomeBanco}]
                    ON PRIMARY (
                        NAME = N'{nomeBanco}',
                        FILENAME = '{_caminhoBancoAtual}'
                    )
                    LOG ON (
                        NAME = N'{nomeBanco}_log',
                        FILENAME = '{Path.ChangeExtension(_caminhoBancoAtual, ".ldf")}'
                    );";

                using (SqlCommand cmd = new(sqlCreate, conexao))
                    cmd.ExecuteNonQuery();
            }

            CriarTabelasBancoAtual();
            RegistrarBancoNoAppData(nomeBanco);
        }

        private void CriarTabelasBancoAtual()
        {
            using (SqlConnection conexao = new(_connectionStringAtual))
            {
                conexao.Open();

                string sql = @"
                    IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Usuarios' AND xtype='U')
                        CREATE TABLE Usuarios (
                            UsuarioId INT IDENTITY(1,1) PRIMARY KEY,
                            Nome NVARCHAR(100) NOT NULL,
                            DataCadastro DATETIME NOT NULL,
                            NivelPermissao INT NOT NULL DEFAULT 1,
                            Email NVARCHAR(200) NULL,
                            EmailConfirmado BIT NOT NULL DEFAULT 0
                        );

                    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Usuarios') AND name = 'Email')
                        ALTER TABLE Usuarios ADD Email NVARCHAR(200) NULL;

                    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Usuarios') AND name = 'EmailConfirmado')
                        ALTER TABLE Usuarios ADD EmailConfirmado BIT NOT NULL DEFAULT 0;

                    IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Rostos' AND xtype='U')
                        CREATE TABLE Rostos (
                            FotoId INT IDENTITY(1,1) PRIMARY KEY,
                            UsuarioId INT NOT NULL,
                            Rosto VARBINARY(MAX) NOT NULL,
                            DataCadastro DATETIME NOT NULL,
                            FOREIGN KEY(UsuarioId) REFERENCES Usuarios(UsuarioId)
                        );";

                using (SqlCommand cmd = new(sql, conexao))
                    cmd.ExecuteNonQuery();
            }
        }

        private void RegistrarBancoNoAppData(string nomeBanco)
        {
            using (SqlConnection conexao = new(_connectionStringAppData))
            {
                conexao.Open();
                string sql = "INSERT INTO Databases (NomeBanco, DataCriacao) VALUES (@NomeBanco, @DataCadastro)";
                using (SqlCommand cmd = new(sql, conexao))
                {
                    cmd.Parameters.AddWithValue("@NomeBanco", nomeBanco);
                    cmd.Parameters.AddWithValue("@DataCadastro", DateTime.Now);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        #endregion

        #region Operações com Fotos e Usuários

        public int ContarUsuarios()
        {
            using (SqlConnection conexao = new(_connectionStringAtual))
            {
                conexao.Open();
                string sql = "SELECT COUNT(*) FROM Usuarios";
                using (SqlCommand cmd = new(sql, conexao))
                {
                    return Convert.ToInt32(cmd.ExecuteScalar());
                }
            }
        }

        public Usuario ObterUsuarioPorNome(string nome)
        {
            using (SqlConnection conexao = new(_connectionStringAtual))
            {
                conexao.Open();
                string sql = "SELECT UsuarioId, NivelPermissao FROM Usuarios WHERE Nome = @Nome";
                using (SqlCommand cmd = new(sql, conexao))
                {
                    cmd.Parameters.AddWithValue("@Nome", nome);
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            int userId = reader.GetInt32(0);
                            NivelPermissao permissao = (NivelPermissao)reader.GetInt32(1);

                            return new Usuario
                            {
                                Id = userId,
                                Nome = nome,
                                Permissao = permissao,
                                Rostos = new List<byte[]>()
                            };
                        }
                    }
                }
            }
            return null;
        }

        public int CriarUsuario(string nome, NivelPermissao permissao = NivelPermissao.Basico)
        {
            int contagemUsuarios = ContarUsuarios();
            NivelPermissao nivelFinal = (contagemUsuarios == 0) ? NivelPermissao.AdminSupremo : permissao;

            using (SqlConnection conexao = new(_connectionStringAtual))
            {
                conexao.Open();
                string sql = "INSERT INTO Usuarios (Nome, DataCadastro, NivelPermissao) VALUES (@Nome, @DataCadastro, @Permissao); SELECT SCOPE_IDENTITY();";
                using (SqlCommand cmd = new(sql, conexao))
                {
                    cmd.Parameters.AddWithValue("@Nome", nome);
                    cmd.Parameters.AddWithValue("@DataCadastro", DateTime.Now);
                    cmd.Parameters.AddWithValue("@Permissao", (int)nivelFinal);

                    int novoId = Convert.ToInt32(cmd.ExecuteScalar());

                    // LOG: Registro de novo usuário
                    RegistrarLog("Sistema", "Criação de Usuário", $"Usuário '{nome}' criado com ID {novoId}. Nível: {nivelFinal}");

                    return novoId;
                }
            }
        }
        public void InserirRosto(int usuarioId, byte[] imagemBytes)
        {
            byte[] chaveAleatoria = new byte[32];
            using (var rng = RandomNumberGenerator.Create())
                rng.GetBytes(chaveAleatoria);

            byte[] imagemCripto = CriptografarComChave(imagemBytes, chaveAleatoria);

            int fotoId;
            using (SqlConnection conexao = new(_connectionStringAtual))
            {
                conexao.Open();
                string sqlFoto = "INSERT INTO Rostos (UsuarioId, Rosto, DataCadastro) VALUES (@UsuarioId, @Rosto, @DataCadastro); SELECT SCOPE_IDENTITY();";
                using (SqlCommand cmd = new(sqlFoto, conexao))
                {
                    cmd.Parameters.AddWithValue("@UsuarioId", usuarioId);
                    cmd.Parameters.AddWithValue("@Rosto", imagemCripto);
                    cmd.Parameters.AddWithValue("@DataCadastro", DateTime.Now);
                    fotoId = Convert.ToInt32(cmd.ExecuteScalar());
                }
            }

            byte[] chaveCripto = CriptografarComChaveFixa(chaveAleatoria);
            using (SqlConnection conexao = new(_connectionStringAppData))
            {
                conexao.Open();
                string sql = "INSERT INTO ChavesFotos (DatabaseId, FotoId, ChaveCriptografada) VALUES ((SELECT TOP 1 DatabaseId FROM Databases ORDER BY DatabaseId DESC), @FotoId, @ChaveCriptografada)";
                using (SqlCommand cmd = new(sql, conexao))
                {
                    cmd.Parameters.AddWithValue("@FotoId", fotoId);
                    cmd.Parameters.AddWithValue("@ChaveCriptografada", chaveCripto);
                    cmd.ExecuteNonQuery();
                }
            }

            // LOG: Registro de nova face
            RegistrarLog("Sistema", "Cadastro Facial", $"Nova biometria (ID: {fotoId}) adicionada para o usuário ID: {usuarioId}");
        }

        public void InserirUsuarioComRosto(string nome, byte[] imagemBytes, NivelPermissao permissao = NivelPermissao.Basico)
        {
            int usuarioId = ObterUsuarioIdPorNome(nome);

            if (usuarioId == 0)
                usuarioId = CriarUsuario(nome, permissao);


            byte[] chaveAleatoria = new byte[32];
            using (var rng = RandomNumberGenerator.Create())
                rng.GetBytes(chaveAleatoria);

            byte[] imagemCripto = CriptografarComChave(imagemBytes, chaveAleatoria);

            int fotoId;
            using (SqlConnection conexao = new(_connectionStringAtual))
            {
                conexao.Open();
                string sqlFoto = "INSERT INTO Rostos (UsuarioId, Rosto, DataCadastro) VALUES (@UsuarioId, @Rosto, @DataCadastro); SELECT SCOPE_IDENTITY();";
                using (SqlCommand cmd = new(sqlFoto, conexao))
                {
                    cmd.Parameters.AddWithValue("@UsuarioId", usuarioId);
                    cmd.Parameters.AddWithValue("@Rosto", imagemCripto);
                    cmd.Parameters.AddWithValue("@DataCadastro", DateTime.Now);
                    fotoId = Convert.ToInt32(cmd.ExecuteScalar());
                }
            }

            byte[] chaveCripto = CriptografarComChaveFixa(chaveAleatoria);
            using (SqlConnection conexao = new(_connectionStringAppData))
            {
                conexao.Open();
                string sql = "INSERT INTO ChavesFotos (DatabaseId, FotoId, ChaveCriptografada) VALUES ((SELECT TOP 1 DatabaseId FROM Databases ORDER BY DatabaseId DESC), @FotoId, @ChaveCriptografada)";
                using (SqlCommand cmd = new(sql, conexao))
                {
                    cmd.Parameters.AddWithValue("@FotoId", fotoId);
                    cmd.Parameters.AddWithValue("@ChaveCriptografada", chaveCripto);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public int ObterUsuarioIdPorNome(string nome)
        {
            using (SqlConnection conexao = new(_connectionStringAtual))
            {
                conexao.Open();
                string sql = "SELECT UsuarioId FROM Usuarios WHERE Nome = @Nome";
                using (SqlCommand cmd = new(sql, conexao))
                {
                    cmd.Parameters.AddWithValue("@Nome", nome);
                    var resultado = cmd.ExecuteScalar();
                    return resultado != null ? Convert.ToInt32(resultado) : 0;
                }
            }
        }
        public byte[] ObterRostoPorId(int fotoId)
        {
            byte[] chaveAleatoria;
            using (SqlConnection conexao = new(_connectionStringAppData))
            {
                conexao.Open();
                string sql = "SELECT TOP 1 ChaveCriptografada FROM ChavesFotos WHERE FotoId = @FotoId ORDER BY ChaveId DESC";
                using (SqlCommand cmd = new(sql, conexao))
                {
                    cmd.Parameters.AddWithValue("@FotoId", fotoId);
                    var resultado = cmd.ExecuteScalar();
                    if (resultado == null || resultado == DBNull.Value)
                        return null;
                    chaveAleatoria = DescriptografarComChaveFixa((byte[])resultado);
                }
            }

            using (SqlConnection conexao = new(_connectionStringAtual))
            {
                conexao.Open();
                string sql = "SELECT Rosto FROM Rostos WHERE FotoId = @FotoId";
                using (SqlCommand cmd = new(sql, conexao))
                {
                    cmd.Parameters.AddWithValue("@FotoId", fotoId);
                    var resultado = cmd.ExecuteScalar();
                    if (resultado != null && resultado != DBNull.Value)
                        return DescriptografarComChave((byte[])resultado, chaveAleatoria);
                }
            }

            return null;
        }

        public List<Usuario> ObterUsuariosComRostos()
        {
            List<Usuario> usuarios = new List<Usuario>();

            using (SqlConnection conexao2 = new(_connectionStringAtual))
            {
                conexao2.Open();
                string sqlUsuarios = "SELECT UsuarioId, Nome, NivelPermissao FROM Usuarios";
                using (SqlCommand cmdUsuarios = new(sqlUsuarios, conexao2))
                using (SqlDataReader reader = cmdUsuarios.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        int userId = reader.GetInt32(0);
                        string nome = reader.GetString(1);
                        NivelPermissao permissao = (NivelPermissao)reader.GetInt32(2);

                        Usuario usuario = new Usuario { Id = userId, Nome = nome, Permissao = permissao };
                        string sqlRostos = "SELECT FotoId, Rosto FROM Rostos WHERE UsuarioId = @UsuarioId";
                        using (SqlCommand cmdRostos = new SqlCommand(sqlRostos, conexao2))
                        {
                            cmdRostos.Parameters.AddWithValue("@UsuarioId", userId);
                            using (SqlDataReader readerRostos = cmdRostos.ExecuteReader())
                            {
                                while (readerRostos.Read())
                                {
                                    int fotoId = readerRostos.GetInt32(0);
                                    byte[] rostoCripto = (byte[])readerRostos["Rosto"];
                                    byte[] chaveAleatoria;

                                    using (SqlConnection conexaoApp = new(_connectionStringAppData))
                                    {
                                        conexaoApp.Open();
                                        string sqlChave = "SELECT TOP 1 ChaveCriptografada FROM ChavesFotos WHERE FotoId = @FotoId ORDER BY ChaveId DESC";
                                        using (SqlCommand cmdChave = new SqlCommand(sqlChave, conexaoApp))
                                        {
                                            cmdChave.Parameters.AddWithValue("@FotoId", fotoId);
                                            var resultado = cmdChave.ExecuteScalar();
                                            if (resultado == null || resultado == DBNull.Value)
                                                continue;
                                            chaveAleatoria = DescriptografarComChaveFixa((byte[])resultado);
                                        }
                                    }

                                    usuario.Rostos.Add(DescriptografarComChave(rostoCripto, chaveAleatoria));
                                }
                            }
                        }
                        usuarios.Add(usuario);
                    }
                }
            }

            return usuarios;
        }

        #endregion

        #region Criptografia

        private byte[] CriptografarComChave(byte[] dados, byte[] chave)
        {
            using (var aes = Aes.Create())
            {
                aes.Key = chave;
                aes.IV = new byte[16];
                using (var ms = new MemoryStream())
                using (var cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
                {
                    cs.Write(dados, 0, dados.Length);
                    cs.FlushFinalBlock();
                    return ms.ToArray();
                }
            }
        }

        private byte[] DescriptografarComChave(byte[] dados, byte[] chave)
        {
            using (var aes = Aes.Create())
            {
                aes.Key = chave;
                aes.IV = new byte[16];
                using (var ms = new MemoryStream())
                using (var cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Write))
                {
                    cs.Write(dados, 0, dados.Length);
                    cs.FlushFinalBlock();
                    return ms.ToArray();
                }
            }
        }

        [SupportedOSPlatform("windows")]
        private byte[] CriptografarComChaveFixa(byte[] dados)
        {
            using (var aes = Aes.Create())
            {
                aes.Key = ObterChaveMestra();
                aes.IV = ivFixa;
                using (var ms = new MemoryStream())
                using (var cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
                {
                    cs.Write(dados, 0, dados.Length);
                    cs.FlushFinalBlock();
                    return ms.ToArray();
                }
            }
        }

        [SupportedOSPlatform("windows")]
        private byte[] DescriptografarComChaveFixa(byte[] dados)
        {
            using (var aes = Aes.Create())
            {
                aes.Key = ObterChaveMestra();
                aes.IV = ivFixa;
                using (var ms = new MemoryStream())
                using (var cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Write))
                {
                    cs.Write(dados, 0, dados.Length);
                    cs.FlushFinalBlock();
                    return ms.ToArray();
                }
            }
        }

        #endregion

        #region Gestão de usuarios
        public void AtualizarPermissao(int usuarioId, NivelPermissao novaPermissao)
        {
            using (SqlConnection conexao = new(_connectionStringAtual))
            {
                conexao.Open();
                string sql = "UPDATE Usuarios SET NivelPermissao = @Permissao WHERE UsuarioId = @UsuarioId";
                using (SqlCommand cmd = new(sql, conexao))
                {
                    cmd.Parameters.AddWithValue("@Permissao", (int)novaPermissao);
                    cmd.Parameters.AddWithValue("@UsuarioId", usuarioId);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public void AtualizarEmail(int usuarioId, string email, bool confirmado = false)
        {
            using (SqlConnection conexao = new(_connectionStringAtual))
            {
                conexao.Open();
                string sql = "UPDATE Usuarios SET Email = @Email, EmailConfirmado = @EmailConfirmado WHERE UsuarioId = @UsuarioId";
                using (SqlCommand cmd = new(sql, conexao))
                {
                    cmd.Parameters.AddWithValue("@Email", (object?)email ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@EmailConfirmado", confirmado);
                    cmd.Parameters.AddWithValue("@UsuarioId", usuarioId);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public (string? Email, bool EmailConfirmado) ObterEmailUserMaster()
        {
            using (SqlConnection conexao = new(_connectionStringAtual))
            {
                conexao.Open();
                string sql = "SELECT TOP 1 Email, EmailConfirmado FROM Usuarios WHERE NivelPermissao = 3";
                using (SqlCommand cmd = new(sql, conexao))
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        string? email = reader.IsDBNull(0) ? null : reader.GetString(0);
                        bool confirmado = reader.GetBoolean(1);
                        return (email, confirmado);
                    }
                }
            }
            return (null, false);
        }


        // Remove todos os utilizadores e biometrias, mantendo apenas o user master
        public void ResetarSistema(string nomeMaster)
        {
            // cria um banco novo — o sistema vai usá-lo automaticamente na próxima sessão
            CriarBancoDinamico();
            RegistrarLog("Sistema", "Reset Total", $"Sistema resetado por {nomeMaster}. Novo banco criado.");
        }

        // Transfere o título AdminSupremo para outro utilizador e rebaixa o master atual para Admin
        public void TransferirAdminMaster(int idMasterAtual, int idNovoMaster)
        {
            using SqlConnection cx = new(_connectionStringAtual);
            cx.Open();

            using (SqlCommand cmd = new("UPDATE Usuarios SET NivelPermissao = @P WHERE UsuarioId = @Id", cx))
            {
                cmd.Parameters.AddWithValue("@P", (int)NivelPermissao.AdminComum);
                cmd.Parameters.AddWithValue("@Id", idMasterAtual);
                cmd.ExecuteNonQuery();
            }

            using (SqlCommand cmd = new("UPDATE Usuarios SET NivelPermissao = @P WHERE UsuarioId = @Id", cx))
            {
                cmd.Parameters.AddWithValue("@P", (int)NivelPermissao.AdminSupremo);
                cmd.Parameters.AddWithValue("@Id", idNovoMaster);
                cmd.ExecuteNonQuery();
            }

            RegistrarLog("Sistema", "Transferência Master", $"Master transferido de ID {idMasterAtual} para ID {idNovoMaster}.");
        }

        public void RemoverUsuario(int usuarioId)
        {
            using (SqlConnection conexaoAtual = new(_connectionStringAtual))
            using (SqlConnection conexaoApp = new(_connectionStringAppData))
            {
                conexaoAtual.Open();
                conexaoApp.Open();

                List<int> fotoIds = new List<int>();
                string sqlRostos = "SELECT FotoId FROM Rostos WHERE UsuarioId = @UsuarioId";
                using (SqlCommand cmdRostos = new(sqlRostos, conexaoAtual))
                {
                    cmdRostos.Parameters.AddWithValue("@UsuarioId", usuarioId);
                    using (SqlDataReader reader = cmdRostos.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            fotoIds.Add(reader.GetInt32(0));
                        }
                    }
                }

                if (fotoIds.Count > 0)
                {
                    string fotoIdList = string.Join(",", fotoIds);
                    string sqlChaves = $"DELETE FROM ChavesFotos WHERE FotoId IN ({fotoIdList})";
                    using (SqlCommand cmdChaves = new(sqlChaves, conexaoApp))
                    {
                        cmdChaves.ExecuteNonQuery();
                    }
                }

                string sqlDeleteRostos = "DELETE FROM Rostos WHERE UsuarioId = @UsuarioId";
                using (SqlCommand cmdDeleteRostos = new(sqlDeleteRostos, conexaoAtual))
                {
                    cmdDeleteRostos.Parameters.AddWithValue("@UsuarioId", usuarioId);
                    cmdDeleteRostos.ExecuteNonQuery();
                }

                string sqlDeleteUsuario = "DELETE FROM Usuarios WHERE UsuarioId = @UsuarioId";
                using (SqlCommand cmdDeleteUsuario = new(sqlDeleteUsuario, conexaoAtual))
                {
                    cmdDeleteUsuario.Parameters.AddWithValue("@UsuarioId", usuarioId);
                    cmdDeleteUsuario.ExecuteNonQuery();
                }
            }

            // LOG: Registro de exclusão
            RegistrarLog("Sistema", "Exclusão", $"Usuário ID {usuarioId} e todos os seus dados vinculados foram removidos.");
        }

        #endregion

        #region PIN de Emergência

        // Hash SHA-256 do PIN — nunca guardar em plain text
        private static string HashPin(string pin)
        {
            using var sha = System.Security.Cryptography.SHA256.Create();
            var bytes = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(pin.Trim()));
            return Convert.ToHexString(bytes).ToLower();
        }

        // Define ou atualiza o PIN de emergência (guarda o hash)
        public void DefinirPinEmergencia(string pin)
        {
            string hash = HashPin(pin);
            string sql = @"UPDATE Databases SET PinEmergencia = @Hash
                           WHERE DatabaseId = (SELECT TOP 1 DatabaseId FROM Databases ORDER BY DatabaseId DESC)";
            using SqlConnection conn = new(_connectionStringAppData);
            conn.Open();
            using SqlCommand cmd = new(sql, conn);
            cmd.Parameters.AddWithValue("@Hash", hash);
            cmd.ExecuteNonQuery();
        }

        // Verifica o PIN introduzido — devolve true se correto
        public bool VerificarPinEmergencia(string pin)
        {
            string hashIntroduzido = HashPin(pin);
            string sql = @"SELECT PinEmergencia FROM Databases
                           WHERE DatabaseId = (SELECT TOP 1 DatabaseId FROM Databases ORDER BY DatabaseId DESC)";
            using SqlConnection conn = new(_connectionStringAppData);
            conn.Open();
            using SqlCommand cmd = new(sql, conn);
            var resultado = cmd.ExecuteScalar()?.ToString();
            return !string.IsNullOrEmpty(resultado) && resultado == hashIntroduzido;
        }

        // Verifica se já existe um PIN definido
        public bool ExistePinEmergencia()
        {
            // Verificação segura: a coluna pode ainda não existir na BD atual
            try
            {
                string sql = @"SELECT PinEmergencia FROM Databases
                               WHERE DatabaseId = (SELECT TOP 1 DatabaseId FROM Databases ORDER BY DatabaseId DESC)";
                using SqlConnection conn = new(_connectionStringAppData);
                conn.Open();
                using SqlCommand cmd = new(sql, conn);
                var resultado = cmd.ExecuteScalar()?.ToString();
                return !string.IsNullOrEmpty(resultado);
            }
            catch { return false; }
        }

        #endregion

        #region Relatórios

        // ── Método auxiliar privado — evita repetição de código nas queries ──────
        private T ExecutarScalarApp<T>(string sql, DateTime inicio, DateTime fim)
        {
            using SqlConnection conn = new(_connectionStringAppData);
            conn.Open();
            using SqlCommand cmd = new(sql, conn);
            cmd.Parameters.AddWithValue("@i", inicio);
            cmd.Parameters.AddWithValue("@f", fim);
            var r = cmd.ExecuteScalar();
            return (r != null && r != DBNull.Value) ? (T)Convert.ChangeType(r, typeof(T)) : default;
        }

        // Devolve todos os logs do período, do mais recente para o mais antigo.
        // Usado pela tabela de eventos na página de relatórios.
        public DataTable ObterLogsFiltrados(DateTime inicio, DateTime fim)
        {
            DataTable dt = new DataTable();
            string sql = @"SELECT LogId, DataHora, Usuario, Acao, Detalhes
                           FROM Logs
                           WHERE DatabaseId = (SELECT TOP 1 DatabaseId FROM Databases ORDER BY DatabaseId DESC)
                           AND DataHora BETWEEN @Inicio AND @Fim
                           ORDER BY DataHora DESC";
            using (SqlConnection conn = new(_connectionStringAppData))
            {
                SqlCommand cmd = new(sql, conn);
                cmd.Parameters.AddWithValue("@Inicio", inicio);
                cmd.Parameters.AddWithValue("@Fim", fim);
                new SqlDataAdapter(cmd).Fill(dt);
            }
            return dt;
        }

        // Devolve os 5 KPIs calculados para o período.
        // Usa os nomes de Acao exatos já gravados pela aplicação.
        public EstatisticasRelatorio ObterEstatisticasPeriodo(DateTime inicio, DateTime fim)
        {
            var s = new EstatisticasRelatorio();

            s.TotalAcessos = ExecutarScalarApp<int>(
                @"SELECT COUNT(*) FROM Logs
                  WHERE DatabaseId=(SELECT TOP 1 DatabaseId FROM Databases ORDER BY DatabaseId DESC)
                  AND Acao='Login' AND DataHora BETWEEN @i AND @f",
                inicio, fim);

            s.TentativasFalhadas = ExecutarScalarApp<int>(
                @"SELECT COUNT(*) FROM Logs
                  WHERE DatabaseId=(SELECT TOP 1 DatabaseId FROM Databases ORDER BY DatabaseId DESC)
                  AND Acao='Acesso Negado' AND DataHora BETWEEN @i AND @f",
                inicio, fim);

            s.UtilizadorMaisAtivo = ExecutarScalarApp<string>(
                @"SELECT TOP 1 Usuario FROM Logs
                  WHERE DatabaseId=(SELECT TOP 1 DatabaseId FROM Databases ORDER BY DatabaseId DESC)
                  AND Acao='Login' AND DataHora BETWEEN @i AND @f
                  GROUP BY Usuario ORDER BY COUNT(*) DESC",
                inicio, fim) ?? "—";

            s.HoraDePico = ExecutarScalarApp<int>(
                @"SELECT TOP 1 DATEPART(HOUR,DataHora) FROM Logs
                  WHERE DatabaseId=(SELECT TOP 1 DatabaseId FROM Databases ORDER BY DatabaseId DESC)
                  AND Acao='Login' AND DataHora BETWEEN @i AND @f
                  GROUP BY DATEPART(HOUR,DataHora) ORDER BY COUNT(*) DESC",
                inicio, fim);

            s.TotalCadastros = ExecutarScalarApp<int>(
                @"SELECT COUNT(*) FROM Logs
                  WHERE DatabaseId=(SELECT TOP 1 DatabaseId FROM Databases ORDER BY DatabaseId DESC)
                  AND Acao='Criação de Usuário' AND DataHora BETWEEN @i AND @f",
                inicio, fim);

            return s;
        }

        // Dia 17: Devolve os últimos N eventos do utilizador (para o painel UserLogado).
        public DataTable ObterHistoricoUtilizador(string nomeUtilizador, int limite = 10)
        {
            DataTable dt = new DataTable();
            string sql = @"SELECT TOP (@Limite) DataHora, Acao, Detalhes
                           FROM Logs
                           WHERE DatabaseId = (SELECT TOP 1 DatabaseId FROM Databases ORDER BY DatabaseId DESC)
                           AND Usuario = @Nome
                           ORDER BY DataHora DESC";
            using SqlConnection conn = new(_connectionStringAppData);
            SqlCommand cmd = new(sql, conn);
            cmd.Parameters.AddWithValue("@Limite", limite);
            cmd.Parameters.AddWithValue("@Nome", nomeUtilizador);
            new SqlDataAdapter(cmd).Fill(dt);
            return dt;
        }

        // Devolve lista de (Dia, Contagem) para o gráfico de barras.
        // Apenas conta logins bem-sucedidos agrupados por dia.
        public List<(DateTime Dia, int Total)> ObterAcessosPorDia(DateTime inicio, DateTime fim)
        {
            var resultado = new List<(DateTime, int)>();
            string sql = @"SELECT CAST(DataHora AS DATE) as Dia, COUNT(*) as Total
                           FROM Logs
                           WHERE DatabaseId=(SELECT TOP 1 DatabaseId FROM Databases ORDER BY DatabaseId DESC)
                           AND Acao='Login' AND DataHora BETWEEN @i AND @f
                           GROUP BY CAST(DataHora AS DATE)
                           ORDER BY Dia";
            using SqlConnection conn = new(_connectionStringAppData);
            conn.Open();
            using SqlCommand cmd = new(sql, conn);
            cmd.Parameters.AddWithValue("@i", inicio);
            cmd.Parameters.AddWithValue("@f", fim);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                resultado.Add((reader.GetDateTime(0), reader.GetInt32(1)));
            return resultado;
        }

        #endregion
    }
}
