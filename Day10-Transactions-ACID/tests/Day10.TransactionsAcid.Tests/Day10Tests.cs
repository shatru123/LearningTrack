using FluentAssertions;
using Xunit;

namespace Day10.TransactionsAcid.Tests;

public class TransactionIsolationSimulatorTests
{
    [Fact]
    public void SimulateDirtyRead_ShouldAllowInReadUncommitted_AndPreventInReadCommitted()
    {
        var dirtyAllowed = TransactionIsolationSimulator.SimulateDirtyRead(SqlIsolationLevel.ReadUncommitted);
        var dirtyPrevented = TransactionIsolationSimulator.SimulateDirtyRead(SqlIsolationLevel.ReadCommitted);

        dirtyAllowed.AnomalyObserved.Should().BeTrue();
        dirtyPrevented.AnomalyObserved.Should().BeFalse();
    }

    [Fact]
    public void SimulateNonRepeatableRead_ShouldOccurInReadCommitted_AndBePreventedInRepeatableRead()
    {
        var committedResult = TransactionIsolationSimulator.SimulateNonRepeatableRead(SqlIsolationLevel.ReadCommitted);
        var repeatableResult = TransactionIsolationSimulator.SimulateNonRepeatableRead(SqlIsolationLevel.RepeatableRead);

        committedResult.AnomalyObserved.Should().BeTrue();
        repeatableResult.AnomalyObserved.Should().BeFalse();
    }

    [Fact]
    public void SimulatePhantomRead_ShouldOccurInReadCommitted_AndBePreventedInRepeatableRead()
    {
        var committedResult = TransactionIsolationSimulator.SimulatePhantomRead(SqlIsolationLevel.ReadCommitted);
        var repeatableResult = TransactionIsolationSimulator.SimulatePhantomRead(SqlIsolationLevel.RepeatableRead);

        committedResult.AnomalyObserved.Should().BeTrue();
        repeatableResult.AnomalyObserved.Should().BeFalse();
    }

    [Fact]
    public void SimulateWriteSkew_ShouldOccurInRepeatableRead_AndBePreventedInSerializable()
    {
        var rrResult = TransactionIsolationSimulator.SimulateWriteSkew(SqlIsolationLevel.RepeatableRead);
        var serResult = TransactionIsolationSimulator.SimulateWriteSkew(SqlIsolationLevel.Serializable);

        rrResult.AnomalyObserved.Should().BeTrue();
        serResult.AnomalyObserved.Should().BeFalse();
    }

    [Fact]
    public void IsVisible_ShouldRespectMvccVisibilityRules()
    {
        // Snapshot taken at Tx 100 with active Tx [102]
        var snapshot = new MvccSnapshot(SnapshotXmin: 100, SnapshotXmax: 105, ActiveXids: [102]);
        var committed = new HashSet<long> { 98, 99, 101 };

        // Tuple created by uncommitted Tx 104 => invisible
        var uncommittedTuple = new MvccTuple<int>(1, 50, xmin: 104);
        TransactionIsolationSimulator.IsVisible(uncommittedTuple, snapshot, committed).Should().BeFalse();

        // Tuple created by committed Tx 99 before snapshot => visible
        var committedTuple = new MvccTuple<int>(2, 100, xmin: 99);
        TransactionIsolationSimulator.IsVisible(committedTuple, snapshot, committed).Should().BeTrue();

        // Tuple created by Tx 102 (active when snapshot was taken) => invisible
        committed.Add(102);
        var inFlightTuple = new MvccTuple<int>(3, 150, xmin: 102);
        TransactionIsolationSimulator.IsVisible(inFlightTuple, snapshot, committed).Should().BeFalse();
    }
}

public class StackProblemsTests
{
    [Theory]
    [InlineData("()", true)]
    [InlineData("()[]{}", true)]
    [InlineData("{[]}", true)]
    [InlineData("(]", false)]
    [InlineData("([)]", false)]
    [InlineData("]", false)]
    [InlineData("[", false)]
    [InlineData("", true)]
    public void ValidParentheses_ShouldValidateProperly(string s, bool expected)
    {
        ValidParenthesesSolver.IsValid(s).Should().Be(expected);
    }

    [Fact]
    public void MinStack_ShouldMaintainConstantTimeMinimum()
    {
        var minStack = new MinStack();

        minStack.Push(-2);
        minStack.Push(0);
        minStack.Push(-3);

        minStack.GetMin().Should().Be(-3);
        minStack.Pop();
        minStack.Top().Should().Be(0);
        minStack.GetMin().Should().Be(-2);

        minStack.Push(-5);
        minStack.GetMin().Should().Be(-5);
        minStack.Pop();
        minStack.GetMin().Should().Be(-2);
    }
}
