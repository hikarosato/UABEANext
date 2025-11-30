using AssetsTools.NET.Extra;
using Avalonia.Platform.Storage;
using System.Text;
using UABEANext4.AssetWorkspace;
using UABEANext4.Plugins;
using UABEANext4.Util;

namespace FontPlugin;

public class ImportFontOption : IUavPluginOption
{
    public string Name => "Import Font";
    public string Description => "Imports Fonts from ttf/otf";
    public UavPluginMode Options => UavPluginMode.Import;

    public bool SupportsSelection(Workspace workspace, UavPluginMode mode, IList<AssetInst> selection)
    {
        if (mode != UavPluginMode.Import)
        {
            return false;
        }

        var typeId = (int)AssetClassID.Font;
        return selection.All(a => a.TypeId == typeId);
    }

    public async Task<bool> Execute(Workspace workspace, IUavPluginFunctions funcs, UavPluginMode mode, IList<AssetInst> selection)
    {
        if (selection.Count > 1)
        {
            return await BatchImport(workspace, funcs, selection);
        }
        else
        {
            return await SingleImport(workspace, funcs, selection);
        }
    }

    public async Task<bool> BatchImport(Workspace workspace, IUavPluginFunctions funcs, IList<AssetInst> selection)
    {
        var dir = await funcs.ShowOpenFolderDialog(new FolderPickerOpenOptions()
        {
            Title = "Select import directory"
        });

        if (dir == null)
        {
            return false;
        }

        var errorBuilder = new StringBuilder();
        var fileNamesToDirty = new HashSet<string>();

        foreach (var asset in selection)
        {
            var errorAssetName = $"{Path.GetFileName(asset.FileInstance.path)}/{asset.PathId}";
            var textBaseField = FontHelper.GetByteArrayFont(workspace, asset);
            if (textBaseField == null)
            {
                errorBuilder.AppendLine($"[{errorAssetName}]: failed to read");
                continue;
            }

            var name = textBaseField["m_Name"].AsString;
            var assetName = PathUtils.ReplaceInvalidPathChars(name);

            // Шукаємо файл з таким же ім'ям
            var ttfPath = Path.Combine(dir, assetName + ".ttf");
            var otfPath = Path.Combine(dir, assetName + ".otf");

            string? filePath = null;
            if (File.Exists(ttfPath))
                filePath = ttfPath;
            else if (File.Exists(otfPath))
                filePath = otfPath;

            if (filePath == null)
            {
                errorBuilder.AppendLine($"[{errorAssetName}]: font file not found");
                continue;
            }

            try
            {
                var byteData = File.ReadAllBytes(filePath);
                textBaseField["m_FontData"]["Array"].AsByteArray = byteData;

                var newData = textBaseField.WriteToByteArray();
                asset.UpdateAssetDataAndRow(workspace, newData);
                fileNamesToDirty.Add(asset.FileInstance.name);
            }
            catch (Exception ex)
            {
                errorBuilder.AppendLine($"[{errorAssetName}]: {ex.Message}");
            }
        }

        foreach (var fileName in fileNamesToDirty)
        {
            var fileToDirty = workspace.ItemLookup[fileName];
            workspace.Dirty(fileToDirty);
        }

        if (errorBuilder.Length > 0)
        {
            string[] firstLines = errorBuilder.ToString().Split('\n').Take(20).ToArray();
            string firstLinesStr = string.Join('\n', firstLines);
            await funcs.ShowMessageDialog("Error", firstLinesStr);
        }

        return fileNamesToDirty.Count > 0;
    }

    public async Task<bool> SingleImport(Workspace workspace, IUavPluginFunctions funcs, IList<AssetInst> selection)
    {
        var asset = selection[0];
        var textBaseField = FontHelper.GetByteArrayFont(workspace, asset);
        if (textBaseField == null)
        {
            await funcs.ShowMessageDialog("Error", "Failed to read Font");
            return false;
        }

        var filePath = await funcs.ShowOpenFileDialog(new FilePickerOpenOptions()
        {
            Title = "Open font",
            FileTypeFilter = new List<FilePickerFileType>()
            {
                new FilePickerFileType("Font files") { Patterns = new List<string>() { "*.ttf", "*.otf" } },
            },
            AllowMultiple = false
        });

        if (filePath == null || filePath.Length == 0)
        {
            return false;
        }

        try
        {
            var byteData = File.ReadAllBytes(filePath[0]);
            textBaseField["m_FontData"]["Array"].AsByteArray = byteData;

            var newData = textBaseField.WriteToByteArray();
            asset.UpdateAssetDataAndRow(workspace, newData);

            var fileToDirty = workspace.ItemLookup[asset.FileInstance.name];
            workspace.Dirty(fileToDirty);

            return true;
        }
        catch (Exception ex)
        {
            await funcs.ShowMessageDialog("Error", $"Failed to import font: {ex.Message}");
            return false;
        }
    }
}