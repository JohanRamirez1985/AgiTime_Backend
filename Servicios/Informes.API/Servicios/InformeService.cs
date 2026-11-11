using Compartido.Constantes;
using Compartido.Data;
using Compartido.Data.Modelos;
using Compartido.DTOs;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using System.Globalization;
using System.IO;
using System.Net.Http;
using Microsoft.EntityFrameworkCore;

namespace Informes.API.Servicios
{
    public class InformeService
    {
        public async Task<byte[]> GenerarInformeDiario(List<dynamic> resumenUsuarios, string fecha, string nombreEmpresa, string? rutaLogo = null, bool mostrarComision = true)
        {
            using var stream = new MemoryStream();
            var doc = new PdfDocument();

            PdfPage pagina = doc.AddPage();
            XGraphics gfx = XGraphics.FromPdfPage(pagina);

            var fontSmall = new XFont("OpenSans", 6, XFontStyleEx.Regular);
            var font = new XFont("OpenSans", 9, XFontStyleEx.Regular);
            var fontBold = new XFont("OpenSans", 9, XFontStyleEx.Bold);
            var fontTitle = new XFont("OpenSans", 14, XFontStyleEx.Bold);

            double y = 0;

            // =========================
            // CONFIG TABLA
            // =========================
            double tableLeft = 40;
            double tableRight = pagina.Width - 40;

            double colNombre = 45;
            double colDetalle = 200;
            double colValor = 430;
            double colComision = 495;
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
                        var img = XImage.FromStream(msImg);

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
                gfx.DrawString($"Colaborador: {r.nombre}", fontBold, XBrushes.Black,
                    colNombre, y);
                y += filaAlto;

                gfx.DrawString($"Total servicios: {r.totalServicios}", font,
                    XBrushes.Black, colNombre, y);
                y += filaAlto;

                gfx.DrawString("Servicios", fontBold, XBrushes.Black,
                    colNombre, y);
                if (mostrarComision)
                    gfx.DrawString("Comisión", fontBold, XBrushes.Black,
                        colComision, y);
                y += filaAlto;

                foreach (var s in r.servicios)
                {
                    var porcentajeTexto = mostrarComision
                        ? $" ({((decimal)s.porcentaje).ToString("0.##")}%)"
                        : "";
                    gfx.DrawString($"• {s.servicio}{porcentajeTexto}{(!string.IsNullOrWhiteSpace(s.metodoPago) ? "  -   " + s.metodoPago : "")}", font, XBrushes.Black,
                        colDetalle, y);
                    var esBono = s.bono is bool b && b;
                    var valorTexto = Moneda((decimal)s.valor) + (esBono ? " *" : "");
                    gfx.DrawString(valorTexto, font,
                        XBrushes.Black, colValor, y);
                    if (mostrarComision)
                        gfx.DrawString(Moneda((decimal)s.comision), font,
                            XBrushes.Black, colComision, y);
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
                decimal totalVentas = 0;
                //if (r.comisionVentas > 0)
                if (r.comisionVentas is IEnumerable<dynamic> venta && venta.Any())
                {
                    gfx.DrawString("Productos", fontBold, XBrushes.Black,
                            colNombre, y);
                    //gfx.DrawString(Moneda((decimal)r.comisionVentas), font,
                    //    XBrushes.Black, colValor, y);
                    //y += filaAlto;
                    y += filaAlto;

                    foreach (var d in venta)
                    {
                        gfx.DrawString($"• {d.producto}", font, XBrushes.Black,
                            colDetalle, y);
                        gfx.DrawString(Moneda((decimal)d.valor), font,
                            XBrushes.Black, colValor, y);
                        y += filaAlto;
                        totalVentas += (decimal)d.valor;
                    }
                }

                decimal totalBarbero = (r.totalPlata ?? 0) + (r.totalPlataObsequios ?? 0) + (r.totalPlataDescanso ?? 0) + (totalVentas);
                totalGeneralDia += totalBarbero;

                // ===== Línea limpia antes del total =====
                //gfx.DrawLine(lineLight, tableLeft, y, tableRight, y);
                y += 5;

                gfx.DrawString("Total Colaborador", fontBold, XBrushes.Black,
                    colDetalle, y);
                gfx.DrawString(Moneda(totalBarbero), fontBold, XBrushes.Black,
                    colValor, y);
                if (mostrarComision)
                    gfx.DrawString(Moneda((decimal)(r.totalComision ?? 0m)), fontBold, XBrushes.Black,
                        colComision, y);

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
            //var font = new XFont("OpenSans", 10, XFontStyleEx.Regular);
            //var fontBold = new XFont("OpenSans", 10, XFontStyleEx.Bold);
            //var fontTitle = new XFont("OpenSans", 14, XFontStyleEx.Bold);
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
            //        var img = XImage.FromStream(msImg);

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

        public async Task<byte[]> GenerarInformeSemanal(List<dynamic> resumenUsuarios, string fechaIni, string fechaFin, string nombreEmpresa, int cliente, string? rutaLogo = null, List<TotalSedeDTO>? totalesSede = null)
        {
            try
            {
                using var stream = new MemoryStream();
                var doc = new PdfDocument();

                PdfPage pagina = doc.AddPage();
                XGraphics gfx = XGraphics.FromPdfPage(pagina);

                var fontSmall = new XFont("OpenSans", 6, XFontStyleEx.Regular);
                var font = new XFont("OpenSans", 9, XFontStyleEx.Regular);
                var fontBold = new XFont("OpenSans", 9, XFontStyleEx.Bold);
                var fontTitle = new XFont("OpenSans", 14, XFontStyleEx.Bold);

                double y = 0;

                // =========================
                // CONFIG TABLA
                // =========================
                double tableLeft = 40;
                double tableRight = pagina.Width - 40;
                double filaAlto = 18;

                // =========================
                // CONFIG COLUMNAS
                // =========================
                bool mostrarIngreso = true;
                bool mostrarBarberia = true;
                bool mostrarEmpleado = true;
                bool mostrarObsequio = true;
                bool mostrarDescanso = true;
                bool mostrarTotalSedes = true;
                bool mostrarTotalBonos = true;
                bool mostrarComision = true;

                if ( cliente == 8 || cliente == 11 || cliente == 12 || cliente == 14)
                {
                    mostrarObsequio = false;
                    mostrarDescanso = false;
                    mostrarTotalSedes = false;
                    mostrarTotalBonos = false;
                }

                // =========================
                // COLUMNAS DINÁMICAS
                // =========================
                var columnas = new List<(string Nombre, double Width)>
                {
                    ("Colaborador", 100),
                    ("Servicios", 50)
                };

                if (mostrarIngreso)
                    columnas.Add(("Ingreso", 55));

                if (mostrarBarberia)
                    columnas.Add(("Negocio", 55));

                if (mostrarEmpleado)
                    columnas.Add(("Empleado", 55));

                if (mostrarObsequio)
                    columnas.Add(("Obsequios", 55));

                if (mostrarDescanso)
                    columnas.Add(("Descanso", 55));

                if (mostrarComision)
                    columnas.Add(("Comision", 55));

                columnas.Add(("Total", 60));

                // =========================
                // POSICIONES
                // =========================
                var posiciones = new Dictionary<string, double>();

                double posicionActual = tableLeft;

                foreach (var col in columnas)
                {
                    posiciones[col.Nombre] = posicionActual;
                    posicionActual += col.Width;
                }

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
                            var img = XImage.FromStream(msImg);

                            gfx.DrawImage(img, tableLeft, 20, 60, 60);
                        }
                        catch { }
                    }

                    gfx.DrawString(nombreEmpresa, fontTitle, XBrushes.DarkBlue,
                        new XRect(0, 20, pagina.Width, 25), XStringFormats.TopCenter);

                    gfx.DrawString($"Resumen {fechaIni} al {fechaFin}",
                        fontBold, XBrushes.Black,
                        new XRect(0, 45, pagina.Width, 20), XStringFormats.TopCenter);

                    y = headerHeight;
                }

                void DibujarEncabezadoTabla()
                {
                    foreach (var col in columnas)
                    {
                        gfx.DrawString(
                            col.Nombre,
                            fontBold,
                            XBrushes.Black,
                            posiciones[col.Nombre],
                            y
                        );
                    }

                    y += filaAlto;
                    y += 5;
                }

                // =========================
                // INICIO DOCUMENTO
                // =========================
                DibujarEncabezado();
                DibujarEncabezadoTabla();

                int totalGeneralServicios = 0;
                decimal totalGeneral = 0;
                decimal totalGeneralIngreso = 0;
                decimal totalGeneralBarberia = 0;
                decimal totalGeneralEmpleado = 0;
                decimal totalGeneralObsequio = 0;
                decimal totalGeneralDescanso = 0;
                decimal totalGeneralComision = 0;

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
                        decimal comisionVentas = r.comisionVentas ?? 0;

                        totalGeneralServicios += totalServicios;
                        totalGeneral += totalBarbero;
                        totalGeneralIngreso += ingreso;
                        totalGeneralBarberia += barberia;
                        totalGeneralEmpleado += empleado;
                        totalGeneralObsequio += obsequio;
                        totalGeneralDescanso += descanso;
                        totalGeneralComision += comisionVentas;

                        var valorTexto = totalBonos > 0 ? totalServicios.ToString() + " (" + totalBonos.ToString() + ")*" : totalServicios.ToString();

                        gfx.DrawString(r.nombre,font,XBrushes.Black,posiciones["Colaborador"],y);
                        gfx.DrawString(valorTexto,font,XBrushes.Black,posiciones["Servicios"],y);

                        if (mostrarIngreso)
                        {
                            gfx.DrawString(Moneda(ingreso),font,XBrushes.Black,posiciones["Ingreso"],y);
                        }

                        if (mostrarBarberia)
                        {
                            gfx.DrawString(Moneda(barberia),font,XBrushes.Black,posiciones["Negocio"],y);
                        }

                        if (mostrarEmpleado)
                        {
                            gfx.DrawString(Moneda(empleado),font,XBrushes.Black,posiciones["Empleado"],y);
                        }

                        if (mostrarObsequio)
                        {
                            gfx.DrawString("(" + r.totalObsequios + ") " + Moneda(obsequio),font,XBrushes.Black,posiciones["Obsequios"],y);
                        }

                        if (mostrarDescanso)
                        {
                            gfx.DrawString("(" + r.totaldescanso + ") " + Moneda(descanso),font,XBrushes.Black,posiciones["Descanso"],y);
                        }

                        if (mostrarComision)
                        {
                            gfx.DrawString(Moneda(comisionVentas), font, XBrushes.Black, posiciones["Comision"], y);
                        }

                        gfx.DrawString(Moneda(totalBarbero),fontBold,XBrushes.Black,posiciones["Total"],y);

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

                gfx.DrawString("Total General",fontBold,XBrushes.Black,posiciones["Colaborador"],y);
                gfx.DrawString(totalGeneralServicios.ToString(), fontBold, XBrushes.Black, posiciones["Servicios"], y);

                if (mostrarIngreso)
                {
                    gfx.DrawString(Moneda(totalGeneralIngreso),font,XBrushes.Black,posiciones["Ingreso"],y);
                }

                if (mostrarBarberia)
                {
                    gfx.DrawString(Moneda(totalGeneralBarberia),font,XBrushes.Black,posiciones["Negocio"],y);
                }

                if (mostrarEmpleado)
                {
                    gfx.DrawString(Moneda(totalGeneralEmpleado),font,XBrushes.Black,posiciones["Empleado"],y);
                }

                if (mostrarObsequio)
                {
                    gfx.DrawString(Moneda(totalGeneralObsequio),font,XBrushes.Black,posiciones["Obsequios"],y);
                }

                if (mostrarDescanso)
                {
                    gfx.DrawString(Moneda(totalGeneralDescanso),font,XBrushes.Black,posiciones["Descanso"],y);
                }

                if (mostrarComision)
                {
                    gfx.DrawString(Moneda(totalGeneralComision), font, XBrushes.Black, posiciones["Comision"], y);
                }

                gfx.DrawString(Moneda(totalGeneral),fontBold,XBrushes.Black,posiciones["Total"],y);

                if (mostrarTotalSedes)
                {
                    List<TotalSedeDTO> totalesPorSede;

                    if (totalesSede?.Any() == true)
                    {
                        totalesPorSede = totalesSede;
                    }
                    else
                    {
                        var nombresExcluir = new[]
                        {
                            "José Daniel Quintero",
                            "Juan Felipe Quintero"
                        };

                        totalesPorSede = resumenUsuarios
                            .Where(r => cliente != 2 || !nombresExcluir.Contains((string)r.nombre))
                        .GroupBy(r => r.sede)
                        .Select(g => new TotalSedeDTO
                        {
                            Sede = g.Key,
                            TotalSer = g.Sum(x => (int)(x.totalServicios ?? 0)),
                            TotalBar = g.Sum(x => (decimal)(x.totalBarber ?? 0)),
                            TotalBon = g.Sum(x => (x.totalBonos ?? 0))
                        })
                        .ToList();
                    }

                    ValidarSalto(40);

                    // =========================
                    // TOTAL SEDE
                    // =========================
                    y += 20;


                    foreach (var sede in totalesPorSede)
                    {
                        ValidarSalto(filaAlto);

                        var valorTexto = sede.TotalBon > 0 ? sede.TotalSer.ToString() + " (" + sede.TotalBon.ToString() + ")*" : sede.TotalSer.ToString();

                        gfx.DrawString(sede.Sede, fontBold, XBrushes.Black, posiciones["Colaborador"], y);

                        gfx.DrawString(valorTexto, font, XBrushes.Black, posiciones["Servicios"], y);

                        if (mostrarBarberia)
                        {
                            gfx.DrawString(Moneda(sede.TotalBar), fontBold, XBrushes.Black, posiciones["Negocio"], y);
                        }

                        y += filaAlto;
                    }
                }

                if (mostrarTotalBonos)
                {
                    y += 10;
                    gfx.DrawString($"(*) Cantidad de servicios que fueron cubiertos mediante bono.", fontSmall, XBrushes.Black, posiciones["Colaborador"], y);
                }

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
            //var font = new XFont("OpenSans", 10, XFontStyleEx.Regular);
            //var fontBold = new XFont("OpenSans", 10, XFontStyleEx.Bold);
            //var fontTitle = new XFont("OpenSans", 14, XFontStyleEx.Bold);
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
            //        var img = XImage.FromStream(msImg);

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

        public async Task<byte[]> GenerarInformeSemanalBarber(List<dynamic> resumenUsuarios, string fechaIni, string fechaFin, string nombreEmpresa, int cliente, string? rutaLogo = null)
        {
            using var stream = new MemoryStream();
            var doc = new PdfDocument();

            PdfPage pagina = doc.AddPage();
            XGraphics gfx = XGraphics.FromPdfPage(pagina);

            var fontSmall = new XFont("OpenSans", 6, XFontStyleEx.Regular);
            var font = new XFont("OpenSans", 9, XFontStyleEx.Regular);
            var fontBold = new XFont("OpenSans", 9, XFontStyleEx.Bold);
            var fontTitle = new XFont("OpenSans", 14, XFontStyleEx.Bold);

            double y = 0;

            // =========================
            // CONFIG TABLA
            // =========================
            double tableLeft = 40;
            double tableRight = pagina.Width - 40;
            double filaAlto = 18;

            // =========================
            // CONFIG COLUMNAS
            // =========================
            bool mostrarEmpleado = true;
            bool mostrarObsequio = true;
            bool mostrarDescanso = true;
            bool mostrarTotalBonos = true;
            bool mostrarComision = true;

            if (cliente == 8 || cliente == 11 || cliente == 12 || cliente == 14)
            {
                mostrarObsequio = false;
                mostrarDescanso = false;
                mostrarTotalBonos = false;
            }

            // =========================
            // COLUMNAS DINÁMICAS
            // =========================
            var columnas = new List<(string Nombre, double Width)>
            {
                ("Colaborador", 100),
                ("Servicios", 50)
            };

            if (mostrarEmpleado)
                columnas.Add(("Empleado", 90));

            if (mostrarObsequio)
                columnas.Add(("Obsequios", 90));

            if (mostrarDescanso)
                columnas.Add(("Descanso", 90));

            if (mostrarComision)
                columnas.Add(("Comision", 90));

            columnas.Add(("Total", 90));

            // =========================
            // POSICIONES
            // =========================
            var posiciones = new Dictionary<string, double>();

            double posicionActual = tableLeft;

            foreach (var col in columnas)
            {
                posiciones[col.Nombre] = posicionActual;
                posicionActual += col.Width;
            }

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
                        var img = XImage.FromStream(msImg);

                        gfx.DrawImage(img, tableLeft, 20, 60, 60);
                    }
                    catch { }
                }

