using System;
using System.Collections.Generic;

public static class MementoCaretakerInstance
{
    public static IMementoCaretaker Instance { get; } = new MementoCaretaker();
}

public interface IMementoCaretaker
{
    void AddMemento(IMemento memento, string description);
    IMemento? Undo();
    IMemento? Redo();
}

public class MementoCaretaker : IMementoCaretaker
{
    private readonly Stack<MementoItem> _undoStack = new Stack<MementoItem>();
    private readonly Stack<MementoItem> _redoStack = new Stack<MementoItem>();

    public void AddMemento(IMemento memento, string description)
    {
        _undoStack.Push(new MementoItem(memento, description));
        _redoStack.Clear();
    }

    public IMemento? Undo()
    {
        if (_undoStack.Count == 0) return null;
        var mementoItem = _undoStack.Pop();
        _redoStack.Push(mementoItem); // Push the current state onto the redo stack
        if (_undoStack.Count > 0)
        {
            var previousMementoItem = _undoStack.Peek();

            long totalSize = 0;
            foreach (var item in _undoStack)
            {
                totalSize += GC.GetTotalMemory(true);
            }
            Console.WriteLine($"Undo stack size: {totalSize} bytes");

            return previousMementoItem.Memento;
        }
        else
        {
            return null;
        }
    }

    public IMemento? Redo()
    {    
        if (_redoStack.Count == 0) return null;
        var mementoItem = _redoStack.Pop();
        _undoStack.Push(mementoItem); // Push the next state onto the undo stack
        return mementoItem.Memento;
    }
}

public class MementoItem
{
    public IMemento Memento { get; set; }
    public string Description { get; set; }

    public MementoItem(IMemento memento, string description)
    {
        Memento = memento;
        Description = description;
    }
}

public interface IMemento
{
    string Description { get; }
    void Restore();
}