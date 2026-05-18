using EventosAPI.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace EventosAPI.Services
{
    // Clases auxiliares para el reporte
    public class EstadisticasReporte
    {
        public int totalEventos { get; set; }
        public int totalEntradas { get; set; }
        public decimal totalIngresos { get; set; }
        public int eventosConVentas { get; set; }
    }

    public class EventoReporte
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public DateTime Fecha { get; set; }
        public string Lugar { get; set; } = string.Empty;
        public int EntradasVendidas { get; set; }
        public decimal Ingresos { get; set; }
        public bool Activo { get; set; }
    }

    public class PdfService
    {
        public PdfService()
        {
            QuestPDF.Settings.License = LicenseType.Community;
        }

        public byte[] GenerarReporteVentas(EstadisticasReporte estadisticas, List<EventoReporte> eventos)
        {
            var pdf = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(2, Unit.Centimetre);
                    page.Background("#ffffff");

                    // Header
                    page.Header().ShowOnce().Column(header =>
                    {
                        header.Item().Text("REPORTE DE VENTAS")
                            .FontSize(20)
                            .Bold()
                            .FontColor("#1e293b")
                            .AlignCenter();

                        header.Item().Text($"Fecha de generación: {DateTime.Now:dd/MM/yyyy HH:mm}")
                            .FontSize(10)
                            .FontColor("#64748b")
                            .AlignCenter();

                        header.Item().PaddingTop(10).LineHorizontal(1);
                    });

                    // Content
                    page.Content().PaddingVertical(10).Column(content =>
                    {
                        // Resumen General
                        content.Item().Column(resumen =>
                        {
                            resumen.Item().Text("RESUMEN GENERAL")
                                .FontSize(14)
                                .Bold()
                                .FontColor("#1e293b");

                            resumen.Item().PaddingTop(5).Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.ConstantColumn(180);
                                    columns.RelativeColumn();
                                });

                                table.Cell().BorderBottom(1).Padding(5).Text("Total de ingresos:").Bold();
                                table.Cell().BorderBottom(1).Padding(5).Text($"${estadisticas.totalIngresos:N2}");

                                table.Cell().BorderBottom(1).Padding(5).Text("Total de entradas vendidas:").Bold();
                                table.Cell().BorderBottom(1).Padding(5).Text(estadisticas.totalEntradas.ToString());

                                table.Cell().BorderBottom(1).Padding(5).Text("Eventos con ventas:").Bold();
                                table.Cell().BorderBottom(1).Padding(5).Text(estadisticas.eventosConVentas.ToString());

                                table.Cell().BorderBottom(1).Padding(5).Text("Total de eventos:").Bold();
                                table.Cell().BorderBottom(1).Padding(5).Text(estadisticas.totalEventos.ToString());
                            });
                        });

                        // Detalle por Evento
                        content.Item().PaddingTop(20).Column(detalle =>
                        {
                            detalle.Item().Text("DETALLE POR EVENTO")
                                .FontSize(14)
                                .Bold()
                                .FontColor("#1e293b");

                            detalle.Item().PaddingTop(10).Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(3);
                                    columns.RelativeColumn(2);
                                    columns.RelativeColumn(2);
                                    columns.RelativeColumn(1);
                                    columns.RelativeColumn(2);
                                });

                                // Header
                                table.Header(header =>
                                {
                                    header.Cell().Background("#1e293b").Padding(5).Text("Evento").Bold().FontColor("#ffffff");
                                    header.Cell().Background("#1e293b").Padding(5).Text("Fecha").Bold().FontColor("#ffffff");
                                    header.Cell().Background("#1e293b").Padding(5).Text("Lugar").Bold().FontColor("#ffffff");
                                    header.Cell().Background("#1e293b").Padding(5).Text("Entradas").Bold().FontColor("#ffffff");
                                    header.Cell().Background("#1e293b").Padding(5).Text("Ingresos").Bold().FontColor("#ffffff");
                                });

                                // Rows
                                int rowIndex = 0;
                                foreach (var e in eventos)
                                {
                                    if (e.EntradasVendidas > 0)
                                    {
                                        var bgColor = rowIndex % 2 == 0 ? "#ffffff" : "#f8fafc";
                                        table.Cell().Background(bgColor).Padding(5).Text(e.Nombre);
                                        table.Cell().Background(bgColor).Padding(5).Text(e.Fecha.ToString("dd/MM/yyyy"));
                                        table.Cell().Background(bgColor).Padding(5).Text(e.Lugar);
                                        table.Cell().Background(bgColor).Padding(5).AlignRight().Text(e.EntradasVendidas.ToString());
                                        table.Cell().Background(bgColor).Padding(5).AlignRight().Text($"${e.Ingresos:N2}");
                                        rowIndex++;
                                    }
                                }

                                // Totales
                                table.Cell().ColumnSpan(3).Background("#e2e8f0").Padding(5).Text("TOTALES").Bold();
                                table.Cell().Background("#e2e8f0").Padding(5).AlignRight().Text(estadisticas.totalEntradas.ToString()).Bold();
                                table.Cell().Background("#e2e8f0").Padding(5).AlignRight().Text($"${estadisticas.totalIngresos:N2}").Bold();
                            });
                        });
                    });

                    // Footer
                    page.Footer().AlignCenter().Text($"Sistema de Gestión de Eventos - {DateTime.Now.Year}")
                        .FontSize(8)
                        .FontColor("#64748b");
                });
            });

            return pdf.GeneratePdf();
        }

        public byte[] GenerarTicket(Entrada entrada)
        {
            var pdf = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A6);
                    page.Margin(1, Unit.Centimetre);
                    page.Background("#ffffff");

                    page.Content().Column(col =>
                    {
                        col.Item().Text("TICKET DE ENTRADA")
                            .FontSize(14)
                            .Bold()
                            .FontColor("#1e293b")
                            .AlignCenter();

                        col.Item().PaddingTop(5).LineHorizontal(1);

                        col.Item().PaddingTop(10).Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(65);
                                columns.RelativeColumn();
                            });

                            table.Cell().Text("Evento:").Bold();
                            table.Cell().Text(entrada.Evento?.Nombre ?? "Evento eliminado");

                            table.Cell().Text("Fecha:").Bold();
                            table.Cell().Text(entrada.Evento?.Fecha.ToString("dd/MM/yyyy HH:mm") ?? "No disponible");

                            table.Cell().Text("Lugar:").Bold();
                            table.Cell().Text(entrada.Evento?.Lugar ?? "No disponible");

                            table.Cell().Text("Asiento:").Bold();
                            table.Cell().Text(entrada.Asiento);

                            table.Cell().Text("Precio:").Bold();
                            table.Cell().Text($"${entrada.PrecioPagado:N2}");

                            table.Cell().Text("Código QR:").Bold();
                            table.Cell().Text(entrada.CodigoQR ?? "N/A");
                        });

                        col.Item().PaddingTop(15).AlignCenter().Text("Presenta este ticket en la entrada")
                            .FontSize(8)
                            .FontColor("#64748b");
                    });
                });
            });

            return pdf.GeneratePdf();
        }
    }
}