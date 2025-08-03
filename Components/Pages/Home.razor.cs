using BlazeTorrent.Components.Handlers;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using System.Text;
using System.Security.Cryptography;
using System.Net;
using System.IO;
using System.Net.Sockets;
using System.Linq;
using System.Collections.Concurrent;

namespace BlazeTorrent.Components.Pages;

public partial class Home : ComponentBase
{
    protected string FileInfo = "";
    private string displayFileName = "Upload a file";

    // private Decoder _decoder = new Decoder();
    private readonly BencodeDecoder _bencodeDecoder = new BencodeDecoder();
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

    void UpdateStatus(string message, string cssClass = "alert-info")
    {
        alertMessage = message;
        alertClass = cssClass;
        StateHasChanged(); // Force UI to update
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

                    byte[] piecesRaw = infoBytes[dataStart..(dataStart + piecesLen)];
                    int numberOfPieces = piecesRaw.Length / 20;

                    List<string> pieceHashes = BencodeUtils.ExtractPieceHashes(piecesBytes);

                    #region Peers

                    var peerIdBytes = new byte[20];
                    RandomNumberGenerator.Fill(peerIdBytes);

                    var trackerRequest = new TrackerRequest
                    {
                        TrackerUrl = new Uri(tracker),
                        InfoHash = hashBytes,
                        PeerId = Encoding.ASCII.GetString(peerIdBytes),
                        Port = 6881,
                        Uploaded = 0,
                        Downloaded = 0,
                        Left = length,
                        Compact = true
                    };

                    var client = new TrackerClient();
                    var peers = await client.GetPeersAsync(trackerRequest);
                  
                    #endregion

                    #region Tcp HandShake
                    string fileName = (string)infoDict["name"]; 
                                                             

                    string downloadsFolder = Environment.OSVersion.Platform switch
                    {
                        PlatformID.Unix => "/storage/emulated/0/Download", // Android
                        _ => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads")
                    };


                    string outputPath = Path.Combine(downloadsFolder, fileName);

                    bool success = false;
                    ConcurrentQueue<int> remainingPieces = new(Enumerable.Range(0, numberOfPieces));
                    ConcurrentDictionary<int, int> retryCounts = new();
                    byte[] fullFile = new byte[length];
                    object lockObj = new();
                    int maxRetries = 5;

                    foreach (var i in Enumerable.Range(0, numberOfPieces))
                        retryCounts[i] = 0;

                    List<Task> downloadTasks = new();
                    var peerList = peers.ToList();

                    foreach (var peer in peerList)
                    {
                        downloadTasks.Add(Task.Run(async () =>
                        {
                            try
                            {
                                await InvokeAsync(() => UpdateStatus($"🤝 Connecting to {peer.ip}:{peer.port}", "alert-secondary"));

                                var (peerId, stream, _) = await client.PerformHandshake(peer.ip, peer.port, hashBytes, peerIdBytes);
                                await InvokeAsync(() => UpdateStatus($"✅ Connected to {peer.ip}:{peer.port}", "alert-success"));

                                var (msg1, _) = await TrackerClient.ReadMessage(stream);
                                if (msg1 != 5) throw new Exception("Expected bitfield");

                                await stream.WriteAsync(TrackerClient.BuildMessage(2)); // Interested

                                bool unchoked = false;
                                while (!unchoked)
                                {
                                    var (msg, _) = await TrackerClient.ReadMessage(stream);
                                    if (msg == 1) unchoked = true;
                                    else if (msg == 0) throw new Exception("Choked");
                                }

                                while (remainingPieces.TryDequeue(out int pieceIndex))
                                {
                                    try
                                    {
                                        await InvokeAsync(() => UpdateStatus($"📦 Downloading piece {pieceIndex} from {peer.ip}", "alert-warning"));

                                        byte[] expectedHash = piecesRaw[(pieceIndex * 20)..((pieceIndex + 1) * 20)];
                                        int actualPieceLength = (int)Math.Min(pieceLength, length - (long)pieceIndex * pieceLength);

                                        byte[] pieceData = await client.DownloadPiece(stream, pieceIndex, actualPieceLength);
                                        byte[] actualHash = SHA1.HashData(pieceData);

                                        if (!expectedHash.SequenceEqual(actualHash))
                                            throw new Exception("Hash mismatch");

                                        lock (lockObj)
                                            Buffer.BlockCopy(pieceData, 0, fullFile, (int)(pieceIndex * pieceLength), pieceData.Length);

                                        await InvokeAsync(() => UpdateStatus($"✅ Downloaded piece {pieceIndex} from {peer.ip}", "alert-success"));
                                    }
                                    catch (Exception ex)
                                    {
                                        // int retry = Interlocked.Increment(ref retryCounts[pieceIndex]);
                                        int retry = retryCounts.AddOrUpdate(pieceIndex, 1, (_, old) => old + 1);

                                        if (retry < maxRetries)
                                        {
                                            remainingPieces.Enqueue(pieceIndex);
                                            await InvokeAsync(() => UpdateStatus($"⚠️ Retrying piece {pieceIndex} ({retry})", "alert-warning"));
                                        }
                                        else
                                        {
                                            await InvokeAsync(() => UpdateStatus($"❌ Failed piece {pieceIndex} after {maxRetries} retries", "alert-danger"));
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                await InvokeAsync(() => UpdateStatus($"❌ Error from {peer.ip}:{peer.port} - {ex.Message}", "alert-danger"));
                            }
                        }));
                    }

                    await Task.WhenAll(downloadTasks);

                    await File.WriteAllBytesAsync(outputPath, fullFile);
                    await InvokeAsync(() => UpdateStatus($"🎉 Download complete. File saved to {outputPath}", "alert-primary"));

                    #endregion

                    FileInfo = name;
                  
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