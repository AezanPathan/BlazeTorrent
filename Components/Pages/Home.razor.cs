using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace BlazeTorrent.Components.Pages;

public partial class Home : ComponentBase
{
    protected string FileInfo = "";
    private string displayFileName = "Upload a file";

    // protected async Task OnInputFileChanged(ChangeEventArgs e)
    // {

    //     if (e.Value is not null && e.Value is string == false)
    //     {
    //         // e.Value is not useful for file upload here
    //         FileInfo = "File selected, but input type='file' needs InputFile component in Blazor.";
    //     }
    // }
      private async Task OnInputFileChanged(InputFileChangeEventArgs e)
    {
        var file = e.File;
        if (file != null)
        {
            var name = file.Name;
            // Trim to first 4 chars + ... + extension
            var ext = Path.GetExtension(name);
            var baseName = Path.GetFileNameWithoutExtension(name);

            if (baseName.Length > 5)
                displayFileName = baseName.Substring(0, 5) + "..." + ext;
            else
                displayFileName = name;
        }
    }
}