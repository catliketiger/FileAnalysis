using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;

namespace FileStruct.App.ViewModels;

public partial class FileMetaViewModel : ObservableObject
{
    [ObservableProperty] private string _format = "";
    [ObservableProperty] private string _filePath = "";
    [ObservableProperty] private string _fileSize = "";
    [ObservableProperty] private string _created = "";
    [ObservableProperty] private string _modified = "";
    [ObservableProperty] private bool _hasFile;

    public void Update(string filePath, string format, long length)
    {
        FilePath = filePath;
        Format = string.IsNullOrEmpty(format) ? "未知" : format;
        FileSize = FormatFileSize(length);
        HasFile = true;

        try
        {
            var fi = new FileInfo(filePath);
            Created = fi.CreationTime.ToString("yyyy-MM-dd HH:mm:ss");
            Modified = fi.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss");
        }
        catch
        {
            Created = Modified = "";
        }
    }

    public void Clear()
    {
        HasFile = false;
        Format = FilePath = FileSize = Created = Modified = "";
    }

    private static string FormatFileSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        return $"{bytes / (1024.0 * 1024.0):F1} MB";
    }
}
