using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.RegularExpressions;
using Flee.PublicTypes;

namespace QuantumCore.API.Systems.Formulas;

public sealed partial class SkillFormula
{
    public static SkillFormula Zero { get; } = new("0", null, [], 0.0);

    private readonly IDynamicExpression? _compiledExpr;
    private readonly ReadOnlyCollection<EFormulaVariable> _requiredVariables;

    public IReadOnlyList<EFormulaVariable> RequiredVariables => _requiredVariables;

    // will have a value only for constant expressions.
    private readonly double? _constantValue;
    
    public string RawExpression { get; private init; }

    private SkillFormula(string rawExpression, IDynamicExpression? compiledExpr, IReadOnlyList<EFormulaVariable> usedVariables, double? constantValue)
    {
        RawExpression = rawExpression;
        _compiledExpr = compiledExpr;
        _requiredVariables = new ReadOnlyCollection<EFormulaVariable>(usedVariables.ToList());
        _constantValue = constantValue;
    }

    /// <summary>
    /// Creates and compiles a SkillFormula object. Optimizes for constant expressions by pre-calculating their value.
    /// </summary>
    public static SkillFormula ParseRawExpression(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return Zero;
        }

        var (compiled, usedVars) = CompileAndCapture(expression);

        if (usedVars.Count == 0 && !ContainsNondeterministic(expression))
        {
            var constantResult = Convert.ToDouble(compiled.Evaluate());
            return new SkillFormula(expression, null, [], constantResult);
        }

        return new SkillFormula(expression, compiled, usedVars, null);
    }

    /// <summary>
    /// Evaluates the expression with the given parameters. Returns a pre-calculated value for constant expressions.
    /// </summary>
    public double Evaluate(IReadOnlyDictionary<EFormulaVariable, double> parameters)
    {
        if (_constantValue.HasValue)
        {
            return _constantValue.Value;
        }

        // note that this clones the shared global SkillData formula to get a thread-safe instance for this specific evaluation
        // optimization: create per-player instances of compiled expr and update parameters directly on its context
        var localExpr = (IDynamicExpression)_compiledExpr!.Clone();

        foreach (var requiredVariable in _requiredVariables)
        {
            // throw if missing from parameters
            var passedValue = parameters[requiredVariable];
            localExpr.Context.Variables[requiredVariable.GetIdentifier()] = passedValue;
        }

        return Convert.ToDouble(localExpr.Evaluate());
    }

    private static (IDynamicExpression Compiled, IReadOnlyList<EFormulaVariable> Used) CompileAndCapture(string expression)
    {
        var ctx = new ExpressionContext { Options = { ParseCulture = CultureInfo.InvariantCulture } };
        ctx.Imports.AddType(typeof(FormulaFunctions));

        var used = new HashSet<EFormulaVariable>();
        // Hook into Flee's variable resolution process to identify used variables.
        ctx.Variables.ResolveVariableType += (_, e) =>
        {
            foreach (var variable in Enum.GetValues<EFormulaVariable>())
            {
                if (variable.GetIdentifier() == e.VariableName)
                {
                    used.Add(variable);
                    e.VariableType = typeof(double);
                    return;
                }
            }
        };

        return (ctx.CompileDynamic(expression), used.ToArray());
    }

    [GeneratedRegex(@"\b(number|irand|irandom|frand|frandom)\s*\(", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant)]
    private static partial Regex NondeterministicCallsRegex();
    
    private static bool ContainsNondeterministic(string? expression)
    {
        return NondeterministicCallsRegex().IsMatch(expression ?? string.Empty);
    }

}
