using FileStruct.Core.Models;

namespace FileStruct.Core.Tests.Models;

public class StructureNodeTests
{
    [Fact]
    public void Constructor_DefaultValues_AreCorrect()
    {
        var node = new StructureNode();

        Assert.Equal("unnamed", node.Name);
        Assert.Equal(0, node.Offset);
        Assert.Equal(0, node.Length);
        Assert.Equal(FieldDataType.Bytes, node.DataType);
        Assert.Equal(StructureNodeSource.AutoDetected, node.Source);
        Assert.True(node.IsExpanded);
        Assert.False(node.IsSelected);
        Assert.True(node.IsLeaf);
        Assert.True(node.IsRoot);
    }

    [Fact]
    public void AddChild_SetsParentAndUpdatesChildren()
    {
        var parent = new StructureNode { Name = "Root" };
        var child = new StructureNode { Name = "Child" };

        parent.AddChild(child);

        Assert.Single(parent.Children);
        Assert.Same(child, parent.Children[0]);
        Assert.Same(parent, child.Parent);
        Assert.False(parent.IsLeaf);
        Assert.False(child.IsRoot);
    }

    [Fact]
    public void RemoveChild_ClearsParent()
    {
        var parent = new StructureNode();
        var child = new StructureNode();
        parent.AddChild(child);

        var removed = parent.RemoveChild(child);

        Assert.True(removed);
        Assert.Empty(parent.Children);
        Assert.Null(child.Parent);
        Assert.True(parent.IsLeaf);
    }

    [Fact]
    public void InsertChild_InsertsAtCorrectIndex()
    {
        var parent = new StructureNode();
        var first = new StructureNode { Name = "First" };
        var second = new StructureNode { Name = "Second" };
        var middle = new StructureNode { Name = "Middle" };

        parent.AddChild(first);
        parent.AddChild(second);
        parent.InsertChild(1, middle);

        Assert.Equal(3, parent.Children.Count);
        Assert.Equal("First", parent.Children[0].Name);
        Assert.Equal("Middle", parent.Children[1].Name);
        Assert.Equal("Second", parent.Children[2].Name);
    }

    [Fact]
    public void PathName_RootOnly_IsName()
    {
        var node = new StructureNode { Name = "Root" };
        Assert.Equal("Root", node.PathName);
    }

    [Fact]
    public void PathName_NestedNodes_IncludesAllAncestors()
    {
        var root = new StructureNode { Name = "Root" };
        var child = new StructureNode { Name = "Child" };
        var grandchild = new StructureNode { Name = "Grandchild" };

        root.AddChild(child);
        child.AddChild(grandchild);

        Assert.Equal("Root/Child/Grandchild", grandchild.PathName);
    }

    [Fact]
    public void FindByOffset_ExactMatch_ReturnsNode()
    {
        var root = new StructureNode { Offset = 0, Length = 100 };
        var child = new StructureNode { Offset = 10, Length = 20 };
        root.AddChild(child);

        var found = root.FindByOffset(15);

        Assert.Same(child, found);
    }

    [Fact]
    public void FindByOffset_NoMatch_ReturnsNull()
    {
        var root = new StructureNode { Offset = 0, Length = 100 };
        var child = new StructureNode { Offset = 10, Length = 20 };
        root.AddChild(child);

        var found = root.FindByOffset(200);

        Assert.Null(found);
    }

    [Fact]
    public void FindByOffset_DeepMatch_ReturnsDeepest()
    {
        var root = new StructureNode { Offset = 0, Length = 100 };
        var child = new StructureNode { Offset = 0, Length = 50 };
        var grandchild = new StructureNode { Offset = 10, Length = 10 };
        root.AddChild(child);
        child.AddChild(grandchild);

        var found = root.FindByOffset(15);

        Assert.Same(grandchild, found);
    }

    [Fact]
    public void Snapshot_CreatesDeepCopy()
    {
        var root = new StructureNode { Name = "Root", Offset = 0, Length = 100 };
        var child = new StructureNode { Name = "Child", Offset = 10, Length = 20 };
        root.AddChild(child);

        var snap = root.Snapshot();

        Assert.Equal(root.Name, snap.Name);
        Assert.Equal(root.Offset, snap.Offset);
        Assert.Single(snap.Children);
        Assert.Equal(child.Name, snap.Children[0].Name);
        Assert.NotSame(child, snap.Children[0]);
    }

    [Fact]
    public void RestoreFrom_RestoresAllProperties()
    {
        var original = new StructureNode { Name = "Original", Offset = 10, Length = 20 };
        var node = new StructureNode { Name = "Modified", Offset = 99, Length = 99 };

        node.RestoreFrom(original);

        Assert.Equal("Original", node.Name);
        Assert.Equal(10, node.Offset);
        Assert.Equal(20, node.Length);
    }

    [Fact]
    public void PropertyChanged_FiresOnNameChange()
    {
        var node = new StructureNode();
        var fired = false;
        node.PropertyChanged += (_, e) => { if (e.PropertyName == "Name") fired = true; };

        node.Name = "NewName";

        Assert.True(fired);
    }

    [Fact]
    public void IsConfidenceAvailable_Negative_ReturnsFalse()
    {
        var node = new StructureNode { Confidence = -1 };
        Assert.False(node.IsConfidenceAvailable);

        node.Confidence = 0.5;
        Assert.True(node.IsConfidenceAvailable);
    }

    [Fact]
    public void Snapshot_PreservesChildParentNotSet()
    {
        var parent = new StructureNode();
        var child = new StructureNode();
        parent.AddChild(child);

        var snap = child.Snapshot();

        Assert.Null(snap.Parent); // Snapshot should not preserve parent reference
    }
}
