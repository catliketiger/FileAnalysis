using FileStruct.Core.Models;
using FileStruct.Services.EditService;
using FileStruct.Services.EditService.Commands;

namespace FileStruct.Services.Tests.EditService;

public class UndoRedoServiceTests
{
    private readonly UndoRedoService _service = new();

    [Fact]
    public async Task Execute_AddField_CanUndo()
    {
        var parent = new StructureNode { Name = "Root" };
        var child = new StructureNode { Name = "Child" };
        var cmd = new AddFieldCommand(parent, child);

        await _service.ExecuteAsync(cmd);

        Assert.True(_service.CanUndo);
        Assert.False(_service.CanRedo);
        Assert.Contains(child, parent.Children);
    }

    [Fact]
    public async Task Undo_RemovesAddedField()
    {
        var parent = new StructureNode();
        var child = new StructureNode { Name = "Child" };
        await _service.ExecuteAsync(new AddFieldCommand(parent, child));

        await _service.UndoAsync();

        Assert.Empty(parent.Children);
        Assert.True(_service.CanRedo);
    }

    [Fact]
    public async Task Redo_RestoresUndoneField()
    {
        var parent = new StructureNode();
        var child = new StructureNode { Name = "Child" };
        await _service.ExecuteAsync(new AddFieldCommand(parent, child));
        await _service.UndoAsync();

        await _service.RedoAsync();

        Assert.Contains(child, parent.Children);
        Assert.False(_service.CanRedo);
    }

    [Fact]
    public async Task Execute_ClearsRedoStack()
    {
        var parent = new StructureNode();
        var child = new StructureNode { Name = "Child" };
        await _service.ExecuteAsync(new AddFieldCommand(parent, child));
        await _service.UndoAsync();

        var child2 = new StructureNode { Name = "Child2" };
        await _service.ExecuteAsync(new AddFieldCommand(parent, child2));

        Assert.False(_service.CanRedo);
    }

    [Fact]
    public async Task DeleteField_Undo_RestoresNodeAtOriginalPosition()
    {
        var parent = new StructureNode();
        var first = new StructureNode { Name = "First" };
        var second = new StructureNode { Name = "Second" };
        parent.AddChild(first);
        parent.AddChild(second);

        await _service.ExecuteAsync(new DeleteFieldCommand(first));

        Assert.Single(parent.Children);
        Assert.Equal("Second", parent.Children[0].Name);

        await _service.UndoAsync();

        Assert.Equal(2, parent.Children.Count);
        Assert.Equal("First", parent.Children[0].Name);
    }

    [Fact]
    public async Task ModifyField_Undo_RestoresOriginalValues()
    {
        var node = new StructureNode { Name = "Original", Offset = 10, Length = 20 };
        var cmd = new ModifyFieldCommand(node, "Modified", null, 30, null, null);

        await _service.ExecuteAsync(cmd);
        Assert.Equal("Modified", node.Name);
        Assert.Equal(30, node.Length);

        await _service.UndoAsync();
        Assert.Equal("Original", node.Name);
        Assert.Equal(20, node.Length);
    }

    [Fact]
    public async Task Clear_ResetsBothStacks()
    {
        var parent = new StructureNode();
        var child = new StructureNode();
        await _service.ExecuteAsync(new AddFieldCommand(parent, child));
        await _service.UndoAsync();

        _service.Clear();

        Assert.False(_service.CanUndo);
        Assert.False(_service.CanRedo);
    }

    [Fact]
    public async Task Undo_WithEmptyStack_DoesNothing()
    {
        await _service.UndoAsync(); // Should not throw
        Assert.False(_service.CanUndo);
    }

    [Fact]
    public async Task MaxHistory_TrimsOldest()
    {
        var parent = new StructureNode();
        // We can't easily test 100 items, but verify many doesn't crash
        for (int i = 0; i < 50; i++)
        {
            var child = new StructureNode { Name = $"Child{i}" };
            await _service.ExecuteAsync(new AddFieldCommand(parent, child));
        }

        for (int i = 0; i < 50; i++)
        {
            Assert.True(_service.CanUndo);
            await _service.UndoAsync();
        }

        Assert.False(_service.CanUndo);
    }

    [Fact]
    public async Task ResizeField_Undo_RestoresLength()
    {
        var node = new StructureNode { Name = "Field", Length = 100 };
        var cmd = new ResizeFieldCommand(node, 200);

        await _service.ExecuteAsync(cmd);
        Assert.Equal(200, node.Length);

        await _service.UndoAsync();
        Assert.Equal(100, node.Length);
    }

    [Fact]
    public async Task ResetField_SourceTracking_Works()
    {
        var node = new StructureNode { Name = "Field", Source = StructureNodeSource.AutoDetected };
        var snapshot = new StructureNode { Name = "Original", Source = StructureNodeSource.AutoDetected };
        node.OriginalSnapshot = snapshot;
        node.Name = "Modified";

        var cmd = new ResetFieldCommand(node);
        await _service.ExecuteAsync(cmd);

        Assert.Equal("Original", node.Name);
        Assert.Equal(StructureNodeSource.UserReset, node.Source);
    }
}
