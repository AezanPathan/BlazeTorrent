using BlazeTorrent.Components.Handlers;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace BlazeTorrent.Components.Pages;

public partial class Home : ComponentBase
{
    protected string FileInfo = "";
    private string displayFileName = "Upload a file";

    private Decoder _decoder = new Decoder();

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
                var fileBytes = _decoder.ReadFileByte(file);
                alertMessage = "Thanks for the torrent file.";
                alertClass = "alert-success";
            }
        }
    }
}