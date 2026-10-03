namespace Application.Common;

public class PagedResponse<T>
{
    public IEnumerable<T> Items { get; set; } = new List<T>();
    public int TotalCount { get; set; }
    public int PageIndex { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPreviousPage => PageIndex > 1;
    public bool HasNextPage => PageIndex < TotalPages;

    public PagedResponse() { }

    public PagedResponse(IEnumerable<T> items, int count, int pageIndex, int pageSize)
    {
        Items = items;
        TotalCount = count;
        PageIndex = pageIndex;
        PageSize = pageSize;
    }
}
