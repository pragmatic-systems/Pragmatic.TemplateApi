using System;
using System.Threading;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using Hangfire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Pragmatic.CQRS;
using Pragmatic.TemplateApi.Core.Handlers;
using Pragmatic.TemplateApi.Core.Model;
using Pragmatic.TemplateApi.Instrumentation;

namespace Pragmatic.TemplateApi.Api.Controllers;

[ApiController]
[Route("todo-list")]
public class TodoListController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly AmazonS3Client _s3;
    private readonly StorageOptions _storage;

    public TodoListController(IMediator mediator, AmazonS3Client s3, IOptions<StorageOptions> storage)
    {
        _mediator = mediator;
        _s3 = s3;
        _storage = storage.Value;
    }

    [HttpGet("v1")]
    [Authorize(Policy = Roles.TodoListRead)]
    [EnableRateLimiting("Basic")]
    public async Task<IActionResult> GetItems(CancellationToken ct)
    {
        var result = await _mediator.Send(new SelectTodo(), ct);
        return Ok(result);
    }

    [HttpPost("v1")]
    [Authorize(Policy = Roles.TodoListWrite)]
    [EnableRateLimiting("Basic")]
    public async Task<IActionResult> InsertItem(InsertTodo insert, CancellationToken ct)
    {
        var result = await _mediator.Send(insert, ct);
        return Ok(result);
    }

    [HttpPost("v1/upload")]
    [Authorize(Policy = Roles.TodoListWrite)]
    [EnableRateLimiting("Basic")]
    public async Task<IActionResult> UploadCsv(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest("No file uploaded.");
        }

        if (!file.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest("Only CSV files are supported.");
        }

        var objectKey = $"csv-imports/{Guid.NewGuid()}-{file.FileName}";

        using var stream = file.OpenReadStream();
        await _s3.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _storage.Bucket,
            Key = objectKey,
            InputStream = stream,
        });

        // Queue the background job to process the CSV
        BackgroundJob.Enqueue<CsvImportJobHandler>(
            handler => handler.ImportCsv(objectKey));

        var response = new CsvUploadResult(objectKey, "CSV uploaded and queued for import.");
        return Ok(response);
    }
}
