namespace SandboxSync.Models;

/// <summary>
/// Normalized client-area rectangle (0..1) where clicks are not synchronized.
/// </summary>
public sealed class ExcludeRegion
{
    public double X { get; set; }

    public double Y { get; set; }

    public double Width { get; set; }

    public double Height { get; set; }

    public bool ContainsNormalized(double nx, double ny) =>
        nx >= X && nx <= X + Width && ny >= Y && ny <= Y + Height;
}
