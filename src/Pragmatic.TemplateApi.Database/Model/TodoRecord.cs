namespace Pragmatic.TemplateApi.Database.Model;

public class TodoRecord
{
    public TodoRecord() { }

    public Guid ItemId { get; set; }

    required public string Title { get; set; }

    required public string Description { get; set; }

    public DateTimeOffset DueDate { get; set; }

    public bool Open { get; set; }

    public DateTimeOffset? ClosedDate { get; set; }

    public long Version { get; set; }

    public static TodoRecord Create(Guid itemId, string? title, string? description, DateTimeOffset dueDate, bool open = true, DateTimeOffset? closedDate = null, long version = 0)
    {
        return new TodoRecord
        {
            ItemId = itemId,
            Title = title ?? string.Empty,
            Description = description ?? string.Empty,
            DueDate = dueDate,
            Open = open,
            ClosedDate = closedDate,
            Version = version,
        };
    }
}
