using System;
using System.IO;
using CodeCrafters.Bittorrent.src;

public class TorrentTestInMain
{
    public static void TestDecoder()
    {
        string[] torrentPaths = {
            @"c:\Users\DELL\Downloads\Snowman.torrent",
            @"c:\Users\DELL\Downloads\big-buck-bunny.torrent",
            @"c:\Users\DELL\Downloads\cosmos-laundromat.torrent"
        };

        var decoder = new BencodeDecoder();

        foreach (string path in torrentPaths)
        {
            try
            {
                Console.WriteLine($"\n=== Testing {Path.GetFileName(path)} ===");
                
                if (!File.Exists(path))
                {
                    Console.WriteLine($"File not found: {path}");
                    continue;
                }

                byte[] data = File.ReadAllBytes(path);
                Console.WriteLine($"File size: {data.Length} bytes");

                // Try to decode
                var result = decoder.DecodeInput(data, 0, decodeStringsAsUtf8: true);
                Console.WriteLine("✓ Successfully decoded!");
                
                if (result.Value is Dictionary<string, object> dict)
                {
                    Console.WriteLine("Keys found:");
                    foreach (var key in dict.Keys)
                    {
                        Console.WriteLine($"  - {key}");
                    }
                    
                    // Check if we have the info dictionary
                    if (dict.ContainsKey("info") && dict["info"] is Dictionary<string, object> info)
                    {
                        Console.WriteLine("Info keys:");
                        foreach (var infoKey in info.Keys)
                        {
                            Console.WriteLine($"    - {infoKey}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Error: {ex.Message}");
                Console.WriteLine($"Exception type: {ex.GetType().Name}");
                if (ex.StackTrace != null)
                {
                    Console.WriteLine($"Stack trace: {ex.StackTrace.Split('\n')[0]}");
                }
            }
        }
    }
}
