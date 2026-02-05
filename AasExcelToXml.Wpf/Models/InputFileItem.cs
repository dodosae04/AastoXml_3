using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AasExcelToXml.Wpf.Models;

public sealed class InputFileItem : INotifyPropertyChanged
{
    private string _status = string.Empty;

    public InputFileItem(string path)
    {
        Path = path;
        FileName = System.IO.Path.GetFileName(path);
    }

    public string Path { get; }
    public string FileName { get; }

    public string Status
    {
        get => _status;
        set
        {
            if (_status == value)
            {
                return;
            }

            _status = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
