using Syncfusion.Drawing;
using Syncfusion.Pdf;
using Syncfusion.Pdf.Graphics;

var repoRoot = FindRepoRoot();
var fontPath = Path.Combine(repoRoot, "UniversalLIMS", "wwwroot", "fonts", "arial.ttf");
var outputPath = Path.Combine(repoRoot, "docs", "assets", "templates", "REF-MOZ-001.pdf");

if (!File.Exists(fontPath))
{
    Console.Error.WriteLine($"Шрифт не знайдено: {fontPath}");
    return 1;
}

Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

using var document = new PdfDocument();
var page = document.Pages.Add();
var graphics = page.Graphics;

var titleFont = new PdfTrueTypeFont(fontPath, 14f, PdfFontStyle.Bold);
var labelFont = new PdfTrueTypeFont(fontPath, 10f, PdfFontStyle.Bold);
var textFont = new PdfTrueTypeFont(fontPath, 10f, PdfFontStyle.Regular);
var smallFont = new PdfTrueTypeFont(fontPath, 8f, PdfFontStyle.Regular);
var pen = new PdfPen(Color.Black, 0.5f);

const float left = 40f;
const float right = 555f;
const float width = right - left;

DrawCentered("НАПРАВЛЕННЯ", 52f, titleFont);
DrawCentered("на лабораторне дослідження зразків (пілот ЦКПХ)", 72f, textFont);
DrawCentered("Форма REF-MOZ-001 — спрощений макет для цифрового заповнення", 88f, smallFont);

DrawLabel("№ справи:", 112f);
DrawLine(130f, 112f, 220f);

DrawLabel("від", 112f, 250f);
DrawLine(270f, 112f, 120f);

DrawLabel("Замовник (ПІБ / найменування):", 148f);
DrawLine(148f, 220f, width - 220f);

DrawLabel("Адреса:", 172f);
DrawLine(172f, 110f, width - 110f);

DrawLabel("Телефон:", 196f);
DrawLine(196f, 110f, 180f);

DrawLabel("Найменування зразка / матеріалу:", 232f);
DrawLine(232f, 250f, width - 250f);

DrawLabel("Місце відбору:", 256f);
DrawLine(256f, 130f, width - 130f);

DrawLabel("Дата відбору:", 280f);
DrawLine(280f, 110f, 110f);
DrawLabel("Час:", 280f, 240f);
DrawLine(280f, 280f, 80f);

DrawLabel("Мета дослідження:", 316f);
DrawBox(336f, 72f);

DrawLabel("Показники для дослідження:", 416f);
DrawBox(436f, 72f);

DrawLabel("№ проби (LIMS):", 516f);
DrawLine(516f, 130f, 180f);

DrawLabel("Лабораторія (підрозділ):", 540f);
DrawLine(540f, 170f, width - 170f);

DrawLabel("Примітки:", 564f);
DrawLine(564f, 90f, width - 90f);

DrawLabel("Підпис замовника", 700f);
DrawLine(700f, left, 150f);
DrawLabel("Підпис реєстратора", 700f, 280f);
DrawLine(700f, 280f, 150f);

DrawFooter(
    "Макет пілоту UniversalLIMS. Офіційний бланк МОЗ можна замінити через Upload нової версії шаблону REF-MOZ-001.");

using (var stream = File.Create(outputPath))
{
    document.Save(stream);
}

Console.WriteLine($"Збережено: {outputPath}");
return 0;

void DrawCentered(string text, float y, PdfFont font)
{
    var size = font.MeasureString(text);
    var x = left + (width - size.Width) / 2f;
    graphics.DrawString(text, font, PdfBrushes.Black, new PointF(x, y));
}

void DrawLabel(string text, float y, float x = left)
{
    graphics.DrawString(text, labelFont, PdfBrushes.Black, new PointF(x, y));
}

void DrawLine(float y, float x, float lineWidth)
{
    graphics.DrawLine(pen, x, y + 12f, x + lineWidth, y + 12f);
}

void DrawBox(float y, float height)
{
    graphics.DrawRectangle(pen, left, y, width, height);
}

void DrawFooter(string text)
{
    graphics.DrawString(text, smallFont, PdfBrushes.Gray, new RectangleF(left, 780f, width, 40f));
}

static string FindRepoRoot()
{
    var candidates = new[]
    {
        Directory.GetCurrentDirectory(),
        AppContext.BaseDirectory,
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."))
    };

    foreach (var start in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
    {
        var dir = new DirectoryInfo(start);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "UniversalLIMS", "wwwroot", "fonts", "arial.ttf"))
                && Directory.Exists(Path.Combine(dir.FullName, "docs", "data")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }
    }

    throw new InvalidOperationException("Не знайдено корінь репозиторію UniversalLIMS.");
}
