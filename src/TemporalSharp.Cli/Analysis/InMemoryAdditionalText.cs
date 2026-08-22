using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace TemporalSharp.Cli.Analysis;

/// <summary>
/// An <see cref="AdditionalText"/> backed by an in-memory string, used to hand a
/// solution-level reachability set to the analyzers.
/// </summary>
internal sealed class InMemoryAdditionalText : AdditionalText
{
    private readonly SourceText _text;

    public InMemoryAdditionalText(string path, string content)
    {
        Path = path;
        _text = SourceText.From(content);
    }

    public override string Path { get; }

    public override SourceText? GetText(CancellationToken cancellationToken = default) => _text;
}
