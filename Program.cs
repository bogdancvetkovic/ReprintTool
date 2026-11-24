using System.Diagnostics;
using System.Text.RegularExpressions;
using UglyToad.PdfPig.Writer;
using PigDoc = UglyToad.PdfPig.PdfDocument;

static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}

class MainForm : Form
{
    readonly ListBox filesBox = new() { AllowDrop = true, Dock = DockStyle.Fill, HorizontalScrollbar = true };
    readonly TextBox numbersBox = new() { Multiline = true, Dock = DockStyle.Fill, ScrollBars = ScrollBars.Vertical };
    readonly TextBox logBox = new() { Multiline = true, ReadOnly = true, Dock = DockStyle.Fill, ScrollBars = ScrollBars.Vertical };
    readonly Button runButton = new() { Text = "Napravi reprint", Dock = DockStyle.Fill };

    public MainForm()
    {
        Text = "ReprintTool";
        ClientSize = new Size(640, 680);
        MinimumSize = new Size(520, 560);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 10);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 7,
            Padding = new Padding(12)
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 40));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 20));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 40));

        layout.Controls.Add(new Label
        {
            Text = "1. Prevuci PDF fajlove ili folder u polje ispod (ili koristi dugmiće):",
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 4)
        }, 0, 0);
        layout.Controls.Add(filesBox, 0, 1);

        var addFiles = new Button { Text = "Dodaj fajlove...", AutoSize = true };
        var addFolder = new Button { Text = "Dodaj folder...", AutoSize = true };
        var clear = new Button { Text = "Očisti listu", AutoSize = true };
        layout.Controls.Add(new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            Controls = { addFiles, addFolder, clear }
        }, 0, 2);

        layout.Controls.Add(new Label
        {
            Text = "2. Upiši brojeve za reprint (odvojeni zarezom, razmakom ili novim redom):",
            AutoSize = true,
            Margin = new Padding(0, 8, 0, 4)
        }, 0, 3);
        layout.Controls.Add(numbersBox, 0, 4);
        layout.Controls.Add(runButton, 0, 5);
        layout.Controls.Add(logBox, 0, 6);
        Controls.Add(layout);

        filesBox.DragEnter += (_, e) =>
            e.Effect = e.Data?.GetDataPresent(DataFormats.FileDrop) == true
                ? DragDropEffects.Copy : DragDropEffects.None;
        filesBox.DragDrop += (_, e) => AddPaths(e.Data?.GetData(DataFormats.FileDrop) as string[]);

        addFiles.Click += (_, _) =>
        {
            using var dlg = new OpenFileDialog { Multiselect = true, Filter = "PDF (*.pdf)|*.pdf" };
            if (dlg.ShowDialog() == DialogResult.OK) AddPaths(dlg.FileNames);
        };
        addFolder.Click += (_, _) =>
        {
            using var dlg = new FolderBrowserDialog();
            if (dlg.ShowDialog() == DialogResult.OK) AddPaths(new[] { dlg.SelectedPath });
        };
        clear.Click += (_, _) => filesBox.Items.Clear();
        runButton.Click += (_, _) => Run();
    }

    void AddPaths(string[]? paths)
    {
        if (paths == null) return;
        foreach (var p in paths)
        {
            bool ok = Directory.Exists(p) ||
                      (File.Exists(p) && p.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase));
            if (ok && !filesBox.Items.Contains(p)) filesBox.Items.Add(p);
        }
    }

    async void Run()
    {
        var numbers = Regex.Matches(numbersBox.Text, @"\d+").Select(m => m.Value).Distinct().ToList();
        var inputs = filesBox.Items.Cast<string>().ToList();

        if (inputs.Count == 0)
        {
            MessageBox.Show("Dodaj bar jedan PDF fajl ili folder.", "ReprintTool");
            return;
        }
        if (numbers.Count == 0)
        {
            MessageBox.Show("Upiši bar jedan broj.", "ReprintTool");
            return;
        }

        runButton.Enabled = false;
        runButton.Text = "Radim...";
        logBox.Clear();
        var progress = new Progress<string>(m => logBox.AppendText(m + Environment.NewLine));

        try
        {
            var outPath = await Task.Run(() => Reprint.Create(inputs, numbers, progress));
            if (outPath != null)
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{outPath}\"")
                { UseShellExecute = true });
            }
        }
        catch (Exception ex)
        {
            logBox.AppendText("GREŠKA: " + ex.Message + Environment.NewLine);
        }
        finally
        {
            runButton.Enabled = true;
            runButton.Text = "Napravi reprint";
        }
    }
}

static class Reprint
{
    public static string? Create(List<string> inputs, List<string> numbers, IProgress<string> log)
    {
        var files = new List<string>();
        foreach (var i in inputs)
        {
            if (Directory.Exists(i))
                files.AddRange(Directory.EnumerateFiles(i, "*.pdf").OrderBy(f => f));
            else if (File.Exists(i))
                files.Add(i);
        }
        files = files.Distinct().ToList();

        var patterns = numbers.ToDictionary(n => n, n => new Regex($@"(?<!\d){n}(?!\d)"));
        var hits = new List<(string Number, string File, int Page)>();

        foreach (var file in files)
        {
            log.Report($"Pretražujem {Path.GetFileName(file)}...");
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
                log.Report($"  Ne mogu da pročitam fajl: {ex.Message}");
            }
        }

        log.Report("");
        var ordered = numbers.SelectMany(n => hits.Where(h => h.Number == n)).ToList();
        foreach (var h in ordered)
            log.Report($"{h.Number}  ->  {Path.GetFileName(h.File)}, strana {h.Page}");

        var notFound = numbers.Where(n => !hits.Any(h => h.Number == n)).ToList();
        if (notFound.Count > 0)
            log.Report($"NIJE PRONAĐENO: {string.Join(", ", notFound)}");

        if (ordered.Count == 0)
        {
            log.Report("Nijedna strana nije pronađena, fajl nije napravljen.");
            return null;
        }

        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        var outPath = Path.Combine(desktop, $"reprint_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");

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

        log.Report("");
        log.Report($"GOTOVO. Strana u fajlu: {ordered.Count}");
        log.Report($"Sačuvano na Desktopu: {Path.GetFileName(outPath)}");
        return outPath;
    }
}