                gfx.DrawString(nombreEmpresa, fontTitle, XBrushes.DarkBlue,
                    new XRect(0, 20, pagina.Width, 25), XStringFormats.TopCenter);

                gfx.DrawString($"Resumen {fechaIni} al {fechaFin}",
                    fontBold, XBrushes.Black,
                    new XRect(0, 45, pagina.Width, 20), XStringFormats.TopCenter);

                //gfx.DrawLine(lineLight, tableLeft, 80, tableRight, 80);
                y = headerHeight;
            }

            void DibujarEncabezadoTabla()
            {
                foreach (var col in columnas)
                {
                    gfx.DrawString(
                        col.Nombre,
                        fontBold,
                        XBrushes.Black,
                        posiciones[col.Nombre],
                        y
                    );
                }

                y += filaAlto;
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
                decimal comisionVentas = r.comisionVentas ?? 0;

                totalGeneral += totalBarbero;

                var valorTexto = totalBonos > 0 ? totalServicios.ToString() + " (" + totalBonos.ToString() + ")*" : totalServicios.ToString();

                gfx.DrawString(r.nombre, font, XBrushes.Black, posiciones["Colaborador"], y);
                gfx.DrawString(valorTexto, font, XBrushes.Black, posiciones["Servicios"], y);

                if (mostrarEmpleado)
                {
                    gfx.DrawString(Moneda(servicios), font, XBrushes.Black, posiciones["Empleado"], y);
                }

                if (mostrarObsequio)
                {
                    gfx.DrawString(Moneda(obsequios), font, XBrushes.Black, posiciones["Obsequios"], y);
                }

                if (mostrarDescanso)
                {
                    gfx.DrawString(Moneda(descanso), font, XBrushes.Black, posiciones["Descanso"], y);
                }

                if (mostrarComision)
                {
                    gfx.DrawString(Moneda(comisionVentas), font, XBrushes.Black, posiciones["Comision"], y);
                }

                gfx.DrawString(Moneda(totalBarbero), fontBold, XBrushes.Black, posiciones["Total"], y);

                y += filaAlto;
                y += 3;
            }

