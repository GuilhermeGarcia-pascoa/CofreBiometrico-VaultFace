using System.Net;
using System.Net.Mail;

namespace PapAtualizacaoBeleza
{
    public class EmailService
    {
        private readonly string _smtpHost;
        private readonly int _smtpPort;
        private readonly string _smtpUser;
        private readonly string _smtpPass;
        private readonly string _remetente;
        private readonly string _nomeRemetente;

        public EmailService(IConfiguration config)
        {
            _smtpHost = config["Email:SmtpHost"] ?? "smtp.gmail.com";
            _smtpPort = int.Parse(config["Email:SmtpPort"] ?? "587");
            _smtpUser = config["Email:SmtpUser"] ?? "";
            _smtpPass = config["Email:SmtpPass"] ?? "";
            _remetente = config["Email:Remetente"] ?? _smtpUser;
            _nomeRemetente = config["Email:NomeRemetente"] ?? "VaultFace";
        }
        // API pública
        // Gera e envia um código de verificação de 6 dígitos.
        // Devolve o código para ser comparado na UI.
        public async Task<string> EnviarCodigoVerificacaoAsync(string emailDestino, string nomeUtilizador)
        {
            string codigo = GerarCodigo6Digitos();
            await EnviarEmailAsync(
                emailDestino,
                nomeUtilizador,
                "O seu código de acesso VaultFace",
                CorpoCodigoVerificacao(nomeUtilizador, codigo)
            );
            return codigo;
        }

        // Envia o email de boas-vindas após registo completo.
        public async Task EnviarConfirmacaoUserMasterAsync(string emailDestino, string nomeUtilizador)
        {
            await EnviarEmailAsync(
                emailDestino,
                nomeUtilizador,
                "Conta Master ativada — VaultFace",
                CorpoConfirmacao(nomeUtilizador)
            );
        }

        // Envio SMTP

        private async Task EnviarEmailAsync(string emailDestino, string nomeDestinatario,
                                            string assunto, string corpo)
        {
            using var msg = new MailMessage
            {
                From = new MailAddress(_remetente, _nomeRemetente),
                Subject = assunto,
                Body = corpo,
                IsBodyHtml = true,
            };
            msg.To.Add(new MailAddress(emailDestino, nomeDestinatario));

            using var smtp = new SmtpClient(_smtpHost, _smtpPort)
            {
                Credentials = new NetworkCredential(_smtpUser, _smtpPass),
                EnableSsl = true,
                DeliveryMethod = SmtpDeliveryMethod.Network,
            };
            await smtp.SendMailAsync(msg);
        }

        // Geração do código
        private static string GerarCodigo6Digitos()
        {
            using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
            byte[] bytes = new byte[4];
            rng.GetBytes(bytes);
            uint val = BitConverter.ToUInt32(bytes, 0) % 1_000_000;
            return val.ToString("D6");
        }

        private const string FontFamily = "font-family:'Segoe UI',Helvetica,Arial,sans-serif;";
        private const string CardShadow = "box-shadow:0 1px 4px rgba(0,0,0,0.08),0 4px 24px rgba(0,0,0,0.06);";
        private const string BorderColor = "#e2e8f0";
        private const string TextPrimary = "#0f172a";
        private const string TextMuted = "#64748b";
        private const string TextDim = "#94a3b8";
        private const string AccentBlue = "#1a56db";
        private const string BgPage = "#f4f6f9";
        private const string BgCard = "#ffffff";
        private const string BgSubtle = "#f8fafc";

