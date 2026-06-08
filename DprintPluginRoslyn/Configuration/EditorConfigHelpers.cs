using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Formatting;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Dprint.Plugins.Roslyn.Configuration;

public static class EditorConfigHelpers
{
    public static OptionSet ApplyEditorConfig(string sourceFilePath, OptionSet options, IReadOnlyList<string> languageNames)
    {
        var configs = LoadEditorConfigFiles(sourceFilePath);
        if (configs.Count == 0)
            return options;

        var configSet = AnalyzerConfigSet.Create(configs);
        var analyzerOptions = configSet.GetOptionsForSourcePath(sourceFilePath).AnalyzerOptions;

        options = ApplyUniversalOptions(analyzerOptions, options, languageNames);
        options = ApplyCSharpFormattingOptions(analyzerOptions, options);

        return options;
    }

    private static List<AnalyzerConfig> LoadEditorConfigFiles(string sourceFilePath)
    {
        var configs = new List<AnalyzerConfig>();
        var fullPath = Path.GetFullPath(sourceFilePath);
        var dir = Path.GetDirectoryName(fullPath);

        while (dir != null)
        {
            var editorConfigPath = Path.Combine(dir, ".editorconfig");
            if (File.Exists(editorConfigPath))
            {
                var content = File.ReadAllText(editorConfigPath);
                var config = AnalyzerConfig.Parse(content, editorConfigPath);
                configs.Add(config);
                if (IsEditorConfigRoot(content))
                    break;
            }

            var parent = Path.GetDirectoryName(dir);
            if (parent == dir)
                break; // reached filesystem root
            dir = parent;
        }

        // AnalyzerConfigSet.Create expects ascending specificity: root/global first, local/child last
        configs.Reverse();
        return configs;
    }