            // =========================
            // TOTAL GENERAL
            // =========================
            ValidarSalto(40);

            gfx.DrawLine(lineDark, tableLeft, y, tableRight, y);
            y += 10;

            gfx.DrawString("Total General", fontBold, XBrushes.Black, posiciones["Empleado"], y);

            gfx.DrawString(Moneda(totalGeneral), fontBold, XBrushes.Black, posiciones["Total"], y);

            if (mostrarTotalBonos)
            {
                y += 10;
                gfx.DrawString($"(*) Cantidad de servicios que fueron cubiertos mediante bono.", fontSmall, XBrushes.Black, posiciones["Colaborador"], y);
            }

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
            //var font = new XFont("OpenSans", 10, XFontStyleEx.Regular);
            //var fontBold = new XFont("OpenSans", 10, XFontStyleEx.Bold);
            //var fontTitle = new XFont("OpenSans", 14, XFontStyleEx.Bold);
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
            //        var img = XImage.FromStream(msImg);

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

        public async Task<byte[]> GenerarReporteVentas(List<dynamic> ventas, string fechaIni, string fechaFin, string nombreEmpresa, int cliente, string? rutaLogo = null, List<TotalSedeDTO>? totalesSede = null)
        {
            try
            {
                using var stream = new MemoryStream();
                var doc = new PdfDocument();

                PdfPage pagina = doc.AddPage();
                XGraphics gfx = XGraphics.FromPdfPage(pagina);

                var fontSmall = new XFont("OpenSans", 6, XFontStyleEx.Regular);
                var font = new XFont("OpenSans", 8, XFontStyleEx.Regular);
                var fontBold = new XFont("OpenSans", 8, XFontStyleEx.Bold);
                var fontTitle = new XFont("OpenSans", 14, XFontStyleEx.Bold);

                double y = 0;

                double tableLeft = 20;
                double tableRight = pagina.Width - 20;
                double filaAlto = 18;

                var columnas = new List<(string Nombre, double Width)>
        {
            //("Fecha", 70),
            ("Producto", 110),
            ("Vendedor", 110),
            ("Cantidad", 45),
            ("Venta", 60),
            ("Costo", 60),
            ("Descuento", 60),
            ("Comision", 60),
            ("Ganancia", 60)
        };

                var posiciones = new Dictionary<string, double>();

                double posicionActual = tableLeft;

                foreach (var col in columnas)
                {
                    posiciones[col.Nombre] = posicionActual;
                    posicionActual += col.Width;
                }

                var lineDark = new XPen(XColors.Black, 1.2);

                string Moneda(decimal valor) =>
                    valor.ToString("C0", new CultureInfo("es-CO"));

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
                            var img = XImage.FromStream(msImg);

                            gfx.DrawImage(img, tableLeft, 20, 60, 60);
                        }
                        catch
                        {
                        }
                    }

