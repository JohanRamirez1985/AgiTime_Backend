using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using System.Globalization;
using System.IO;
using System.Net.Http;

namespace Informes.API.Servicios
{
    public class InformeService
    {
        public async Task<byte[]> GenerarInformeDiario(List<dynamic> resumenUsuarios, string fecha, string nombreEmpresa, string? rutaLogo = null)
        {
            using var stream = new MemoryStream();
            var doc = new PdfDocument();

            PdfPage pagina = doc.AddPage();
            XGraphics gfx = XGraphics.FromPdfPage(pagina);

            var fontSmall = new XFont("OpenSans", 6, XFontStyle.Regular);
            var font = new XFont("OpenSans", 9, XFontStyle.Regular);
            var fontBold = new XFont("OpenSans", 9, XFontStyle.Bold);
            var fontTitle = new XFont("OpenSans", 14, XFontStyle.Bold);

            double y = 0;

            // =========================
            // CONFIG TABLA
            // =========================
            double tableLeft = 40;
            double tableRight = pagina.Width - 40;

            double colNombre = 45;
            double colDetalle = 200;
            double colValor = 430;
            double filaAlto = 18;

            // =========================
            // PENS (LÍNEAS)
            // =========================
            var lineLight = new XPen(XColors.LightGray, 0.8);
            var lineDark = new XPen(XColors.Black, 1.2);

            // =========================
            // UTILIDADES
            // =========================
            string Moneda(decimal valor) =>
                valor.ToString("C0", new CultureInfo("es-CO"));

            void NuevaPagina()
            {
                pagina = doc.AddPage();
                gfx = XGraphics.FromPdfPage(pagina);
                DibujarEncabezado();
            }

            void ValidarSalto(double espacio)
            {
                if (y + espacio > pagina.Height - 40)
                {
                    NuevaPagina();
                }
            }

            void DibujarEncabezado()
            {
                double headerHeight = 90;

                if (!string.IsNullOrEmpty(rutaLogo))
                {
                    try
                    {
                        using var http = new HttpClient();
                        var imgBytes = http.GetByteArrayAsync(rutaLogo).Result;
                        using var msImg = new MemoryStream(imgBytes);
                        var img = XImage.FromStream(() => msImg);

                        gfx.DrawImage(img, tableLeft, 20, 60, 60);
                    }
                    catch { }
                }

                gfx.DrawString(nombreEmpresa, fontTitle, XBrushes.DarkBlue,
                    new XRect(0, 20, pagina.Width, 25), XStringFormats.TopCenter);

                gfx.DrawString($"Resumen diario - {fecha}",
                    fontBold, XBrushes.Black,
                    new XRect(0, 45, pagina.Width, 20), XStringFormats.TopCenter);

                //gfx.DrawLine(lineLight, tableLeft, 80, tableRight, 80);

                y = headerHeight;
            }

            // =========================
            // INICIO DOCUMENTO
            // =========================
            DibujarEncabezado();

            decimal totalGeneralDia = 0;

            foreach (var r in resumenUsuarios)
            {
                // =========================
                // CALCULAR ALTO DEL BLOQUE
                // =========================
                int totalLineas =
                    3 +
                    r.servicios.Count +
                    (r.obsequios?.Count ?? 0) +
                    (r.descanso?.Count ?? 0) +
                    2;

                double altoBloque = totalLineas * filaAlto;
                ValidarSalto(altoBloque);

                // =========================
                // BARBERO
                // =========================
                gfx.DrawString($"Barbero: {r.nombre}", fontBold, XBrushes.Black,
                    colNombre, y);
                y += filaAlto;

                gfx.DrawString($"Total servicios: {r.totalServicios}", font,
                    XBrushes.Black, colNombre, y);
                y += filaAlto;

                gfx.DrawString("Servicios", fontBold, XBrushes.Black,
                    colNombre, y);
                y += filaAlto;

                foreach (var s in r.servicios)
                {
                    gfx.DrawString($"• {s.servicio}", font, XBrushes.Black,
                        colDetalle, y);
                    var esBono = s.bono is bool b && b;
                    var valorTexto = Moneda((decimal)s.valor) + (esBono ? " *" : "");
                    gfx.DrawString(valorTexto, font,
                        XBrushes.Black, colValor, y);
                    y += filaAlto;
                }

                if (r.obsequios is IEnumerable<dynamic> obsequios && obsequios.Any())
                {
                    gfx.DrawString("Obsequios", fontBold, XBrushes.Black,
                        colNombre, y);
                    y += filaAlto;

                    foreach (var o in obsequios)
                    {
                        gfx.DrawString($"• {o.servicio}", font, XBrushes.Black,
                            colDetalle, y);
                        gfx.DrawString(Moneda((decimal)o.valor), font,
                            XBrushes.Black, colValor, y);
                        y += filaAlto;
                    }
                }

                if (r.descanso is IEnumerable<dynamic> descanso && descanso.Any())
                {
                    gfx.DrawString("Descanso", fontBold, XBrushes.Black,
                        colNombre, y);
                    y += filaAlto;

                    foreach (var d in descanso)
                    {
                        gfx.DrawString($"• {d.servicio}", font, XBrushes.Black,
                            colDetalle, y);
                        gfx.DrawString(Moneda((decimal)d.valor), font,
                            XBrushes.Black, colValor, y);
                        y += filaAlto;
                    }
                }

                decimal totalBarbero = (r.totalPlata ?? 0) + (r.totalPlataObsequios ?? 0) + (r.totalPlataDescanso ?? 0);
                totalGeneralDia += totalBarbero;

                // ===== Línea limpia antes del total =====
                //gfx.DrawLine(lineLight, tableLeft, y, tableRight, y);
                y += 5;

                gfx.DrawString("Total barbero", fontBold, XBrushes.Black,
                    colDetalle, y);
                gfx.DrawString(Moneda(totalBarbero), fontBold, XBrushes.Black,
                    colValor, y);

                y += filaAlto + 10;
            }

            // =========================
            // TOTAL GENERAL
            // =========================
            ValidarSalto(40);

            gfx.DrawLine(lineDark, tableLeft, y, tableRight, y);
            y += 10;

            gfx.DrawString("TOTAL GENERAL DEL DÍA", fontBold, XBrushes.Black,
                colDetalle, y);
            gfx.DrawString(Moneda(totalGeneralDia), fontBold, XBrushes.Black,
                colValor, y);
            y += 10;
            gfx.DrawString($"(*) Indica que parte del servicio fue cubierto mediante bono.", fontSmall, XBrushes.Black,
                    colNombre, y);

            // =========================
            // GUARDAR
            // =========================
            doc.Save(stream, false);
            return stream.ToArray();

            ///*REPORTE ANTERIOR*/
            //using var stream = new MemoryStream();
            //var doc = new PdfDocument();
            //var pagina = doc.AddPage();
            //var gfx = XGraphics.FromPdfPage(pagina);
            //var font = new XFont("OpenSans", 10, XFontStyle.Regular);
            //var fontBold = new XFont("OpenSans", 10, XFontStyle.Bold);
            //var fontTitle = new XFont("OpenSans", 14, XFontStyle.Bold);
            //double y = 40;

            //void NuevaPagina()
            //{
            //    pagina = doc.AddPage();
            //    gfx = XGraphics.FromPdfPage(pagina);
            //    y = 40;
            //}

            //if (!string.IsNullOrEmpty(rutaLogo))
            //{
            //    try
            //    {
            //        using var http = new HttpClient();
            //        var imgBytes = await http.GetByteArrayAsync(rutaLogo);

            //        using var msImg = new MemoryStream(imgBytes);
            //        var img = XImage.FromStream(() => msImg);

            //        double logoWidth = 120;
            //        double logoHeight = 180;
            //        double logoX = (pagina.Width - logoWidth) / 2;

            //        gfx.DrawImage(img, logoX, y, logoWidth, logoHeight);
            //        y += logoHeight + 10;
            //    }
            //    catch (Exception ex)
            //    {
            //        Console.WriteLine("Error al cargar logo: " + ex.Message);
            //        y += 10;
            //    }
            //}

            //gfx.DrawString(nombreEmpresa, fontTitle, XBrushes.DarkBlue,
            //    new XRect(0, y, pagina.Width, 30), XStringFormats.TopCenter);
            //y += 30;

            //gfx.DrawString("Resumen diario", fontBold, XBrushes.Black,
            //    new XRect(0, y, pagina.Width, 25), XStringFormats.TopCenter);
            //y += 40;

            //gfx.DrawString("Fecha: " + fecha, fontBold, XBrushes.Black,
            //   new XRect(45, y, pagina.Width, 25), XStringFormats.TopLeft);
            //y += 40;

            //foreach (var r in resumenUsuarios)
            //{
            //    if (y > pagina.Height - 100)
            //    {
            //        NuevaPagina();
            //    }

            //    gfx.DrawString($"Nombre: {r.nombre}", fontBold, XBrushes.Black, new XRect(45, y, pagina.Width, 20), XStringFormats.TopLeft);
            //    y += 20;

            //    gfx.DrawString($"Total Servicios: {r.totalServicios}", font, XBrushes.Black, new XRect(50, y, pagina.Width, 20), XStringFormats.TopLeft);
            //    y += 20;

            //    gfx.DrawString("Servicios:", fontBold, XBrushes.Black, new XRect(50, y, pagina.Width, 20), XStringFormats.TopLeft);
            //    y += 20;
            //    foreach (var s in r.servicios)
            //    {
            //        gfx.DrawString($"- {s.servicio} {s.valor}", font, XBrushes.Black, new XRect(60, y, pagina.Width, 20), XStringFormats.TopLeft);
            //        y += 15;
            //    }

            //    gfx.DrawString($"Total plata: {r.totalPlata}", fontBold, XBrushes.Black, new XRect(50, y, pagina.Width, 20), XStringFormats.TopLeft);
            //    y += 25;

            //    if (r.obsequios is IEnumerable<dynamic> obsequios && obsequios.Any())
            //    {
            //        gfx.DrawString("Obsequios:", fontBold, XBrushes.Black, new XRect(50, y, pagina.Width, 20), XStringFormats.TopLeft);
            //        y += 20;
            //        foreach (var o in r.obsequios)
            //        {
            //            gfx.DrawString($"- {o.servicio} {o.valor}", font, XBrushes.Black, new XRect(60, y, pagina.Width, 20), XStringFormats.TopLeft);
            //            y += 15;
            //        }
            //    }

            //    if (r.descanso is IEnumerable<dynamic> descanso && descanso.Any())
            //    {
            //        gfx.DrawString("Descanso:", fontBold, XBrushes.Black, new XRect(50, y, pagina.Width, 20), XStringFormats.TopLeft);
            //        y += 20;
            //        foreach (var o in r.descanso)
            //        {
            //            gfx.DrawString($"- {o.servicio} {o.valor}", font, XBrushes.Black, new XRect(60, y, pagina.Width, 20), XStringFormats.TopLeft);
            //            y += 15;
            //        }
            //    }

            //    y += 30;
            //}

            //doc.Save(stream, false);

            //return stream.ToArray();
        }

