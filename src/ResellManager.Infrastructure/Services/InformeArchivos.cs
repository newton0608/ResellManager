using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using ResellManager.Application.DTOs;
using SkiaSharp;

namespace ResellManager.Infrastructure.Services;

internal static class InformeArchivos
{
    internal static ArchivoInformeDto Crear(string nombre, string formato, string[] columnas, IReadOnlyList<object?[]> filas, DateTime generadoUtc, string contexto, DateTime? corteUtc = null)
    {
        var meta = $"Generado UTC: {generadoUtc:O}. Corte de registro: {corteUtc ?? generadoUtc:O}. {contexto}";
        return formato == "xlsx" ? new(nombre + ".xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", Excel(columnas, filas, meta))
            : new(nombre + ".pdf", "application/pdf", Pdf(nombre, columnas, filas, meta));
    }

    private static byte[] Excel(string[] columnas, IReadOnlyList<object?[]> filas, string meta)
    {
        using var memoria = new MemoryStream();
        using (var zip = new ZipArchive(memoria, ZipArchiveMode.Create, true))
        {
            void Escribir(string ruta, string texto) { using var writer = new StreamWriter(zip.CreateEntry(ruta).Open(), new UTF8Encoding(false)); writer.Write(texto); }
            Escribir("[Content_Types].xml", "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/><Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/></Types>");
            Escribir("_rels/.rels", "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/></Relationships>");
            Escribir("xl/workbook.xml", "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets><sheet name=\"Informe\" sheetId=\"1\" r:id=\"rId1\"/></sheets></workbook>");
            Escribir("xl/_rels/workbook.xml.rels", "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/></Relationships>");
            XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            var data = new XElement(ns + "sheetData"); var indice = 0;
            foreach (var valores in new[] { new object?[] { meta }, columnas.Cast<object?>().ToArray() }.Concat(filas))
            {
                var row = new XElement(ns + "row", new XAttribute("r", ++indice));
                for (var i = 0; i < valores.Length; i++)
                {
                    var valor = valores[i]; var celda = new XElement(ns + "c", new XAttribute("r", Columna(i) + indice));
                    if (valor is decimal or int or long) celda.Add(new XElement(ns + "v", Convert.ToString(valor, CultureInfo.InvariantCulture)));
                    else
                    {
                        // inlineStr never executes text as a formula, including =, +, - and @ prefixes.
                        celda.Add(new XAttribute("t", "inlineStr"), new XElement(ns + "is", new XElement(ns + "t",
                            new XAttribute(XNamespace.Xml + "space", "preserve"), Limpiar(Texto(valor)))));
                    }
                    row.Add(celda);
                }
                data.Add(row);
            }
            Escribir("xl/worksheets/sheet1.xml", new XDocument(new XElement(ns + "worksheet", data)).ToString());
        }
        return memoria.ToArray();
    }

    private static string Columna(int n) { var s = ""; for (n++; n > 0; n = (n - 1) / 26) s = (char)('A' + (n - 1) % 26) + s; return s; }
    private static string Limpiar(string texto) => new(texto.Where(c => c >= ' ' || c is '\n' or '\r' or '\t').ToArray());
    private static string Texto(object? valor) => valor switch
    { DateOnly d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), DateTime d => d.ToString("O", CultureInfo.InvariantCulture), decimal d => d.ToString("0.00", CultureInfo.InvariantCulture), _ => valor?.ToString() ?? "" };

    private static byte[] Pdf(string nombre, string[] columnas, IReadOnlyList<object?[]> filas, string meta)
    {
        using var memoria = new MemoryStream(); using var doc = SKDocument.CreatePdf(memoria);
        using var fuente = new SKFont(SKTypeface.FromFamilyName("DejaVu Sans"), 10);
        using var pintura = new SKPaint { Color = SKColors.Black, IsAntialias = true };
        SKCanvas? canvas = null; var y = 0f; var pagina = 0;
        void Pagina()
        {
            if (canvas is not null) doc.EndPage();
            canvas = doc.BeginPage(595, 842); y = 35; pagina++;
            canvas.DrawText($"ResellManager · {nombre} · {pagina}", 30, y, SKTextAlign.Left, fuente, pintura); y += 22;
        }
        void Linea(string texto)
        {
            foreach (var raw in texto.Replace("\r", "").Split('\n'))
            {
                var resto = raw;
                do
                {
                    if (canvas is null || y > 805) Pagina();
                    var largo = resto.Length;
                    while (largo > 0 && fuente.MeasureText(resto[..largo]) > 535) largo--;
                    largo = Math.Max(1, largo);
                    canvas!.DrawText(resto[..Math.Min(largo, resto.Length)], 30, y, SKTextAlign.Left, fuente, pintura); y += 14;
                    resto = resto[Math.Min(largo, resto.Length)..];
                } while (resto.Length > 0);
            }
        }
        Linea(meta); Linea("");
        foreach (var fila in filas)
        {
            for (var i = 0; i < fila.Length; i++) Linea($"{(i < columnas.Length ? columnas[i] : "Dato")}: {Texto(fila[i])}");
            Linea("────────────────────────────────────────────────────────────────────");
        }
        if (canvas is not null) doc.EndPage(); doc.Close(); return memoria.ToArray();
    }
}