                    gfx.DrawString(
                        nombreEmpresa,
                        fontTitle,
                        XBrushes.DarkBlue,
                        new XRect(0, 20, pagina.Width, 25),
                        XStringFormats.TopCenter);

                    gfx.DrawString(
                        $"Reporte de Ventas {fechaIni} al {fechaFin}",
                        fontBold,
                        XBrushes.Black,
                        new XRect(0, 45, pagina.Width, 20),
                        XStringFormats.TopCenter);

                    y = headerHeight;
                }

                void DibujarEncabezadoTabla()
                {
                    foreach (var col in columnas)
                    {
                        gfx.DrawString(
                            col.Nombre,
                            fontBold,
                            XBrushes.Black,
                            posiciones[col.Nombre],
                            y);
                    }

                    y += filaAlto;
                    y += 5;
                }

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

                DibujarEncabezado();
                DibujarEncabezadoTabla();

                int totalCantidad = 0;
                decimal totalVenta = 0;
                decimal totalCosto = 0;
                decimal totalDescuento = 0;
                decimal totalComision = 0;
                decimal totalGanancia = 0;

                foreach (var r in ventas)
                {
                    ValidarSalto(filaAlto);

                    decimal venta = Convert.ToDecimal(r.Venta);
                    decimal costo = Convert.ToDecimal(r.Costo);
                    decimal descuento = Convert.ToDecimal(r.Descuento);
                    decimal comision = Convert.ToDecimal(r.Comision);
                    decimal ganancia = Convert.ToDecimal(r.Ganancia);

                    int cantidad = Convert.ToInt32(r.Cantidad);

                    totalCantidad += cantidad;
                    totalVenta += venta;
                    totalCosto += costo;
                    totalDescuento += descuento;
                    totalComision += comision;
                    totalGanancia += ganancia;

                    DateTime fechaVenta = Convert.ToDateTime(r.FechaVenta);

                    //gfx.DrawString(
                    //    fechaVenta.ToString("dd/MM/yyyy"),
                    //    font,
                    //    XBrushes.Black,
                    //    posiciones["Fecha"],
                    //    y);

                    gfx.DrawString(
                        Convert.ToString(r.Producto),
                        font,
                        XBrushes.Black,
                        posiciones["Producto"],
                        y);

                    gfx.DrawString(
                        Convert.ToString(r.Vendedor),
                        font,
                        XBrushes.Black,
                        posiciones["Vendedor"],
                        y);

                    gfx.DrawString(
                        cantidad.ToString(),
                        font,
                        XBrushes.Black,
                        posiciones["Cantidad"],
                        y);

                    gfx.DrawString(
                        Moneda(venta),
                        font,
                        XBrushes.Black,
                        posiciones["Venta"],
                        y);

                    gfx.DrawString(
                        Moneda(costo),
                        font,
                        XBrushes.Black,
                        posiciones["Costo"],
                        y);

                    gfx.DrawString(
                        Moneda(descuento),
                        font,
                        XBrushes.Black,
                        posiciones["Descuento"],
                        y);

                    gfx.DrawString(
                        Moneda(comision),
                        font,
                        XBrushes.Black,
                        posiciones["Comision"],
                        y);

                    gfx.DrawString(
                        Moneda(ganancia),
                        fontBold,
                        XBrushes.Black,
                        posiciones["Ganancia"],
                        y);

                    y += filaAlto;
                }

                ValidarSalto(50);

                gfx.DrawLine(lineDark, tableLeft, y, tableRight, y);

                y += 10;

                gfx.DrawString(
                    "TOTAL",
                    fontBold,
                    XBrushes.Black,
                    posiciones["Producto"],
                    y);

                gfx.DrawString(
                    totalCantidad.ToString(),
                    fontBold,
                    XBrushes.Black,
                    posiciones["Cantidad"],
                    y);

                gfx.DrawString(
                    Moneda(totalVenta),
                    fontBold,
                    XBrushes.Black,
                    posiciones["Venta"],
                    y);

                gfx.DrawString(
                    Moneda(totalCosto),
                    fontBold,
                    XBrushes.Black,
                    posiciones["Costo"],
                    y);

                gfx.DrawString(
                    Moneda(totalDescuento),
                    fontBold,
                    XBrushes.Black,
                    posiciones["Descuento"],
                    y);

                gfx.DrawString(
                    Moneda(totalComision),
                    fontBold,
                    XBrushes.Black,
                    posiciones["Comision"],
                    y);

                gfx.DrawString(
                    Moneda(totalGanancia),
                    fontBold,
                    XBrushes.Black,
                    posiciones["Ganancia"],
                    y);

                doc.Save(stream, false);

                return stream.ToArray();
            }
            catch
            {
                return null;
            }
        }

        public async Task<byte[]> GenerarReporteInventarioProducto(List<ProductoDTO> producto, string nombreEmpresa, int cliente, string? rutaLogo = null)
        {
            try
            {
                using var stream = new MemoryStream();
                var doc = new PdfDocument();

                PdfPage pagina = doc.AddPage();
                XGraphics gfx = XGraphics.FromPdfPage(pagina);

                var font = new XFont("OpenSans", 8, XFontStyleEx.Regular);
                var fontBold = new XFont("OpenSans", 8, XFontStyleEx.Bold);
                var fontTitle = new XFont("OpenSans", 14, XFontStyleEx.Bold);

                double y = 0;

                double tableLeft = 20;
                double tableRight = pagina.Width - 20;
                double filaAlto = 18;

                var columnas = new List<(string Nombre, double Width)>
    {
                ("Producto",150),
                ("Stock",30),
                ("P. Venta",40),
                ("T. P. Venta", 50),
                ("P. Costo",40),
                ("T. P. Costo", 50),
                ("Comision",50),
                ("T. Comision", 50),
                ("Ganancia",50),
                ("T. Ganancia",50)
    };

                var posiciones = new Dictionary<string, double>();

                double pos = tableLeft;

                foreach (var c in columnas)
                {
                    posiciones[c.Nombre] = pos;
                    pos += c.Width;
                }

                var lineDark = new XPen(XColors.Black, 1.2);

                string Moneda(decimal valor) =>
                    valor.ToString("C0", new CultureInfo("es-CO"));

                void DibujarEncabezado()
                {
                    double headerHeight = 90;

                    if (!string.IsNullOrEmpty(rutaLogo))
                    {
                        try
                        {
                            using var http = new HttpClient();
                            var bytes = http.GetByteArrayAsync(rutaLogo).Result;

                            using var ms = new MemoryStream(bytes);
                            var img = XImage.FromStream(ms);

                            gfx.DrawImage(img, tableLeft, 20, 60, 60);
                        }
                        catch { }
                    }

                    gfx.DrawString(
                        nombreEmpresa,
                        fontTitle,
                        XBrushes.DarkBlue,
                        new XRect(0, 20, pagina.Width, 25),
                        XStringFormats.TopCenter);

                    gfx.DrawString(
                        "Reporte Utilidad Inventario",
                        fontBold,
                        XBrushes.Black,
                        new XRect(0, 45, pagina.Width, 20),
                        XStringFormats.TopCenter);

                    y = headerHeight;
                }

                void DibujarEncabezadoTabla()
                {
                    foreach (var c in columnas)
                    {
                        gfx.DrawString(
                            c.Nombre,
                            fontBold,
                            XBrushes.Black,
                            posiciones[c.Nombre],
                            y);
                    }

                    y += filaAlto + 5;
                }

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
                        NuevaPagina();
                }

                DibujarEncabezado();
                DibujarEncabezadoTabla();

                decimal totalVenta = 0;
                decimal totalTVenta = 0;
                decimal totalCosto = 0;
                decimal totalTCosto = 0;
                decimal totalPorcentaje = 0;
                decimal totalTPorcentaje = 0;
                decimal totalGanancia = 0;
                decimal totalTGanancia = 0;
                int totalStock = 0;

                foreach (var p in producto)
                {
                    bool primeraLinea = true;

                    foreach (var lote in p.preciosCosto)
                    {
                        ValidarSalto(filaAlto);

                        int stock = lote.cantidad;
                        decimal venta = p.PrecioVenta;
                        decimal tVenta = p.PrecioVenta * stock;
                        decimal costo = lote.precioCosto;
                        decimal tCosto = lote.precioCosto * stock;
                        decimal porcentaje = p.Porcentaje;
                        decimal tPorcentaje = p.Porcentaje * stock;
                        decimal ganancia = venta - costo - porcentaje;
                        decimal tGanancia = (venta - costo - porcentaje) * stock;

                        totalStock += stock;
                        totalVenta += venta;
                        totalTVenta += tVenta;
                        totalCosto += costo;
                        totalTCosto += tCosto;
                        totalPorcentaje += porcentaje;
                        totalTPorcentaje += tPorcentaje;
                        totalGanancia += ganancia;
                        totalTGanancia += tGanancia;

                        // Solo escribe el nombre del producto en la primera línea
                        gfx.DrawString(
                            primeraLinea ? p.Nombre : "",
                            font,
                            XBrushes.Black,
                            posiciones["Producto"],
                            y);

                        gfx.DrawString(
                            stock.ToString(),
                            font,
                            XBrushes.Black,
                            posiciones["Stock"],
                            y);

                        gfx.DrawString(
                            Moneda(venta),
                            font,
                            XBrushes.Black,
                            posiciones["P. Venta"],
                            y);

                        gfx.DrawString(
                            Moneda(tVenta),
                            font,
                            XBrushes.Black,
                            posiciones["T. P. Venta"],
                            y);

                        gfx.DrawString(
                            Moneda(costo),
                            font,
                            XBrushes.Black,
                            posiciones["P. Costo"],
                            y);

                        gfx.DrawString(
                           Moneda(tCosto),
                           font,
                           XBrushes.Black,
                           posiciones["T. P. Costo"],
                           y);

                        gfx.DrawString(
                            Moneda(porcentaje),
                            font,
                            XBrushes.Black,
                            posiciones["Comision"],
                            y);

                        gfx.DrawString(
                            Moneda(tPorcentaje),
                            font,
                            XBrushes.Black,
                            posiciones["T. Comision"],
                            y);

                        gfx.DrawString(
                            Moneda(ganancia),
                            fontBold,
                            XBrushes.Black,
                            posiciones["Ganancia"],
                            y);

                        gfx.DrawString(
                            Moneda(tGanancia),
                            fontBold,
                            XBrushes.Black,
                            posiciones["T. Ganancia"],
                            y);

                        y += filaAlto;

                        primeraLinea = false;
                    }
                }

                ValidarSalto(40);

                gfx.DrawLine(lineDark, tableLeft, y, tableRight, y);

                y += 10;

                gfx.DrawString(
                    "TOTAL",
                    fontBold,
                    XBrushes.Black,
                    posiciones["Producto"],
                    y);

                gfx.DrawString(
                    totalStock.ToString(),
                    fontBold,
                    XBrushes.Black,
                    posiciones["Stock"],
                    y);

                gfx.DrawString(
                    Moneda(totalVenta),
                    fontBold,
                    XBrushes.Black,
                    posiciones["P. Venta"],
                    y);

                gfx.DrawString(
                    Moneda(totalTVenta),
                    fontBold,
                    XBrushes.Black,
                    posiciones["T. P. Venta"],
                    y);

                gfx.DrawString(
                    Moneda(totalCosto),
                    fontBold,
                    XBrushes.Black,
                    posiciones["P. Costo"],
                    y);

                gfx.DrawString(
                    Moneda(totalTCosto),
                    fontBold,
                    XBrushes.Black,
                    posiciones["T. P. Costo"],
                    y);

                gfx.DrawString(
                    Moneda(totalPorcentaje),
                    fontBold,
                    XBrushes.Black,
                    posiciones["Comision"],
                    y);

                gfx.DrawString(
                    Moneda(totalTPorcentaje),
                    fontBold,
                    XBrushes.Black,
                    posiciones["T. Comision"],
                    y);

                gfx.DrawString(
                    Moneda(totalGanancia),
                    fontBold,
                    XBrushes.Black,
                    posiciones["Ganancia"],
                    y);

                gfx.DrawString(
                    Moneda(totalTGanancia),
                    fontBold,
                    XBrushes.Black,
                    posiciones["T. Ganancia"],
                    y);

                doc.Save(stream, false);

                return stream.ToArray();
            }
            catch
            {
                return null;
            }
        }

        public decimal GetDecimal(Parametro? parametro)
        {
            if (parametro == null)
                return 0m;

            if (decimal.TryParse(parametro.valor, out var resultado))
                return resultado;

            return 0m;
        }

        public int GetInt(Parametro? parametro)
        {
            if (parametro == null)
                return 0;

            if (int.TryParse(parametro.valor, out var resultado))
                return resultado;

            return 0;
        }

        public decimal GetValorDescuento(List<InfoCitaDTO> info, List<Parametro> parametros, List<int> listaBarberos, Usuario u)
        {
            int total = 0;
            decimal valorDescuentoPorCorte = 0;

            var pagas = info.Where(i => i.pagoCita == true && i.pagoServicio == true).ToList();

            total = pagas.Count;

            foreach (var item in pagas)
            {
                if (item.servicio.Equals("Corte VIP"))
                {
                    total++;
                }
            }

            bool validaBarberos = listaBarberos.Any() && listaBarberos.Contains(u.id);

            if (validaBarberos)
                valorDescuentoPorCorte = u.incumplimiento ?? 0m;
            else
                valorDescuentoPorCorte = u.porcentaje ?? 0m;

            return valorDescuentoPorCorte;
        }

        public decimal ObtenerPorcentajeOcupacion(DateTime fechaInicial, DateTime fechaFinal, ModeloContext ContextBD)
        {
            /*var horariosAgenda = ContextBD.Agenda
                .Where(a => a.estado)
                .OrderBy(a => a.valor)
                .Select(a => a.valor)
                .ToList();

            int totalDias = (fechaFinal.Date - fechaInicial.Date).Days + 1;

            int totalDisponibles = horariosAgenda.Count * totalDias;

            var citas = ContextBD.Citas
                .Where(c =>
                    c.fechaInicio >= fechaInicial &&
                    c.fechaFin <= fechaFinal &&
                    c.estado != Estados.Cancelada)
                .ToList();

            List<string> espaciosOcupados = new List<string>();

            foreach (var cita in citas)
            {
                DateTime inicio = cita.fechaInicio;
                DateTime fin = cita.fechaFin.Value;

                while (inicio < fin)
                {
                    espaciosOcupados.Add(
                        inicio.ToString("yyyy-MM-dd HH:mm")
                    );

                    inicio = inicio.AddMinutes(30);
                }
            }

            int totalOcupados = espaciosOcupados
                .Distinct()
                .Count();

            decimal porcentaje = totalDisponibles == 0
                ? 0
                : ((decimal)totalOcupados / totalDisponibles) * 100;

            return Math.Round(porcentaje, 2);*/

            int rango = Convert.ToInt32(ContextBD.Parametros.Where(p => p.tipo == "Tiempo_Agenda").Select(p => p.valor).FirstOrDefault());

            var horariosAgenda = ContextBD.Agenda
        .Where(a => a.estado)
        .OrderBy(a => a.valor)
        .Select(a => a.valor)
        .ToList();

            List<DateTime> espaciosDisponibles = new List<DateTime>();

            DateTime fecha = fechaInicial.Date;

            while (fecha <= fechaFinal.Date)
            {
                foreach (var hora in horariosAgenda)
                {
                    DateTime fechaHora = DateTime.Parse(
                        $"{fecha:yyyy-MM-dd} {hora}"
                    );

                    espaciosDisponibles.Add(fechaHora);
                }

                fecha = fecha.AddDays(1);
            }

            var bloqueos = ContextBD.BloqueosHorarios
                .Where(b => b.estado == "Activo")
                .ToList();

            List<DateTime> espaciosBloqueados = new List<DateTime>();

            fecha = fechaInicial.Date;

            while (fecha <= fechaFinal.Date)
            {
                int diaSemana = (int)fecha.DayOfWeek;

                diaSemana = diaSemana == 0 ? 7 : diaSemana;

                foreach (var bloqueo in bloqueos)
                {
                    if (string.IsNullOrWhiteSpace(bloqueo.diasSemana))
                        continue;

                    List<int> dias = bloqueo.diasSemana
                        .Split(',')
                        .Select(x => int.Parse(x.Trim()))
                        .ToList();

                    if (!dias.Contains(diaSemana))
                        continue;

                    DateTime horaInicio = fecha.Date + bloqueo.horaInicio;
                    DateTime horaFin = fecha.Date + bloqueo.horaFin;

                    while (horaInicio < horaFin)
                    {
                        espaciosBloqueados.Add(horaInicio);

                        horaInicio = horaInicio.AddMinutes(rango);
                    }
                }

                fecha = fecha.AddDays(1);
            }

            int totalDisponibles = espaciosDisponibles
                .Except(espaciosBloqueados)
                .Distinct()
                .Count();

            var citas = ContextBD.Citas
                .Where(c =>
                    c.fechaInicio >= fechaInicial &&
                    c.fechaFin <= fechaFinal &&
                    c.estado != Estados.Cancelada)
                .ToList();

            List<DateTime> espaciosOcupados = new List<DateTime>();

            foreach (var cita in citas)
            {
                DateTime inicio = cita.fechaInicio;
                DateTime fin = cita.fechaFin.Value;

                while (inicio < fin)
                {
                    espaciosOcupados.Add(inicio);

                    inicio = inicio.AddMinutes(rango);
                }
            }

            int totalOcupados = espaciosOcupados
                .Distinct()
                .Count();

            decimal porcentaje = totalDisponibles == 0
                ? 0
                : ((decimal)totalOcupados / totalDisponibles) * 100;

            return Math.Round(porcentaje, 2);
        }

        public async Task<object> ObtenerValorDiario(ModeloContext ContextBD)
        {
            var zonaColombia = TimeZoneInfo.FindSystemTimeZoneById("America/Bogota");

            var ahoraColombia = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, zonaColombia);

            var hoy = ahoraColombia.Date;

            int diasDesdeLunes = ((int)hoy.DayOfWeek == 0 ? 6 : (int)hoy.DayOfWeek - 1);

            var lunesActual = hoy.AddDays(-diasDesdeLunes);

            var domingoActual = lunesActual.AddDays(7);

            var lunesPasado = lunesActual.AddDays(-7);

            var ingresos = await ContextBD.Citas
                .Where(c =>
                    c.fechaInicio >= lunesPasado &&
                    c.fechaInicio < domingoActual &&
                    c.estado == Estados.Asistio)
                .GroupBy(c => c.fechaInicio.Date)
                .Select(g => new
                {
                    fecha = g.Key,
                    total = g.Sum(x => x.valor)
                })
                .ToListAsync();

            var semanaActual = Enumerable.Range(0, 7)
                .Select(i =>
                {
                    var fecha = lunesActual.AddDays(i);

                    return new CitaDTO
                    {
                        dia = fecha.ToString("dddd", new CultureInfo("es-CO")),
                        totalDia = ingresos
                            .FirstOrDefault(x => x.fecha == fecha)?.total ?? 0
                    };
                }).ToList();

            var semanaPasada = Enumerable.Range(0, 7)
                .Select(i =>
                {
                    var fecha = lunesPasado.AddDays(i);

                    return new CitaDTO
                    {
                        dia = fecha.ToString("dddd", new CultureInfo("es-CO")),
                        totalDia = ingresos
                            .FirstOrDefault(x => x.fecha == fecha)?.total ?? 0
                    };
                }).ToList();

            return new
            {
                semanaActual,
                semanaPasada
            };
        }
    }
}
