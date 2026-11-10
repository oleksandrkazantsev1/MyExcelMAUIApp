using System.Numerics;

namespace MyExcelMAUIApp.Logic;

/// <summary>Контекст, через який вузли дерева отримують значення клітинок.</summary>
public interface IEvaluationContext
{
    Value GetCellValue(int row, int column, int position);
}

/// <summary>Вузол абстрактного синтаксичного дерева. Кожен вузол вміє обчислити себе (поліморфізм).</summary>
public abstract class Node
{
    protected Node(int position) => Position = position;

    public int Position { get; }

    public abstract Value Evaluate(IEvaluationContext context);

    protected static BigInteger EvaluateInteger(Node node, IEvaluationContext context, string operation)
    {
        Value value = node.Evaluate(context);
        if (value.Kind != ValueKind.Integer)
            throw new FormulaEvaluationException(
                $"Операція \"{operation}\" вимагає цілих чисел, а отримано логічне значення", node.Position);
        return value.Integer;
    }
}

public sealed class NumberNode : Node
{
    private readonly BigInteger _value;
    public NumberNode(BigInteger value, int position) : base(position) => _value = value;
    public override Value Evaluate(IEvaluationContext context) => Value.FromInteger(_value);
}

public sealed class CellNode : Node
{
    public CellNode(int row, int column, int position) : base(position)
    {
        Row = row;
        Column = column;
    }

    public int Row { get; }
    public int Column { get; }

    public override Value Evaluate(IEvaluationContext context) =>
        context.GetCellValue(Row, Column, Position);
}

/// <summary>Унарні + та -.</summary>
public sealed class UnaryNode : Node
{
    private readonly char _operator;
    private readonly Node _operand;

    public UnaryNode(char op, Node operand, int position) : base(position)
    {
        _operator = op;
        _operand = operand;
    }

    public override Value Evaluate(IEvaluationContext context)
    {
        BigInteger v = EvaluateInteger(_operand, context, $"унарний {_operator}");
        return Value.FromInteger(_operator == '-' ? BigInteger.Negate(v) : v);
    }
}

/// <summary>Бінарні + - * /.</summary>
public sealed class BinaryNode : Node
{
    private readonly char _operator;
    private readonly Node _left;
    private readonly Node _right;

    public BinaryNode(char op, Node left, Node right, int position) : base(position)
    {
        _operator = op;
        _left = left;
        _right = right;
    }

    public override Value Evaluate(IEvaluationContext context)
    {
        string name = _operator.ToString();
        BigInteger l = EvaluateInteger(_left, context, name);
        BigInteger r = EvaluateInteger(_right, context, name);
        switch (_operator)
        {
            case '+': return Value.FromInteger(l + r);
            case '-': return Value.FromInteger(l - r);
            case '*': return Value.FromInteger(l * r);
            default:
                if (r.IsZero) throw new FormulaEvaluationException("Ділення на нуль", Position);
                return Value.FromInteger(BigInteger.Divide(l, r));
        }
    }
}

/// <summary>Порівняння = &lt; &gt; &lt;= &gt;= &lt;&gt; — результат логічний.</summary>
public sealed class ComparisonNode : Node
{
    private readonly TokenType _operator;
    private readonly Node _left;
    private readonly Node _right;

    public ComparisonNode(TokenType op, Node left, Node right, int position) : base(position)
    {
        _operator = op;
        _left = left;
        _right = right;
    }

    private string Symbol => _operator switch
    {
        TokenType.Equal => "=",
        TokenType.Less => "<",
        TokenType.Greater => ">",
        TokenType.LessEqual => "<=",
        TokenType.GreaterEqual => ">=",
        _ => "<>"
    };

    public override Value Evaluate(IEvaluationContext context)
    {
        Value l = _left.Evaluate(context);
        Value r = _right.Evaluate(context);

        if (l.Kind != r.Kind)
            throw new FormulaEvaluationException("Не можна порівнювати число з логічним значенням", Position);

        if (l.Kind == ValueKind.Boolean)
        {
            if (_operator == TokenType.Equal) return Value.FromBoolean(l.Boolean == r.Boolean);
            if (_operator == TokenType.NotEqual) return Value.FromBoolean(l.Boolean != r.Boolean);
            throw new FormulaEvaluationException(
                $"Операція \"{Symbol}\" не застосовна до логічних значень (дозволені лише = та <>)", Position);
        }

        int cmp = l.Integer.CompareTo(r.Integer);
        bool result = _operator switch
        {
            TokenType.Equal => cmp == 0,
            TokenType.Less => cmp < 0,
            TokenType.Greater => cmp > 0,
            TokenType.LessEqual => cmp <= 0,
            TokenType.GreaterEqual => cmp >= 0,
            _ => cmp != 0
        };
        return Value.FromBoolean(result);
    }
}

/// <summary>mmax(x1,...,xN) та mmin(x1,...,xN), N &gt;= 1.</summary>
public sealed class FunctionNode : Node
{
    private readonly string _name;
    private readonly IReadOnlyList<Node> _arguments;

    public FunctionNode(string name, IReadOnlyList<Node> arguments, int position) : base(position)
    {
        _name = name;
        _arguments = arguments;
    }

    public override Value Evaluate(IEvaluationContext context)
    {
        BigInteger? best = null;
        foreach (Node argument in _arguments)
        {
            BigInteger v = EvaluateInteger(argument, context, _name);
            if (best == null || (_name == "mmax" ? v > best.Value : v < best.Value))
                best = v;
        }
        return Value.FromInteger(best!.Value);
    }
}
