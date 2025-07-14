using Microsoft.AspNetCore.Components.Forms;

namespace BlazeTorrent.Components.Handlers;

public class Decoder
{
    public async Task <byte[]> ReadFileByte(IBrowserFile file)
    {
        using var stream = file.OpenReadStream();
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms);
        return ms.ToArray(); // full byte[] of the .torrent file
    }

}
