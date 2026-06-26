using FileStruct.App.Controls;

namespace FileStruct.Services.Tests.Controls;

public class SelectionManagerTests
{
    [Fact]
    public void SetSelection_FiresExactlyOneEvent()
    {
        int fired = 0;
        var sm = new SelectionManager();
        sm.SelectionChanged += (_, _) => fired++;

        sm.SetSelection(100, 200);

        Assert.Equal(1, fired);
    }

    [Fact]
    public void SetSelection_SetsCorrectRange()
    {
        var sm = new SelectionManager();
        sm.SetSelection(50, 99);

        Assert.True(sm.HasSelection);
        Assert.Equal(50, sm.StartOffset);
        Assert.Equal(99, sm.EndOffset);
        Assert.Equal(50, sm.Length);
    }

    [Fact]
    public void SetSelection_ZeroLength()
    {
        var sm = new SelectionManager();
        sm.SetSelection(42, 42);

        Assert.True(sm.HasSelection);
        Assert.Equal(1, sm.Length);
    }

    [Fact]
    public void SetSelection_ReversedOrder()
    {
        var sm = new SelectionManager();
        sm.SetSelection(200, 100);

        Assert.Equal(200, sm.StartOffset);
        Assert.Equal(100, sm.EndOffset);
        Assert.Equal(101, sm.Length);
    }

    [Fact]
    public void ClearSelection_DoesNotFireWhenNoSelection()
    {
        int fired = 0;
        var sm = new SelectionManager();
        sm.SelectionChanged += (_, _) => fired++;

        sm.ClearSelection(); // no selection present

        Assert.Equal(0, fired);
    }

    [Fact]
    public void ClearSelection_FiresWhenHasSelection()
    {
        int fired = 0;
        var sm = new SelectionManager();
        sm.SetSelection(0, 10);
        sm.SelectionChanged += (_, _) => fired++;

        sm.ClearSelection();

        Assert.Equal(1, fired);
        Assert.False(sm.HasSelection);
        Assert.Equal(0, sm.Length);
    }

    [Fact]
    public void BeginSelection_FiresEvent()
    {
        int fired = 0;
        var sm = new SelectionManager();
        sm.SelectionChanged += (_, _) => fired++;

        sm.BeginSelection(42);

        Assert.Equal(1, fired);
        Assert.True(sm.HasSelection);
        Assert.Equal(42, sm.StartOffset);
        Assert.Equal(42, sm.EndOffset);
        Assert.Equal(1, sm.Length);
    }

    [Fact]
    public void ExtendSelection_FiresEvent()
    {
        int fired = 0;
        var sm = new SelectionManager();
        sm.BeginSelection(10);
        sm.SelectionChanged += (_, _) => fired++;

        sm.ExtendSelection(30);

        Assert.Equal(1, fired);
        Assert.Equal(10, sm.StartOffset);
        Assert.Equal(30, sm.EndOffset);
        Assert.Equal(21, sm.Length);
    }

    [Fact]
    public void ClearBeginExtend_FiresThreeTimes()
    {
        int fired = 0;
        var sm = new SelectionManager();
        sm.BeginSelection(0);  // pre-set a selection
        sm.SelectionChanged += (_, _) => fired++;

        sm.ClearSelection();  // fires (had selection)
        sm.BeginSelection(10); // fires
        sm.ExtendSelection(30); // fires

        Assert.Equal(3, fired);
    }

    [Fact]
    public void IsSelected_WithinRange()
    {
        var sm = new SelectionManager();
        sm.SetSelection(100, 110);

        Assert.True(sm.IsSelected(100));
        Assert.True(sm.IsSelected(105));
        Assert.True(sm.IsSelected(110));
        Assert.False(sm.IsSelected(99));
        Assert.False(sm.IsSelected(111));
    }

    [Fact]
    public void IsSelected_NoSelection_AlwaysFalse()
    {
        var sm = new SelectionManager();
        Assert.False(sm.IsSelected(0));
        Assert.False(sm.IsSelected(100));
    }

    [Fact]
    public void SetSelection_SecondCallFiresEventAgain()
    {
        int fired = 0;
        var sm = new SelectionManager();
        sm.SelectionChanged += (_, _) => fired++;

        sm.SetSelection(0, 10);
        sm.SetSelection(20, 30);

        Assert.Equal(2, fired);
        Assert.Equal(20, sm.StartOffset);
        Assert.Equal(30, sm.EndOffset);
    }
}