    // AnalyzerConfig.IsRoot is internal; check the raw content instead.
    private static bool IsEditorConfigRoot(string content)
    {
        foreach (var rawLine in content.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line[0] == '#' || line[0] == ';')
                continue;
            if (line[0] == '[')
                break; // past the global section
            var eqIdx = line.IndexOf('=');
            if (eqIdx < 0)
                continue;
            var key = line[..eqIdx].Trim();
            var val = line[(eqIdx + 1)..].Trim();
            if (string.Equals(key, "root", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(val, "true", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static OptionSet ApplyUniversalOptions(AnalyzerConfigOptions analyzerOptions, OptionSet options, IReadOnlyList<string> languageNames)
    {
        if (analyzerOptions.TryGetValue("indent_style", out var indentStyle) && indentStyle != null)
        {
            var useTabs = string.Equals(indentStyle.Trim(), "tab", StringComparison.OrdinalIgnoreCase);
            foreach (var lang in languageNames)
                options = options.WithChangedOption(FormattingOptions.UseTabs, lang, useTabs);
        }

        if (analyzerOptions.TryGetValue("indent_size", out var indentSizeStr) &&
            int.TryParse(indentSizeStr?.Trim(), out var indentSize))
        {
            foreach (var lang in languageNames)
            {
                options = options.WithChangedOption(FormattingOptions.IndentationSize, lang, indentSize);
                options = options.WithChangedOption(FormattingOptions.TabSize, lang, indentSize);
            }
        }

        if (analyzerOptions.TryGetValue("tab_width", out var tabWidthStr) &&
            int.TryParse(tabWidthStr?.Trim(), out var tabWidth))
        {
            foreach (var lang in languageNames)
                options = options.WithChangedOption(FormattingOptions.TabSize, lang, tabWidth);
        }

        if (analyzerOptions.TryGetValue("end_of_line", out var endOfLine) && endOfLine != null)
        {
            var newLine = endOfLine.Trim().ToLowerInvariant() switch
            {
                "lf" => "\n",
                "crlf" => "\r\n",
                "cr" => "\r",
                _ => null,
            };
            if (newLine != null)
            {
                foreach (var lang in languageNames)
                    options = options.WithChangedOption(FormattingOptions.NewLine, lang, newLine);
            }
        }

        return options;
    }

    private static OptionSet ApplyCSharpFormattingOptions(AnalyzerConfigOptions analyzerOptions, OptionSet options)
    {
        // New line options
        options = ApplyBraceNewLineOptions(analyzerOptions, options);
        options = ApplyBool(analyzerOptions, "csharp_new_line_before_else", CSharpFormattingOptions.NewLineForElse, options);
        options = ApplyBool(analyzerOptions, "csharp_new_line_before_catch", CSharpFormattingOptions.NewLineForCatch, options);
        options = ApplyBool(analyzerOptions, "csharp_new_line_before_finally", CSharpFormattingOptions.NewLineForFinally, options);
        options = ApplyBool(analyzerOptions, "csharp_new_line_before_members_in_object_initializers", CSharpFormattingOptions.NewLineForMembersInObjectInit, options);
        options = ApplyBool(analyzerOptions, "csharp_new_line_before_members_in_anonymous_types", CSharpFormattingOptions.NewLineForMembersInAnonymousTypes, options);
        options = ApplyBool(analyzerOptions, "csharp_new_line_between_query_expression_clauses", CSharpFormattingOptions.NewLineForClausesInQuery, options);

        // Indentation options
        options = ApplyBool(analyzerOptions, "csharp_indent_case_contents", CSharpFormattingOptions.IndentSwitchCaseSection, options);
        options = ApplyBool(analyzerOptions, "csharp_indent_switch_labels", CSharpFormattingOptions.IndentSwitchSection, options);
        options = ApplyBool(analyzerOptions, "csharp_indent_block_contents", CSharpFormattingOptions.IndentBlock, options);
        options = ApplyBool(analyzerOptions, "csharp_indent_braces", CSharpFormattingOptions.IndentBraces, options);
        options = ApplyBool(analyzerOptions, "csharp_indent_case_contents_when_block", CSharpFormattingOptions.IndentSwitchCaseSectionWhenBlock, options);
        options = ApplyLabelPositioning(analyzerOptions, options);

        // Spacing options
        options = ApplyBool(analyzerOptions, "csharp_space_after_cast", CSharpFormattingOptions.SpaceAfterCast, options);
        options = ApplyBool(analyzerOptions, "csharp_space_after_keywords_in_control_flow_statements", CSharpFormattingOptions.SpaceAfterControlFlowStatementKeyword, options);
        options = ApplyBool(analyzerOptions, "csharp_space_before_colon_in_inheritance_clause", CSharpFormattingOptions.SpaceBeforeColonInBaseTypeDeclaration, options);
        options = ApplyBool(analyzerOptions, "csharp_space_after_colon_in_inheritance_clause", CSharpFormattingOptions.SpaceAfterColonInBaseTypeDeclaration, options);
        options = ApplyBinaryOperatorSpacing(analyzerOptions, options);
        options = ApplyBool(analyzerOptions, "csharp_space_between_method_declaration_parameter_list_parentheses", CSharpFormattingOptions.SpaceWithinMethodDeclarationParenthesis, options);
        options = ApplyBool(analyzerOptions, "csharp_space_between_method_declaration_empty_parameter_list_parentheses", CSharpFormattingOptions.SpaceBetweenEmptyMethodDeclarationParentheses, options);
        options = ApplyBool(analyzerOptions, "csharp_space_between_method_declaration_name_and_open_parenthesis", CSharpFormattingOptions.SpacingAfterMethodDeclarationName, options);
        options = ApplyBool(analyzerOptions, "csharp_space_between_method_call_parameter_list_parentheses", CSharpFormattingOptions.SpaceWithinMethodCallParentheses, options);
        options = ApplyBool(analyzerOptions, "csharp_space_between_method_call_empty_parameter_list_parentheses", CSharpFormattingOptions.SpaceBetweenEmptyMethodCallParentheses, options);
        options = ApplyBool(analyzerOptions, "csharp_space_between_method_call_name_and_opening_parenthesis", CSharpFormattingOptions.SpaceAfterMethodCallName, options);
        options = ApplyBool(analyzerOptions, "csharp_space_after_comma", CSharpFormattingOptions.SpaceAfterComma, options);
        options = ApplyBool(analyzerOptions, "csharp_space_before_comma", CSharpFormattingOptions.SpaceBeforeComma, options);
        options = ApplyBool(analyzerOptions, "csharp_space_after_dot", CSharpFormattingOptions.SpaceAfterDot, options);
        options = ApplyBool(analyzerOptions, "csharp_space_before_dot", CSharpFormattingOptions.SpaceBeforeDot, options);
        options = ApplyBool(analyzerOptions, "csharp_space_after_semicolon_in_for_statement", CSharpFormattingOptions.SpaceAfterSemicolonsInForStatement, options);
        options = ApplyBool(analyzerOptions, "csharp_space_before_semicolon_in_for_statement", CSharpFormattingOptions.SpaceBeforeSemicolonsInForStatement, options);
        options = ApplySpaceBetweenParentheses(analyzerOptions, options);
        options = ApplyDeclarationStatementSpacing(analyzerOptions, options);
        options = ApplyBool(analyzerOptions, "csharp_space_before_open_square_brackets", CSharpFormattingOptions.SpaceBeforeOpenSquareBracket, options);
        options = ApplyBool(analyzerOptions, "csharp_space_between_empty_square_brackets", CSharpFormattingOptions.SpaceBetweenEmptySquareBrackets, options);
        options = ApplyBool(analyzerOptions, "csharp_space_between_square_brackets", CSharpFormattingOptions.SpaceWithinSquareBrackets, options);

        // Wrapping options
        options = ApplyBool(analyzerOptions, "csharp_preserve_single_line_statements", CSharpFormattingOptions.WrappingKeepStatementsOnSingleLine, options);
        options = ApplyBool(analyzerOptions, "csharp_preserve_single_line_blocks", CSharpFormattingOptions.WrappingPreserveSingleLine, options);

        return options;
    }

    private static OptionSet ApplyBraceNewLineOptions(AnalyzerConfigOptions analyzerOptions, OptionSet options)
    {
        if (!analyzerOptions.TryGetValue("csharp_new_line_before_open_brace", out var value) || value == null)
            return options;

        var trimmed = value.Trim().ToLowerInvariant();
        bool setAll = trimmed == "all";
        bool setNone = trimmed == "none";

        var enabled = (!setAll && !setNone)
            ? new HashSet<string>(trimmed.Split(',').Select(p => p.Trim()))
            : new HashSet<string>();

        bool Has(string ctx) => setAll || (!setNone && enabled.Contains(ctx));

        options = options.WithChangedOption(CSharpFormattingOptions.NewLinesForBracesInTypes, Has("types"));
        options = options.WithChangedOption(CSharpFormattingOptions.NewLinesForBracesInMethods, Has("methods"));
        options = options.WithChangedOption(CSharpFormattingOptions.NewLinesForBracesInProperties, Has("properties"));
        options = options.WithChangedOption(CSharpFormattingOptions.NewLinesForBracesInAccessors, Has("accessors"));
        options = options.WithChangedOption(CSharpFormattingOptions.NewLinesForBracesInAnonymousMethods, Has("anonymous_methods"));
        options = options.WithChangedOption(CSharpFormattingOptions.NewLinesForBracesInControlBlocks, Has("control_blocks"));
        options = options.WithChangedOption(CSharpFormattingOptions.NewLinesForBracesInAnonymousTypes, Has("anonymous_types"));
        options = options.WithChangedOption(CSharpFormattingOptions.NewLinesForBracesInObjectCollectionArrayInitializers, Has("object_collection_array_initializers"));
        options = options.WithChangedOption(CSharpFormattingOptions.NewLinesForBracesInLambdaExpressionBody, Has("lambdas"));

        return options;
    }

    private static OptionSet ApplyLabelPositioning(AnalyzerConfigOptions analyzerOptions, OptionSet options)
    {
        if (!analyzerOptions.TryGetValue("csharp_indent_labels", out var value) || value == null)
            return options;

        return value.Trim().ToLowerInvariant() switch
        {
            "flush_left" => options.WithChangedOption(CSharpFormattingOptions.LabelPositioning, LabelPositionOptions.LeftMost),
            "no_indent" => options.WithChangedOption(CSharpFormattingOptions.LabelPositioning, LabelPositionOptions.NoIndent),
            "one_less_than_current" => options.WithChangedOption(CSharpFormattingOptions.LabelPositioning, LabelPositionOptions.OneLess),
            _ => options,
        };
    }

    private static OptionSet ApplyBinaryOperatorSpacing(AnalyzerConfigOptions analyzerOptions, OptionSet options)
    {
        if (!analyzerOptions.TryGetValue("csharp_space_around_binary_operators", out var value) || value == null)
            return options;

        return value.Trim().ToLowerInvariant() switch
        {
            "before_and_after" => options.WithChangedOption(CSharpFormattingOptions.SpacingAroundBinaryOperator, BinaryOperatorSpacingOptions.Single),
            "ignore" => options.WithChangedOption(CSharpFormattingOptions.SpacingAroundBinaryOperator, BinaryOperatorSpacingOptions.Ignore),
            "none" => options.WithChangedOption(CSharpFormattingOptions.SpacingAroundBinaryOperator, BinaryOperatorSpacingOptions.Remove),
            _ => options,
        };
    }

    private static OptionSet ApplySpaceBetweenParentheses(AnalyzerConfigOptions analyzerOptions, OptionSet options)
    {
        if (!analyzerOptions.TryGetValue("csharp_space_between_parentheses", out var value) || value == null)
            return options;

        var trimmed = value.Trim().ToLowerInvariant();
        bool disable = trimmed == "false" || trimmed == "none";
        var parts = disable
            ? new HashSet<string>()
            : new HashSet<string>(trimmed.Split(',').Select(p => p.Trim()));

        options = options.WithChangedOption(CSharpFormattingOptions.SpaceWithinOtherParentheses, parts.Contains("control_flow_statements"));
        options = options.WithChangedOption(CSharpFormattingOptions.SpaceWithinExpressionParentheses, parts.Contains("expressions"));
        options = options.WithChangedOption(CSharpFormattingOptions.SpaceWithinCastParentheses, parts.Contains("type_casts"));

        return options;
    }

    private static OptionSet ApplyDeclarationStatementSpacing(AnalyzerConfigOptions analyzerOptions, OptionSet options)
    {
        if (!analyzerOptions.TryGetValue("csharp_space_around_declaration_statements", out var value) || value == null)
            return options;

        return value.Trim().ToLowerInvariant() switch
        {
            "ignore" => options.WithChangedOption(CSharpFormattingOptions.SpacesIgnoreAroundVariableDeclaration, true),
            "false" => options.WithChangedOption(CSharpFormattingOptions.SpacesIgnoreAroundVariableDeclaration, false),
            _ => options,
        };
    }

    private static OptionSet ApplyBool(AnalyzerConfigOptions analyzerOptions, string key, Option<bool> option, OptionSet options)
    {
        if (!analyzerOptions.TryGetValue(key, out var value) || value == null)
            return options;

        return value.Trim().ToLowerInvariant() switch
        {
            "true" => options.WithChangedOption(option, true),
            "false" => options.WithChangedOption(option, false),
            _ => options,
        };
    }
}
