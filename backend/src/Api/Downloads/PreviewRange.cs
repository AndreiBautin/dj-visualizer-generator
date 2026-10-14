using Microsoft.Net.Http.Headers;

namespace DjVisualizer.Api.Downloads;

/// <summary>
/// How many bytes a preview response will actually send, so the egress budget is charged for
/// that rather than for the whole file. Charging a whole file per request let a handful of
/// one-byte range requests spend the instance's entire window. Mirrors what ASP.NET's range
/// processing serves: one satisfiable range is sent as asked; anything it would answer with the
/// whole file (no Range, several ranges, an If-Range precondition, a malformed header) is charged
/// in full, so the charge never undercounts what is sent.
/// </summary>
public static class PreviewRange
{
    public static long BytesToServe(IHeaderDictionary headers, long fileLength)
    {
        if (fileLength <= 0)
        {
            return 0;
        }

        if (headers.ContainsKey(HeaderNames.IfRange)
            || !RangeHeaderValue.TryParse(headers.Range.ToString(), out var range)
            || !string.Equals(range.Unit.Value, "bytes", StringComparison.OrdinalIgnoreCase)
            || range.Ranges.Count != 1)
        {
            return fileLength;
        }

        var item = range.Ranges.First();
        long start, end;
        if (item.From is { } from)
        {
            if (from >= fileLength)
            {
                return 0; // Unsatisfiable: answered 416 with no body.
            }

            start = from;
            end = item.To is { } to ? Math.Min(to, fileLength - 1) : fileLength - 1;
        }
        else if (item.To is { } suffix)
        {
            start = Math.Max(0, fileLength - suffix);
            end = fileLength - 1;
        }
        else
        {
            return fileLength;
        }

        return end < start ? fileLength : end - start + 1;
    }
}
