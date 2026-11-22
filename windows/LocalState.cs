using System;
using System.Diagnostics;
using System.IO;
using System.Security;
using System.Text.Json;
using Microsoft.Win32;

namespace WenMind.Daily;

internal sealed class LocalState
{
    private const string StartupKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string StartupEntry = "WenMindDaily";
    private readonly string directory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WenMind", "Daily");

    public T? Read<T>(string name) where T : class
    {
        try
        {
            return JsonSerializer.Deserialize<T>(File.ReadAllText(Path.Combine(directory, name)));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            Debug.WriteLine(ex);
            return null;
        }
    }

    public bool Write<T>(string name, T value)
    {
        string path = Path.Combine(directory, name);
        string temporary = path + ".tmp";
        try
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(temporary, JsonSerializer.Serialize(value));
            File.Move(temporary, path, overwrite: true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Debug.WriteLine(ex);
            return false;
        }
    }

    public static bool StartupEnabled()
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(StartupKey);
            return key?.GetValue(StartupEntry) is string;
        }
        catch (Exception ex) when (ex is SecurityException or UnauthorizedAccessException or IOException)
        {
            Debug.WriteLine(ex);
            return false;
        }
    }

    public static bool SetStartup(bool enabled)
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.CreateSubKey(StartupKey);
            if (key is null)
                return false;

            if (enabled && Environment.ProcessPath is { } exe)
                key.SetValue(StartupEntry, $"\"{exe}\"", RegistryValueKind.String);
            else if (!enabled)
                key.DeleteValue(StartupEntry, throwOnMissingValue: false);
            else
                return false;

            return true;
        }
        catch (Exception ex) when (ex is SecurityException or UnauthorizedAccessException or IOException)
        {
            Debug.WriteLine(ex);
            return false;
        }
    }
}
