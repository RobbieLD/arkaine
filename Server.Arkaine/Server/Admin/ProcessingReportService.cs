using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace Server.Arkaine.Admin
{
    public sealed class ProcessingReportService : IProcessingReportService
    {
        private readonly ArkaineDbContext _context;

        public ProcessingReportService(ArkaineDbContext context)
        {
            _context = context;
        }

        public async Task SaveAsync(
            ProcessingReportType type,
            DateTimeOffset createdUtc,
            string html,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(html))
            {
                throw new ArgumentException("Report HTML must be provided.", nameof(html));
            }

            var created = createdUtc.ToUniversalTime();
            _context.ProcessingReports.Add(new ProcessingReport
            {
                Type = ToTypeName(type),
                Name = created.ToString("yyyy-MM-dd HH:mm:ss.fff 'UTC'", CultureInfo.InvariantCulture),
                CreatedUtc = created,
                Html = html
            });

            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<ProcessingReportSummary>> ListAsync(CancellationToken cancellationToken)
        {
            return await _context.ProcessingReports
                .AsNoTracking()
                .OrderByDescending(report => report.CreatedUtc)
                .ThenByDescending(report => report.Id)
                .Select(report => new ProcessingReportSummary(
                    report.Id,
                    report.Type,
                    report.Name,
                    report.CreatedUtc))
                .ToListAsync(cancellationToken);
        }

        public async Task<StoredProcessingReport?> GetAsync(int id, CancellationToken cancellationToken)
        {
            return await _context.ProcessingReports
                .AsNoTracking()
                .Where(report => report.Id == id)
                .Select(report => new StoredProcessingReport(
                    report.Id,
                    report.Type,
                    report.Name,
                    report.CreatedUtc,
                    report.Html))
                .SingleOrDefaultAsync(cancellationToken);
        }

        public async Task ClearAsync(CancellationToken cancellationToken)
        {
            var reports = await _context.ProcessingReports.ToListAsync(cancellationToken);
            _context.ProcessingReports.RemoveRange(reports);
            await _context.SaveChangesAsync(cancellationToken);
        }

        private static string ToTypeName(ProcessingReportType type)
        {
            return type switch
            {
                ProcessingReportType.Thumbnail => "thumbnail",
                ProcessingReportType.Conversion => "conversion",
                _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown processing report type.")
            };
        }
    }
}
