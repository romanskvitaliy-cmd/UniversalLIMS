using System.Diagnostics;
using UniversalLIMS.Application.Templates.Abstractions;

namespace UniversalLIMS.Infrastructure.Templates;

public sealed class LibreOfficeWordToPdfDocumentConverter : IWordToPdfDocumentConverter
{
    private readonly string _executablePath;

    public LibreOfficeWordToPdfDocumentConverter(string executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            throw new ArgumentException("LibreOffice executable path is required.", nameof(executablePath));
        }

        if (!File.Exists(executablePath))
        {
            throw new FileNotFoundException("LibreOffice executable was not found.", executablePath);
        }

        _executablePath = executablePath;
    }

    public static string? ResolveExecutablePath(string? configuredPath)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath) && File.Exists(configuredPath))
        {
            return configuredPath;
        }

        if (OperatingSystem.IsWindows())
        {
            var windowsCandidates = new[]
            {
                @"C:\Program Files\LibreOffice\program\soffice.exe",
                @"C:\Program Files (x86)\LibreOffice\program\soffice.exe"
            };

            return windowsCandidates.FirstOrDefault(File.Exists);
        }

        if (OperatingSystem.IsLinux())
        {
            var linuxCandidates = new[]
            {
                "/usr/bin/soffice",
                "/usr/bin/libreoffice"
            };

            return linuxCandidates.FirstOrDefault(File.Exists);
        }

        if (OperatingSystem.IsMacOS())
        {
            const string macOsCandidate = "/Applications/LibreOffice.app/Contents/MacOS/soffice";
            return File.Exists(macOsCandidate) ? macOsCandidate : null;
        }

        return null;
    }

    public async Task<MemoryStream> ConvertAsync(
        Stream wordDocumentStream,
        string extension,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(wordDocumentStream);

        var normalizedExtension = extension.ToLowerInvariant();
        if (normalizedExtension is not ".doc" and not ".docx")
        {
            throw new InvalidOperationException("LibreOffice converter supports only .doc and .docx files.");
        }

        if (wordDocumentStream.CanSeek)
        {
            wordDocumentStream.Position = 0;
        }

        var tempRoot = Path.Combine(Path.GetTempPath(), "UniversalLIMS", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var inputFileName = $"source{normalizedExtension}";
            var inputPath = Path.Combine(tempRoot, inputFileName);
            await using (var inputFile = File.Create(inputPath))
            {
                await wordDocumentStream.CopyToAsync(inputFile, cancellationToken);
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = _executablePath,
                Arguments = $"--headless --nologo --nofirststartwizard --convert-to pdf --outdir \"{tempRoot}\" \"{inputPath}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                throw new InvalidOperationException("Не вдалося запустити LibreOffice для конвертації Word у PDF.");
            }

            var standardError = await process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            if (process.ExitCode != 0)
            {
                throw new InvalidDataException(
                    string.IsNullOrWhiteSpace(standardError)
                        ? "LibreOffice не зміг конвертувати Word у PDF."
                        : $"LibreOffice не зміг конвертувати Word у PDF: {standardError.Trim()}");
            }

            var outputPath = Path.Combine(tempRoot, Path.ChangeExtension(inputFileName, ".pdf")!);
            if (!File.Exists(outputPath))
            {
                throw new InvalidDataException("LibreOffice не створив PDF-файл після конвертації.");
            }

            var pdfStream = new MemoryStream();
            await using (var outputFile = File.OpenRead(outputPath))
            {
                await outputFile.CopyToAsync(pdfStream, cancellationToken);
            }

            pdfStream.Position = 0;
            return pdfStream;
        }
        finally
        {
            try
            {
                Directory.Delete(tempRoot, recursive: true);
            }
            catch (IOException)
            {
                // ignore temp cleanup issues
            }
            catch (UnauthorizedAccessException)
            {
                // ignore temp cleanup issues
            }
        }
    }
}
