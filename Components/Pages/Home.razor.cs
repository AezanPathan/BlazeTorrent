using BlazeTorrent.Components.Handlers;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using System.Text;
using System.Security.Cryptography;
using System.Net;
using System.IO;
using System.Net.Sockets;

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

                    //Console.WriteLine($"Tracker URL: {tracker}");
                    //Console.WriteLine($"Length: {length}");
                    //Console.WriteLine($"Info Hash: {infoHash}");
                    //Console.WriteLine($"Piece Length: {pieceLength}");
                    //Console.WriteLine("Piece Hashes:");
                    //foreach (var h in pieceHashes) Console.WriteLine(h);

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
                  //  foreach (var (ip, port) in peers)
                    //    Console.WriteLine($"{ip}:{port}");

                    #endregion

                    #region Tcp HandShake
                    //await Task.WhenAll(peers.Select(async (peer) =>
                    //{
                    //    try
                    //    {
                    //        string peerId = await client.PerformHandshake(peer.ip, peer.port, hashBytes, peerIdBytes);
                    //        Console.WriteLine($"✅ Connected to {peer.ip}:{peer.port} -> Peer ID: {peerId}");
                    //                alertMessage = $"✓ {peerId}";
                    //                alertClass = "alert-success";
                    //    }
                    //    catch (Exception ex)
                    //    {
                    //        Console.WriteLine($"❌ {peer.ip}:{peer.port} failed: {ex.Message}");
                    //                alertMessage = $"❌ Handshake failed with {peer.ip}:{peer.port} - {ex.Message}";
                    //                alertClass = "alert-danger";
                    //    }
                    //}));

                    // maybe change or improve later
                    string fileName = (string)infoDict["name"]; // torrent filename
                  //  string downloadsFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");

                    string downloadsFolder = Environment.OSVersion.Platform switch
                    {
                        PlatformID.Unix => "/storage/emulated/0/Download", // Android
                        //PlatformID.MacOSX => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Downloads"),
                        _ => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads")
                    };


                    string outputPath = Path.Combine(downloadsFolder, fileName);

                    bool success = false;


                    foreach (var peer in peers)
                    {
                        try
                        {
                            var (peerId, stream, tcpClient) = await client.PerformHandshake(peer.ip, peer.port, hashBytes, peerIdBytes);
                            Console.WriteLine($"✅ Connected to {peer.ip}:{peer.port} -> Peer ID: {peerId}");


                            // Step : Wait for bitfield (ID 5)
                            var (msg1, _) = await TrackerClient.ReadMessage(stream);
                            if (msg1 != 5) throw new Exception("Expected bitfield");

                            // Step : Send interested (ID 2)
                            await stream.WriteAsync(TrackerClient.BuildMessage(2));

                            // Step : Wait for unchoke (ID 1)
                            //var (msg2, _) = await TrackerClient.ReadMessage(stream);
                            //if (msg2 != 1) throw new Exception("Expected unchoke");
                            bool unchoked = false;

                            while (!unchoked)
                            {
                                var (msg, _) = await TrackerClient.ReadMessage(stream);
                                if (msg == 1) unchoked = true;
                                else if (msg == 0) throw new Exception("choked"); // choke
                                else continue;
                            }

                            byte[] fullFile = new byte[length];
                            for (int i = 0; i < numberOfPieces; i++)
                            {
                            UpdateStatus($"⬇ Downloading piece {i + 1}/{numberOfPieces}...", "alert-info");
                                byte[] expectedHash = piecesRaw[(i * 20)..((i + 1) * 20)];

                                int actualPieceLength = (int)Math.Min((long)pieceLength, length - (long)i * (long)pieceLength);
                                //int actualPieceLength = Math.Min(pieceLength, (int)(length - (long)i * pieceLength));
                                byte[] pieceData = await client.DownloadPiece(stream, i, actualPieceLength);

                                byte[] actualHash = SHA1.HashData(pieceData);
                                if (!expectedHash.SequenceEqual(actualHash))
                                    throw new Exception($"Piece {i} hash mismatch");

                                Buffer.BlockCopy(pieceData, 0, fullFile, (int)(i * pieceLength), pieceData.Length);
                            }

                            await File.WriteAllBytesAsync(outputPath, fullFile);

                            UpdateStatus($"✅ Connected to {peer.ip}:{peer.port} -> Peer ID: {peerId}", "alert-success");


                            success = true;
                            break; // Stop trying other peers once successful
                        }
                        catch (Exception ex)
                        {
                          //  Console.WriteLine($"❌ {peer.ip}:{peer.port} failed: {ex.Message}");
                          ////  alertMessage = $"❌ Handshake failed with {peer.ip}:{peer.port} - {ex.Message}";
                        //    alertClass = "alert-danger";
                            UpdateStatus($"❌ Handshake failed with {peer.ip}:{peer.port} - {ex.Message}", "alert-danger");

                        }
                    }


                    //foreach (var (ip, port) in peers)
                    //{
                    //    try
                    //    {
                    //        var peerIp = ip; // ensure valid IP format
                    //        string peerId = await client.PerformHandshake(peerIp, port, hashBytes, peerIdBytes);
                    //        Console.WriteLine($"✅ Handshake successful with {ip}:{port}, Peer ID: {peerId}");
                    //        alertMessage = $"✓ {peerId}";
                    //        alertClass = "alert-success";
                    //    }
                    //    catch (Exception ex)
                    //    {
                    //        Console.WriteLine($"❌ Handshake failed with {ip}:{port} - {ex.Message}");
                    //        alertMessage = $"❌ Handshake failed with {ip}:{port} - {ex.Message}";
                    //        alertClass = "alert-danger";
                    //        FileInfo = $"Error details: {ex.GetType().Name}";
                    //    }
                    //}

                    #endregion
                    FileInfo = name;
                    // alertMessage = "✓ Torrent file successfully parsed!";
                    //  alertClass = "alert-success";
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