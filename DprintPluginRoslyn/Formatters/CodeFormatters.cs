using Dprint.Plugins.Roslyn.Communication;
using Dprint.Plugins.Roslyn.Configuration;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace Dprint.Plugins.Roslyn.Formatters;

public class CodeFormatters
{
    private readonly ICodeFormatter[] _codeFormatters;
    private readonly OptionSet _options;
    private readonly IReadOnlyList<string> _languageNames;
    private readonly ConcurrentDictionary<string, OptionSet> _editorConfigCache = new();

    public CodeFormatters(ICodeFormatter[] codeFormatters, OptionSet options)
    {
        _codeFormatters = codeFormatters;
        _options = options;
        _languageNames = codeFormatters.Select(f => f.RoslynLanguageName).ToArray();
    }

    public byte[]? FormatCode(string filePath, byte[] code, TextSpan? range, CancellationToken token)
    {
        var formatter = _codeFormatters.FirstOrDefault(formatter => formatter.ShouldFormat(filePath));
        if (formatter is null)
            throw new Exception($"Could not find formatter for file path: {filePath}");
        var sourceText = SourceText.From(
            new MemoryStream(code),
            encoding: null  // Let it auto-detect
        );
        var options = GetOptionsForFile(filePath);
        var result = formatter.FormatText(sourceText, range, options, token);
        return result.SequenceEqual(code) ? null : result;
    }

    private OptionSet GetOptionsForFile(string filePath)
    {
        var dir = Path.GetDirectoryName(Path.GetFullPath(filePath)) ?? string.Empty;
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        var cacheKey = dir + "|" + ext;
        return _editorConfigCache.GetOrAdd(cacheKey, _ =>
        {
            try
            {
                return EditorConfigHelpers.ApplyEditorConfig(filePath, _options, _languageNames);
            }
            catch
            {
                return _options;
            }
        });
    }

    public Dictionary<string, object> GetResolvedConfig()
    {
        var config = new Dictionary<string, object>();
        foreach (var formatter in _codeFormatters)
        {
            foreach (var (key, value) in formatter.GetResolvedConfig(_options))
                config[key] = value;
        }

        return config;
    }
}
