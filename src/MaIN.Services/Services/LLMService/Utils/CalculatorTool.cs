using System.Globalization;
using System.Text.Json;
using MaIN.Domain.Entities.Tools;

namespace MaIN.Services.Services.LLMService.Utils;

public static class CalculatorTool
{
    public const string Name = "calculator";

    public static ToolDefinition Create(string toolName = Name)
    {
        return new ToolDefinition
        {
            Type = "function",
            IsClientSide = false,
            Function = new()
            {
                Name = toolName,
                Description = """
                    Evaluate a mathematical expression and return the exact numeric result.
                    Supports + - * / % ^ (power) and parentheses. Call this tool for any calculation
                    instead of computing it yourself -- it never makes arithmetic mistakes.
                    """,
                Parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        expression = new
                        {
                            type = "string",
                            description = "The arithmetic expression to evaluate, e.g. \"(3 + 4) * 2 / 7\"."
                        }
                    },
                    required = new[] { "expression" }
                }
            },
            Execute = argsJson => Task.FromResult(Execute(argsJson))
        };
    }

    private static string Execute(string argsJson)
    {
        var expression = ExtractExpression(argsJson);
        if (string.IsNullOrWhiteSpace(expression))
        {
            return "Error: Empty expression provided.";
        }

        try
        {
            return Evaluate(expression).ToString(CultureInfo.InvariantCulture);
        }
        catch (Exception ex) when (ex is FormatException or DivideByZeroException)
        {
            return $"Error: Could not evaluate expression '{expression}': {ex.Message}";
        }
    }

    private static string ExtractExpression(string argsJson)
    {
        if (string.IsNullOrWhiteSpace(argsJson)) return string.Empty;
        try
        {
            using var doc = JsonDocument.Parse(argsJson);
            if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                doc.RootElement.TryGetProperty("expression", out var expressionElement))
            {
                return expressionElement.GetString() ?? argsJson;
            }
        }
        catch (JsonException) { /* fall back to treating the raw args as the expression */ }
        return argsJson;
    }

    /// <summary>
    /// Evaluates a numeric expression via a small recursive-descent parser (no eval/reflection):
    /// expression := term (('+' | '-') term)*
    /// term       := power (('*' | '/' | '%') power)*
    /// power      := unary ('^' power)?           // right-associative
    /// unary      := ('-' | '+')? primary
    /// primary    := number | '(' expression ')'
    /// </summary>
    public static double Evaluate(string expression)
    {
        var parser = new ExpressionParser(expression);
        var result = parser.ParseExpression();
        parser.SkipWhitespace();
        if (!parser.IsAtEnd)
        {
            throw new FormatException($"Unexpected character '{parser.Current}' at position {parser.Position}.");
        }

        return result;
    }

    private sealed class ExpressionParser(string text)
    {
        private int _pos;

        public int Position => _pos;
        public bool IsAtEnd => _pos >= text.Length;
        public char Current => text[_pos];

        public void SkipWhitespace()
        {
            while (_pos < text.Length && char.IsWhiteSpace(text[_pos])) _pos++;
        }

        public double ParseExpression()
        {
            var value = ParseTerm();
            while (true)
            {
                SkipWhitespace();
                if (IsAtEnd) break;

                if (Current == '+') { _pos++; value += ParseTerm(); }
                else if (Current == '-') { _pos++; value -= ParseTerm(); }
                else break;
            }

            return value;
        }

        private double ParseTerm()
        {
            var value = ParsePower();
            while (true)
            {
                SkipWhitespace();
                if (IsAtEnd) break;

                if (Current == '*') { _pos++; value *= ParsePower(); }
                else if (Current == '/')
                {
                    _pos++;
                    var divisor = ParsePower();
                    if (divisor == 0) throw new DivideByZeroException("Division by zero.");
                    value /= divisor;
                }
                else if (Current == '%')
                {
                    _pos++;
                    var divisor = ParsePower();
                    if (divisor == 0) throw new DivideByZeroException("Modulo by zero.");
                    value %= divisor;
                }
                else break;
            }

            return value;
        }

        private double ParsePower()
        {
            var value = ParseUnary();
            SkipWhitespace();
            if (!IsAtEnd && Current == '^')
            {
                _pos++;
                value = Math.Pow(value, ParsePower()); // right-associative: 2^3^2 == 2^(3^2)
            }

            return value;
        }

        private double ParseUnary()
        {
            SkipWhitespace();
            if (!IsAtEnd && Current == '-') { _pos++; return -ParseUnary(); }
            if (!IsAtEnd && Current == '+') { _pos++; return ParseUnary(); }
            return ParsePrimary();
        }

        private double ParsePrimary()
        {
            SkipWhitespace();
            if (IsAtEnd)
            {
                throw new FormatException("Unexpected end of expression.");
            }

            if (Current == '(')
            {
                _pos++;
                var value = ParseExpression();
                SkipWhitespace();
                if (IsAtEnd || Current != ')')
                {
                    throw new FormatException("Missing closing parenthesis.");
                }

                _pos++;
                return value;
            }

            var start = _pos;
            while (_pos < text.Length && (char.IsDigit(text[_pos]) || text[_pos] == '.'))
            {
                _pos++;
            }

            if (_pos == start)
            {
                throw new FormatException($"Expected a number at position {start}.");
            }

            var numberText = text[start.._pos];
            if (!double.TryParse(numberText, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
            {
                throw new FormatException($"Invalid number '{numberText}'.");
            }

            return number;
        }
    }
}
