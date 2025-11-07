using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using System.Net.Http;

namespace Informes.API.Servicios
{
    public class InformeService
    {
        public async Task<byte[]> GenerarInformeDiario(List<dynamic> resumenUsuarios, string nombreEmpresa, string? rutaLogo = null)
        {
            using var stream = new MemoryStream();
            var doc = new PdfDocument();
            var pagina = doc.AddPage();
            var gfx = XGraphics.FromPdfPage(pagina);
            var font = new XFont("OpenSans", 10, XFontStyle.Regular);
            var fontBold = new XFont("OpenSans", 10, XFontStyle.Bold);
            var fontTitle = new XFont("OpenSans", 14, XFontStyle.Bold);
            double y = 40;

            void NuevaPagina()
            {
                pagina = doc.AddPage();
                gfx = XGraphics.FromPdfPage(pagina);
                y = 40;
            }

            if (!string.IsNullOrEmpty(rutaLogo))
            {
                try
                {
                    using var http = new HttpClient();
                    var imgBytes = await http.GetByteArrayAsync(rutaLogo);

                    using var msImg = new MemoryStream(imgBytes);
                    var img = XImage.FromStream(() => msImg);

                    double logoWidth = 120;
                    double logoHeight = 60;
                    double logoX = (pagina.Width - logoWidth) / 2;

                    gfx.DrawImage(img, logoX, y, logoWidth, logoHeight);
                    y += logoHeight + 10;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error al cargar logo: " + ex.Message);
                    y += 10;
                }
            }

            gfx.DrawString(nombreEmpresa, fontTitle, XBrushes.DarkBlue,
                new XRect(0, y, pagina.Width, 30), XStringFormats.TopCenter);
            y += 30;

            gfx.DrawString("Resumen diario", fontBold, XBrushes.Black,
                new XRect(0, y, pagina.Width, 25), XStringFormats.TopCenter);
            y += 40;

            foreach (var r in resumenUsuarios)
            {
                if (y > pagina.Height - 100)
                {
                    NuevaPagina();
                }

                gfx.DrawString($"Nombre: {r.nombre}", fontBold, XBrushes.Black, new XRect(45, y, pagina.Width, 20), XStringFormats.TopLeft);
                y += 20;

                gfx.DrawString($"Total Servicios: {r.totalServicios}", font, XBrushes.Black, new XRect(50, y, pagina.Width, 20), XStringFormats.TopLeft);
                y += 20;

                gfx.DrawString("Servicios:", fontBold, XBrushes.Black, new XRect(50, y, pagina.Width, 20), XStringFormats.TopLeft);
                y += 20;
                foreach (var s in r.servicios)
                {
                    gfx.DrawString($"- {s.servicio} {s.valor}", font, XBrushes.Black, new XRect(60, y, pagina.Width, 20), XStringFormats.TopLeft);
                    y += 15;
                }

                gfx.DrawString($"Total plata: {r.totalPlata}", fontBold, XBrushes.Black, new XRect(50, y, pagina.Width, 20), XStringFormats.TopLeft);
                y += 25;

                if (r.obsequios is IEnumerable<dynamic> obsequios && obsequios.Any())
                {
                    gfx.DrawString("Obsequios:", fontBold, XBrushes.Black, new XRect(50, y, pagina.Width, 20), XStringFormats.TopLeft);
                    y += 20;
                    foreach (var o in r.obsequios)
                    {
                        gfx.DrawString($"- {o.servicio} {o.valor}", font, XBrushes.Black, new XRect(60, y, pagina.Width, 20), XStringFormats.TopLeft);
                        y += 15;
                    }
                }

                if (r.descanso is IEnumerable<dynamic> descanso && descanso.Any())
                {
                    gfx.DrawString("Descanso:", fontBold, XBrushes.Black, new XRect(50, y, pagina.Width, 20), XStringFormats.TopLeft);
                    y += 20;
                    foreach (var o in r.descanso)
                    {
                        gfx.DrawString($"- {o.servicio} {o.valor}", font, XBrushes.Black, new XRect(60, y, pagina.Width, 20), XStringFormats.TopLeft);
                        y += 15;
                    }
                }

                y += 30;
            }

            doc.Save(stream, false);

            return stream.ToArray();
        }

