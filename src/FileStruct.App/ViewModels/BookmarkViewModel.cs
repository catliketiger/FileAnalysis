using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileStruct.Core.Models;

namespace FileStruct.App.ViewModels;

public partial class BookmarkViewModel : ObservableObject
{
    public ObservableCollection<Bookmark> Bookmarks { get; } = new();

    [ObservableProperty]
    private Bookmark? _selectedBookmark;

    public void AddBookmark(string name, long offset, string? description = null)
    {
        Bookmarks.Add(new Bookmark { Name = name, Offset = offset, Description = description });
    }

    [RelayCommand]
    private void DeleteSelected()
    {
        if (SelectedBookmark != null)
        {
            Bookmarks.Remove(SelectedBookmark);
            SelectedBookmark = null;
        }
    }

    [RelayCommand]
    private void ClearAll() { Bookmarks.Clear(); SelectedBookmark = null; }
}
