using PdfSharp.Fonts;
using System;
using System.IO;

namespace Informes.API
{
    public class CustomFontResolver : IFontResolver
    {
        public static readonly CustomFontResolver Instance = new CustomFontResolver();

        public byte[] GetFont(string faceName)
        {
            var fontPath = Path.Combine(AppContext.BaseDirectory, "Fonts", "OpenSans-Regular.ttf");

            if (!File.Exists(fontPath))
            {
                throw new FileNotFoundException("No se encontró la fuente en la ruta especificada", fontPath);
            }

            return File.ReadAllBytes(fontPath);
        }

        public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic)
        {
            // Podrías tener más lógica aquí si agregas Negrita o Italic
            return new FontResolverInfo("OpenSans");
        }

        public string DefaultFontName => "OpenSans";
    }
}