        public async Task<byte[]> GenerarInformeSemanal(List<dynamic> resumenUsuarios, string nombreEmpresa, string? rutaLogo = null)
        {
            using var stream = new MemoryStream();
            var doc = new PdfDocument();
            var pagina = doc.AddPage();
            var gfx = XGraphics.FromPdfPage(pagina);
            var font = new XFont("OpenSans", 10, XFontStyle.Regular);
            var fontBold = new XFont("OpenSans", 10, XFontStyle.Bold);
            var fontTitle = new XFont("OpenSans", 14, XFontStyle.Bold);
            double y = 40;

            void NuevaPagina()
            {
                pagina = doc.AddPage();
                gfx = XGraphics.FromPdfPage(pagina);
                y = 40;
            }

            if (!string.IsNullOrEmpty(rutaLogo))
            {
                try
                {
                    using var http = new HttpClient();
                    var imgBytes = await http.GetByteArrayAsync(rutaLogo);

                    using var msImg = new MemoryStream(imgBytes);
                    var img = XImage.FromStream(() => msImg);

                    double logoWidth = 120;
                    double logoHeight = 60;
                    double logoX = (pagina.Width - logoWidth) / 2;

                    gfx.DrawImage(img, logoX, y, logoWidth, logoHeight);
                    y += logoHeight + 10;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error al cargar logo: " + ex.Message);
                    y += 10;
                }
            }

            gfx.DrawString(nombreEmpresa, fontTitle, XBrushes.DarkBlue,
                new XRect(0, y, pagina.Width, 30), XStringFormats.TopCenter);
            y += 30;

            gfx.DrawString("Resumen semanal", fontBold, XBrushes.Black,
                new XRect(0, y, pagina.Width, 25), XStringFormats.TopCenter);
            y += 40;

            foreach (var r in resumenUsuarios)
            {
                if (y > pagina.Height - 100)
                {
                    NuevaPagina();
                }

                gfx.DrawString($"Nombre: {r.nombre}", fontBold, XBrushes.Black, new XRect(45, y, pagina.Width, 20), XStringFormats.TopLeft);
                y += 20;

                gfx.DrawString($"Total servicios: {r.totalServicios}", font, XBrushes.Black, new XRect(50, y, pagina.Width, 20), XStringFormats.TopLeft);
                y += 20;

                //gfx.DrawString($"Porcentaje aplicar: {r.porcentaje}", font, XBrushes.Black, new XRect(50, y, pagina.Width, 20), XStringFormats.TopLeft);
                //y += 20;

                gfx.DrawString($"Total ingreso: {r.totalPlata}", font, XBrushes.Black, new XRect(50, y, pagina.Width, 20), XStringFormats.TopLeft);
                y += 20;

                gfx.DrawString($"Total barbero: {r.totalEmpleado}", font, XBrushes.Black, new XRect(50, y, pagina.Width, 20), XStringFormats.TopLeft);
                y += 20;

                gfx.DrawString($"Total barberia: {r.totalBarber}", font, XBrushes.Black, new XRect(50, y, pagina.Width, 20), XStringFormats.TopLeft);
                y += 20;

                if (r.obsequios is IEnumerable<dynamic> obsequios && obsequios.Any())
                {
                    gfx.DrawString($"Total servicios obsequio: {r.totalObsequios}", font, XBrushes.Black, new XRect(50, y, pagina.Width, 20), XStringFormats.TopLeft);
                    y += 20;

                    gfx.DrawString($"Total Ingreso obsequio: {r.totalPlataObsequios}", font, XBrushes.Black, new XRect(50, y, pagina.Width, 20), XStringFormats.TopLeft);
                    y += 20;
                }

                if (r.descanso is IEnumerable<dynamic> descanso && descanso.Any())
                {
                    gfx.DrawString($"Total servicio descanso: {r.totaldescanso}", font, XBrushes.Black, new XRect(50, y, pagina.Width, 20), XStringFormats.TopLeft);
                    y += 20;

                    gfx.DrawString($"Total ingreso descanso: {r.totalPlataDescanso}", font, XBrushes.Black, new XRect(50, y, pagina.Width, 20), XStringFormats.TopLeft);
                    y += 20;
                }

                y += 30;
            }

            doc.Save(stream, false);

            return stream.ToArray();
        }
    }
}