        private static string CorpoCodigoVerificacao(string nome, string codigo)
        {
            string bloco1 = codigo[..3];
            string bloco2 = codigo[3..];
            string ano = DateTime.Now.Year.ToString();

            // Linha divisória reutilizável
            string divider = $"<tr><td style=\"border-bottom:1px solid {BorderColor};font-size:0;line-height:0;\">&nbsp;</td></tr>";

            return
                "<!DOCTYPE html>" +
                "<html lang=\"pt\">" +
                "<head>" +
                "<meta charset=\"UTF-8\" />" +
                "<meta name=\"viewport\" content=\"width=device-width,initial-scale=1.0\" />" +
                $"<title>Código de acesso VaultFace</title>" +
                "</head>" +
                $"<body style=\"margin:0;padding:0;background:{BgPage};{FontFamily}\">" +

                // Wrapper externo
                $"<table width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" style=\"background:{BgPage};\">" +
                "<tr><td align=\"center\" style=\"padding:48px 16px;\">" +

                // Card
                $"<table width=\"560\" cellpadding=\"0\" cellspacing=\"0\" style=\"background:{BgCard};border-radius:8px;{CardShadow}border-collapse:collapse;\">" +

                // Barra azul topo
                $"<tr><td style=\"background:{AccentBlue};border-radius:8px 8px 0 0;height:4px;font-size:0;line-height:0;\">&nbsp;</td></tr>" +

                // Cabeçalho
                $"<tr><td style=\"padding:32px 48px 24px;border-bottom:1px solid {BorderColor};\">" +
                "<table width=\"100%\" cellpadding=\"0\" cellspacing=\"0\"><tr>" +

                // Logo
                "<td style=\"vertical-align:middle;\">" +
                "<table cellpadding=\"0\" cellspacing=\"0\"><tr>" +
                $"<td style=\"background:{AccentBlue};border-radius:6px;width:32px;height:32px;text-align:center;vertical-align:middle;\">" +
                "<span style=\"color:#ffffff;font-size:16px;font-weight:700;line-height:32px;\">V</span></td>" +
                $"<td style=\"padding-left:10px;vertical-align:middle;\"><span style=\"font-size:17px;font-weight:700;color:{TextPrimary};letter-spacing:-0.3px;\">VaultFace</span></td>" +
                "</tr></table></td>" +

                // Subtítulo direita
                $"<td align=\"right\" style=\"vertical-align:middle;\"><span style=\"font-size:11px;color:{TextDim};font-weight:500;text-transform:uppercase;letter-spacing:0.8px;\">Verificação de identidade</span></td>" +

                "</tr></table></td></tr>" +

                // Corpo
                "<tr><td style=\"padding:40px 48px 36px;\">" +

                $"<p style=\"margin:0 0 4px;font-size:13px;color:{TextMuted};font-weight:500;\">Olá, {nome}</p>" +
                $"<h1 style=\"margin:0 0 20px;font-size:22px;font-weight:700;color:{TextPrimary};letter-spacing:-0.5px;line-height:1.3;\">O seu código de verificação</h1>" +
                $"<p style=\"margin:0 0 32px;font-size:15px;color:#475569;line-height:1.7;\">Utilize o código abaixo para confirmar o seu endereço de email e concluir o registo como <strong style=\"color:{TextPrimary};\">Administrador Master</strong> no sistema VaultFace.</p>" +

                // Bloco do código
                $"<table width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" style=\"margin-bottom:32px;\"><tr>" +
                $"<td align=\"center\" style=\"background:{BgSubtle};border:1px solid {BorderColor};border-radius:8px;padding:28px 24px;\">" +
                $"<p style=\"margin:0 0 12px;font-size:11px;font-weight:600;color:{TextDim};text-transform:uppercase;letter-spacing:1.2px;\">Código de acesso</p>" +
                $"<p style=\"margin:0;font-size:44px;font-weight:800;color:{AccentBlue};letter-spacing:8px;font-family:'Courier New',Courier,monospace;line-height:1;\">" +
                $"{bloco1}<span style=\"color:#cbd5e1;font-weight:200;font-size:36px;letter-spacing:0;margin:0 6px;vertical-align:middle;\">&#8211;</span>{bloco2}</p>" +
                $"<p style=\"margin:14px 0 0;font-size:12px;color:{TextDim};\">Válido durante <strong style=\"color:{TextMuted};\">10 minutos</strong></p>" +
                "</td></tr></table>" +

                // Aviso segurança
                $"<table width=\"100%\" cellpadding=\"0\" cellspacing=\"0\"><tr>" +
                $"<td style=\"border-left:3px solid {BorderColor};padding:12px 16px;\">" +
                $"<p style=\"margin:0;font-size:13px;color:{TextMuted};line-height:1.6;\">Se não foi você a iniciar este registo, pode ignorar este email com segurança. Ninguém da equipa VaultFace irá solicitar este código.</p>" +
                "</td></tr></table>" +

                "</td></tr>" +

                // Rodapé
                $"<tr><td style=\"padding:20px 48px 28px;border-top:1px solid {BorderColor};\">" +
                $"<p style=\"margin:0;font-size:11px;color:{TextDim};line-height:1.6;\">© {ano} VaultFace &nbsp;·&nbsp; Gerado automaticamente &nbsp;·&nbsp; Não responda a este email</p>" +
                "</td></tr>" +

                "</table>" + // fim card
                "</td></tr></table>" + // fim wrapper
                "</body></html>";
        }

