using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Formatting;
using Microsoft.CodeAnalysis.Formatting;
using NUnit.Framework;
using System.Collections.Generic;
using System.IO;

namespace Dprint.Plugins.Roslyn.Configuration;

[TestFixture]
public class EditorConfigHelpersTests
{
    private string _tempDir = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "dprint-editorconfig-tests-" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private OptionSet DefaultOptions() => new AdhocWorkspace().Options;

    private string WriteEditorConfig(string dir, string content)
    {
        var path = Path.Combine(dir, ".editorconfig");
        File.WriteAllText(path, content);
        return path;
    }

    private string WriteSourceFile(string dir, string name = "test.cs")
    {
        var path = Path.Combine(dir, name);
        File.WriteAllText(path, "");
        return path;
    }

    private static readonly IReadOnlyList<string> AllLanguages = new[]
    {
        LanguageNames.CSharp,
        LanguageNames.VisualBasic,
    };

    [Test]
    public void ApplyEditorConfig_NoEditorConfigFile_ReturnsUnchangedOptions()
    {
        var sourceFile = WriteSourceFile(_tempDir);
        var baseOptions = DefaultOptions();

        var result = EditorConfigHelpers.ApplyEditorConfig(sourceFile, baseOptions, AllLanguages);

        Assert.That(result, Is.SameAs(baseOptions));
    }

    [Test]
    public void ApplyEditorConfig_IndentStyleTab_SetsUseTabs()
    {
        WriteEditorConfig(_tempDir, "[*]\nroot = true\nindent_style = tab\n");
        var sourceFile = WriteSourceFile(_tempDir);
        var baseOptions = DefaultOptions();

        var result = EditorConfigHelpers.ApplyEditorConfig(sourceFile, baseOptions, AllLanguages);

        Assert.That(result.GetOption(FormattingOptions.UseTabs, LanguageNames.CSharp), Is.True);
        Assert.That(result.GetOption(FormattingOptions.UseTabs, LanguageNames.VisualBasic), Is.True);
    }

    [Test]
    public void ApplyEditorConfig_IndentStyleSpace_SetsUseTabsFalse()
    {
        WriteEditorConfig(_tempDir, "[*]\nroot = true\nindent_style = space\n");
        var sourceFile = WriteSourceFile(_tempDir);

        var result = EditorConfigHelpers.ApplyEditorConfig(sourceFile, DefaultOptions(), AllLanguages);

        Assert.That(result.GetOption(FormattingOptions.UseTabs, LanguageNames.CSharp), Is.False);
    }

    [Test]
    public void ApplyEditorConfig_IndentSize_SetsIndentationSizeAndTabSize()
    {
        WriteEditorConfig(_tempDir, "[*]\nroot = true\nindent_size = 2\n");
        var sourceFile = WriteSourceFile(_tempDir);

        var result = EditorConfigHelpers.ApplyEditorConfig(sourceFile, DefaultOptions(), AllLanguages);

        Assert.That(result.GetOption(FormattingOptions.IndentationSize, LanguageNames.CSharp), Is.EqualTo(2));
        Assert.That(result.GetOption(FormattingOptions.TabSize, LanguageNames.CSharp), Is.EqualTo(2));
    }

    [Test]
    public void ApplyEditorConfig_TabWidth_SetsTabSizeOnly()
    {
        WriteEditorConfig(_tempDir, "[*]\nroot = true\nindent_size = 4\ntab_width = 8\n");
        var sourceFile = WriteSourceFile(_tempDir);

        var result = EditorConfigHelpers.ApplyEditorConfig(sourceFile, DefaultOptions(), AllLanguages);

        Assert.That(result.GetOption(FormattingOptions.IndentationSize, LanguageNames.CSharp), Is.EqualTo(4));
        Assert.That(result.GetOption(FormattingOptions.TabSize, LanguageNames.CSharp), Is.EqualTo(8));
    }

