using System.Globalization;
using System.Net;

namespace Server.Arkaine.Admin
{
    public static class ProcessingReportHtmlRenderer
    {
        private const string Styles = """
            :root { color-scheme: light dark; font-family: system-ui, sans-serif; }
            body { max-width: 1100px; margin: 0 auto; padding: 2rem; line-height: 1.45; }
            h1, h2 { line-height: 1.2; }
            .summary { display: grid; grid-template-columns: repeat(auto-fit, minmax(10rem, 1fr)); gap: .75rem; margin: 1.5rem 0 2rem; }
            .summary div { padding: .75rem; border: 1px solid #8886; border-radius: .35rem; }
            .summary dt { color: #888; font-size: .8rem; }
            .summary dd { margin: .2rem 0 0; font-weight: 600; overflow-wrap: anywhere; }
            .table-wrap { overflow-x: auto; }
            table { width: 100%; border-collapse: collapse; }
            th, td { padding: .65rem; border: 1px solid #8886; text-align: left; vertical-align: top; }
            th { background: #8882; }
            td { overflow-wrap: anywhere; }
            .status-error { color: #d33; font-weight: 600; }
            .status-converted { color: #287a42; font-weight: 600; }
            .status-skipped { color: #996d00; font-weight: 600; }
            .empty { color: #888; }
            """;

        public static string RenderThumbnail(GenerationReport report)
        {
            var rows = report.Failures.Count == 0
                ? "<p class=\"empty\">No thumbnail errors or failures were recorded.</p>"
                : $"""
                    <div class="table-wrap">
                        <table>
                            <thead>
                                <tr>
                                    <th>File</th>
                                    <th>File ID</th>
                                    <th>Type</th>
                                    <th>Content type</th>
                                    <th>Size</th>
                                    <th>Error</th>
                                </tr>
                            </thead>
                            <tbody>
                                {string.Join(Environment.NewLine, report.Failures.Select(RenderThumbnailFailure))}
                            </tbody>
                        </table>
                    </div>
                    """;

            return Document(
                "Thumbnail generation report",
                RenderThumbnailSummary(report),
                "Errors and failures",
                rows,
                report.FinishedUtc ?? report.StartedUtc);
        }

        public static string RenderConversion(ConversionReport report)
        {
            var reportFiles = report.Files
                .Where(file => !string.Equals(file.Status, "skipped", StringComparison.OrdinalIgnoreCase))
                .ToList();
            var rows = reportFiles.Count == 0
                ? "<p class=\"empty\">No converted files or errors were recorded.</p>"
                : $"""
                    <div class="table-wrap">
                        <table>
                            <thead>
                                <tr>
                                    <th>Source file</th>
                                    <th>Target file</th>
                                    <th>Status</th>
                                    <th>File ID</th>
                                    <th>Type</th>
                                    <th>Content type</th>
                                    <th>Size</th>
                                    <th>Details</th>
                                </tr>
                            </thead>
                            <tbody>
                                {string.Join(Environment.NewLine, reportFiles.Select(RenderConversionFile))}
                            </tbody>
                        </table>
                    </div>
                    """;

            return Document(
                "Media conversion report",
                RenderConversionSummary(report),
                "Processed files",
                rows,
                report.FinishedUtc ?? report.StartedUtc);
        }

        private static string Document(
            string title,
            string summary,
            string tableTitle,
            string table,
            DateTimeOffset? createdUtc)
        {
            return $"""
                <!doctype html>
                <html lang="en">
                <head>
                    <meta charset="utf-8">
                    <meta name="viewport" content="width=device-width, initial-scale=1">
                    <title>{Encode(title)} - {Encode(FormatDate(createdUtc))}</title>
                    <style>{Styles}</style>
                </head>
                <body>
                    <h1>{Encode(title)}</h1>
                    <p>Generated {Encode(FormatDate(createdUtc))}</p>
                    <h2>Summary</h2>
                    <dl class="summary">{summary}</dl>
                    <h2>{Encode(tableTitle)}</h2>
                    {table}
                </body>
                </html>
                """;
        }

        private static string RenderThumbnailSummary(GenerationReport report)
        {
            return string.Join(
                Environment.NewLine,
                SummaryItem("Status", report.Status),
                SummaryItem("Started", FormatDate(report.StartedUtc)),
                SummaryItem("Finished", FormatDate(report.FinishedUtc)),
                SummaryItem("Scanned", report.Scanned.ToString("N0", CultureInfo.InvariantCulture)),
                SummaryItem("Generated", report.Generated.ToString("N0", CultureInfo.InvariantCulture)),
                SummaryItem("Failed", report.Failed.ToString("N0", CultureInfo.InvariantCulture)),
                SummaryItem("Cancelled", report.Cancelled ? "Yes" : "No"),
                SummaryItem("Run error", report.Error));
        }

        private static string RenderConversionSummary(ConversionReport report)
        {
            return string.Join(
                Environment.NewLine,
                SummaryItem("Status", report.Status),
                SummaryItem("Path", report.Path),
                SummaryItem("Started", FormatDate(report.StartedUtc)),
                SummaryItem("Finished", FormatDate(report.FinishedUtc)),
                SummaryItem("Scanned", report.Scanned.ToString("N0", CultureInfo.InvariantCulture)),
                SummaryItem("Converted", report.Converted.ToString("N0", CultureInfo.InvariantCulture)),
                SummaryItem("Skipped", report.Skipped.ToString("N0", CultureInfo.InvariantCulture)),
                SummaryItem("Failed", report.Failed.ToString("N0", CultureInfo.InvariantCulture)),
                SummaryItem("Cancelled", report.Cancelled ? "Yes" : "No"),
                SummaryItem("Run error", report.Error));
        }

        private static string RenderThumbnailFailure(ThumbnailFailure failure)
        {
            return $"""
                <tr>
                    <td>{Encode(failure.FileName)}</td>
                    <td>{Encode(failure.FileId)}</td>
                    <td>{Encode(failure.Type)}</td>
                    <td>{Encode(failure.ContentType)}</td>
                    <td>{Encode(failure.Size)}</td>
                    <td class="status-error">{Encode(failure.Error)}</td>
                </tr>
                """;
        }

        private static string RenderConversionFile(ConversionFileResult file)
        {
            var statusClass = file.Status switch
            {
                "converted" => "status-converted",
                "skipped" => "status-skipped",
                "error" => "status-error",
                _ => string.Empty
            };

            return $"""
                <tr>
                    <td>{Encode(file.SourceFile)}</td>
                    <td>{Encode(file.TargetFile)}</td>
                    <td class="{statusClass}">{Encode(file.Status)}</td>
                    <td>{Encode(file.FileId)}</td>
                    <td>{Encode(file.Type)}</td>
                    <td>{Encode(file.ContentType)}</td>
                    <td>{Encode(file.Size)}</td>
                    <td>{Encode(file.Details)}</td>
                </tr>
                """;
        }

        private static string SummaryItem(string label, string? value)
        {
            return $"""
                <div>
                    <dt>{Encode(label)}</dt>
                    <dd>{Encode(string.IsNullOrWhiteSpace(value) ? "None" : value)}</dd>
                </div>
                """;
        }

        private static string FormatDate(DateTimeOffset? value)
        {
            return value?.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss.fff 'UTC'", CultureInfo.InvariantCulture)
                ?? "Not recorded";
        }

        private static string Encode(string? value)
        {
            return WebUtility.HtmlEncode(value ?? string.Empty);
        }
    }
}
