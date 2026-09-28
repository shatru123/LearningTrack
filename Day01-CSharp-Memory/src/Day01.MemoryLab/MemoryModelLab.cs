namespace Day01.MemoryLab;

public struct CoordinateStruct
{
    public double X { get; set; }
    public double Y { get; set; }

    public CoordinateStruct(double x, double y)
    {
        X = x;
        Y = y;
    }
}

public class VenueLocationClass
{
    // A value type living on the HEAP because it is an instance field of a reference type!
    public CoordinateStruct Coordinates;
    public string VenueName;

    public VenueLocationClass(string venueName, CoordinateStruct coords)
    {
        VenueName = venueName;
        Coordinates = coords;
    }
}

public static class MemoryModelInspector
{
    /// <summary>
    /// Demonstrates that value types do NOT always live on the stack.
    /// Nuance 1: Struct as a field of a class lives on the managed heap.
    /// Nuance 2: Boxed struct lives on the heap.
    /// Nuance 3: Closure-captured struct lives on the heap in a compiler-generated class.
    /// Nuance 4: Array of structs lives on the heap.
    /// Nuance 5: Local struct variable lives on the execution stack (or CPU registers).
    /// </summary>
    public static (object Boxed, VenueLocationClass Container, CoordinateStruct[] ArrayOnHeap, Func<double> ClosureOnHeap)
        DemonstrateValueTypeLocations()
    {
        // 1. Stack local:
        CoordinateStruct stackLocal = new CoordinateStruct(51.5549, -0.1084);

        // 2. Value type on Heap via enclosing reference type:
        VenueLocationClass container = new VenueLocationClass("Emirates Stadium", stackLocal);

        // 3. Value type on Heap via Boxing:
        object boxed = stackLocal;

        // 4. Value types on Heap via Array:
        CoordinateStruct[] arrayOnHeap = new CoordinateStruct[] { stackLocal, new CoordinateStruct(51.4816, -0.1910) };

        // 5. Value type captured in closure (hoisted to compiler-generated display class on heap):
        double capturedValue = stackLocal.X;
        Func<double> closureOnHeap = () => capturedValue * 2;

        return (boxed, container, arrayOnHeap, closureOnHeap);
    }
}