    [Test]
    public void ApplyEditorConfig_EndOfLineLf_SetsNewLine()
    {
        WriteEditorConfig(_tempDir, "[*]\nroot = true\nend_of_line = lf\n");
        var sourceFile = WriteSourceFile(_tempDir);

        var result = EditorConfigHelpers.ApplyEditorConfig(sourceFile, DefaultOptions(), AllLanguages);

        Assert.That(result.GetOption(FormattingOptions.NewLine, LanguageNames.CSharp), Is.EqualTo("\n"));
        Assert.That(result.GetOption(FormattingOptions.NewLine, LanguageNames.VisualBasic), Is.EqualTo("\n"));
    }

    [Test]
    public void ApplyEditorConfig_EndOfLineCrlf_SetsNewLine()
    {
        WriteEditorConfig(_tempDir, "[*]\nroot = true\nend_of_line = crlf\n");
        var sourceFile = WriteSourceFile(_tempDir);

        var result = EditorConfigHelpers.ApplyEditorConfig(sourceFile, DefaultOptions(), AllLanguages);

        Assert.That(result.GetOption(FormattingOptions.NewLine, LanguageNames.CSharp), Is.EqualTo("\r\n"));
    }

    [Test]
    public void ApplyEditorConfig_CSharpIndentCaseContentsFalse_SetsOption()
    {
        WriteEditorConfig(_tempDir, "[*.cs]\nroot = true\ncsharp_indent_case_contents = false\n");
        var sourceFile = WriteSourceFile(_tempDir);

        var result = EditorConfigHelpers.ApplyEditorConfig(sourceFile, DefaultOptions(), AllLanguages);

        Assert.That(result.GetOption(CSharpFormattingOptions.IndentSwitchCaseSection), Is.False);
    }

    [Test]
    public void ApplyEditorConfig_CSharpIndentCaseContentsTrue_SetsOption()
    {
        WriteEditorConfig(_tempDir, "[*.cs]\nroot = true\ncsharp_indent_case_contents = true\n");
        var sourceFile = WriteSourceFile(_tempDir);
        // Start from an option set with the default value overridden to false
        var baseOptions = DefaultOptions().WithChangedOption(CSharpFormattingOptions.IndentSwitchCaseSection, false);

        var result = EditorConfigHelpers.ApplyEditorConfig(sourceFile, baseOptions, AllLanguages);

        Assert.That(result.GetOption(CSharpFormattingOptions.IndentSwitchCaseSection), Is.True);
    }

    [Test]
    public void ApplyEditorConfig_LabelPositioning_FlushLeft()
    {
        WriteEditorConfig(_tempDir, "[*.cs]\nroot = true\ncsharp_indent_labels = flush_left\n");
        var sourceFile = WriteSourceFile(_tempDir);

        var result = EditorConfigHelpers.ApplyEditorConfig(sourceFile, DefaultOptions(), AllLanguages);

        Assert.That(result.GetOption(CSharpFormattingOptions.LabelPositioning), Is.EqualTo(LabelPositionOptions.LeftMost));
    }

    [Test]
    public void ApplyEditorConfig_LabelPositioning_NoIndent()
    {
        WriteEditorConfig(_tempDir, "[*.cs]\nroot = true\ncsharp_indent_labels = no_indent\n");
        var sourceFile = WriteSourceFile(_tempDir);

        var result = EditorConfigHelpers.ApplyEditorConfig(sourceFile, DefaultOptions(), AllLanguages);

        Assert.That(result.GetOption(CSharpFormattingOptions.LabelPositioning), Is.EqualTo(LabelPositionOptions.NoIndent));
    }

    [Test]
    public void ApplyEditorConfig_LabelPositioning_OneLessThanCurrent()
    {
        WriteEditorConfig(_tempDir, "[*.cs]\nroot = true\ncsharp_indent_labels = one_less_than_current\n");
        var sourceFile = WriteSourceFile(_tempDir);

        var result = EditorConfigHelpers.ApplyEditorConfig(sourceFile, DefaultOptions(), AllLanguages);

        Assert.That(result.GetOption(CSharpFormattingOptions.LabelPositioning), Is.EqualTo(LabelPositionOptions.OneLess));
    }

