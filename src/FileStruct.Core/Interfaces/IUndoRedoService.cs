using FileStruct.Core.Models;

namespace FileStruct.Core.Interfaces;

public interface IUndoRedoService
{
    bool CanUndo { get; }
    bool CanRedo { get; }
    event EventHandler? StateChanged;

    Task ExecuteAsync(IUndoableCommand command);
    Task UndoAsync();
    Task RedoAsync();
    void Clear();
}

public interface IUndoableCommand
{
    string Description { get; }
    Task ExecuteAsync();
    Task UndoAsync();
}
