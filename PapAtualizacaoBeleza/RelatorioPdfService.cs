using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Data;

namespace PapAtualizacaoBeleza
{
    public class RelatorioPdfService
    {
        private readonly BaseSql _db;

        public RelatorioPdfService(BaseSql db)
        {
            _db = db;
            QuestPDF.Settings.License = LicenseType.Community;
        }

        public byte[] GerarRelatorio(DateTime inicio, DateTime fim)
        {
            var logs    = _db.ObterLogsFiltrados(inicio, fim);
            var stats   = _db.ObterEstatisticasPeriodo(inicio, fim);
            var grafico = _db.ObterAcessosPorDia(inicio, fim);

            return Document.Create(doc =>
            {
                doc.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(1.5f, Unit.Centimetre);
                    page.DefaultTextStyle(t => t.FontFamily("Arial").FontSize(10));

                    page.Header().Column(col =>
                    {
                        col.Item()
                           .Background("#1F3864").Padding(14)
                           .Row(row =>
                           {
                               row.RelativeItem().Column(inner =>
                               {
                                   inner.Item().Text("VaultFace — Relatório de Acessos")
                                        .FontSize(16).Bold().FontColor("#FFFFFF");
                                   inner.Item().Text($"Período: {inicio:dd/MM/yyyy} a {fim:dd/MM/yyyy}")
                                        .FontSize(9).FontColor("#AAC4E0");
                               });
                               row.ConstantItem(170).AlignRight().AlignMiddle()
                                  .Text($"Gerado em {DateTime.Now:dd/MM/yyyy HH:mm}")
                                  .FontSize(8).FontColor("#AAC4E0");
                           });
                        col.Item().Height(4).Background("#2E74B5");
                        col.Item().Height(10);
                    });

                    page.Content().Column(col =>
                    {
                        col.Spacing(14);

                        // KPIs
                        col.Item().Row(row =>
                        {
                            Kpi(row, "Total de Acessos",      stats.TotalAcessos.ToString(),      "#2E74B5");
                            Kpi(row, "Tentativas Falhadas",   stats.TentativasFalhadas.ToString(), "#DC2626");
                            Kpi(row, "Utilizador Mais Ativo", stats.UtilizadorMaisAtivo,           "#EA580C");
                            Kpi(row, "Hora de Pico",          $"{stats.HoraDePico}h",             "#7C3AED");
                            Kpi(row, "Novos Cadastros",       stats.TotalCadastros.ToString(),     "#16A34A");
                        });

                        // Gráfico
                        if (grafico.Any())
                        {
                            col.Item().Column(g =>
                            {
                                g.Item().Text("Acessos por Dia").Bold().FontSize(11).FontColor("#1F3864");
                                g.Item().Height(6);
                                int maxVal = Math.Max(grafico.Max(x => x.Total), 1);

                                g.Item().Table(t =>
                                {
                                    t.ColumnsDefinition(d => { d.ConstantColumn(48); d.RelativeColumn(); d.ConstantColumn(28); });
                                    foreach (var (dia, total) in grafico)
                                    {
                                        int len = (int)((double)total / maxVal * 60);
                                        string barra = new string('█', Math.Max(len, total > 0 ? 1 : 0));
                                        t.Cell().PaddingVertical(2).Text(dia.ToString("dd/MM")).FontSize(8).FontColor("#888888");
                                        t.Cell().PaddingVertical(2).Text(barra).FontSize(7).FontColor("#2E74B5");
                                        t.Cell().PaddingVertical(2).AlignRight().Text(total.ToString()).FontSize(8).Bold().FontColor("#2E74B5");
                                    }
                                });
                            });
                        }

                        // Tabela de logs
                        col.Item().Column(tbl =>
                        {
                            tbl.Item().Text($"Registo de Eventos  ({logs.Rows.Count} entradas)")
                               .Bold().FontSize(11).FontColor("#1F3864");
                            tbl.Item().Height(6);

                            if (logs.Rows.Count == 0)
                            {
                                tbl.Item().Border(1).BorderColor("#E5E9F2").Padding(12)
                                   .Text("Nenhum evento registado no período selecionado.")
                                   .Italic().FontColor("#888888");
                                return;
                            }

                            tbl.Item().Table(t =>
                            {
                                t.ColumnsDefinition(d =>
                                {
                                    d.ConstantColumn(90); d.ConstantColumn(95);
                                    d.ConstantColumn(95); d.RelativeColumn();
                                });

                                t.Header(h =>
                                {
                                    foreach (var title in new[] { "Data / Hora", "Utilizador", "Ação", "Detalhes" })
                                        h.Cell().Background("#1F3864").Padding(6)
                                         .Text(title).FontSize(9).Bold().FontColor("#FFFFFF");
                                });

                                bool alt = false;
                                foreach (DataRow row in logs.Rows)
                                {
                                    string acao = row["Acao"]?.ToString() ?? "";
                                    string bg   = alt ? "#F9FAFB" : "#FFFFFF";
                                    string corAcao  = acao switch { "Login" => "#16A34A", "Acesso Negado" => "#DC2626", "Criação de Usuário" => "#2563EB", _ => "#555555" };
                                    string corBorda = acao switch { "Login" => "#16A34A", "Acesso Negado" => "#DC2626", "Criação de Usuário" => "#2563EB", _ => "#CCCCCC" };

                                    t.Cell().Background(bg).BorderLeft(3).BorderColor(corBorda).Padding(5)
                                     .Text(Convert.ToDateTime(row["DataHora"]).ToString("dd/MM/yyyy HH:mm"))
                                     .FontSize(8).FontColor("#666666");
                                    t.Cell().Background(bg).Padding(5).Text(row["Usuario"]?.ToString() ?? "").FontSize(9);
                                    t.Cell().Background(bg).Padding(5).Text(acao).FontSize(9).Bold().FontColor(corAcao);
                                    t.Cell().Background(bg).Padding(5).Text(row["Detalhes"]?.ToString() ?? "").FontSize(8).FontColor("#666666");
                                    alt = !alt;
                                }
                            });
                        });
                    });

                    page.Footer().PaddingTop(6).Row(row =>
                    {
                        row.RelativeItem().Text("VaultFace — Sistema de Controlo de Acesso Biométrico").FontSize(8).FontColor("#AAAAAA");
                        row.ConstantItem(100).AlignRight().Text(txt =>
                        {
                            txt.Span("Página ").FontSize(8).FontColor("#AAAAAA");
                            txt.CurrentPageNumber().FontSize(8).FontColor("#AAAAAA");
                            txt.Span(" de ").FontSize(8).FontColor("#AAAAAA");
                            txt.TotalPages().FontSize(8).FontColor("#AAAAAA");
                        });
                    });
                });
            }).GeneratePdf();
        }

        private static void Kpi(RowDescriptor row, string label, string valor, string cor)
        {
            row.RelativeItem().Border(1).BorderColor("#E5E9F2").Padding(10).Column(col =>
            {
                col.Item().Text(valor).FontSize(20).Bold().FontColor(cor);
                col.Item().Text(label).FontSize(8).FontColor("#888888");
            });
        }
    }
}