        public async Task<byte[]> GenerarInformeSemanal(List<dynamic> resumenUsuarios, string fechaIni, string fechaFin, string nombreEmpresa, string? rutaLogo = null)
        {
            try
            {
                using var stream = new MemoryStream();
                var doc = new PdfDocument();

                PdfPage pagina = doc.AddPage();
                XGraphics gfx = XGraphics.FromPdfPage(pagina);

                var fontSmall = new XFont("OpenSans", 6, XFontStyle.Regular);
                var font = new XFont("OpenSans", 9, XFontStyle.Regular);
                var fontBold = new XFont("OpenSans", 9, XFontStyle.Bold);
                var fontTitle = new XFont("OpenSans", 14, XFontStyle.Bold);

                double y = 0;

                // =========================
                // CONFIG TABLA
                // =========================
                double tableLeft = 40;
                double tableRight = pagina.Width - 40;

                double colNombre = 40;
                double colServicios = 140;
                double colIngreso = 190;
                double colBarberia = 250;
                double colEmpleado = 310;
                double colObsequio = 370;
                double colDescanso = 440;
                double colTotal = 510;
                double filaAlto = 18;

                // =========================
                // LÍNEAS
                // =========================
                var lineLight = new XPen(XColors.LightGray, 0.8);
                var lineDark = new XPen(XColors.Black, 1.2);

                // =========================
                // UTILIDADES
                // =========================
                string Moneda(decimal valor) =>
                    valor.ToString("C0", new CultureInfo("es-CO"));

                void NuevaPagina()
                {
                    pagina = doc.AddPage();
                    gfx = XGraphics.FromPdfPage(pagina);
                    DibujarEncabezado();
                    DibujarEncabezadoTabla();
                }

                void ValidarSalto(double espacio)
                {
                    if (y + espacio > pagina.Height - 40)
                    {
                        NuevaPagina();
                    }
                }

                void DibujarEncabezado()
                {
                    double headerHeight = 90;

                    if (!string.IsNullOrEmpty(rutaLogo))
                    {
                        try
                        {
                            using var http = new HttpClient();
                            var imgBytes = http.GetByteArrayAsync(rutaLogo).Result;
                            using var msImg = new MemoryStream(imgBytes);
                            var img = XImage.FromStream(() => msImg);

                            gfx.DrawImage(img, tableLeft, 20, 60, 60);
                        }
                        catch { }
                    }

                    gfx.DrawString(nombreEmpresa, fontTitle, XBrushes.DarkBlue,
                        new XRect(0, 20, pagina.Width, 25), XStringFormats.TopCenter);

                    gfx.DrawString($"Resumen semanal {fechaIni} al {fechaFin}",
                        fontBold, XBrushes.Black,
                        new XRect(0, 45, pagina.Width, 20), XStringFormats.TopCenter);

                    y = headerHeight;
                }

                void DibujarEncabezadoTabla()
                {
                    var center = new XStringFormat
                    {
                        Alignment = XStringAlignment.Center,
                        LineAlignment = XLineAlignment.Center
                    };

                    gfx.DrawString("Barbero", fontBold, XBrushes.Black, colNombre, y);
                    gfx.DrawString("Servicios", fontBold, XBrushes.Black, colServicios, y);
                    gfx.DrawString("Ingreso", fontBold, XBrushes.Black, colIngreso, y);
                    gfx.DrawString("Barbería", fontBold, XBrushes.Black, colBarberia, y);
                    gfx.DrawString("Empleado", fontBold, XBrushes.Black, colEmpleado, y);
                    gfx.DrawString("Obsequios", fontBold, XBrushes.Black, colObsequio, y);
                    gfx.DrawString("Descanso", fontBold, XBrushes.Black, colDescanso, y);
                    gfx.DrawString("Total", fontBold, XBrushes.Black, colTotal, y);

                    y += filaAlto;
                    y += 5;
                }

                // =========================
                // INICIO DOCUMENTO
                // =========================
                DibujarEncabezado();
                DibujarEncabezadoTabla();

                decimal totalGeneral = 0;
                decimal totalGeneralIngreso = 0;
                decimal totalGeneralBarberia = 0;
                decimal totalGeneralEmpleado = 0;
                decimal totalGeneralObsequio = 0;
                decimal totalGeneralDescanso = 0;

                foreach (var r in resumenUsuarios)
                {
                    if (r.totalServicios > 0)
                    {
                        ValidarSalto(filaAlto + 5);

                        int totalServicios = r.totalServicios ?? 0;
                        decimal ingreso = r.totalPlata ?? 0;
                        decimal empleado = r.totalEmpleado ?? 0;
                        decimal barberia = r.totalBarber ?? 0;
                        decimal obsequio = r.totalPlataObsequios ?? 0;
                        decimal descanso = r.totalPlataDescanso ?? 0;
                        decimal totalBarbero = r.totalIngresoBarber ?? 0;
                        decimal totalBonos = r.totalBonos ?? 0;

                        totalGeneral += totalBarbero;
                        totalGeneralIngreso += ingreso;
                        totalGeneralBarberia += barberia;
                        totalGeneralEmpleado += empleado;
                        totalGeneralObsequio += obsequio;
                        totalGeneralDescanso += descanso;

                        var valorTexto = totalBonos > 0 ? totalServicios.ToString() + " (" + totalBonos.ToString() + ")*" : totalServicios.ToString();

                        gfx.DrawString(r.nombre, font, XBrushes.Black, colNombre, y);
                        gfx.DrawString(valorTexto, font, XBrushes.Black, colServicios, y);
                        gfx.DrawString(Moneda(ingreso), font, XBrushes.Black, colIngreso, y);
                        gfx.DrawString(Moneda(barberia), font, XBrushes.Black, colBarberia, y);
                        gfx.DrawString(Moneda(empleado), font, XBrushes.Black, colEmpleado, y);
                        gfx.DrawString("(" + r.totalObsequios + ") " + Moneda(obsequio), font, XBrushes.Black, colObsequio, y);
                        gfx.DrawString("(" + r.totaldescanso + ") " + Moneda(descanso), font, XBrushes.Black, colDescanso, y);
                        gfx.DrawString(Moneda(totalBarbero), fontBold, XBrushes.Black, colTotal, y);

                        y += filaAlto;
                        y += 3;
                    }
                }
                // =========================
                // TOTAL GENERAL
                // =========================
                ValidarSalto(40);

                gfx.DrawLine(lineDark, tableLeft, y, tableRight, y);
                y += 10;

                gfx.DrawString("Total General Semanal", fontBold, XBrushes.Black, colNombre, y);
                gfx.DrawString("", font, XBrushes.Black, colServicios, y);
                gfx.DrawString(Moneda(totalGeneralIngreso), font, XBrushes.Black, colIngreso, y);
                gfx.DrawString(Moneda(totalGeneralBarberia), font, XBrushes.Black, colBarberia, y);
                gfx.DrawString(Moneda(totalGeneralEmpleado), font, XBrushes.Black, colEmpleado, y);
                gfx.DrawString(Moneda(totalGeneralObsequio), font, XBrushes.Black, colObsequio, y);
                gfx.DrawString(Moneda(totalGeneralDescanso), font, XBrushes.Black, colDescanso, y);
                gfx.DrawString(Moneda(totalGeneral), fontBold, XBrushes.Black, colTotal, y);

                var totalesPorSede = resumenUsuarios
                    .GroupBy(r => r.sede)
                    .Select(g => new
                    {
                        sede = g.Key,
                        totalSer = g.Sum(x => (int)(x.totalServicios ?? 0)),
                        totalBar = g.Sum(x => (decimal)(x.totalBarber ?? 0)),
                        totalBon = g.Sum(x => (decimal)(x.totalBonos ?? 0))
                    })
                .ToList();

                ValidarSalto(40);

                // =========================
                // TOTAL SEDE
                // =========================
                y += 20;

                foreach (var sede in totalesPorSede)
                {
                    ValidarSalto(filaAlto);

                    var valorTexto = sede.totalBon > 0 ? sede.totalSer.ToString() + " (" + sede.totalBon.ToString() + ")*" : sede.totalSer.ToString();

                    gfx.DrawString(sede.sede, fontBold, XBrushes.Black, colNombre, y);
                    gfx.DrawString(valorTexto, font, XBrushes.Black, colServicios, y);
                    gfx.DrawString("", fontBold, XBrushes.Black, colIngreso, y);
                    gfx.DrawString(Moneda(sede.totalBar), fontBold, XBrushes.Black, colBarberia, y);
                    gfx.DrawString("", font, XBrushes.Black, colEmpleado, y);
                    gfx.DrawString("", font, XBrushes.Black, colObsequio, y);
                    gfx.DrawString("", font, XBrushes.Black, colDescanso, y);
                    gfx.DrawString("", fontBold, XBrushes.Black, colTotal, y);

                    y += filaAlto;
                }

                y += 10;
                gfx.DrawString($"(*) Cantidad de servicios que fueron cubiertos mediante bono.", fontSmall, XBrushes.Black,
                        colNombre, y);

                // =========================
                // GUARDAR
                // =========================
                doc.Save(stream, false);
                return stream.ToArray();
            }
            catch(Exception e)
            {
                var error = e.Message;
                return null;
            }

            ///*REPORTE ANTERIOR*/
            //using var stream = new MemoryStream();
            //var doc = new PdfDocument();
            //var pagina = doc.AddPage();
            //var gfx = XGraphics.FromPdfPage(pagina);
            //var font = new XFont("OpenSans", 10, XFontStyle.Regular);
            //var fontBold = new XFont("OpenSans", 10, XFontStyle.Bold);
            //var fontTitle = new XFont("OpenSans", 14, XFontStyle.Bold);
            //double y = 40;

            //void NuevaPagina()
            //{
            //    pagina = doc.AddPage();
            //    gfx = XGraphics.FromPdfPage(pagina);
            //    y = 40;
            //}

            //if (!string.IsNullOrEmpty(rutaLogo))
            //{
            //    try
            //    {
            //        using var http = new HttpClient();
            //        var imgBytes = await http.GetByteArrayAsync(rutaLogo);

            //        using var msImg = new MemoryStream(imgBytes);
            //        var img = XImage.FromStream(() => msImg);

            //        double logoWidth = 120;
            //        double logoHeight = 180;
            //        double logoX = (pagina.Width - logoWidth) / 2;

            //        gfx.DrawImage(img, logoX, y, logoWidth, logoHeight);
            //        y += logoHeight + 10;
            //    }
            //    catch (Exception ex)
            //    {
            //        Console.WriteLine("Error al cargar logo: " + ex.Message);
            //        y += 10;
            //    }
            //}

            //gfx.DrawString(nombreEmpresa, fontTitle, XBrushes.DarkBlue,
            //    new XRect(0, y, pagina.Width, 30), XStringFormats.TopCenter);
            //y += 30;

            //gfx.DrawString("Resumen semanal", fontBold, XBrushes.Black,
            //    new XRect(0, y, pagina.Width, 25), XStringFormats.TopCenter);
            //y += 40;

            //gfx.DrawString("Semana del: " + fechaIni + " al " + fechaFin, fontBold, XBrushes.Black,
            //  new XRect(45, y, pagina.Width, 25), XStringFormats.TopLeft);
            //y += 40;

            //foreach (var r in resumenUsuarios)
            //{
            //    if (y > pagina.Height - 100)
            //    {
            //        NuevaPagina();
            //    }

            //    if (r.totalServicios > 0)
            //    {

            //        gfx.DrawString($"Nombre: {r.nombre}", fontBold, XBrushes.Black, new XRect(45, y, pagina.Width, 20), XStringFormats.TopLeft);
            //        y += 20;

            //        gfx.DrawString($"Total servicios: {r.totalServicios}", font, XBrushes.Black, new XRect(50, y, pagina.Width, 20), XStringFormats.TopLeft);
            //        y += 20;

            //        //gfx.DrawString($"Porcentaje aplicar: {r.porcentaje}", font, XBrushes.Black, new XRect(50, y, pagina.Width, 20), XStringFormats.TopLeft);
            //        //y += 20;

            //        gfx.DrawString($"Total ingreso: {r.totalPlata}", font, XBrushes.Black, new XRect(50, y, pagina.Width, 20), XStringFormats.TopLeft);
            //        y += 20;

            //        gfx.DrawString($"Total barbero: {r.totalEmpleado}", font, XBrushes.Black, new XRect(50, y, pagina.Width, 20), XStringFormats.TopLeft);
            //        y += 20;

            //        gfx.DrawString($"Total barberia: {r.totalBarber}", font, XBrushes.Black, new XRect(50, y, pagina.Width, 20), XStringFormats.TopLeft);
            //        y += 20;
            //    }

            //    if (r.obsequios is IEnumerable<dynamic> obsequios && obsequios.Any())
            //    {
            //        gfx.DrawString($"Total servicios obsequio: {r.totalObsequios}", font, XBrushes.Black, new XRect(50, y, pagina.Width, 20), XStringFormats.TopLeft);
            //        y += 20;

            //        gfx.DrawString($"Total Ingreso obsequio: {r.totalPlataObsequios}", font, XBrushes.Black, new XRect(50, y, pagina.Width, 20), XStringFormats.TopLeft);
            //        y += 20;
            //    }

            //    if (r.descanso is IEnumerable<dynamic> descanso && descanso.Any())
            //    {
            //        gfx.DrawString($"Total servicio descanso: {r.totaldescanso}", font, XBrushes.Black, new XRect(50, y, pagina.Width, 20), XStringFormats.TopLeft);
            //        y += 20;

            //        gfx.DrawString($"Total ingreso descanso: {r.totalPlataDescanso}", font, XBrushes.Black, new XRect(50, y, pagina.Width, 20), XStringFormats.TopLeft);
            //        y += 20;
            //    }

            //    if (r.totalServicios > 0)
            //    {
            //        gfx.DrawString($"Total ingreso Barbero: {r.totalIngresoBarber}", fontBold, XBrushes.Black, new XRect(50, y, pagina.Width, 20), XStringFormats.TopLeft);
            //    }

            //    y += 30;
            //}

            //doc.Save(stream, false);

            //return stream.ToArray();
        }

