using QuantumCore.API.Systems.Formulas;
using Xunit;

namespace Core.Tests;

public class SkillFormulaTests
{
    private static readonly IReadOnlyDictionary<EFormulaVariable, double> EmptyVariables =
        new Dictionary<EFormulaVariable, double>();


    [Fact]
    public void PhysicalDamageExpressionEvaluatesAsExpected()
    {
        var formula = SkillFormula.ParseRawExpression("-(3*atk + (0.8*atk + 5*str + 3*dex + con)*k)");

        var variables = new Dictionary<EFormulaVariable, double>
        {
            [EFormulaVariable.AttackValue] = 210.0,
            [EFormulaVariable.Strength] = 55.0,
            [EFormulaVariable.Dexterity] = 48.0,
            [EFormulaVariable.Constitution] = 32.0,
            [EFormulaVariable.SkillLevel] = 5.0
        };

        var expected = -(3 * variables[EFormulaVariable.AttackValue] +
                         (0.8 * variables[EFormulaVariable.AttackValue] +
                          5 * variables[EFormulaVariable.Strength] +
                          3 * variables[EFormulaVariable.Dexterity] +
                          variables[EFormulaVariable.Constitution]) *
                         variables[EFormulaVariable.SkillLevel]);

        var result = formula.Evaluate(variables);

        Assert.Equal(expected, result, 10);
    }

    [Fact]
    public void BuffDurationExpressionMatchesFormula()
    {
        var formula = SkillFormula.ParseRawExpression("30 + 50*k");

        var variables = new Dictionary<EFormulaVariable, double>
        {
            [EFormulaVariable.SkillLevel] = 7.0
        };

        var expected = 30 + 50 * variables[EFormulaVariable.SkillLevel];

        var result = formula.Evaluate(variables);

        Assert.Equal(expected, result, 10);
    }

    [Fact]
    public void BuffStrengthExpressionUsesLevelAndStrength()
    {
        var formula = SkillFormula.ParseRawExpression("(100 + str + lv*3) * k");

        var variables = new Dictionary<EFormulaVariable, double>
        {
            [EFormulaVariable.Strength] = 65.0,
            [EFormulaVariable.Level] = 42.0,
            [EFormulaVariable.SkillLevel] = 8.0
        };

        var expected = (100 +
                        variables[EFormulaVariable.Strength] +
                        variables[EFormulaVariable.Level] * 3) *
                       variables[EFormulaVariable.SkillLevel];

        var result = formula.Evaluate(variables);

        Assert.Equal(expected, result, 10);
    }

    [Fact]
    public void CompositeExpressionCombinesVariablesAndHelpers()
    {
        var formula = SkillFormula.ParseRawExpression("abs(-(2*atk) + max(str, dex) * sin(0.5) + floor(irand(3, 3)))");

        var variables = new Dictionary<EFormulaVariable, double>
        {
            [EFormulaVariable.AttackValue] = 180.0,
            [EFormulaVariable.Strength] = 60.0,
            [EFormulaVariable.Dexterity] = 48.0
        };

        var expected = Math.Abs(-(2 * variables[EFormulaVariable.AttackValue]) +
                                Math.Max(variables[EFormulaVariable.Strength], variables[EFormulaVariable.Dexterity]) *
                                Math.Sin(0.5) +
                                Math.Floor(3.0));

        var result = formula.Evaluate(variables);

        Assert.Equal(expected, result, 10);
    }

    [Fact]
    public void ConstantExpressionReturnsExactValue()
    {
        var formula = SkillFormula.ParseRawExpression("123.456");

        var result = formula.Evaluate(EmptyVariables);

        Assert.Equal(123.456, result, 12);
    }

    [Fact]
    public void DeterministicConstantExpressionIsPrecomputed()
    {
        // TODO: find a way to mock internally and verify that result is not recomputed on successive Evaluate() calls
        var formula = SkillFormula.ParseRawExpression("cos(1) + sin(2)");

        var expected = Math.Cos(1) + Math.Sin(2);
        var result = formula.Evaluate(EmptyVariables);

        Assert.Equal(expected, result, 12);
    }

    [Fact]
    public void NumberExpressionStaysWithinInclusiveRange()
    {
        var formula = SkillFormula.ParseRawExpression("number(2, 5)");

        var observedMin = double.MaxValue;
        var observedMax = double.MinValue;

        for (var i = 0; i < 64; i++)
        {
            var value = formula.Evaluate(EmptyVariables);
            observedMin = Math.Min(observedMin, value);
            observedMax = Math.Max(observedMax, value);
        }

        Assert.InRange(observedMin, 2.0, 5.0);
        Assert.InRange(observedMax, 2.0, 5.0);
    }

    [Fact]
    public void FrandExpressionProducesHalfOpenRange()
    {
        var formula = SkillFormula.ParseRawExpression("frand(1.5, 3.5)");

        for (var i = 0; i < 64; i++)
        {
            var value = formula.Evaluate(EmptyVariables);
            Assert.True(value >= 1.5);
            Assert.True(value < 3.5);
        }
    }

    [Fact]
    public void LogExpressionWithInvalidArgumentsReturnsZero()
    {
        var formula = SkillFormula.ParseRawExpression("log(1, -5)");

        var result = formula.Evaluate(EmptyVariables);

        Assert.Equal(0.0, result);
    }
}
