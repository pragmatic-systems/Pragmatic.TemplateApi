using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pragmatic.TemplateApi.Database;
using Pragmatic.TemplateApi.Database.Model;

namespace Pragmatic.TemplateApi.Instrumentation;

/// <summary>
/// Background job handler that downloads a CSV object from S3-compatible storage
/// and imports the rows as todo records into the database.
/// </summary>
public class CsvImportJobHandler
{
    private readonly AmazonS3Client _s3;
    private readonly StorageOptions _storage;
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<CsvImportJobHandler> _logger;

    public CsvImportJobHandler(
        AmazonS3Client s3,
        IOptions<StorageOptions> storage,
        ApplicationDbContext dbContext,
        ILogger<CsvImportJobHandler> logger)
    {
        _s3 = s3;
        _storage = storage.Value;
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task ImportCsv(string blobName)
    {
        _logger.LogInformation("Starting CSV import for blob: {BlobName}", blobName);

        var getObject = await _s3.GetObjectAsync(new GetObjectRequest
        {
            BucketName = _storage.Bucket,
            Key = blobName,
        });

        using var stream = getObject.ResponseStream;
        using var reader = new StreamReader(stream);

        var lines = new List<string>();
        string? line;
        while ((line = await reader.ReadLineAsync()) != null)
        {
            lines.Add(line);
        }

        // Skip header row if present
        var startIndex = 0;
        if (lines.Count > 0 && (lines[0].Contains("title", StringComparison.OrdinalIgnoreCase)
                                || lines[0].Contains("description", StringComparison.OrdinalIgnoreCase)))
        {
            startIndex = 1;
        }

        var records = new List<TodoRecord>();

        for (var i = startIndex; i < lines.Count; i++)
        {
            var fields = lines[i].Split(',');
            if (fields.Length < 2)
            {
                _logger.LogWarning("Skipping malformed CSV row at line {Line}: {Content}", i + 1, lines[i]);
                continue;
            }

            var title = fields[0].Trim();
            var description = fields[1].Trim();

            // Parse optional due date from third field
            DateTime? dueDate = null;
            if (fields.Length >= 3 && DateTime.TryParse(fields[2].Trim(), out var parsedDate))
            {
                dueDate = parsedDate;
            }

            var record = TodoRecord.Create(
                Guid.NewGuid(),
                title,
                description,
                dueDate ?? DateTime.UtcNow.Date);

            records.Add(record);
        }

        if (records.Count != 0)
        {
            _dbContext.TodoRecords.AddRange(records);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation(
                "Successfully imported {Count} records from CSV blob: {BlobName}",
                records.Count,
                blobName);
        }
        else
        {
            _logger.LogInformation(
                "No valid records found in CSV blob: {BlobName}",
                blobName);
        }

        // Clean up: delete the object after successful import (S3 DeleteObject is idempotent)
        await _s3.DeleteObjectAsync(new DeleteObjectRequest
        {
            BucketName = _storage.Bucket,
            Key = blobName,
        });
        _logger.LogInformation("Deleted blob after import: {BlobName}", blobName);
    }
}
