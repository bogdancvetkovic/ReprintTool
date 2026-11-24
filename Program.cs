using System.Text;
using System.Text.RegularExpressions;
using Spectre.Console;
using PigDoc = UglyToad.PdfPig.PdfDocument;
using UglyToad.PdfPig.Writer;

Console.OutputEncoding = Encoding.UTF8;

var numbersInput = AnsiConsole.Ask<string>("Brojevi (odvojeni zarezom ili razmakom):");
var numbers = Regex.Matches(numbersInput, @"\d+").Select(m => m.Value).Distinct().ToList();
if (numbers.Count == 0)
{
    AnsiConsole.MarkupLine("[red]Nema unetih brojeva.[/]");
    Pause();
    return;
}

var inputs = args.Select(a => a.Trim().Trim('"')).Where(a => a.Length > 0).ToList();
if (inputs.Count == 0)
    inputs.Add(AnsiConsole.Ask<string>("Putanja do PDF fajla ili foldera:").Trim().Trim('"'));

var files = new List<string>();
foreach (var i in inputs)
{
    if (Directory.Exists(i)) files.AddRange(Directory.EnumerateFiles(i, "*.pdf").OrderBy(f => f));
    else if (File.Exists(i)) files.Add(i);
    else AnsiConsole.MarkupLine($"[red]Ne postoji:[/] {Markup.Escape(i)}");
}
if (files.Count == 0)
{
    Pause();
    return;
}

var patterns = numbers.ToDictionary(n => n, n => new Regex($@"(?<!\d){n}(?!\d)", RegexOptions.Compiled));
var hits = new List<(string Number, string File, int Page)>();

foreach (var file in files)
{
    AnsiConsole.MarkupLine($"Pretražujem [yellow]{Markup.Escape(Path.GetFileName(file))}[/]...");
    try
    {
        using var doc = PigDoc.Open(file);
        foreach (var page in doc.GetPages())
        {
            var text = page.Text;
            foreach (var n in numbers)
                if (patterns[n].IsMatch(text))
                    hits.Add((n, file, page.Number));
        }
    }
    catch (Exception ex)
    {
        AnsiConsole.MarkupLine($"[red]Ne mogu da pročitam fajl:[/] {Markup.Escape(ex.Message)}");
    }
}

var ordered = numbers.SelectMany(n => hits.Where(h => h.Number == n)).ToList();
var notFound = numbers.Where(n => !hits.Any(h => h.Number == n)).ToList();

if (ordered.Count > 0)
{
    var table = new Table().Border(TableBorder.Rounded);
    table.AddColumn("Broj");
    table.AddColumn("Fajl");
    table.AddColumn("Strana");
    foreach (var h in ordered)
        table.AddRow(h.Number, Markup.Escape(Path.GetFileName(h.File)), h.Page.ToString());
    AnsiConsole.Write(table);
}

if (notFound.Count > 0)
    AnsiConsole.MarkupLine($"[bold red]Nije pronađeno:[/] {string.Join(", ", notFound)}");

if (ordered.Count == 0)
{
    Pause();
    return;
}

var outPath = Path.Combine(Path.GetDirectoryName(files[0])!, $"reprint_{DateTime.Now:yyyyMMdd_HHmm}.pdf");
var opened = new Dictionary<string, PigDoc>();
var builder = new PdfDocumentBuilder();

foreach (var h in ordered)
{
    if (!opened.TryGetValue(h.File, out var src))
    {
        src = PigDoc.Open(h.File);
        opened[h.File] = src;
    }
    builder.AddPage(src, h.Page);
}

File.WriteAllBytes(outPath, builder.Build());
foreach (var d in opened.Values) d.Dispose();

AnsiConsole.MarkupLine($"[green]Gotovo.[/] Strana u fajlu: {ordered.Count}");
AnsiConsole.MarkupLine($"Sačuvano: {Markup.Escape(outPath)}");
Pause();

void Pause()
{
    if (!Console.IsInputRedirected)
    {
        AnsiConsole.MarkupLine("\n[grey]Pritisni bilo koji taster za izlaz...[/]");
        Console.ReadKey(true);
    }
}