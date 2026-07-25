using System.Text;
using System.Text.Json;
using MaIN.Domain.Models;

namespace MaIN.Services.Utils;

public class JsonSchemaToGbnfConverter
{
    private readonly Dictionary<string, string> _rules = new();
    private int _ruleCounter;

    public string Convert(JsonElement schema)
    {
        _rules.Clear();
        _ruleCounter = 0;
        
        var rootBody = GenerateExpression(schema);
        
        var sb = new StringBuilder();
        
        sb.AppendLine($"root ::= {rootBody}");
        sb.AppendLine();
        
        foreach (var kvp in _rules)
        {
            sb.AppendLine($"{kvp.Key} ::= {kvp.Value}");
        }
        
        sb.AppendLine("ws ::= | \" \" | \"\\n\" [ \\t]{0,20}");
        sb.AppendLine("char ::= [^\"\\\\\\x7F\\x00-\\x1F] | [\\\\] ([\"\\\\bfnrt] | \"u\" [0-9a-fA-F]{4})");
        sb.AppendLine("string ::= [\\\"] char* [\\\"] ws");
        sb.AppendLine("integer ::= (\"-\"? ([0-9] | [1-9] [0-9]{0,15})) ws");
        sb.AppendLine("number ::= (\"-\"? ([0-9] | [1-9] [0-9]{0,15})) (\".\" [0-9]+)? ([eE] [-+]? [0-9] [1-9]{0,15})? ws");
        sb.AppendLine("boolean ::= (\"true\" | \"false\") ws");
        sb.AppendLine("null ::= \"null\" ws");
        
        return sb.ToString();
    }

    public string Convert(string jsonSchema)
    {
        var schema = JsonDocument.Parse(jsonSchema).RootElement;
        return Convert(schema);
    }

    public string Convert(Grammar grammar)
    {
        if (grammar.Format == GrammarFormat.GBNF)
            return grammar.Value;
        
        if (grammar.Format == GrammarFormat.JSONSchema)
            return Convert(grammar.Value);
        
        throw new NotSupportedException($"Grammar format '{grammar.Format}' is not supported");
    }

    private string GenerateExpression(JsonElement schema)
    {
        if (!schema.TryGetProperty("type", out var typeProp))
        {
            if (schema.TryGetProperty("anyOf", out _) || schema.TryGetProperty("oneOf", out _))
                return GenerateAnyOf(schema);
            return "value";
        }

        var type = typeProp.GetString();
        return type switch
        {
            "object" => GenerateObject(schema),
            "array" => GenerateArray(schema),
            "string" => GenerateString(schema),
            "integer" => "integer",
            "number" => "number",
            "boolean" => "boolean",
            "null" => "null",
            _ => "string"
        };
    }

    private string GenerateObject(JsonElement schema)
    {
        var properties = new List<(string key, JsonElement value)>();

        if (schema.TryGetProperty("properties", out var props))
        {
            foreach (var prop in props.EnumerateObject())
            {
                properties.Add((prop.Name, prop.Value));
            }
        }

        if (properties.Count == 0)
            return "\"{\" ws \"}\" ws";

        if (properties.Count == 1)
        {
            var (key, value) = properties[0];
            var valExpr = GenerateExpression(value);
            return $"\"{{\" ws \"\\\"{EscapeGbnfString(key)}\\\":\" ws {valExpr} \"}}\" ws";
        }

        var ruleName = NewRuleName();
        var sb = new StringBuilder();
        sb.Append("\"{\" ws ");

        for (int i = 0; i < properties.Count; i++)
        {
            var (key, value) = properties[i];

            if (i > 0)
                sb.Append("\",\" ws ");

            sb.Append($"\"\\\"{EscapeGbnfString(key)}\\\":\" ws ");
            sb.Append(GenerateExpression(value));
            sb.Append(' ');
        }

        sb.Append("\"}\" ws");
        _rules[ruleName] = sb.ToString();
        return ruleName;
    }

    private string GenerateArray(JsonElement schema)
    {
        if (!schema.TryGetProperty("items", out var items))
            return "\"[\" ws (value (ws \",\" ws value)*)? ws \"]\" ws";

        var itemExpr = GenerateExpression(items);

        if (IsPrimitiveExpression(itemExpr))
        {
            return $"\"[\" ws ({itemExpr} (ws \",\" ws {itemExpr})*)? ws \"]\" ws";
        }

        var itemRule = NewRuleName();
        _rules[itemRule] = itemExpr;

        return $"\"[\" ws ({itemRule} (ws \",\" ws {itemRule})*)? ws \"]\" ws";
    }

    private string GenerateString(JsonElement schema)
    {
        if (schema.TryGetProperty("enum", out var enumValues))
        {
            var alternatives = new List<string>();
            foreach (var val in enumValues.EnumerateArray())
            {
                alternatives.Add(GenerateGbnfStringLiteral(val.GetString() ?? ""));
            }
            return string.Join(" | ", alternatives);
        }

        if (schema.TryGetProperty("const", out var constVal) && constVal.ValueKind == JsonValueKind.String)
        {
            return GenerateGbnfStringLiteral(constVal.GetString() ?? "");
        }

        return "string";
    }

    private string GenerateAnyOf(JsonElement schema)
    {
        var alternatives = new List<string>();
        var source = schema.TryGetProperty("anyOf", out var anyOf) ? anyOf
                   : schema.TryGetProperty("oneOf", out var oneOf) ? oneOf
                   : default;

        if (source.ValueKind != JsonValueKind.Array)
            return "value";

        foreach (var option in source.EnumerateArray())
        {
            alternatives.Add(GenerateExpression(option));
        }

        if (alternatives.Count == 1)
            return alternatives[0];

        var ruleName = NewRuleName();
        _rules[ruleName] = string.Join(" | ", alternatives);
        return ruleName;
    }

    private static string GenerateGbnfStringLiteral(string value)
    {
        var sb = new StringBuilder();
        sb.Append('"');
        foreach (var c in value)
        {
            switch (c)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (c < 0x20 || c == 0x7F)
                        sb.Append($"\\x{(int)c:X2}");
                    else
                        sb.Append(c);
                    break;
            }
        }
        sb.Append('"');
        return sb.ToString();
    }

    private static string EscapeGbnfString(string value)
    {
        return value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"");
    }

    private static bool IsPrimitiveExpression(string expr)
    {
        var trimmed = expr.Trim();
        return trimmed is "string" or "integer" or "number" or "boolean" or "null" or "value";
    }

    private string NewRuleName()
    {
        return $"r{_ruleCounter++}";
    }
}
