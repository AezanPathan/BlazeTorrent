using BlazeTorrent.Components.Handlers;
using CodeCrafters.Bittorrent.src;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using System.Text;
using System.Security.Cryptography;

namespace BlazeTorrent.Components.Pages;

public partial class Home : ComponentBase
{
    protected string FileInfo = "";
    private string displayFileName = "Upload a file";

   // private Decoder _decoder = new Decoder();
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
                    var fileBytes = await _bencodeDecoder.ReadFileByte(file);
                    (object result, _) = _bencodeDecoder.DecodeInput(fileBytes, 0, decodeStringsAsUtf8: true);

                    var meta = (Dictionary<string, object>)result;
                    var infoDict = (Dictionary<string, object>)meta["info"];

                    string tracker = (string)meta["announce"];
                    long length = 0;

                    // for the single file torrent files
                    if (infoDict.ContainsKey("length"))
                        length = (long)infoDict["length"];

                    // For multi file torrent files 
                    else if (infoDict.ContainsKey("files"))
                    {
                        var files = (List<object>)infoDict["files"];
                        length = files.Sum(f => (long)((Dictionary<string, object>)f)["length"]);
                    }

                    const string marker = "4:infod";
                    int markerPosition = BencodeUtils.FindMarkerPosition(fileBytes, marker);
                    int infoStartIndex = markerPosition + marker.Length - 1;
                    byte[] infoBytes = fileBytes[infoStartIndex..^1];
                    byte[] hashBytes = SHA1.HashData(infoBytes);
                    string infoHash = Convert.ToHexString(hashBytes).ToLower();

                    long pieceLength = (long)infoDict["piece length"];

                    const string piecesKey = "6:pieces";
                    int piecesKeyPos = BencodeUtils.FindMarkerPosition(infoBytes, piecesKey);
                    int lenStart = piecesKeyPos + piecesKey.Length;
                    int colonPos = Array.IndexOf(infoBytes, (byte)':', lenStart);
                    string lenStr = Encoding.ASCII.GetString(infoBytes[lenStart..colonPos]);
                    if (!int.TryParse(lenStr, out int piecesLen))
                        throw new InvalidOperationException($"Invalid pieces length: {lenStr}");
                    int dataStart = colonPos + 1;
                    byte[] piecesBytes = infoBytes[dataStart..(dataStart + piecesLen)];

                    List<string> pieceHashes = BencodeUtils.ExtractPieceHashes(piecesBytes);

                    Console.WriteLine($"Tracker URL: {tracker}");
                    Console.WriteLine($"Length: {length}");
                    Console.WriteLine($"Info Hash: {infoHash}");
                    Console.WriteLine($"Piece Length: {pieceLength}");
                    Console.WriteLine("Piece Hashes:");
                    foreach (var h in pieceHashes) Console.WriteLine(h);


                    FileInfo = name;
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