        private static string CorpoConfirmacao(string nome)
        {
            string ano = DateTime.Now.Year.ToString();

            return
                "<!DOCTYPE html>" +
                "<html lang=\"pt\">" +
                "<head>" +
                "<meta charset=\"UTF-8\" />" +
                "<meta name=\"viewport\" content=\"width=device-width,initial-scale=1.0\" />" +
                "<title>Conta Master ativada — VaultFace</title>" +
                "</head>" +
                $"<body style=\"margin:0;padding:0;background:{BgPage};{FontFamily}\">" +

                $"<table width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" style=\"background:{BgPage};\">" +
                "<tr><td align=\"center\" style=\"padding:48px 16px;\">" +

                $"<table width=\"560\" cellpadding=\"0\" cellspacing=\"0\" style=\"background:{BgCard};border-radius:8px;{CardShadow}border-collapse:collapse;\">" +

                // Barra azul topo
                $"<tr><td style=\"background:{AccentBlue};border-radius:8px 8px 0 0;height:4px;font-size:0;line-height:0;\">&nbsp;</td></tr>" +

                // Cabeçalho
                $"<tr><td style=\"padding:32px 48px 24px;border-bottom:1px solid {BorderColor};\">" +
                "<table width=\"100%\" cellpadding=\"0\" cellspacing=\"0\"><tr>" +
                "<td style=\"vertical-align:middle;\">" +
                "<table cellpadding=\"0\" cellspacing=\"0\"><tr>" +
                $"<td style=\"background:{AccentBlue};border-radius:6px;width:32px;height:32px;text-align:center;vertical-align:middle;\">" +
                "<span style=\"color:#ffffff;font-size:16px;font-weight:700;line-height:32px;\">V</span></td>" +
                $"<td style=\"padding-left:10px;vertical-align:middle;\"><span style=\"font-size:17px;font-weight:700;color:{TextPrimary};letter-spacing:-0.3px;\">VaultFace</span></td>" +
                "</tr></table></td>" +
                $"<td align=\"right\" style=\"vertical-align:middle;\"><span style=\"font-size:11px;color:{TextDim};font-weight:500;text-transform:uppercase;letter-spacing:0.8px;\">Conta ativada</span></td>" +
                "</tr></table></td></tr>" +

                // Corpo
                "<tr><td style=\"padding:40px 48px 36px;\">" +

                // Ícone + nome
                "<table cellpadding=\"0\" cellspacing=\"0\" style=\"margin-bottom:28px;\"><tr>" +
                $"<td style=\"background:#eff6ff;border:1px solid #dbeafe;border-radius:50%;width:52px;height:52px;text-align:center;vertical-align:middle;\">" +
                $"<span style=\"color:{AccentBlue};font-size:22px;font-weight:700;line-height:52px;\">&#10003;</span></td>" +
                $"<td style=\"padding-left:16px;vertical-align:middle;\">" +
                $"<p style=\"margin:0;font-size:13px;color:{TextMuted};\">Registo concluído com sucesso</p>" +
                $"<p style=\"margin:3px 0 0;font-size:18px;font-weight:700;color:{TextPrimary};letter-spacing:-0.3px;\">Bem-vindo, {nome}</p>" +
                "</td></tr></table>" +

                $"<p style=\"margin:0 0 28px;font-size:15px;color:#475569;line-height:1.7;\">A sua conta <strong style=\"color:{TextPrimary};\">Administrador Master</strong> foi criada e verificada com sucesso. Tem agora acesso completo ao sistema VaultFace, incluindo gestão de utilizadores, biometria e configurações do cofre.</p>" +

                // Tabela de detalhes
                $"<table width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" style=\"background:{BgSubtle};border:1px solid {BorderColor};border-radius:8px;margin-bottom:28px;\">" +
                "<tr><td style=\"padding:16px 24px;border-bottom:1px solid #e2e8f0;\">" +
                "<table width=\"100%\" cellpadding=\"0\" cellspacing=\"0\"><tr>" +
                $"<td style=\"font-size:12px;font-weight:600;color:{TextDim};text-transform:uppercase;letter-spacing:0.8px;\">Nível de acesso</td>" +
                "<td align=\"right\"><span style=\"background:#dbeafe;color:#1d4ed8;font-size:12px;font-weight:600;padding:3px 12px;border-radius:20px;\">Admin Supremo</span></td>" +
                "</tr></table></td></tr>" +
                "<tr><td style=\"padding:16px 24px;\">" +
                "<table width=\"100%\" cellpadding=\"0\" cellspacing=\"0\"><tr>" +
                $"<td style=\"font-size:12px;font-weight:600;color:{TextDim};text-transform:uppercase;letter-spacing:0.8px;\">Permissões</td>" +
                $"<td align=\"right\" style=\"font-size:13px;color:#475569;font-weight:500;\">Gestão total do sistema</td>" +
                "</tr></table></td></tr>" +
                "</table>" +

                // Aviso
                $"<table width=\"100%\" cellpadding=\"0\" cellspacing=\"0\"><tr>" +
                $"<td style=\"border-left:3px solid {BorderColor};padding:12px 16px;\">" +
                $"<p style=\"margin:0;font-size:13px;color:{TextMuted};line-height:1.6;\">Se não reconhece esta atividade, contacte o administrador do sistema de imediato e altere as suas credenciais.</p>" +
                "</td></tr></table>" +

                "</td></tr>" +

                // Rodapé
                $"<tr><td style=\"padding:20px 48px 28px;border-top:1px solid {BorderColor};\">" +
                $"<p style=\"margin:0;font-size:11px;color:{TextDim};line-height:1.6;\">© {ano} VaultFace &nbsp;·&nbsp; Gerado automaticamente &nbsp;·&nbsp; Não responda a este email</p>" +
                "</td></tr>" +

                "</table>" +
                "</td></tr></table>" +
                "</body></html>";
        }
    }
}
