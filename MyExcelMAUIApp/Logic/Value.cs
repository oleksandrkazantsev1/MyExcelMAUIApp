using System.Numerics;

namespace MyExcelMAUIApp.Logic;

public enum ValueKind { Integer, Boolean }

/// <summary>Результат обчислення виразу: ціле число довільної довжини або логічне значення.</summary>
public readonly struct Value
{
    private Value(ValueKind kind, BigInteger integer, bool boolean)
    {
        Kind = kind;
        Integer = integer;
        Boolean = boolean;
    }

    public ValueKind Kind { get; }
    public BigInteger Integer { get; }
    public bool Boolean { get; }

    public static Value FromInteger(BigInteger value) => new(ValueKind.Integer, value, false);
    public static Value FromBoolean(bool value) => new(ValueKind.Boolean, BigInteger.Zero, value);

    public override string ToString() =>
        Kind == ValueKind.Integer ? Integer.ToString() : (Boolean ? "ІСТИНА" : "ХИБНІСТЬ");
}