    [Test]
    public void ApplyEditorConfig_BraceNewLine_All()
    {
        WriteEditorConfig(_tempDir, "[*.cs]\nroot = true\ncsharp_new_line_before_open_brace = all\n");
        var sourceFile = WriteSourceFile(_tempDir);

        var result = EditorConfigHelpers.ApplyEditorConfig(sourceFile, DefaultOptions(), AllLanguages);

        Assert.That(result.GetOption(CSharpFormattingOptions.NewLinesForBracesInTypes), Is.True);
        Assert.That(result.GetOption(CSharpFormattingOptions.NewLinesForBracesInMethods), Is.True);
        Assert.That(result.GetOption(CSharpFormattingOptions.NewLinesForBracesInControlBlocks), Is.True);
    }

    [Test]
    public void ApplyEditorConfig_BraceNewLine_None()
    {
        WriteEditorConfig(_tempDir, "[*.cs]\nroot = true\ncsharp_new_line_before_open_brace = none\n");
        var sourceFile = WriteSourceFile(_tempDir);

        var result = EditorConfigHelpers.ApplyEditorConfig(sourceFile, DefaultOptions(), AllLanguages);

        Assert.That(result.GetOption(CSharpFormattingOptions.NewLinesForBracesInTypes), Is.False);
        Assert.That(result.GetOption(CSharpFormattingOptions.NewLinesForBracesInMethods), Is.False);
        Assert.That(result.GetOption(CSharpFormattingOptions.NewLinesForBracesInControlBlocks), Is.False);
    }

    [Test]
    public void ApplyEditorConfig_BraceNewLine_SpecificContexts()
    {
        WriteEditorConfig(_tempDir, "[*.cs]\nroot = true\ncsharp_new_line_before_open_brace = types, methods\n");
        var sourceFile = WriteSourceFile(_tempDir);

        var result = EditorConfigHelpers.ApplyEditorConfig(sourceFile, DefaultOptions(), AllLanguages);

        Assert.That(result.GetOption(CSharpFormattingOptions.NewLinesForBracesInTypes), Is.True);
        Assert.That(result.GetOption(CSharpFormattingOptions.NewLinesForBracesInMethods), Is.True);
        Assert.That(result.GetOption(CSharpFormattingOptions.NewLinesForBracesInControlBlocks), Is.False);
    }

    [Test]
    public void ApplyEditorConfig_BinaryOperatorSpacing_BeforeAndAfter()
    {
        WriteEditorConfig(_tempDir, "[*.cs]\nroot = true\ncsharp_space_around_binary_operators = before_and_after\n");
        var sourceFile = WriteSourceFile(_tempDir);

        var result = EditorConfigHelpers.ApplyEditorConfig(sourceFile, DefaultOptions(), AllLanguages);

        Assert.That(result.GetOption(CSharpFormattingOptions.SpacingAroundBinaryOperator), Is.EqualTo(BinaryOperatorSpacingOptions.Single));
    }

    [Test]
    public void ApplyEditorConfig_BinaryOperatorSpacing_None()
    {
        WriteEditorConfig(_tempDir, "[*.cs]\nroot = true\ncsharp_space_around_binary_operators = none\n");
        var sourceFile = WriteSourceFile(_tempDir);

        var result = EditorConfigHelpers.ApplyEditorConfig(sourceFile, DefaultOptions(), AllLanguages);

        Assert.That(result.GetOption(CSharpFormattingOptions.SpacingAroundBinaryOperator), Is.EqualTo(BinaryOperatorSpacingOptions.Remove));
    }

    [Test]
    public void ApplyEditorConfig_SpaceBetweenParentheses_ControlFlowOnly()
    {
        WriteEditorConfig(_tempDir, "[*.cs]\nroot = true\ncsharp_space_between_parentheses = control_flow_statements\n");
        var sourceFile = WriteSourceFile(_tempDir);

        var result = EditorConfigHelpers.ApplyEditorConfig(sourceFile, DefaultOptions(), AllLanguages);

        Assert.That(result.GetOption(CSharpFormattingOptions.SpaceWithinOtherParentheses), Is.True);
        Assert.That(result.GetOption(CSharpFormattingOptions.SpaceWithinExpressionParentheses), Is.False);
        Assert.That(result.GetOption(CSharpFormattingOptions.SpaceWithinCastParentheses), Is.False);
    }

