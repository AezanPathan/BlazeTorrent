using BlazeTorrent.Components.Handlers;
using CodeCrafters.Bittorrent.src;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace BlazeTorrent.Components.Pages;

public partial class Home : ComponentBase
{
    protected string FileInfo = "";
    private string displayFileName = "Upload a file";

    private Decoder _decoder = new Decoder();
    private BencodeDecoder _bencodeDecoder = new BencodeDecoder();
    //alert 

    private string? alertMessage;
    private string? alertClass;

    private void CloseAlert()
    {
        alertMessage = "";
        alertClass = "";
    }

    private void ShowAlert(string message, string type = "info")
    {
        alertMessage = message;
        alertClass = type;
    }

    private async Task OnInputFileChanged(InputFileChangeEventArgs e)
    {
        var file = e.File;
        if (file != null)
        {
            var name = file.Name;
            // Trim to first 4 chars + ... + extension
            var ext = Path.GetExtension(name);
            var baseName = Path.GetFileNameWithoutExtension(name);

            displayFileName = baseName.Length > 5 ? baseName.Substring(0, 5) + "..." + ext : name;

            if (ext != ".torrent")
            {
                alertMessage = "Please upload torrent file.";
                alertClass = "alert-danger";
            }

            else
            {
                try
                {
                    var fileBytes = await _decoder.ReadFileByte(file);
                    (object result, _) = _bencodeDecoder.DecodeInput(fileBytes, 0, decodeStringsAsUtf8: true);

                    var meta = (Dictionary<string, object>)result;
                    var infoDict = (Dictionary<string, object>)meta["info"];

                    string tracker = (string)meta["announce"];
                    long length = 0;
                    
                    // Handle different torrent structures
                    if (infoDict.ContainsKey("length"))
                    {
                        length = (long)infoDict["length"];
                    }
                    else if (infoDict.ContainsKey("files") && infoDict["files"] is List<object> files)
                    {
                        // Multi-file torrent
                        foreach (var fileObj in files)
                        {
                            if (fileObj is Dictionary<string, object> fileDict && fileDict.ContainsKey("length"))
                            {
                                length += (long)fileDict["length"];
                            }
                        }
                    }

                    string infoText = $"Tracker: {tracker}\nTotal Size: {length:N0} bytes";
                    if (infoDict.ContainsKey("name"))
                    {
                        infoText = $"Name: {(string)infoDict["name"]}\n{infoText}";
                    }

                    FileInfo = infoText;
                    alertMessage = "✓ Torrent file successfully parsed!";
                    alertClass = "alert-success";
                }
                catch (Exception ex)
                {
                    alertMessage = $"Error parsing torrent: {ex.Message}";
                    alertClass = "alert-danger";
                    FileInfo = $"Error details: {ex.GetType().Name}";
                }
            }
        }
    }
}