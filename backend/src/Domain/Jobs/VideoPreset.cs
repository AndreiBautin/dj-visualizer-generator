using DjVisualizer.Domain.Exceptions;

namespace DjVisualizer.Domain.Jobs;

public sealed class VideoPreset
{
    public static readonly VideoPreset FullHd1080p = new("1080p", 1920, 1080);
    public static readonly VideoPreset Hd720p = new("720p", 1280, 720);

    public string Name { get; }
    public int Width { get; }
    public int Height { get; }

    private VideoPreset(string name, int width, int height)
    {
        Name = name;
        Width = width;
        Height = height;
    }

    public static VideoPreset FromName(string name) => name switch
    {
        "1080p" => FullHd1080p,
        "720p" => Hd720p,
        _ => throw new InvalidVideoPresetException(name),
    };

    public override string ToString() => Name;
}
