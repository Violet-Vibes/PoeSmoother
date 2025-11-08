using System;
using System.IO;
using System.Reflection;
using LibBundle3.Nodes;
using LibGGPK3.Records;

namespace PoeSmoother.Patches;

public class Camera : IPatch
{
    public string Name => "Camera Patch";
    public object Description => "Allows adjusting the default camera zoom level.";

    public double ZoomLevel { get; set; } = 2.4;

    private readonly string[] _directory = {
        "metadata",
    };

    private readonly string[] _extensions = {
        ".ot",
        ".otc",
    };

    private readonly string[] _functions = {
        "CreateCameraZoomNode",
        "ClearCameraZoomNodes",
        "CreateCameraLookAtNode",
        "SetCustomCameraSpeed",
        "CreateCameraPanNode",
        "RemoveCustomCameraSpeed",

    };

    private void RecursivePatcher(DirectoryNode dir)
    {
        foreach (var d in dir.Children)
        {
            if (d is DirectoryNode childDir)
            {
                RecursivePatcher(childDir);
            }
            else if (d is FileNode file)
            {

                if (Array.Exists(_extensions, ext => file.Name.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
                {
                    if (file.Name == "character.ot")
                    {
                        continue;
                    }

                    var record = file.Record;
                    var bytes = record.Read();
                    string data = System.Text.Encoding.Unicode.GetString(bytes.ToArray());
                    
                    if (!_functions.Any(func => data.Contains(func)))
                    {
                        continue;
                    }

                    List<string> lines = data.Split("\r\n").ToList();

                    for (int i = lines.Count - 1; i >= 0; i--)
                    {
                        string? foundFunction = _functions.FirstOrDefault(func => lines[i].Contains(func));
                        
                        while (foundFunction != null)
                        {
                            int start = lines[i].IndexOf(foundFunction);
                            int end = lines[i].IndexOf(';', start);
                            if (end >= 0)
                            {
                                lines[i] = lines[i][..start] + lines[i][(end + 1)..];
                            }
                            foundFunction = _functions.FirstOrDefault(func => lines[i].Contains(func));
                        }
                    }
                    string newData = string.Join("\r\n", lines);
                    var newBytes = System.Text.Encoding.Unicode.GetBytes(newData);
                    record.Write(newBytes);
                }
            }
        }
    }

    public void Apply(DirectoryNode root)
    {
        foreach (var d in root.Children)
        {
            if (d is DirectoryNode dir && _directory.Contains(dir.Name))
            {
                RecursivePatcher(dir);

                // go to metadata/characters/character.ot
                foreach (var dir1 in dir.Children)
                {
                    if (dir1 is DirectoryNode subDir && subDir.Name == "characters")
                    {
                        foreach (var dir2 in subDir.Children)
                        {
                            if (dir2 is FileNode file && file.Name == "character.ot")
                            {
                                var record = file.Record;
                                var bytes = record.Read();
                                string data = System.Text.Encoding.Unicode.GetString(bytes.ToArray());
                                List<string> lines = data.Split("\r\n").ToList();
                                string zoomLevelString = ZoomLevel.ToString().Replace(',', '.');

                                if (data.Contains("CreateCameraZoomNode"))
                                {
                                    int x = lines.FindIndex(line => line.Contains("CreateCameraZoomNode"));
                                    lines[x] = $"\ton_initial_position_set = \"CreateCameraZoomNode(5000.0, 5000.0, {zoomLevelString});\" ";
                                }
                                else
                                {
                                    int index = lines.FindIndex(x => x.Contains("team = 1"));
                                    if (index == -1) continue;
                                    lines.Insert(index + 1, $"\ton_initial_position_set = \"CreateCameraZoomNode(5000.0, 5000.0, {zoomLevelString});\" ");
                                }
                                string newData = string.Join("\r\n", lines);
                                var newBytes = System.Text.Encoding.Unicode.GetBytes(newData);
                                record.Write(newBytes);
                            }
                        }
                    }
                }
            }
        }
    }
}