using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace RealmGate.Desktop.Services;

public interface ICreatorStudioFilePicker
{
    Task<string?> PickMdbAsync();
    Task<string?> PickNativeEvidenceFolderAsync();
    Task<string?> PickCustomPackageAsync();
    Task<string?> PickCustomPackageExportAsync(string suggestedFileName);
}

public sealed class AvaloniaCreatorStudioFilePicker(TopLevel owner) : ICreatorStudioFilePicker
{
    public async Task<string?> PickMdbAsync()
    {
        var files = await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select a MajorMUD MDB",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("MajorMUD Access database")
                {
                    Patterns = ["*.mdb"]
                }
            ]
        });

        return files.FirstOrDefault()?.TryGetLocalPath();
    }

    public async Task<string?> PickNativeEvidenceFolderAsync()
    {
        var folders = await owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select the matching WCC/WGS source folder",
            AllowMultiple = false
        });

        return folders.FirstOrDefault()?.TryGetLocalPath();
    }

    public async Task<string?> PickCustomPackageAsync()
    {
        var files = await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Preview a RealmGate Custom1 change package",
            AllowMultiple = false,
            FileTypeFilter = [CustomPackageType]
        });

        return files.FirstOrDefault()?.TryGetLocalPath();
    }

    public async Task<string?> PickCustomPackageExportAsync(string suggestedFileName)
    {
        var file = await owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export a RealmGate Custom1 change package",
            SuggestedFileName = suggestedFileName,
            DefaultExtension = "json",
            FileTypeChoices = [CustomPackageType]
        });

        return file?.TryGetLocalPath();
    }

    private static FilePickerFileType CustomPackageType { get; } =
        new("RealmGate Custom1 package")
        {
            Patterns = ["*.json"]
        };
}
