# ReprintTool

A small Windows desktop tool that finds specific pages in large PDF files by a unique number and merges them into a single PDF, ready to be reprinted.

## Why

When a printer damages a few pages out of thousands, the pages have to be found, extracted and merged by hand: search every large PDF for each number, extract the page, then combine them in another program. ReprintTool does this in one step across as many files as needed.

## How it works

1. Drag PDF files or a folder into the window (or use the buttons).
2. Type the numbers to find, separated by commas, spaces or new lines.
3. Click **Napravi reprint** ("Create reprint").
4. The tool searches every file, lists which page was found where, and saves one merged PDF to the Desktop.

Numbers that are not found are reported, and pages in the output follow the order in which the numbers were entered.

## Features

- Searches multiple large PDFs in one run
- Matches whole numbers only (a search for `1234` will not match `51234`)
- Merged output saved to the Desktop with a timestamp, so nothing gets overwritten
- Single `.exe`, no installation needed

## Build from source

Requires the .NET 10 SDK on Windows.

```
dotnet run
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

## Built with

- C# / .NET 10, Windows Forms
- [PdfPig](https://github.com/UglyToad/PdfPig) for reading and merging PDF pages