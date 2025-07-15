using Microsoft.AspNetCore.Components.Forms;

namespace BlazeTorrent.Components.Handlers;

public class Decoder
{
    public async Task <byte[]> ReadFileByte(IBrowserFile file)
    {
        using var stream = file.OpenReadStream();
        using var memoryStream = new MemoryStream();
        await stream.CopyToAsync(memoryStream);
        return memoryStream.ToArray();
    }

}