        public async Task<byte[]> GenerarInformeSemanalBarber(List<dynamic> resumenUsuarios, string fechaIni, string fechaFin, string nombreEmpresa, string? rutaLogo = null)
        {
            using var stream = new MemoryStream();
            var doc = new PdfDocument();

            PdfPage pagina = doc.AddPage();
            XGraphics gfx = XGraphics.FromPdfPage(pagina);

            var fontSmall = new XFont("OpenSans", 6, XFontStyle.Regular);
            var font = new XFont("OpenSans", 9, XFontStyle.Regular);
            var fontBold = new XFont("OpenSans", 9, XFontStyle.Bold);
            var fontTitle = new XFont("OpenSans", 14, XFontStyle.Bold);

            double y = 0;

            // =========================
            // CONFIG TABLA
            // =========================
            double tableLeft = 40;
            double tableRight = pagina.Width - 40;

            double colNombre = 45;
            double colTotalServicio = 180;
            double colServicios = 260;
            double colObsequios = 330;
            double colDescanso = 400;
            double colTotal = 470;
            double filaAlto = 20;

            // =========================
            // LÍNEAS
            // =========================
            var lineLight = new XPen(XColors.LightGray, 0.8);
            var lineDark = new XPen(XColors.Black, 1.2);

            // =========================
            // UTILIDADES
            // =========================
            string Moneda(decimal valor) =>
                valor.ToString("C0", new CultureInfo("es-CO"));

            void NuevaPagina()
            {
                pagina = doc.AddPage();
                gfx = XGraphics.FromPdfPage(pagina);
                DibujarEncabezado();
                DibujarEncabezadoTabla();
            }

            void ValidarSalto(double espacio)
            {
                if (y + espacio > pagina.Height - 40)
                {
                    NuevaPagina();
                }
            }

            void DibujarEncabezado()
            {
                double headerHeight = 90;

                if (!string.IsNullOrEmpty(rutaLogo))
                {
                    try
                    {
                        using var http = new HttpClient();
                        var imgBytes = http.GetByteArrayAsync(rutaLogo).Result;
                        using var msImg = new MemoryStream(imgBytes);
                        var img = XImage.FromStream(() => msImg);

                        gfx.DrawImage(img, tableLeft, 20, 60, 60);
                    }
                    catch { }
                }

                gfx.DrawString(nombreEmpresa, fontTitle, XBrushes.DarkBlue,
                    new XRect(0, 20, pagina.Width, 25), XStringFormats.TopCenter);

                gfx.DrawString($"Resumen semanal {fechaIni} al {fechaFin}",
                    fontBold, XBrushes.Black,
                    new XRect(0, 45, pagina.Width, 20), XStringFormats.TopCenter);

                //gfx.DrawLine(lineLight, tableLeft, 80, tableRight, 80);
                y = headerHeight;
            }

            void DibujarEncabezadoTabla()
            {
                gfx.DrawString("Barbero", fontBold, XBrushes.Black, colNombre, y);
                gfx.DrawString("Total Servicios", fontBold, XBrushes.Black, colTotalServicio, y);
                gfx.DrawString("Servicios", fontBold, XBrushes.Black, colServicios, y);
                gfx.DrawString("Obsequios", fontBold, XBrushes.Black, colObsequios, y);
                gfx.DrawString("Descanso", fontBold, XBrushes.Black, colDescanso, y);
                gfx.DrawString("Total", fontBold, XBrushes.Black, colTotal, y);

                y += filaAlto;
                //gfx.DrawLine(lineLight, tableLeft, y, tableRight, y);
                y += 5;
            }

            // =========================
            // INICIO DOCUMENTO
            // =========================
            DibujarEncabezado();
            DibujarEncabezadoTabla();

            decimal totalGeneral = 0;

            foreach (var r in resumenUsuarios)
            {
                ValidarSalto(filaAlto + 5);

                int totalServicios = r.totalServicios ?? 0;
                decimal servicios = r.totalEmpleado ?? 0;
                decimal obsequios = r.totalPlataObsequios ?? 0;
                decimal descanso = r.totalPlataDescanso ?? 0;
                decimal totalBarbero = r.totalIngresoBarber ?? 0;
                decimal totalBonos = r.totalBonos ?? 0;

                totalGeneral += totalBarbero;

                var valorTexto = totalBonos > 0 ? totalServicios.ToString() + " (" + totalBonos.ToString() + ")*" : totalServicios.ToString();

                gfx.DrawString(r.nombre, font, XBrushes.Black, colNombre, y);
                gfx.DrawString(valorTexto, font, XBrushes.Black, colTotalServicio, y);
                gfx.DrawString(Moneda(servicios), font, XBrushes.Black, colServicios, y);
                gfx.DrawString(Moneda(obsequios), font, XBrushes.Black, colObsequios, y);
                gfx.DrawString(Moneda(descanso), font, XBrushes.Black, colDescanso, y);
                gfx.DrawString(Moneda(totalBarbero), fontBold, XBrushes.Black, colTotal, y);

                y += filaAlto;
                //gfx.DrawLine(lineLight, tableLeft, y, tableRight, y);
                y += 3;
            }

            // =========================
            // TOTAL GENERAL
            // =========================
            ValidarSalto(40);

            gfx.DrawLine(lineDark, tableLeft, y, tableRight, y);
            y += 10;

            gfx.DrawString("TOTAL GENERAL SEMANAL", fontBold, XBrushes.Black,
                colServicios, y);
            gfx.DrawString(Moneda(totalGeneral), fontBold, XBrushes.Black,
                colTotal, y);

            y += 10;
            gfx.DrawString($"(*) Cantidad de servicios que fueron cubiertos mediante bono.", fontSmall, XBrushes.Black,
                    colNombre, y);

            // =========================
            // GUARDAR PDF
            // =========================
            doc.Save(stream, false);
            return stream.ToArray();

            ///*REPORTE ANTERIOR*/
            //using var stream = new MemoryStream();
            //var doc = new PdfDocument();
            //var pagina = doc.AddPage();
            //var gfx = XGraphics.FromPdfPage(pagina);
            //var font = new XFont("OpenSans", 10, XFontStyle.Regular);
            //var fontBold = new XFont("OpenSans", 10, XFontStyle.Bold);
            //var fontTitle = new XFont("OpenSans", 14, XFontStyle.Bold);
            //double y = 40;

            //void NuevaPagina()
            //{
            //    pagina = doc.AddPage();
            //    gfx = XGraphics.FromPdfPage(pagina);
            //    y = 40;
            //}

            //if (!string.IsNullOrEmpty(rutaLogo))
            //{
            //    try
            //    {
            //        using var http = new HttpClient();
            //        var imgBytes = await http.GetByteArrayAsync(rutaLogo);

            //        using var msImg = new MemoryStream(imgBytes);
            //        var img = XImage.FromStream(() => msImg);

            //        double logoWidth = 120;
            //        double logoHeight = 180;
            //        double logoX = (pagina.Width - logoWidth) / 2;

            //        gfx.DrawImage(img, logoX, y, logoWidth, logoHeight);
            //        y += logoHeight + 10;
            //    }
            //    catch (Exception ex)
            //    {
            //        Console.WriteLine("Error al cargar logo: " + ex.Message);
            //        y += 10;
            //    }
            //}

            //gfx.DrawString(nombreEmpresa, fontTitle, XBrushes.DarkBlue,
            //    new XRect(0, y, pagina.Width, 30), XStringFormats.TopCenter);
            //y += 30;

            //gfx.DrawString("Resumen semanal", fontBold, XBrushes.Black,
            //    new XRect(0, y, pagina.Width, 25), XStringFormats.TopCenter);
            //y += 40;

            //gfx.DrawString("Semana del: " + fechaIni + " al " + fechaFin, fontBold, XBrushes.Black,
            //  new XRect(45, y, pagina.Width, 25), XStringFormats.TopLeft);
            //y += 40;

            //foreach (var r in resumenUsuarios)
            //{
            //    if (y > pagina.Height - 100)
            //    {
            //        NuevaPagina();
            //    }

            //    if (r.totalServicios > 0)
            //    {

            //        gfx.DrawString($"Nombre: {r.nombre}", fontBold, XBrushes.Black, new XRect(45, y, pagina.Width, 20), XStringFormats.TopLeft);
            //        y += 20;

            //        gfx.DrawString($"Total servicios: {r.totalServicios}", font, XBrushes.Black, new XRect(50, y, pagina.Width, 20), XStringFormats.TopLeft);
            //        y += 20;

            //        gfx.DrawString($"Total barbero: {r.totalEmpleado}", font, XBrushes.Black, new XRect(50, y, pagina.Width, 20), XStringFormats.TopLeft);
            //        y += 20;
            //    }

            //    if (r.obsequios is IEnumerable<dynamic> obsequios && obsequios.Any())
            //    {
            //        //gfx.DrawString($"Total servicios obsequio: {r.totalObsequios}", font, XBrushes.Black, new XRect(50, y, pagina.Width, 20), XStringFormats.TopLeft);
            //        //y += 20;

            //        gfx.DrawString($"Total Ingreso obsequio: {r.totalPlataObsequios}", font, XBrushes.Black, new XRect(50, y, pagina.Width, 20), XStringFormats.TopLeft);
            //        y += 20;
            //    }

            //    if (r.descanso is IEnumerable<dynamic> descanso && descanso.Any())
            //    {
            //        //gfx.DrawString($"Total servicio descanso: {r.totaldescanso}", font, XBrushes.Black, new XRect(50, y, pagina.Width, 20), XStringFormats.TopLeft);
            //        //y += 20;

            //        gfx.DrawString($"Total ingreso descanso: {r.totalPlataDescanso}", font, XBrushes.Black, new XRect(50, y, pagina.Width, 20), XStringFormats.TopLeft);
            //        y += 20;
            //    }

            //    if (r.totalServicios > 0)
            //    {
            //        gfx.DrawString($"Total ingreso Barbero: {r.totalIngresoBarber}", fontBold, XBrushes.Black, new XRect(50, y, pagina.Width, 20), XStringFormats.TopLeft);
            //    }

            //    y += 30;
            //}

            //doc.Save(stream, false);

            //return stream.ToArray();
        }
    }
}