    [Test]
    public void ApplyEditorConfig_SpaceBetweenParentheses_False_DisablesAll()
    {
        WriteEditorConfig(_tempDir, "[*.cs]\nroot = true\ncsharp_space_between_parentheses = false\n");
        var sourceFile = WriteSourceFile(_tempDir);

        var result = EditorConfigHelpers.ApplyEditorConfig(sourceFile, DefaultOptions(), AllLanguages);

        Assert.That(result.GetOption(CSharpFormattingOptions.SpaceWithinOtherParentheses), Is.False);
        Assert.That(result.GetOption(CSharpFormattingOptions.SpaceWithinExpressionParentheses), Is.False);
        Assert.That(result.GetOption(CSharpFormattingOptions.SpaceWithinCastParentheses), Is.False);
    }

    [Test]
    public void ApplyEditorConfig_RootTrue_StopsDirectorySearch()
    {
        // Parent dir has root = true, child dir has no .editorconfig
        var childDir = Path.Combine(_tempDir, "child");
        Directory.CreateDirectory(childDir);

        // Parent .editorconfig with root = true and indent_size = 2
        WriteEditorConfig(_tempDir, "[*]\nroot = true\nindent_size = 2\n");
        var sourceFile = WriteSourceFile(childDir);

        var result = EditorConfigHelpers.ApplyEditorConfig(sourceFile, DefaultOptions(), AllLanguages);

        // Should read the parent (since child has no .editorconfig), and stop at root = true
        Assert.That(result.GetOption(FormattingOptions.IndentationSize, LanguageNames.CSharp), Is.EqualTo(2));
    }

    [Test]
    public void ApplyEditorConfig_ChildOverridesParent()
    {
        // Parent has indent_size = 4, child has indent_size = 2
        var childDir = Path.Combine(_tempDir, "child");
        Directory.CreateDirectory(childDir);

        WriteEditorConfig(_tempDir, "[*]\nroot = true\nindent_size = 4\n");
        WriteEditorConfig(childDir, "[*]\nindent_size = 2\n");
        var sourceFile = WriteSourceFile(childDir);

        var result = EditorConfigHelpers.ApplyEditorConfig(sourceFile, DefaultOptions(), AllLanguages);

        Assert.That(result.GetOption(FormattingOptions.IndentationSize, LanguageNames.CSharp), Is.EqualTo(2));
    }

    [Test]
    public void ApplyEditorConfig_SectionGlob_OnlyMatchingFiles()
    {
        // [*.cs] section should only apply to .cs files
        WriteEditorConfig(_tempDir, "[*.cs]\nroot = true\nindent_size = 2\n");
        var csFile = WriteSourceFile(_tempDir, "test.cs");
        var vbFile = WriteSourceFile(_tempDir, "test.vb");

        var csResult = EditorConfigHelpers.ApplyEditorConfig(csFile, DefaultOptions(), AllLanguages);
        var vbResult = EditorConfigHelpers.ApplyEditorConfig(vbFile, DefaultOptions(), AllLanguages);

        Assert.That(csResult.GetOption(FormattingOptions.IndentationSize, LanguageNames.CSharp), Is.EqualTo(2));
        Assert.That(vbResult.GetOption(FormattingOptions.IndentationSize, LanguageNames.CSharp), Is.Not.EqualTo(2));
    }

    [Test]
    public void ApplyEditorConfig_CSharpNewLineBefore_Else_True()
    {
        WriteEditorConfig(_tempDir, "[*.cs]\nroot = true\ncsharp_new_line_before_else = true\n");
        var sourceFile = WriteSourceFile(_tempDir);
        var baseOptions = DefaultOptions().WithChangedOption(CSharpFormattingOptions.NewLineForElse, false);

        var result = EditorConfigHelpers.ApplyEditorConfig(sourceFile, baseOptions, AllLanguages);

        Assert.That(result.GetOption(CSharpFormattingOptions.NewLineForElse), Is.True);
    }
}
