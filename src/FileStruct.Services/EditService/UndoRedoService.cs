using FileStruct.Core.Interfaces;
using FileStruct.Core.Models;

namespace FileStruct.Services.EditService;

public class UndoRedoService : IUndoRedoService
{
    private readonly LinkedList<IUndoableCommand> _undoStack = new();
    private readonly Stack<IUndoableCommand> _redoStack = new();
    private const int MaxHistory = 100;

    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;

    public event EventHandler? StateChanged;

    public async Task ExecuteAsync(IUndoableCommand command)
    {
        await command.ExecuteAsync();
        _undoStack.AddLast(command);
        _redoStack.Clear();

        if (_undoStack.Count > MaxHistory)
            _undoStack.RemoveFirst();

        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task UndoAsync()
    {
        if (!CanUndo) return;
        var cmd = _undoStack.Last!.Value;
        _undoStack.RemoveLast();
        await cmd.UndoAsync();
        _redoStack.Push(cmd);
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task RedoAsync()
    {
        if (!CanRedo) return;
        var cmd = _redoStack.Pop();
        await cmd.ExecuteAsync();
        _undoStack.AddLast(cmd);
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Clear()
    {
        _undoStack.Clear();
        _redoStack.Clear();
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
}
